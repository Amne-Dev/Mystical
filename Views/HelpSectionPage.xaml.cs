using Microsoft.UI.Xaml.Navigation;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;
using Windows.System;

namespace Mystical.WinUI.Views;

public sealed partial class HelpSectionPage : Page
{
    public HelpDocSection? Section { get; private set; }

    private void WireCollections()
    {
        if (Section is null)
        {
            return;
        }
        
        // Avoid ItemsSource assignment to bypass failing WinRT set_ItemsSource path.
        LinksItemsControl.Items.Clear();
        foreach (var link in Section.Links)
        {
            LinksItemsControl.Items.Add(link);
        }

        SubsectionsItemsControl.Items.Clear();
        foreach (var subsection in Section.Subsections)
        {
            SubsectionsItemsControl.Items.Add(subsection);
        }
    }

    public HelpSectionPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Section = e.Parameter as HelpDocSection;
        Bindings.Update();
        WireCollections();
    }

    private async void OnDocLinkClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string linkTarget } || string.IsNullOrWhiteSpace(linkTarget))
        {
            return;
        }

        if (linkTarget.StartsWith("help:", StringComparison.OrdinalIgnoreCase))
        {
            var sectionKey = linkTarget["help:".Length..].Trim();
            HelpNavigationService.RequestTopic(sectionKey);
            return;
        }

        if (!Uri.TryCreate(linkTarget, UriKind.Absolute, out var uri))
        {
            return;
        }

        await Launcher.LaunchUriAsync(uri);
    }
}
