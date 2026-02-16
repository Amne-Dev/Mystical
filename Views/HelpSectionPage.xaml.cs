using Microsoft.UI.Xaml.Navigation;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;
using Windows.System;

namespace Mystical.WinUI.Views;

public sealed partial class HelpSectionPage : Page
{
    public HelpSectionPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DataContext = e.Parameter as HelpDocSection;
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
