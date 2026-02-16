using System.Text.RegularExpressions;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.Services.Detection;

public sealed class SteamDetector : IPlatformDetector
{
    public string PlatformName => "Steam";

    public Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var steamPath = GetSteamInstallPath();
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
        {
            return Task.FromResult<IReadOnlyList<GameInfo>>(Array.Empty<GameInfo>());
        }

        var libraryFolders = GetLibraryFolders(steamPath);
        var appStats = LoadAppStats(steamPath);

        var games = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var libraryRoot in libraryFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var steamAppsPath = libraryRoot.EndsWith("steamapps", StringComparison.OrdinalIgnoreCase)
                ? libraryRoot
                : Path.Combine(libraryRoot, "steamapps");

            if (!Directory.Exists(steamAppsPath))
            {
                continue;
            }

            IEnumerable<string> manifestPaths;
            try
            {
                manifestPaths = Directory.EnumerateFiles(steamAppsPath, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var manifestPath in manifestPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryParseAppManifest(manifestPath, out var appId, out var title, out var installDir, out var sizeOnDisk, out var installDate, out var isInstalled) ||
                    !isInstalled)
                {
                    continue;
                }

                var installPath = Path.Combine(steamAppsPath, "common", installDir);
                if (!Directory.Exists(installPath))
                {
                    installPath = string.Empty;
                }

                appStats.TryGetValue(appId, out var stat);

                var game = new GameInfo
                {
                    GameId = appId,
                    Platform = GamePlatform.Steam,
                    Title = title,
                    Description = "Detected from Steam library",
                    InstallPath = installPath,
                    ExecutablePath = FileSystemHelper.FindLikelyExecutable(installPath),
                    LaunchUri = $"steam://rungameid/{appId}",
                    IsInstalled = isInstalled,
                    SizeBytes = sizeOnDisk,
                    InstallDate = installDate,
                    Playtime = TimeSpan.FromMinutes(stat.PlaytimeMinutes),
                    LastPlayed = stat.LastPlayed,
                    CoverArtUrl = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/library_600x900.jpg"
                };

                games[game.Key] = game;
            }
        }

        return Task.FromResult<IReadOnlyList<GameInfo>>(games.Values
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private static string GetSteamInstallPath()
    {
        var candidates = new[]
        {
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath"),
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam"
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> GetLibraryFolders(string steamPath)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            steamPath
        };

        var libraryFilePath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        var root = VdfParser.ParseFile(libraryFilePath);

        var libraryFolders = root.GetObject("libraryfolders") ?? root;

        foreach (var entry in libraryFolders)
        {
            if (!Regex.IsMatch(entry.Key, "^\\d+$"))
            {
                continue;
            }

            if (entry.Value is VdfObject folderObject)
            {
                var path = folderObject.GetString("path").Replace("\\\\", "\\");
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    folders.Add(path);
                }
            }
            else if (entry.Value is string folderPath)
            {
                folderPath = folderPath.Replace("\\\\", "\\");
                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    folders.Add(folderPath);
                }
            }
        }

        return folders.ToList();
    }

    private static bool TryParseAppManifest(
        string manifestPath,
        out string appId,
        out string title,
        out string installDir,
        out long sizeOnDisk,
        out DateTimeOffset? installDate,
        out bool isInstalled)
    {
        appId = string.Empty;
        title = string.Empty;
        installDir = string.Empty;
        sizeOnDisk = 0;
        installDate = null;
        isInstalled = false;

        var root = VdfParser.ParseFile(manifestPath);
        var appState = root.GetObject("AppState") ?? root;

        appId = appState.GetString("appid");
        title = appState.GetString("name");
        installDir = appState.GetString("installdir");

        if (!long.TryParse(appState.GetString("SizeOnDisk"), out sizeOnDisk))
        {
            sizeOnDisk = 0;
        }

        if (long.TryParse(appState.GetString("LastUpdated"), out var lastUpdatedUnix) && lastUpdatedUnix > 0)
        {
            installDate = DateTimeOffset.FromUnixTimeSeconds(lastUpdatedUnix);
        }

        if (int.TryParse(appState.GetString("StateFlags"), out var stateFlags))
        {
            isInstalled = (stateFlags & 0x4) != 0;
        }

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return true;
    }

    private static Dictionary<string, (long PlaytimeMinutes, DateTimeOffset? LastPlayed)> LoadAppStats(string steamPath)
    {
        var stats = new Dictionary<string, (long PlaytimeMinutes, DateTimeOffset? LastPlayed)>(StringComparer.OrdinalIgnoreCase);

        var userdataPath = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userdataPath))
        {
            return stats;
        }

        IEnumerable<string> localConfigFiles;
        try
        {
            localConfigFiles = Directory.EnumerateFiles(userdataPath, "localconfig.vdf", SearchOption.AllDirectories);
        }
        catch
        {
            return stats;
        }

        foreach (var localConfigFile in localConfigFiles)
        {
            var root = VdfParser.ParseFile(localConfigFile);
            var appsNode = root
                .GetObject("UserLocalConfigStore")?
                .GetObject("Software")?
                .GetObject("Valve")?
                .GetObject("Steam")?
                .GetObject("Apps");

            if (appsNode is null)
            {
                continue;
            }

            foreach (var appEntry in appsNode)
            {
                if (appEntry.Value is not VdfObject appStats)
                {
                    continue;
                }

                var playtime = 0L;
                if (long.TryParse(appStats.GetString("Playtime"), out var playtimeValue))
                {
                    playtime = playtimeValue;
                }

                DateTimeOffset? lastPlayed = null;
                if (long.TryParse(appStats.GetString("LastPlayed"), out var lastPlayedUnix) && lastPlayedUnix > 0)
                {
                    lastPlayed = DateTimeOffset.FromUnixTimeSeconds(lastPlayedUnix);
                }

                if (!stats.TryGetValue(appEntry.Key, out var existing))
                {
                    stats[appEntry.Key] = (playtime, lastPlayed);
                    continue;
                }

                var mergedPlaytime = Math.Max(existing.PlaytimeMinutes, playtime);
                var mergedLastPlayed = existing.LastPlayed > lastPlayed ? existing.LastPlayed : lastPlayed;
                stats[appEntry.Key] = (mergedPlaytime, mergedLastPlayed);
            }
        }

        return stats;
    }
}
