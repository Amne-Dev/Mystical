using System.Diagnostics;
using System.Text.Json;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.Services.Detection;

public sealed class EpicAccountDetector : IPlatformDetector
{
    private readonly ISettingsService _settingsService;

    public EpicAccountDetector(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string PlatformName => "Epic Account";

    public async Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync();
        if (!settings.EnableEpicAccountSync)
        {
            return Array.Empty<GameInfo>();
        }

        var manifestDirectories = GetManifestDirectories(settings);
        var detected = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifestDirectory in manifestDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<string> itemManifestFiles;
            try
            {
                itemManifestFiles = Directory.EnumerateFiles(manifestDirectory, "*.item", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var itemManifestFile in itemManifestFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                JsonDocument? document = null;
                try
                {
                    await using var stream = File.OpenRead(itemManifestFile);
                    document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                }
                catch
                {
                    continue;
                }

                using (document)
                {
                    if (document is null)
                    {
                        continue;
                    }

                    var root = document.RootElement;

                    var appName = GetString(root, "AppName");
                    if (string.IsNullOrWhiteSpace(appName))
                    {
                        continue;
                    }

                    var title = GetString(root, "DisplayName");
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = appName;
                    }

                    var installLocation = GetString(root, "InstallLocation");
                    var launchExecutable = GetString(root, "LaunchExecutable");
                    var catalogItemId = GetString(root, "CatalogItemId");
                    if (string.IsNullOrWhiteSpace(catalogItemId))
                    {
                        catalogItemId = GetString(root, "MainGameCatalogItemId");
                    }

                    var isInstalled = !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation);

                    var executablePath = string.Empty;
                    if (isInstalled && !string.IsNullOrWhiteSpace(launchExecutable))
                    {
                        var candidate = Path.Combine(installLocation, launchExecutable);
                        if (File.Exists(candidate))
                        {
                            executablePath = candidate;
                        }
                    }

                    var sizeBytes = ResolveSizeBytes(root, installLocation, isInstalled);

                    var game = new GameInfo
                    {
                        GameId = appName,
                        Platform = GamePlatform.EpicGames,
                        Title = title,
                        Description = isInstalled
                            ? "Detected from linked Epic account"
                            : "Owned on linked Epic account",
                        InstallPath = isInstalled ? installLocation : string.Empty,
                        ExecutablePath = executablePath,
                        LaunchUri = isInstalled
                            ? $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true"
                            : $"com.epicgames.launcher://apps/{appName}?action=install",
                        IsInstalled = isInstalled,
                        SizeBytes = sizeBytes,
                        CoverArtUrl = EpicCoverArtHelper.ResolveCoverArtUrl(root, catalogItemId, appName)
                    };

                    if (!detected.TryGetValue(game.Key, out var existing))
                    {
                        detected[game.Key] = game;
                        continue;
                    }

                    detected[game.Key] = MergeEpicGames(existing, game);
                }
            }
        }

        foreach (var game in await DetectFromLegendaryAsync(cancellationToken))
        {
            if (detected.TryGetValue(game.Key, out var existing))
            {
                detected[game.Key] = MergeEpicGames(existing, game);
                continue;
            }

            detected[game.Key] = game;
        }

        return detected.Values
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetManifestDirectories(AppSettings settings)
    {
        var manifestDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(settings.EpicManifestPathOverride))
        {
            var expanded = Environment.ExpandEnvironmentVariables(settings.EpicManifestPathOverride.Trim());
            if (Directory.Exists(expanded))
            {
                manifestDirectories.Add(expanded);
            }
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var defaultCandidates = new[]
        {
            Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests"),
            Path.Combine(programData, "Epic", "UnrealEngineLauncher", "Data", "Manifests"),
            Path.Combine(programData, "EpicGamesLauncher", "Data", "Manifests"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "Manifests")
        };

        foreach (var candidate in defaultCandidates)
        {
            if (Directory.Exists(candidate))
            {
                manifestDirectories.Add(candidate);
            }
        }

        return manifestDirectories.ToList();
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numericValue))
        {
            return numericValue;
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var stringValue))
        {
            return stringValue;
        }

        return 0;
    }

    private static long ResolveSizeBytes(JsonElement root, string installLocation, bool isInstalled)
    {
        var sizeBytes = GetLong(root, "InstallSize");
        if (sizeBytes > 0)
        {
            return sizeBytes;
        }

        var sizeMb = GetLong(root, "InstallSizeMb");
        if (sizeMb <= 0)
        {
            sizeMb = GetLong(root, "InstallSizeMB");
        }

        if (sizeMb > 0)
        {
            return sizeMb * 1024L * 1024L;
        }

        if (isInstalled && !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            return FileSystemHelper.EstimateDirectorySize(installLocation, maxFiles: 2500);
        }

        return 0;
    }

    private static async Task<IReadOnlyList<GameInfo>> DetectFromLegendaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var executable = ResolveLegendaryExecutable();
            if (string.IsNullOrWhiteSpace(executable))
            {
                return Array.Empty<GameInfo>();
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "list-games --json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Array.Empty<GameInfo>();
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                return Array.Empty<GameInfo>();
            }

            var output = await outputTask;
            if (string.IsNullOrWhiteSpace(output))
            {
                return Array.Empty<GameInfo>();
            }

            using var document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<GameInfo>();
            }

            var games = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var appName = GetString(item, "app_name");
                if (string.IsNullOrWhiteSpace(appName))
                {
                    continue;
                }

                var title = GetString(item, "app_title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = appName;
                }

                var catalogItemId = GetString(item, "catalog_item_id");
                if (string.IsNullOrWhiteSpace(catalogItemId))
                {
                    catalogItemId = GetString(item, "catalogItemId");
                }

                var game = new GameInfo
                {
                    GameId = appName,
                    Platform = GamePlatform.EpicGames,
                    Title = title,
                    Description = "Owned on linked Epic account",
                    InstallPath = string.Empty,
                    ExecutablePath = string.Empty,
                    LaunchUri = $"com.epicgames.launcher://apps/{appName}?action=install",
                    IsInstalled = false,
                    CoverArtUrl = EpicCoverArtHelper.ResolveCoverArtUrl(item, catalogItemId, appName)
                };

                games[game.Key] = game;
            }

            return games.Values
                .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<GameInfo>();
        }
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
                // Ignore malformed PATH entries.
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var commonCandidates = new[]
        {
            Path.Combine(localAppData, "Programs", "Python", "Python314", "Scripts", "legendary.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python313", "Scripts", "legendary.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python312", "Scripts", "legendary.exe")
        };

        return commonCandidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static GameInfo MergeEpicGames(GameInfo existing, GameInfo incoming)
    {
        var preferred = existing.IsInstalled && !incoming.IsInstalled
            ? existing
            : !existing.IsInstalled && incoming.IsInstalled
                ? incoming
                : incoming;

        var secondary = ReferenceEquals(preferred, incoming) ? existing : incoming;

        preferred.CoverArtUrl = EpicCoverArtHelper.PickBetterCoverUrl(preferred.CoverArtUrl, secondary.CoverArtUrl);

        if (string.IsNullOrWhiteSpace(preferred.Description))
        {
            preferred.Description = secondary.Description;
        }

        if (string.IsNullOrWhiteSpace(preferred.InstallPath))
        {
            preferred.InstallPath = secondary.InstallPath;
        }

        if (string.IsNullOrWhiteSpace(preferred.ExecutablePath))
        {
            preferred.ExecutablePath = secondary.ExecutablePath;
        }

        return preferred;
    }
}
