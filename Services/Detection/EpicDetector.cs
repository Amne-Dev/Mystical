using System.Text.Json;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.Services.Detection;

public sealed class EpicDetector : IPlatformDetector
{
    public string PlatformName => "Epic Games";

    public async Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var manifestPath = GetLauncherInstalledPath();
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return Array.Empty<GameInfo>();
        }

        var itemManifestCoverLookup = await ReadItemManifestCoverLookupAsync(cancellationToken);

        await using var stream = File.OpenRead(manifestPath);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("InstallationList", out var installationList) || installationList.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<GameInfo>();
        }

        var games = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var installation in installationList.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var appName = GetString(installation, "AppName");
            var displayName = GetString(installation, "DisplayName");
            var installLocation = GetString(installation, "InstallLocation");
            var launchExecutable = GetString(installation, "LaunchExecutable");
            var catalogItemId = GetString(installation, "CatalogItemId");

            if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(installLocation))
            {
                continue;
            }

            if (!Directory.Exists(installLocation))
            {
                continue;
            }

            if (displayName.Contains("Launcher", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var executablePath = string.Empty;
            if (!string.IsNullOrWhiteSpace(launchExecutable))
            {
                var candidate = Path.Combine(installLocation, launchExecutable);
                if (File.Exists(candidate))
                {
                    executablePath = candidate;
                }
            }

            var coverArtUrl = itemManifestCoverLookup.TryGetValue(appName, out var itemCoverUrl)
                ? itemCoverUrl
                : EpicCoverArtHelper.BuildFallbackOfferCoverUrl(catalogItemId, appName);

            var sizeBytes = GetLong(installation, "InstallSize");
            if (sizeBytes <= 0)
            {
                sizeBytes = FileSystemHelper.EstimateDirectorySize(installLocation, maxFiles: 2500);
            }

            var game = new GameInfo
            {
                GameId = appName,
                Platform = GamePlatform.EpicGames,
                Title = displayName,
                Description = "Detected from Epic Games Launcher",
                InstallPath = installLocation,
                ExecutablePath = executablePath,
                LaunchUri = $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true",
                IsInstalled = true,
                SizeBytes = sizeBytes,
                CoverArtUrl = coverArtUrl
            };

            games[game.Key] = game;
        }

        return games.Values
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetLauncherInstalledPath()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var candidates = new[]
        {
            Path.Combine(programData, "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat"),
            Path.Combine(programData, "EpicGamesLauncher", "Data", "LauncherInstalled.dat"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpicGamesLauncher", "Saved", "LauncherInstalled.dat")
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadItemManifestCoverLookupAsync(CancellationToken cancellationToken)
    {
        var coverLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in GetItemManifestDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<string> itemFiles;
            try
            {
                itemFiles = Directory.EnumerateFiles(directory, "*.item", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var itemFile in itemFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                JsonDocument? document = null;
                try
                {
                    await using var stream = File.OpenRead(itemFile);
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

                    var catalogItemId = GetString(root, "CatalogItemId");
                    if (string.IsNullOrWhiteSpace(catalogItemId))
                    {
                        catalogItemId = GetString(root, "MainGameCatalogItemId");
                    }

                    var coverUrl = EpicCoverArtHelper.ResolveCoverArtUrl(root, catalogItemId, appName);
                    if (!string.IsNullOrWhiteSpace(coverUrl))
                    {
                        coverLookup[appName] = coverUrl;
                    }
                }
            }
        }

        return coverLookup;
    }

    private static IReadOnlyList<string> GetItemManifestDirectories()
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidates = new[]
        {
            Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests"),
            Path.Combine(programData, "Epic", "UnrealEngineLauncher", "Data", "Manifests"),
            Path.Combine(programData, "EpicGamesLauncher", "Data", "Manifests"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "Manifests")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                directories.Add(candidate);
            }
        }

        return directories.ToList();
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
}
