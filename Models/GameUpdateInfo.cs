using WinRT;

namespace Mystical.WinUI.Models;

[GeneratedBindableCustomProperty]
public sealed partial class GameUpdateInfo
{
    public string GameKey { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public GamePlatform Platform { get; init; } = GamePlatform.Unknown;

    public string CurrentVersion { get; init; } = string.Empty;

    public string LatestVersion { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string CurrentVersionLabel => $"Current: {CurrentVersion}";

    public string LatestVersionLabel => $"Latest: {LatestVersion}";

    public string PlatformName => Platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.EpicGames => "Epic Games",
        GamePlatform.GOG => "GOG",
        GamePlatform.Xbox => "Xbox",
        GamePlatform.MicrosoftStore => "Microsoft Store",
        _ => "Unknown"
    };
}

public sealed class GameUpdatesSnapshot
{
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    public int InstalledGamesChecked { get; init; }

    public IReadOnlyList<GameUpdateInfo> Updates { get; init; } = Array.Empty<GameUpdateInfo>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
