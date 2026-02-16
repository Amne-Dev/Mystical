using Mystical.WinUI.ViewModels;
using Mystical.WinUI.Services;

namespace Mystical.WinUI.Views;

public sealed partial class StatsPage : Page
{
    public StatsViewModel ViewModel { get; }

    public StatsPage(StatsViewModel viewModel)
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
}
