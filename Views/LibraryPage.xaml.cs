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

    public LibraryPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
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
