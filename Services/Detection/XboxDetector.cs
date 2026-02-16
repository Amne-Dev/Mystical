using System.Xml.Linq;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.Services.Detection;

public sealed class XboxDetector : IPlatformDetector
{
    public string PlatformName => "Xbox";

    public Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = GetXboxLibraryRoots();
        if (libraries.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<GameInfo>>(Array.Empty<GameInfo>());
        }

        var games = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var libraryRoot in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var gameFolder in EnumerateGameFolders(libraryRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var contentFolder = Directory.Exists(Path.Combine(gameFolder, "Content"))
                    ? Path.Combine(gameFolder, "Content")
                    : gameFolder;

                var configPath = FindFirstExistingFile(
                    Path.Combine(gameFolder, "MicrosoftGame.config"),
                    Path.Combine(contentFolder, "MicrosoftGame.config"));

                var appxManifestPath = FindFirstExistingFile(
                    Path.Combine(gameFolder, "AppxManifest.xml"),
                    Path.Combine(gameFolder, "appxmanifest.xml"),
                    Path.Combine(contentFolder, "AppxManifest.xml"),
                    Path.Combine(contentFolder, "appxmanifest.xml"));

                var parsed = ParseGameConfig(configPath);
                if (IsEmpty(parsed))
                {
                    parsed = ParseAppxManifest(appxManifestPath);
                }

                var title = CleanTitle(parsed.Title);
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = Path.GetFileName(gameFolder);
                }

