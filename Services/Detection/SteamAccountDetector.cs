using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.Services.Detection;

public sealed class SteamAccountDetector : IPlatformDetector
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly ISettingsService _settingsService;

    public SteamAccountDetector(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string PlatformName => "Steam Account";

    public async Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync();
        if (!settings.EnableSteamAccountSync)
        {
            return Array.Empty<GameInfo>();
        }

        var steamUserId = ResolveSteamUserId(settings.SteamUserId);
        var steamApiKey = settings.SteamApiKey.Trim();

        if (string.IsNullOrWhiteSpace(steamUserId))
        {
            return Array.Empty<GameInfo>();
        }

        if (!string.IsNullOrWhiteSpace(steamApiKey))
        {
            var viaApi = await DetectOwnedGamesViaWebApiAsync(steamUserId, steamApiKey, cancellationToken);
            if (viaApi.Count > 0)
            {
                return viaApi;
            }
        }

        return await DetectOwnedGamesViaCommunityProfileAsync(steamUserId, cancellationToken);
    }

    private static string ResolveSteamUserId(string configuredUserId)
    {
        var normalized = NormalizeSteamUserId(configuredUserId);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        var steamPath = GetSteamInstallPath();
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
        {
            return string.Empty;
        }

        var userdataPath = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userdataPath))
        {
            return string.Empty;
        }

        var candidates = new List<(string UserId, DateTime LastWriteUtc)>();
        IEnumerable<string> folders;
        try
        {
            folders = Directory.EnumerateDirectories(userdataPath, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return string.Empty;
        }

        foreach (var folder in folders)
        {
            var userId = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(userId) ||
                userId.Length < 6 ||
                userId.Length > 20 ||
                !userId.All(char.IsDigit))
            {
                continue;
            }

            var localConfigPath = Path.Combine(folder, "config", "localconfig.vdf");
            var lastWriteUtc = File.Exists(localConfigPath)
                ? File.GetLastWriteTimeUtc(localConfigPath)
                : Directory.GetLastWriteTimeUtc(folder);

            candidates.Add((userId, lastWriteUtc));
        }

        return candidates
            .OrderByDescending(c => c.LastWriteUtc)
            .Select(c => c.UserId)
            .Select(ConvertToSteamId64IfNeeded)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty;
    }

    private static string NormalizeSteamUserId(string input)
    {
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.All(char.IsDigit))
        {
            return ConvertToSteamId64IfNeeded(value);
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        if (!uri.Host.Contains("steamcommunity.com", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 &&
            string.Equals(segments[0], "profiles", StringComparison.OrdinalIgnoreCase) &&
            segments[1].All(char.IsDigit))
        {
            return ConvertToSteamId64IfNeeded(segments[1]);
        }

        return string.Empty;
    }

    private static string ConvertToSteamId64IfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.All(char.IsDigit))
        {
            return string.Empty;
        }

        if (!ulong.TryParse(value, out var numeric))
        {
            return string.Empty;
        }

        // Steam userdata folders usually contain AccountID32; convert to SteamID64.
        if (numeric < 76561197960265728UL)
        {
            numeric += 76561197960265728UL;
        }

        return numeric.ToString(CultureInfo.InvariantCulture);
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

    private static async Task<IReadOnlyList<GameInfo>> DetectOwnedGamesViaWebApiAsync(
        string steamUserId,
        string steamApiKey,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(steamApiKey)}&steamid={Uri.EscapeDataString(steamUserId)}&include_appinfo=true&include_played_free_games=true&format=json";

        JsonDocument? document = null;
        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<GameInfo>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch
        {
            return Array.Empty<GameInfo>();
        }

        using (document)
        {
            if (document is null ||
                !document.RootElement.TryGetProperty("response", out var responseElement) ||
                !responseElement.TryGetProperty("games", out var gamesArray) ||
                gamesArray.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<GameInfo>();
            }

            var games = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in gamesArray.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var appId = GetInt64(item, "appid");
                if (appId <= 0)
                {
                    continue;
                }

                var title = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"Steam App {appId}";
                }

                var appIdText = appId.ToString(CultureInfo.InvariantCulture);
                var playtimeMinutes = Math.Max(0, GetInt64(item, "playtime_forever"));

                var game = new GameInfo
                {
                    GameId = appIdText,
                    Platform = GamePlatform.Steam,
                    Title = title,
                    Description = "Owned on linked Steam account",
                    InstallPath = string.Empty,
                    ExecutablePath = string.Empty,
                    LaunchUri = $"steam://install/{appIdText}",
                    IsInstalled = false,
                    Playtime = TimeSpan.FromMinutes(playtimeMinutes),
                    CoverArtUrl = $"https://steamcdn-a.akamaihd.net/steam/apps/{appIdText}/library_600x900.jpg"
                };

                games[game.Key] = game;
            }

            return games.Values
                .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private static async Task<IReadOnlyList<GameInfo>> DetectOwnedGamesViaCommunityProfileAsync(
        string steamUserId,
        CancellationToken cancellationToken)
    {
        var url = $"https://steamcommunity.com/profiles/{Uri.EscapeDataString(steamUserId)}/games?tab=all&xml=1";

        string xml;
        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<GameInfo>();
            }

            xml = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return Array.Empty<GameInfo>();
        }

        XDocument? document = null;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch
        {
            return Array.Empty<GameInfo>();
        }

        var games = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var gameNode in document.Descendants("game"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var appId = gameNode.Element("appID")?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(appId) || !appId.All(char.IsDigit))
            {
                continue;
            }

            var title = gameNode.Element("name")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = $"Steam App {appId}";
            }

            var playtime = ParseHoursToMinutes(gameNode.Element("hoursOnRecord")?.Value);

            var game = new GameInfo
            {
                GameId = appId,
                Platform = GamePlatform.Steam,
                Title = title,
                Description = "Owned on linked Steam account",
                InstallPath = string.Empty,
                ExecutablePath = string.Empty,
                LaunchUri = $"steam://install/{appId}",
                IsInstalled = false,
                Playtime = TimeSpan.FromMinutes(playtime),
                CoverArtUrl = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/library_600x900.jpg"
            };

            games[game.Key] = game;
        }

        return games.Values
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static long ParseHoursToMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var cleaned = value.Trim().Replace(",", ".", StringComparison.Ordinal);
        if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours))
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out hours))
            {
                return 0;
            }
        }

        return (long)Math.Max(0, Math.Round(hours * 60.0, MidpointRounding.AwayFromZero));
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static long GetInt64(JsonElement element, string propertyName)
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
