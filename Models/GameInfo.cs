using System.Globalization;

namespace Mystical.WinUI.Models;

public sealed class GameInfo
{
    public string GameId { get; set; } = string.Empty;

    public GamePlatform Platform { get; set; } = GamePlatform.Unknown;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string InstallPath { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string LaunchUri { get; set; } = string.Empty;

    public TimeSpan Playtime { get; set; } = TimeSpan.Zero;

    public DateTimeOffset? LastPlayed { get; set; }

    public DateTimeOffset? InstallDate { get; set; }

    public bool IsInstalled { get; set; } = true;

    public bool IsFavorite { get; set; }

    public long SizeBytes { get; set; }

    public string CoverArtPath { get; set; } = string.Empty;

    public string CoverArtUrl { get; set; } = string.Empty;

    public string Key => $"{Platform}:{GameId}";

    public string PlatformName => Platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.EpicGames => "Epic Games",
        GamePlatform.GOG => "GOG",
        GamePlatform.Minecraft => "Minecraft",
        GamePlatform.Xbox => "Xbox",
        GamePlatform.MicrosoftStore => "Microsoft Store",
        GamePlatform.Custom => "Custom",
        _ => "Unknown"
    };

    public string FavoriteGlyph => IsFavorite ? "★" : "☆";

    public string InstallStateText => IsInstalled ? "Installed" : "Not installed";

    public Uri? CoverArtUri
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CoverArtPath) && File.Exists(CoverArtPath))
            {
                return new Uri(CoverArtPath, UriKind.Absolute);
            }

            if (!string.IsNullOrWhiteSpace(CoverArtUrl) && Uri.TryCreate(CoverArtUrl, UriKind.Absolute, out var uri))
            {
                return uri;
            }

            return null;
        }
    }

    public string FormattedPlaytime
    {
        get
        {
            if (Playtime <= TimeSpan.Zero)
            {
                return "Never played";
            }

            if (Playtime.TotalHours < 1)
            {
                return $"{Math.Max(1, (int)Playtime.TotalMinutes)} min";
            }

            return $"{(int)Playtime.TotalHours}h {Playtime.Minutes}m";
        }
    }

    public string LastPlayedText => LastPlayed is null
        ? "Not played yet"
        : $"Last played {LastPlayed.Value.LocalDateTime:g}";

    public string FormattedSize
    {
        get
        {
            if (SizeBytes <= 0)
            {
                return "Unknown size";
            }

            const double kb = 1024d;
            const double mb = kb * 1024d;
            const double gb = mb * 1024d;

            if (SizeBytes >= gb)
            {
                return FormattableString.Invariant($"{SizeBytes / gb:0.0} GB");
            }

            if (SizeBytes >= mb)
            {
                return FormattableString.Invariant($"{SizeBytes / mb:0.0} MB");
            }

            if (SizeBytes >= kb)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0} KB", SizeBytes / kb);
            }

            return $"{SizeBytes} B";
        }
    }
}