                var gameId = !string.IsNullOrWhiteSpace(parsed.GameId)
                    ? parsed.GameId
                    : SanitizeId(Path.GetFileName(gameFolder));

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(gameId))
                {
                    continue;
                }

                var executablePath = ResolveRelativeFile(parsed.ExecutableRelativePath, contentFolder, gameFolder);
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    executablePath = FileSystemHelper.FindLikelyExecutable(contentFolder);
                }

                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    executablePath = FileSystemHelper.FindLikelyExecutable(gameFolder);
                }

                var coverArtPath = ResolveRelativeFile(parsed.CoverRelativePath, contentFolder, gameFolder);
                if (string.IsNullOrWhiteSpace(coverArtPath) || !File.Exists(coverArtPath))
                {
                    coverArtPath = string.Empty;
                }

                var game = new GameInfo
                {
                    GameId = gameId,
                    Platform = GamePlatform.Xbox,
                    Title = title,
                    Description = "Detected from Xbox/Microsoft Store library",
                    InstallPath = contentFolder,
                    ExecutablePath = executablePath,
                    IsInstalled = true,
                    SizeBytes = FileSystemHelper.EstimateDirectorySize(contentFolder, maxFiles: 2500),
                    CoverArtPath = coverArtPath
                };

                games[game.Key] = game;
            }
        }

        return Task.FromResult<IReadOnlyList<GameInfo>>(games.Values
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private static IReadOnlyList<string> GetXboxLibraryRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                {
                    continue;
                }

                var candidateRoots = new[]
                {
                    Path.Combine(drive.RootDirectory.FullName, "XboxGames"),
                    Path.Combine(drive.RootDirectory.FullName, "ModifiableWindowsApps"),
                    Path.Combine(drive.RootDirectory.FullName, "Program Files", "XboxGames"),
                    Path.Combine(drive.RootDirectory.FullName, "Program Files", "ModifiableWindowsApps")
                };

                foreach (var candidate in candidateRoots)
                {
                    if (Directory.Exists(candidate))
                    {
                        roots.Add(candidate);
                    }
                }
            }
        }
        catch
        {
            // Ignore drive enumeration failures.
        }

        var registryCandidates = new[]
        {
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\GamingServices", "AppInstallPath"),
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\GamingServices", "GameInstallPath"),
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\GamingServices", "AppInstallPath"),
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\GamingServices", "GameInstallPath"),
            RegistryHelper.ReadString(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\GamingServices", "AppInstallPath")
        };

        foreach (var candidate in registryCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var expanded = Environment.ExpandEnvironmentVariables(candidate.Trim());
            var normalized = expanded.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (Directory.Exists(normalized))
            {
                if (IsXboxLibraryRoot(normalized))
                {
                    roots.Add(normalized);
                }
                else
                {
                    var nestedCandidates = new[]
                    {
                        Path.Combine(normalized, "XboxGames"),
                        Path.Combine(normalized, "ModifiableWindowsApps"),
                        Path.Combine(normalized, "Program Files", "XboxGames"),
                        Path.Combine(normalized, "Program Files", "ModifiableWindowsApps")
                    };

                    foreach (var nestedCandidate in nestedCandidates)
                    {
                        if (Directory.Exists(nestedCandidate))
                        {
                            roots.Add(nestedCandidate);
                        }
                    }
                }
            }
        }

        var programFilesXboxGames = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XboxGames");
        if (Directory.Exists(programFilesXboxGames))
        {
            roots.Add(programFilesXboxGames);
        }

        var programFiles86XboxGames = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "XboxGames");
        if (Directory.Exists(programFiles86XboxGames))
        {
            roots.Add(programFiles86XboxGames);
        }

        var modifiableWindowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ModifiableWindowsApps");
        if (Directory.Exists(modifiableWindowsApps))
        {
            roots.Add(modifiableWindowsApps);
        }

        var modifiableWindowsApps86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ModifiableWindowsApps");
        if (Directory.Exists(modifiableWindowsApps86))
        {
            roots.Add(modifiableWindowsApps86);
        }

        return roots.ToList();
    }

    private static (string Title, string GameId, string ExecutableRelativePath, string CoverRelativePath) ParseGameConfig(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return (string.Empty, string.Empty, string.Empty, string.Empty);
        }

        try
        {
            var document = XDocument.Load(configPath);

            var gameNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Game");
            var identityNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Identity");
            var executableNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Executable");
            var shellVisualsNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals");

            var title = ReadAttribute(gameNode, "Name");
            if (string.IsNullOrWhiteSpace(title))
            {
                title = ReadAttribute(shellVisualsNode, "DisplayName");
            }

            var gameId = ReadAttribute(identityNode, "Name");
            if (string.IsNullOrWhiteSpace(gameId))
            {
                gameId = ReadAttribute(gameNode, "Id");
            }

            var executable = ReadAttribute(executableNode, "Name");
            if (string.IsNullOrWhiteSpace(executable))
            {
                executable = ReadAttribute(executableNode, "Executable");
            }

            var cover = ReadAttribute(shellVisualsNode, "Square150x150Logo");
            if (string.IsNullOrWhiteSpace(cover))
            {
                cover = ReadAttribute(shellVisualsNode, "StoreLogo");
            }

            return (title, gameId, executable, cover);
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    private static (string Title, string GameId, string ExecutableRelativePath, string CoverRelativePath) ParseAppxManifest(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return (string.Empty, string.Empty, string.Empty, string.Empty);
        }

        try
        {
            var document = XDocument.Load(manifestPath);
            var identityNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Identity");
            var propertiesNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Properties");
            var applicationNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Application");
            var visualNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName.EndsWith("VisualElements", StringComparison.OrdinalIgnoreCase));

            var title = ReadElement(propertiesNode, "DisplayName");
            if (string.IsNullOrWhiteSpace(title))
            {
                title = ReadAttribute(visualNode, "DisplayName");
            }

            var gameId = ReadAttribute(identityNode, "Name");
            var executable = ReadAttribute(applicationNode, "Executable");

            var cover = ReadElement(propertiesNode, "Logo");
            if (string.IsNullOrWhiteSpace(cover))
            {
                cover = ReadAttribute(visualNode, "Square150x150Logo");
            }

            if (string.IsNullOrWhiteSpace(cover))
            {
                cover = ReadAttribute(visualNode, "Square44x44Logo");
            }

            return (title, gameId, executable, cover);
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    private static string ReadAttribute(XElement? element, string attributeName)
    {
        return element?.Attributes().FirstOrDefault(a =>
                   a.Name.LocalName.Equals(attributeName, StringComparison.OrdinalIgnoreCase))?.Value
               ?? string.Empty;
    }

    private static string ReadElement(XElement? parent, string elementName)
    {
        return parent?.Elements().FirstOrDefault(e =>
                   e.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase))?.Value
               ?? string.Empty;
    }

    private static IEnumerable<string> EnumerateGameFolders(string libraryRoot)
    {
        IEnumerable<string> folders;
        try
        {
            folders = Directory.EnumerateDirectories(libraryRoot, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        foreach (var folder in folders)
        {
            var name = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(name) ||
                string.Equals(name, "AppxMetadata", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "MutableBackup", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "MutableDeleted", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return folder;
        }
    }

    private static string FindFirstExistingFile(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static bool IsXboxLibraryRoot(string path)
    {
        return path.EndsWith("XboxGames", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("ModifiableWindowsApps", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmpty((string Title, string GameId, string ExecutableRelativePath, string CoverRelativePath) parsed)
    {
        return string.IsNullOrWhiteSpace(parsed.Title) &&
               string.IsNullOrWhiteSpace(parsed.GameId) &&
               string.IsNullOrWhiteSpace(parsed.ExecutableRelativePath) &&
               string.IsNullOrWhiteSpace(parsed.CoverRelativePath);
    }

    private static string CleanTitle(string title)
    {
        var cleaned = title.Trim();
        if (cleaned.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return cleaned;
    }

    private static string ResolveRelativeFile(string relativeOrAbsolutePath, params string[] baseFolders)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(relativeOrAbsolutePath) && File.Exists(relativeOrAbsolutePath))
        {
            return relativeOrAbsolutePath;
        }

        var normalizedRelative = relativeOrAbsolutePath
            .Trim()
            .TrimStart('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        foreach (var baseFolder in baseFolders)
        {
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                continue;
            }

            var candidate = Path.Combine(baseFolder, normalizedRelative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string SanitizeId(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
            .ToArray();

        return chars.Length == 0 ? "xbox-game" : new string(chars);
    }
}
