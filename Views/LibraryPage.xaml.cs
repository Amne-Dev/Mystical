using Mystical.WinUI.ViewModels;
using Mystical.WinUI.Services;
using Mystical.WinUI.Models;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Mystical.WinUI.Views;

public sealed partial class LibraryPage : Page
{
    public MainViewModel ViewModel { get; }

    private void RebuildFilterItems()
    {
        PlatformFiltersComboBox.Items.Clear();
        foreach (var filter in ViewModel.PlatformFilters)
        {
            PlatformFiltersComboBox.Items.Add(filter);
        }

        LibraryFiltersComboBox.Items.Clear();
        foreach (var filter in ViewModel.LibraryFilters)
        {
            LibraryFiltersComboBox.Items.Add(filter);
        }
    }

    private void RebuildGameItems()
    {
        var selected = ViewModel.SelectedGame;

        GamesGridView.Items.Clear();
        GamesListView.Items.Clear();

        foreach (var game in ViewModel.Games)
        {
            GamesGridView.Items.Add(game);
            GamesListView.Items.Add(game);
        }

        GamesGridView.SelectedItem = selected;
        GamesListView.SelectedItem = selected;
    }

    public LibraryPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        
        // Avoid ItemsSource assignment to bypass failing WinRT set_ItemsSource path.
        RebuildFilterItems();
        RebuildGameItems();

        ViewModel.PlatformFilters.CollectionChanged += (_, _) => RebuildFilterItems();
        ViewModel.LibraryFilters.CollectionChanged += (_, _) => RebuildFilterItems();
        ViewModel.Games.CollectionChanged += (_, _) => RebuildGameItems();
    }

    private async void OnImportCoverClicked(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail
        };

        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".bmp");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var imported = await ViewModel.ImportCoverFromFileAsync(file.Path);
        if (imported)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Cover import failed",
            Content = "Could not import the selected image file.",
            CloseButtonText = "Close"
        };

        _ = await dialog.ShowAsync();
    }

    private void OnHelpTopicClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string sectionKey })
        {
            return;
        }

        HelpNavigationService.RequestTopic(sectionKey);
    }

    private async void OnGameCardDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GameInfo game })
        {
            return;
        }

        await ViewModel.LaunchGameAsync(game);
        e.Handled = true;
    }

    private async void OnContextToggleFavoriteClicked(object sender, RoutedEventArgs e)
    {
        var game = ResolveContextGame(sender);
        if (game is null)
        {
            return;
        }

        await ViewModel.ToggleFavoriteForGameAsync(game);
    }

    private async void OnContextRefreshCoverClicked(object sender, RoutedEventArgs e)
    {
        var game = ResolveContextGame(sender);
        if (game is null)
        {
            return;
        }

        await ViewModel.RefreshCoverForGameAsync(game);
    }

    private static GameInfo? ResolveContextGame(object sender)
    {
        if (sender is FrameworkElement { Tag: GameInfo taggedGame })
        {
            return taggedGame;
        }

        if (sender is FrameworkElement { DataContext: GameInfo contextGame })
        {
            return contextGame;
        }

        return null;
    }
}
