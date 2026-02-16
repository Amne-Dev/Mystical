namespace Mystical.WinUI.Models;

public sealed class AppSettings
{
    public bool UseDarkTheme { get; set; } = true;

    public bool AutoScanGames { get; set; } = true;

    public int AutoScanIntervalMinutes { get; set; } = 30;

    public bool StartWithWindows { get; set; } = false;

    public bool OnlineCoverArt { get; set; } = true;

    public bool EnableSteamImport { get; set; } = true;

    public bool EnableEpicImport { get; set; } = true;

    public bool EnableXboxImport { get; set; } = true;

    public bool EnableGogImport { get; set; } = true;

    public bool EnableSteamAccountSync { get; set; } = true;

    public string SteamApiKey { get; set; } = string.Empty;

    public string SteamUserId { get; set; } = string.Empty;

    public bool EnableEpicAccountSync { get; set; } = true;

    public string EpicManifestPathOverride { get; set; } = string.Empty;

    public string IgdbClientId { get; set; } = string.Empty;

    public string IgdbClientSecret { get; set; } = string.Empty;

    public bool HasCompletedOnboarding { get; set; } = false;
}
