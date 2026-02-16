using Microsoft.Data.Sqlite;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.Services.Detection;

public sealed class GogDetector : IPlatformDetector
{
    public string PlatformName => "GOG";

    public Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var detected = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in DetectFromGalaxyDatabase(cancellationToken))
        {
            detected[game.Key] = game;
        }

        foreach (var game in DetectFromRegistry(cancellationToken))
        {
            detected[game.Key] = game;
        }

        return Task.FromResult<IReadOnlyList<GameInfo>>(detected.Values
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private static IEnumerable<GameInfo> DetectFromGalaxyDatabase(CancellationToken cancellationToken)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GOG.com",
            "Galaxy",
            "storage",
            "galaxy-2.0.db");

        if (!File.Exists(dbPath))
        {
            yield break;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        try
        {
            connection.Open();
        }
        catch
        {
            yield break;
        }

        const string query = """
            SELECT gameId, gameName, installPath, executable, installedSize
            FROM GamePieces
            WHERE isInstalled = 1
            """;

        using var command = connection.CreateCommand();
        command.CommandText = query;

        SqliteDataReader? reader = null;
        try
        {
            reader = command.ExecuteReader();
        }
        catch
        {
            yield break;
        }

        using (reader)
        {
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var gameId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var installPath = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var executable = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                var installedSize = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);

                if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(installPath))
                {
                    continue;
                }

                if (!Directory.Exists(installPath))
                {
                    continue;
                }

                var executablePath = executable;
                if (!Path.IsPathRooted(executablePath) && !string.IsNullOrWhiteSpace(executablePath))
                {
                    executablePath = Path.Combine(installPath, executablePath);
                }

                if (!File.Exists(executablePath))
                {
                    executablePath = FileSystemHelper.FindLikelyExecutable(installPath);
                }

                yield return new GameInfo
                {
                    GameId = gameId,
                    Platform = GamePlatform.GOG,
                    Title = title,
                    Description = "Detected from GOG Galaxy",
                    InstallPath = installPath,
                    ExecutablePath = executablePath,
                    LaunchUri = $"goggalaxy://openGameView/{gameId}",
                    IsInstalled = true,
                    SizeBytes = installedSize,
                    CoverArtUrl = $"https://images.gog-statics.com/games/{gameId}/box_art.jpg"
                };
            }
        }
    }

    private static IEnumerable<GameInfo> DetectFromRegistry(CancellationToken cancellationToken)
    {
        var basePaths = new[]
        {
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\Games"
        };

        foreach (var basePath in basePaths)
        {
            foreach (var gameKey in RegistryHelper.GetSubKeyNames(basePath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath = $"{basePath}\\{gameKey}";
                var title = RegistryHelper.ReadString(fullPath, "GAMENAME");
                var installPath = RegistryHelper.ReadString(fullPath, "PATH");
                var executable = RegistryHelper.ReadString(fullPath, "EXE");

                if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = gameKey;
                }

                if (!Path.IsPathRooted(executable) && !string.IsNullOrWhiteSpace(executable))
                {
                    executable = Path.Combine(installPath, executable);
                }

                if (!File.Exists(executable))
                {
                    executable = FileSystemHelper.FindLikelyExecutable(installPath);
                }

                yield return new GameInfo
                {
                    GameId = gameKey,
                    Platform = GamePlatform.GOG,
                    Title = title,
                    Description = "Detected from GOG registry entries",
                    InstallPath = installPath,
                    ExecutablePath = executable,
                    LaunchUri = $"goggalaxy://openGameView/{gameKey}",
                    IsInstalled = true,
                    SizeBytes = FileSystemHelper.EstimateDirectorySize(installPath),
                    CoverArtUrl = $"https://images.gog-statics.com/games/{gameKey}/box_art.jpg"
                };
            }
        }
    }
}
