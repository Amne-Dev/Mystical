using System.Diagnostics;
using System.Text.Json;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.Services;

public sealed class GameUpdatesService : IGameUpdatesService
{
    public async Task<GameUpdatesSnapshot> CheckForUpdatesAsync(IReadOnlyList<GameInfo> games, CancellationToken cancellationToken = default)
    {
        var installedGames = games.Where(game => game.IsInstalled).ToList();
        var updates = new List<GameUpdateInfo>();
        var notes = new List<string>();

        updates.AddRange(CheckSteamUpdates(installedGames, cancellationToken));

        var epicResult = await CheckEpicUpdatesAsync(installedGames, cancellationToken);
        updates.AddRange(epicResult.Updates);
        notes.AddRange(epicResult.Notes);

        if (!installedGames.Any(game => game.Platform is GamePlatform.Steam or GamePlatform.EpicGames))
        {
            notes.Add("No installed Steam or Epic games available for update checks.");
        }
        else
        {
            notes.Add("Xbox, Microsoft Store, and GOG update status are not exposed consistently via local manifests.");
        }

        var distinctUpdates = updates
            .GroupBy(update => update.GameKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(update => update.PlatformName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(update => update.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GameUpdatesSnapshot
        {
            CheckedAt = DateTimeOffset.UtcNow,
            InstalledGamesChecked = installedGames.Count,
            Updates = distinctUpdates,
            Notes = notes.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static IReadOnlyList<GameUpdateInfo> CheckSteamUpdates(IReadOnlyList<GameInfo> installedGames, CancellationToken cancellationToken)
    {
        var steamGames = installedGames.Where(game => game.Platform == GamePlatform.Steam).ToList();
        if (steamGames.Count == 0)
        {
            return Array.Empty<GameUpdateInfo>();
        }

        var steamInstallPath = ResolveSteamInstallPath();
        if (string.IsNullOrWhiteSpace(steamInstallPath))
        {
            return Array.Empty<GameUpdateInfo>();
        }

        var steamAppsPath = Path.Combine(steamInstallPath, "steamapps");
        if (!Directory.Exists(steamAppsPath))
        {
            return Array.Empty<GameUpdateInfo>();
        }

        var updates = new List<GameUpdateInfo>();
        foreach (var game in steamGames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryExtractSteamAppId(game.GameId, out var appId))
            {
                continue;
            }

            var manifestPath = Path.Combine(steamAppsPath, $"appmanifest_{appId}.acf");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            VdfObject root;
            try
            {
                root = VdfParser.ParseFile(manifestPath);
            }
            catch
            {
                continue;
            }

            var appState = root.GetObject("AppState");
            if (appState is null)
            {
                continue;
            }

            var stateFlags = ParseLong(appState.GetString("StateFlags"));
            var bytesToDownload = ParseLong(appState.GetString("BytesToDownload"));
            var bytesDownloaded = ParseLong(appState.GetString("BytesDownloaded"));
            var buildId = appState.GetString("buildid");

            var updateRequiredByFlag = (stateFlags & 1024L) != 0 || (stateFlags & 2048L) != 0;
            var updateRequiredByDownload = bytesToDownload > 0 && bytesDownloaded < bytesToDownload;
            if (!updateRequiredByFlag && !updateRequiredByDownload)
            {
                continue;
            }

            var details = updateRequiredByDownload
                ? $"{FormatBytes(Math.Max(0, bytesToDownload - bytesDownloaded))} remaining to download."
                : "Steam manifest reports update required.";

            updates.Add(new GameUpdateInfo
            {
                GameKey = game.Key,
                Title = game.Title,
                Platform = GamePlatform.Steam,
                CurrentVersion = string.IsNullOrWhiteSpace(buildId) ? "Installed build" : buildId,
                LatestVersion = updateRequiredByDownload ? "Pending download" : "Newer build available",
                Details = details,
                Source = "Steam appmanifest"
            });
        }

        return updates;
    }

    private static async Task<(IReadOnlyList<GameUpdateInfo> Updates, IReadOnlyList<string> Notes)> CheckEpicUpdatesAsync(
        IReadOnlyList<GameInfo> installedGames,
        CancellationToken cancellationToken)
    {
        var epicGames = installedGames
            .Where(game => game.Platform == GamePlatform.EpicGames)
            .ToDictionary(game => game.GameId, StringComparer.OrdinalIgnoreCase);

        if (epicGames.Count == 0)
        {
            return (Array.Empty<GameUpdateInfo>(), Array.Empty<string>());
        }

        var legendaryExe = ResolveLegendaryExecutable();
        if (string.IsNullOrWhiteSpace(legendaryExe))
        {
            return (Array.Empty<GameUpdateInfo>(), new[] { "Legendary was not found. Epic update checks are unavailable." });
        }

        var output = await RunProcessCaptureAsync(legendaryExe, new[] { "list-installed", "--check-updates", "--json" }, cancellationToken);
        if (string.IsNullOrWhiteSpace(output))
        {
            return (Array.Empty<GameUpdateInfo>(), new[] { "Epic update check returned no data." });
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(output);
        }
        catch
        {
            return (Array.Empty<GameUpdateInfo>(), new[] { "Epic update output could not be parsed." });
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return (Array.Empty<GameUpdateInfo>(), new[] { "Epic update output format is unsupported." });
            }

            var updates = new List<GameUpdateInfo>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var appName = GetJsonString(item, "app_name", "appName");
                if (string.IsNullOrWhiteSpace(appName) || !epicGames.TryGetValue(appName, out var game))
                {
                    continue;
                }

                var currentVersion = GetJsonString(item, "version", "installed_version", "build_version");
                var latestVersion = GetJsonString(item, "update_version", "latest_version", "new_version");
                var statusText = GetJsonString(item, "status");

                var updateAvailable =
                    GetJsonBool(item, "update_available", "needs_update", "has_update", "updateAvailable") ||
                    (!string.IsNullOrWhiteSpace(currentVersion) &&
                     !string.IsNullOrWhiteSpace(latestVersion) &&
                     !string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase)) ||
                    statusText.Contains("update", StringComparison.OrdinalIgnoreCase);

                if (!updateAvailable)
                {
                    continue;
                }

                updates.Add(new GameUpdateInfo
                {
                    GameKey = game.Key,
                    Title = game.Title,
                    Platform = GamePlatform.EpicGames,
                    CurrentVersion = string.IsNullOrWhiteSpace(currentVersion) ? "Installed build" : currentVersion,
                    LatestVersion = string.IsNullOrWhiteSpace(latestVersion) ? "Newer build available" : latestVersion,
                    Details = string.IsNullOrWhiteSpace(statusText) ? "Epic reports an update is available." : statusText,
                    Source = "Legendary update check"
                });
            }

            return (updates, Array.Empty<string>());
        }
    }

    private static async Task<string> RunProcessCaptureAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return string.Empty;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            if (!string.IsNullOrWhiteSpace(output))
            {
                return output;
            }

            return await errorTask;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool TryExtractSteamAppId(string gameId, out string appId)
    {
        appId = string.Empty;
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return false;
        }

        var digits = new string(gameId.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return false;
        }

        appId = digits;
        return true;
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static string ResolveSteamInstallPath()
    {
        var candidates = new[]
        {
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath"),
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam"
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) ?? string.Empty;
    }

    private static string ResolveLegendaryExecutable()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(entry.Trim(), "legendary.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed entries.
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(localAppData, "Programs", "Python", "Python314", "Scripts", "legendary.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python313", "Scripts", "legendary.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python312", "Scripts", "legendary.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static string GetJsonString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool GetJsonBool(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.String &&
                bool.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return false;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        const double kb = 1024d;
        const double mb = kb * 1024d;
        const double gb = mb * 1024d;

        if (bytes >= gb)
        {
            return $"{bytes / gb:0.00} GB";
        }

        if (bytes >= mb)
        {
            return $"{bytes / mb:0.0} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / kb:0} KB";
        }

        return $"{bytes} B";
    }
}
