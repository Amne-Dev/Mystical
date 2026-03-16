using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Mystical.WinUI.Services;
using Mystical.WinUI.Services.Detection;
using Mystical.WinUI.ViewModels;
using WinRT.Interop;
using System.Text;

namespace Mystical.WinUI;

public partial class App : Application
{
    private static readonly object CrashLogGate = new();
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
        ConfigureGlobalExceptionHandling();
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
        try
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

            _ = InitializeAsync().ContinueWith(task =>
            {
                var rootException = task.Exception?.GetBaseException() ?? task.Exception;
                if (rootException is not null)
                {
                    HandleFatalException(rootException, "App.InitializeAsync");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            HandleFatalException(ex, "App.OnLaunched");
        }
    }

    private void ConfigureGlobalExceptionHandling()
    {
        UnhandledException += OnAppUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskUnobservedTaskException;
    }

    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        HandleFatalException(e.Exception, "Application.UnhandledException");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            HandleFatalException(exception, "AppDomain.UnhandledException");
            return;
        }

        WriteCrashLog("AppDomain.UnhandledException", e.ExceptionObject?.ToString() ?? "Unknown non-Exception fault");
    }

    private void OnTaskUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleFatalException(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }

    private void HandleFatalException(Exception exception, string source)
    {
        var details = BuildCrashDetails(source, exception);
        WriteCrashLog(source, details);

        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window is null)
            {
                return;
            }

            _window.Content = new Grid
            {
                Padding = new Thickness(24),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Mystical encountered an unexpected error.",
                                FontSize = 20,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                TextWrapping = TextWrapping.WrapWholeWords
                            },
                            new TextBlock
                            {
                                Text = "A crash log was written to %LOCALAPPDATA%\\MysticalWinUI\\logs.",
                                TextWrapping = TextWrapping.WrapWholeWords
                            }
                        }
                    }
                }
            };

            _window.Activate();
        });
    }

    private static string BuildCrashDetails(string source, Exception exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp (UTC): {DateTime.UtcNow:O}");
        builder.AppendLine($"Source: {source}");
        builder.AppendLine($"Message: {exception.Message}");
        builder.AppendLine($"Type: {exception.GetType().FullName}");
        builder.AppendLine("StackTrace:");
        builder.AppendLine(exception.StackTrace ?? "<no stack trace>");

        if (exception.InnerException is not null)
        {
            builder.AppendLine();
            builder.AppendLine("InnerException:");
            builder.AppendLine(exception.InnerException.ToString());
        }

        return builder.ToString();
    }

    private static void WriteCrashLog(string source, string details)
    {
        try
        {
            lock (CrashLogGate)
            {
                var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MysticalWinUI");
                var logsFolder = Path.Combine(appFolder, "logs");
                Directory.CreateDirectory(logsFolder);

                var fileName = $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.log";
                var filePath = Path.Combine(logsFolder, fileName);
                File.WriteAllText(filePath, details);

                var latestPath = Path.Combine(logsFolder, "latest-crash.log");
                File.WriteAllText(latestPath, details);
            }
        }
        catch
        {
            // Intentionally swallow to avoid secondary crashes while handling a fatal exception.
        }
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
