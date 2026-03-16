using Mystical.WinUI.ViewModels;
using Mystical.WinUI.Services;
using Windows.System;

namespace Mystical.WinUI.Views;

public sealed partial class DealsPage : Page
{
    public DealsViewModel ViewModel { get; }

    private void RebuildDealItems()
    {
        FreeNowGridView.Items.Clear();
        foreach (var item in ViewModel.FreeNowDeals)
        {
            FreeNowGridView.Items.Add(item);
        }

        FreeSoonGridView.Items.Clear();
        foreach (var item in ViewModel.FreeSoonDeals)
        {
            FreeSoonGridView.Items.Add(item);
        }

        MassiveDiscountListView.Items.Clear();
        foreach (var item in ViewModel.MassiveDiscountDeals)
        {
            MassiveDiscountListView.Items.Add(item);
        }
    }

    public DealsPage(DealsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        
        // Avoid ItemsSource assignment to bypass failing WinRT set_ItemsSource path.
        RebuildDealItems();
        ViewModel.FreeNowDeals.CollectionChanged += (_, _) => RebuildDealItems();
        ViewModel.FreeSoonDeals.CollectionChanged += (_, _) => RebuildDealItems();
        ViewModel.MassiveDiscountDeals.CollectionChanged += (_, _) => RebuildDealItems();
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
