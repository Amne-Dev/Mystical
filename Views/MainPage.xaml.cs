using Microsoft.UI.Xaml.Controls;
using Mystical.WinUI.Services;
using Mystical.WinUI.ViewModels;

namespace Mystical.WinUI.Views;

public sealed partial class MainPage : Page
{
    private readonly MainViewModel _mainViewModel;
    private readonly DealsViewModel _dealsViewModel;
    private readonly UpdatesViewModel _updatesViewModel;
    private readonly StatsViewModel _statsViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    private LibraryPage? _libraryPage;
    private DealsPage? _dealsPage;
    private UpdatesPage? _updatesPage;
    private StatsPage? _statsPage;
    private HelpPage? _helpPage;
    private SettingsPage? _settingsPage;
    private bool _onboardingShown;

    public MainPage(
        MainViewModel mainViewModel,
        DealsViewModel dealsViewModel,
        UpdatesViewModel updatesViewModel,
        StatsViewModel statsViewModel,
        SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        HelpNavigationService.TopicRequested += OnHelpTopicRequested;

        _mainViewModel = mainViewModel;
        _dealsViewModel = dealsViewModel;
        _updatesViewModel = updatesViewModel;
        _statsViewModel = statsViewModel;
        _settingsViewModel = settingsViewModel;

        _libraryPage = new LibraryPage(_mainViewModel);
        ContentFrame.Content = _libraryPage;
        AppNavigationView.SelectedItem = LibraryNavigationItem;
    }

    public async Task EnsureOnboardingAsync()
    {
        if (_onboardingShown || !_settingsViewModel.NeedsOnboarding)
        {
            return;
        }

        if (XamlRoot is null)
        {
            await WaitForLoadedAsync();
        }

        _onboardingShown = true;

        var steamToggle = new ToggleSwitch
        {
            Header = "Import from Steam",
            IsOn = _settingsViewModel.EnableSteamImport
        };

        var epicToggle = new ToggleSwitch
        {
            Header = "Import from Epic",
            IsOn = _settingsViewModel.EnableEpicImport
        };

        var xboxToggle = new ToggleSwitch
        {
            Header = "Import from Xbox app / Microsoft Store",
            IsOn = _settingsViewModel.EnableXboxImport
        };

        var gogToggle = new ToggleSwitch
        {
            Header = "Import from GOG",
            IsOn = _settingsViewModel.EnableGogImport
        };

        var tipText = new TextBlock
        {
            Text = "For better cover consistency, adding an IGDB key is recommended. It is optional and does not affect game import or launching.",
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.8
        };

        var onboardingContent = new StackPanel
        {
            Spacing = 10
        };

        onboardingContent.Children.Add(new TextBlock
        {
            Text = "Choose where to import your library from. You can change these any time in Settings.",
            TextWrapping = TextWrapping.WrapWholeWords
        });
        onboardingContent.Children.Add(steamToggle);
        onboardingContent.Children.Add(epicToggle);
        onboardingContent.Children.Add(xboxToggle);
        onboardingContent.Children.Add(gogToggle);
        onboardingContent.Children.Add(tipText);

        var onboardingDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Welcome to Mystical",
            Content = onboardingContent,
            PrimaryButtonText = "Continue",
            DefaultButton = ContentDialogButton.Primary,
            CloseButtonText = "Use defaults"
        };

        onboardingDialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                await _settingsViewModel.CompleteOnboardingAsync(
                    steamToggle.IsOn,
                    epicToggle.IsOn,
                    xboxToggle.IsOn,
                    gogToggle.IsOn);
            }
            finally
            {
                deferral.Complete();
            }
        };

        onboardingDialog.CloseButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                await _settingsViewModel.CompleteOnboardingAsync(
                    _settingsViewModel.EnableSteamImport,
                    _settingsViewModel.EnableEpicImport,
                    _settingsViewModel.EnableXboxImport,
                    _settingsViewModel.EnableGogImport);
            }
            finally
            {
                deferral.Complete();
            }
        };

        _ = await onboardingDialog.ShowAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        App.MainWindow.SetTitleBar(CustomTitleBar);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        HelpNavigationService.TopicRequested -= OnHelpTopicRequested;
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _settingsPage ??= new SettingsPage(_settingsViewModel);
            ContentFrame.Content = _settingsPage;
            return;
        }

        if (args.SelectedItemContainer?.Tag as string == "library")
        {
            _libraryPage ??= new LibraryPage(_mainViewModel);
            ContentFrame.Content = _libraryPage;
            return;
        }

        if (args.SelectedItemContainer?.Tag as string == "deals")
        {
            _dealsPage ??= new DealsPage(_dealsViewModel);
            ContentFrame.Content = _dealsPage;
            return;
        }

        if (args.SelectedItemContainer?.Tag as string == "stats")
        {
            _statsPage ??= new StatsPage(_statsViewModel);
            ContentFrame.Content = _statsPage;
            return;
        }

        if (args.SelectedItemContainer?.Tag as string == "updates")
        {
            _updatesPage ??= new UpdatesPage(_updatesViewModel);
            ContentFrame.Content = _updatesPage;
            return;
        }

        if (args.SelectedItemContainer?.Tag as string == "help")
        {
            _helpPage ??= new HelpPage();
            ContentFrame.Content = _helpPage;
        }
    }

    private void OnHelpTopicRequested(object? sender, string sectionKey)
    {
        _helpPage ??= new HelpPage();
        ContentFrame.Content = _helpPage;
        AppNavigationView.SelectedItem = HelpNavigationItem;
        _helpPage.NavigateToSection(sectionKey);
    }

    private Task WaitForLoadedAsync()
    {
        if (XamlRoot is not null)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler? loadedHandler = null;
        loadedHandler = (_, _) =>
        {
            Loaded -= loadedHandler;
            tcs.TrySetResult();
        };

        Loaded += loadedHandler;
        return tcs.Task;
    }
}
