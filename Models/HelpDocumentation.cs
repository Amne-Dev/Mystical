using Microsoft.UI.Xaml;

namespace Mystical.WinUI.Models;

public sealed class HelpDocSection
{
    public string SectionKey { get; init; } = string.Empty;

    public string NumberedTitle { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<HelpDocLink> Links { get; init; } = Array.Empty<HelpDocLink>();

    public IReadOnlyList<HelpDocSubsection> Subsections { get; init; } = Array.Empty<HelpDocSubsection>();

    public Visibility LinksVisibility => Links.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
}

public sealed class HelpDocSubsection
{
    public string NumberedTitle { get; init; } = string.Empty;

    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();

    public IReadOnlyList<HelpDocLink> Links { get; init; } = Array.Empty<HelpDocLink>();

    public string Note { get; init; } = string.Empty;

    public Visibility NoteVisibility => string.IsNullOrWhiteSpace(Note) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility LinksVisibility => Links.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
}

public sealed class HelpDocLink
{
    public string Label { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;
}
