using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services.Detection.Internal;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace Mystical.WinUI.Services.Detection;

public sealed class MicrosoftStoreDetector : IPlatformDetector
{
    private static readonly string[] GameKeywords =
    {
        "celeste",
        "forza",
        "halo",
        "minecraft",
        "gears",
        "flight simulator",
        "age of empires",
        "sea of thieves",
        "ori",
        "roblox",
        "among us",
        "fallout",
        "doom",
        "wolfenstein",
        "dishonored",
        "psychonauts"
    };

    private static readonly string[] GamePublisherKeywords =
    {
        "games",
        "gaming",
        "studios",
        "interactive"
    };

    private static readonly string[] NonGameKeywords =
    {
        "calculator",
        "terminal",
        "store",
        "edge",
        "office",
        "onenote",
        "outlook",
        "camera",
        "photos",
        "paint",
        "notepad",
        "todo",
        "clock",
        "maps",
        "mail",
        "teams",
        "webview",
        "codec",
        "runtime",
        "vclibs",
        "framework",
        "nvidia",
        "amd",
        "driver",
        "security",
        "vpn",
        "spotify",
        "whatsapp",
        "discord",
        "adobe",
        "hp",
        "dell",
        "lenovo",
        "intel",
        "game bar",
        "gaming services",
        "gamingservices",
        "xbox app",
        "xbox identity",
        "xbox tcui",
        "xbox game bar"
    };

    public string PlatformName => "Microsoft Store";

    public async Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var packageManager = new PackageManager();

        IEnumerable<Package> packages;
        try
        {
            packages = packageManager.FindPackagesForUser(string.Empty);
        }
        catch
        {
            return Array.Empty<GameInfo>();
        }

