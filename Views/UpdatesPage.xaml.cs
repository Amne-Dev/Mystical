using Mystical.WinUI.ViewModels;
using Mystical.WinUI.Services;
using Mystical.WinUI.Models;

namespace Mystical.WinUI.Views;

public sealed partial class UpdatesPage : Page
{
    public UpdatesViewModel ViewModel { get; }

    private void RebuildUpdateItems()
    {
        UpdateItemsListView.Items.Clear();
        foreach (var item in ViewModel.UpdateItems)
        {
            UpdateItemsListView.Items.Add(item);
        }
    }

    private void RebuildNotesItems()
    {
        NotesItemsControl.Items.Clear();
        foreach (var note in ViewModel.Notes)
        {
            NotesItemsControl.Items.Add(note);
        }
    }

    public UpdatesPage(UpdatesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        
        // Avoid ItemsSource assignment to bypass failing WinRT set_ItemsSource path.
        RebuildNotesItems();
        RebuildUpdateItems();
        ViewModel.Notes.CollectionChanged += (_, _) => RebuildNotesItems();
        ViewModel.UpdateItems.CollectionChanged += (_, _) => RebuildUpdateItems();
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
