using Mystical.WinUI.ViewModels;
using Mystical.WinUI.Services;
using Mystical.WinUI.Models;

namespace Mystical.WinUI.Views;

public sealed partial class UpdatesPage : Page
{
    public UpdatesViewModel ViewModel { get; }

    public UpdatesPage(UpdatesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void OnHelpTopicClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string sectionKey })
        {
            return;
        }

        HelpNavigationService.RequestTopic(sectionKey);
    }

    private async void OnInstallUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GameUpdateInfo update })
        {
            return;
        }

        await ViewModel.ApplyUpdateAsync(update);
    }
}
