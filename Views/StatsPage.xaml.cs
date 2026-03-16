using Mystical.WinUI.ViewModels;
using Mystical.WinUI.Services;

namespace Mystical.WinUI.Views;

public sealed partial class StatsPage : Page
{
    public StatsViewModel ViewModel { get; }

    private void RebuildStatsItems()
    {
        PlatformBreakdownListView.Items.Clear();
        foreach (var item in ViewModel.PlatformBreakdown)
        {
            PlatformBreakdownListView.Items.Add(item);
        }

        GamesActivityGridView.Items.Clear();
        foreach (var item in ViewModel.GamesActivityCells)
        {
            GamesActivityGridView.Items.Add(item);
        }

        TimeActivityGridView.Items.Clear();
        foreach (var item in ViewModel.TimeActivityCells)
        {
            TimeActivityGridView.Items.Add(item);
        }
    }

    public StatsPage(StatsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        
        // Avoid ItemsSource assignment to bypass failing WinRT set_ItemsSource path.
        RebuildStatsItems();
        ViewModel.PlatformBreakdown.CollectionChanged += (_, _) => RebuildStatsItems();
        ViewModel.GamesActivityCells.CollectionChanged += (_, _) => RebuildStatsItems();
        ViewModel.TimeActivityCells.CollectionChanged += (_, _) => RebuildStatsItems();
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
