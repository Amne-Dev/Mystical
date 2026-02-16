using Mystical.WinUI.ViewModels;
using Mystical.WinUI.Services;
using Windows.System;

namespace Mystical.WinUI.Views;

public sealed partial class DealsPage : Page
{
    public DealsViewModel ViewModel { get; }

    public DealsPage(DealsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
    }

    private async void OnOpenDealClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string dealUrl } || string.IsNullOrWhiteSpace(dealUrl))
        {
            return;
        }

        if (!Uri.TryCreate(dealUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        await Launcher.LaunchUriAsync(uri);
    }

    private void OnHelpTopicClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string sectionKey })
        {
            return;
        }

        HelpNavigationService.RequestTopic(sectionKey);
    }
}
