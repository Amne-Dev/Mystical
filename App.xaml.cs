using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Mystical.WinUI.Services;
using Mystical.WinUI.Services.Detection;
using Mystical.WinUI.ViewModels;
using WinRT.Interop;

namespace Mystical.WinUI;

public partial class App : Application
{
    private Window? _window;
    private AppWindow? _appWindow;
    private MainPage? _mainPage;

    public static Window? MainWindow { get; private set; }
    public static AppWindow? MainAppWindow { get; private set; }

    private readonly SettingsViewModel _settingsViewModel;
    private readonly MainViewModel _mainViewModel;
    private readonly DealsViewModel _dealsViewModel;
    private readonly UpdatesViewModel _updatesViewModel;
    private readonly StatsViewModel _statsViewModel;

    public App()
    {
        InitializeComponent();

        ISettingsService settingsService = new SettingsService();
        IGameLauncherService launcherService = new GameLauncherService();
        IDealsService dealsService = new DealsService();
        IGameUpdatesService gameUpdatesService = new GameUpdatesService();
        IGameLibraryService gameLibraryService = new GameLibraryService(
            new IPlatformDetector[]
            {
                new SteamAccountDetector(settingsService),
                new EpicAccountDetector(settingsService),
                new SteamDetector(),
                new EpicDetector(),
                new GogDetector(),
                new MinecraftDetector(),
                new XboxDetector(),
                new MicrosoftStoreDetector()
            },
            settingsService);

        _settingsViewModel = new SettingsViewModel(settingsService, gameLibraryService);
        _mainViewModel = new MainViewModel(gameLibraryService, launcherService, settingsService);
        _dealsViewModel = new DealsViewModel(dealsService);
        _updatesViewModel = new UpdatesViewModel(gameLibraryService, gameUpdatesService, launcherService);
        _statsViewModel = new StatsViewModel(gameLibraryService);

        _settingsViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.UseDarkTheme))
            {
                ApplyTheme();
            }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window ??= new Window();
        MainWindow = _window;

        var hwnd = WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        MainAppWindow = _appWindow;
        TrySetWindowIcon();

        try
        {
            _window.SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            // Mica is unavailable on some configurations; fallback remains functional.
        }

        _mainPage = new MainPage(_mainViewModel, _dealsViewModel, _updatesViewModel, _statsViewModel, _settingsViewModel);
        _window.Content = _mainPage;
        ConfigureTitleBar();
        _window.Activate();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _settingsViewModel.InitializeAsync();
        ApplyTheme();
        if (_mainPage is not null)
        {
            await _mainPage.EnsureOnboardingAsync();
        }
        await _mainViewModel.InitializeAsync();
        await _dealsViewModel.InitializeAsync();
        await _updatesViewModel.InitializeAsync();
        await _statsViewModel.InitializeAsync();
    }

    private void ApplyTheme()
    {
        if (_window?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = _settingsViewModel.UseDarkTheme ? ElementTheme.Dark : ElementTheme.Light;
        ApplyTitleBarButtonTheme();
    }

    private void ConfigureTitleBar()
    {
        if (_appWindow is null || !AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;
        ApplyTitleBarButtonTheme();
    }

    private void TrySetWindowIcon()
    {
        if (_appWindow is null)
        {
            return;
        }

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Icon fallback keeps app functional on unsupported environments.
        }
    }

    private void ApplyTitleBarButtonTheme()
    {
        if (_appWindow is null || !AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _appWindow.TitleBar;
        var useDark = _settingsViewModel.UseDarkTheme;

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(32, 127, 127, 127);
        titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(64, 127, 127, 127);

        titleBar.ButtonForegroundColor = useDark ? Colors.White : Colors.Black;
        titleBar.ButtonHoverForegroundColor = useDark ? Colors.White : Colors.Black;
        titleBar.ButtonPressedForegroundColor = useDark ? Colors.White : Colors.Black;
        titleBar.ButtonInactiveForegroundColor = useDark
            ? ColorHelper.FromArgb(170, 255, 255, 255)
            : ColorHelper.FromArgb(170, 0, 0, 0);
    }
}