        var detected = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);
        var strictGamePackageFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCandidatePackage(package))
            {
                continue;
            }

            var packagePath = TryGetInstallPath(package);
            var manifestPath = FindFirstExistingFile(
                Path.Combine(packagePath, "AppxManifest.xml"),
                Path.Combine(packagePath, "appxmanifest.xml"));

            var parsedManifest = ParseManifest(manifestPath);
            var packageName = package.Id?.Name ?? string.Empty;
            var packageFamilyName = package.Id?.FamilyName ?? string.Empty;
            var publisherName = package.PublisherDisplayName ?? string.Empty;
            var displayName = package.DisplayName ?? string.Empty;
            var entryCandidates = new List<(string AppUserModelId, string Title)>();

            var classificationTitle = parsedManifest.Title;
            if (string.IsNullOrWhiteSpace(classificationTitle) || classificationTitle.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            {
                classificationTitle = displayName;
            }

            if (string.IsNullOrWhiteSpace(classificationTitle) || classificationTitle.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            {
                classificationTitle = GuessTitleFromPackageName(packageName);
            }

            if (!string.IsNullOrWhiteSpace(packageFamilyName) &&
                IsLikelyGame(classificationTitle, packageName, publisherName, packagePath))
            {
                strictGamePackageFamilies.Add(packageFamilyName);
            }

            try
            {
                var appEntries = await package.GetAppListEntriesAsync();
                foreach (var appEntry in appEntries)
                {
                    var appUserModelId = appEntry.AppUserModelId?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(appUserModelId))
                    {
                        continue;
                    }

                    var title = appEntry.DisplayInfo?.DisplayName?.Trim() ?? string.Empty;
                    entryCandidates.Add((appUserModelId, title));
                }
            }
            catch
            {
                // Fallback to manifest and package-family based entry resolution.
            }

            if (entryCandidates.Count == 0 && !string.IsNullOrWhiteSpace(packageFamilyName))
            {
                foreach (var appId in parsedManifest.ApplicationIds)
                {
                    var appUserModelId = BuildAppUserModelId(packageFamilyName, appId);
                    if (!string.IsNullOrWhiteSpace(appUserModelId))
                    {
                        entryCandidates.Add((appUserModelId, parsedManifest.Title));
                    }
                }

                if (entryCandidates.Count == 0)
                {
                    entryCandidates.Add((BuildAppUserModelId(packageFamilyName, "App"), parsedManifest.Title));
                }
            }

            foreach (var entryCandidate in entryCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var title = entryCandidate.Title;
                if (string.IsNullOrWhiteSpace(title) || title.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                {
                    title = parsedManifest.Title;
                }

                if (string.IsNullOrWhiteSpace(title) || title.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                {
                    title = displayName;
                }

                if (string.IsNullOrWhiteSpace(title) || title.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                {
                    title = GuessTitleFromPackageName(packageName);
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = packageName;
                }

                if (!IsLikelyGame(title, packageName, publisherName, packagePath))
                {
                    continue;
                }

                var appUserModelId = entryCandidate.AppUserModelId;
                if (string.IsNullOrWhiteSpace(appUserModelId))
                {
                    continue;
                }

                var coverArtPath = ResolveRelativeFile(parsedManifest.CoverRelativePath, packagePath);

                var game = new GameInfo
                {
                    GameId = SanitizeId(appUserModelId),
                    Platform = GamePlatform.MicrosoftStore,
                    Title = title,
                    Description = "Detected from Microsoft Store app package",
                    InstallPath = packagePath,
                    ExecutablePath = string.Empty,
                    LaunchUri = $"shell:AppsFolder\\{appUserModelId}",
                    IsInstalled = true,
                    SizeBytes = !string.IsNullOrWhiteSpace(packagePath)
                        ? FileSystemHelper.EstimateDirectorySize(packagePath, maxFiles: 1200)
                        : 0,
                    CoverArtPath = coverArtPath
                };

                detected[game.Key] = game;
            }
        }

        foreach (var game in DetectFromStartAppsFallback(strictGamePackageFamilies, cancellationToken))
        {
            detected[game.Key] = game;
        }

        return detected.Values
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsCandidatePackage(Package package)
    {
        if (package.IsFramework || package.IsResourcePackage)
        {
            return false;
        }

        if (package.SignatureKind == PackageSignatureKind.System)
        {
            return false;
        }

        var name = package.Id?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return !name.StartsWith("Microsoft.VCLibs", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("Microsoft.UI.Xaml", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("Microsoft.NET.Native", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyGame(string title, string packageName, string publisherName, string packagePath)
    {
        var combined = $"{title} {packageName} {publisherName}".ToLowerInvariant();
        if (NonGameKeywords.Any(keyword => combined.Contains(keyword, StringComparison.Ordinal)))
        {
            return false;
        }

        var score = 0;

        if (IsLikelyGameBySignals(title, packageName, packagePath))
        {
            score += 2;
        }

        var publisher = publisherName.ToLowerInvariant();
        if (GamePublisherKeywords.Any(keyword => publisher.Contains(keyword, StringComparison.Ordinal)))
        {
            score += 1;
        }

        if (packagePath.Contains("XboxGames", StringComparison.OrdinalIgnoreCase) ||
            packagePath.Contains("ModifiableWindowsApps", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        return score >= 2;
    }

    private static bool IsLikelyGameBySignals(string title, string packageName, string packagePath)
    {
        var combined = $"{title} {packageName}".ToLowerInvariant();

        if (NonGameKeywords.Any(keyword => combined.Contains(keyword, StringComparison.Ordinal)))
        {
            return false;
        }

        if (packagePath.Contains("XboxGames", StringComparison.OrdinalIgnoreCase) ||
            packagePath.Contains("ModifiableWindowsApps", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GameKeywords.Any(keyword => combined.Contains(keyword, StringComparison.Ordinal));
    }

    private static string TryGetInstallPath(Package package)
    {
        try
        {
            return package.InstalledLocation?.Path ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static (string Title, string CoverRelativePath, IReadOnlyList<string> ApplicationIds) ParseManifest(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return (string.Empty, string.Empty, Array.Empty<string>());
        }

        try
        {
            var document = XDocument.Load(manifestPath);
            var propertiesNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Properties");
            var visualNode = document.Descendants().FirstOrDefault(e => e.Name.LocalName.EndsWith("VisualElements", StringComparison.OrdinalIgnoreCase));
            var applicationIds = document
                .Descendants()
                .Where(e => e.Name.LocalName == "Application")
                .Select(e => ReadAttribute(e, "Id"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var title = ReadElement(propertiesNode, "DisplayName");
            if (string.IsNullOrWhiteSpace(title))
            {
                title = ReadAttribute(visualNode, "DisplayName");
            }

            var cover = ReadElement(propertiesNode, "Logo");
            if (string.IsNullOrWhiteSpace(cover))
            {
                cover = ReadAttribute(visualNode, "Square150x150Logo");
            }

            return (title, cover, applicationIds);
        }
        catch
        {
            return (string.Empty, string.Empty, Array.Empty<string>());
        }
    }

    private static string ReadElement(XElement? parent, string elementName)
    {
        return parent?.Elements().FirstOrDefault(e =>
                   e.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase))?.Value
               ?? string.Empty;
    }

    private static string ReadAttribute(XElement? element, string attributeName)
    {
        return element?.Attributes().FirstOrDefault(a =>
                   a.Name.LocalName.Equals(attributeName, StringComparison.OrdinalIgnoreCase))?.Value
               ?? string.Empty;
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

    private static string BuildAppUserModelId(string packageFamilyName, string appId)
    {
        if (string.IsNullOrWhiteSpace(packageFamilyName) || string.IsNullOrWhiteSpace(appId))
        {
            return string.Empty;
        }

        return $"{packageFamilyName}!{appId}";
    }

    private static string GuessTitleFromPackageName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return string.Empty;
        }

        var lastSegment = packageName.Split('.').LastOrDefault() ?? packageName;
        return lastSegment.Replace('_', ' ').Trim();
    }

    private static IEnumerable<GameInfo> DetectFromStartAppsFallback(
        ISet<string> strictGamePackageFamilies,
        CancellationToken cancellationToken)
    {
        var startApps = ReadStartApps();
        if (startApps.Count == 0)
        {
            yield break;
        }

        foreach (var app in startApps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(app.AppId) || string.IsNullOrWhiteSpace(app.Name))
            {
                continue;
            }

            if (!app.AppId.Contains('!'))
            {
                continue;
            }

            var separatorIndex = app.AppId.IndexOf('!');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var packageFamilyName = app.AppId[..separatorIndex];
            var allowedByFamily = strictGamePackageFamilies.Contains(packageFamilyName);
            var allowedByName = IsLikelyGameBySignals(app.Name, app.AppId, string.Empty);

            if (!allowedByFamily && !allowedByName)
            {
                continue;
            }

            yield return new GameInfo
            {
                GameId = SanitizeId(app.AppId),
                Platform = GamePlatform.MicrosoftStore,
                Title = app.Name,
                Description = "Detected from Start app registrations",
                InstallPath = string.Empty,
                ExecutablePath = string.Empty,
                LaunchUri = $"shell:AppsFolder\\{app.AppId}",
                IsInstalled = true,
                SizeBytes = 0
            };
        }
    }

    private static IReadOnlyList<(string Name, string AppId)> ReadStartApps()
    {
        try
        {
            const string command = "Get-StartApps | Select-Object Name,AppID | ConvertTo-Json -Compress";
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Array.Empty<(string Name, string AppId)>();
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (string.IsNullOrWhiteSpace(output))
            {
                return Array.Empty<(string Name, string AppId)>();
            }

            using var document = JsonDocument.Parse(output);
            var list = new List<(string Name, string AppId)>();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    list.Add((GetJsonString(item, "Name"), GetJsonString(item, "AppID")));
                }
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                list.Add((GetJsonString(document.RootElement, "Name"), GetJsonString(document.RootElement, "AppID")));
            }

            return list;
        }
        catch
        {
            return Array.Empty<(string Name, string AppId)>();
        }
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string SanitizeId(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
            .ToArray();

        return chars.Length == 0 ? "ms-store-game" : new string(chars);
    }
}
