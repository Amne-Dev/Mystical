using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IGameLibraryService _gameLibraryService;
    private const string EpicAuthorizationUrl = "https://www.epicgames.com/id/login?redirectUrl=https%3A%2F%2Fwww.epicgames.com%2Fid%2Fapi%2Fredirect%3FclientId%3D34a02cf8f4414e29b15921876da36f9a%26responseType%3Dcode";

    private bool _useDarkTheme = true;
    private bool _autoScanGames = true;
    private int _autoScanIntervalMinutes = 30;
    private bool _startWithWindows;
    private bool _onlineCoverArt = true;
    private bool _enableSteamImport = true;
    private bool _enableEpicImport = true;
    private bool _enableXboxImport = true;
    private bool _enableGogImport = true;
    private bool _enableSteamAccountSync = true;
    private string _steamApiKey = string.Empty;
    private string _steamUserId = string.Empty;
    private bool _enableEpicAccountSync = true;
    private string _epicManifestPathOverride = string.Empty;
    private string _igdbClientId = string.Empty;
    private string _igdbClientSecret = string.Empty;
    private bool _isSteamConnected;
    private bool _isEpicConnected;
    private bool _hasCompletedOnboarding;
    private string _statusMessage = "Settings are loaded from your local profile.";
    private bool _isInitializing;
    private bool _suppressAutoSave;
    private CancellationTokenSource? _autoSaveCts;

    private static readonly HashSet<string> AutoSavePropertyNames = new(StringComparer.Ordinal)
    {
        nameof(UseDarkTheme),
        nameof(AutoScanGames),
        nameof(AutoScanIntervalMinutes),
        nameof(StartWithWindows),
        nameof(OnlineCoverArt),
        nameof(EnableSteamImport),
        nameof(EnableEpicImport),
        nameof(EnableXboxImport),
        nameof(EnableGogImport),
        nameof(EnableSteamAccountSync),
        nameof(SteamApiKey),
        nameof(SteamUserId),
        nameof(EnableEpicAccountSync),
        nameof(EpicManifestPathOverride),
        nameof(IgdbClientId),
        nameof(IgdbClientSecret)
    };

    public bool UseDarkTheme
    {
        get => _useDarkTheme;
        set => SetProperty(ref _useDarkTheme, value);
    }

    public bool AutoScanGames
    {
        get => _autoScanGames;
        set => SetProperty(ref _autoScanGames, value);
    }

    public int AutoScanIntervalMinutes
    {
        get => _autoScanIntervalMinutes;
        set => SetProperty(ref _autoScanIntervalMinutes, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool OnlineCoverArt
    {
        get => _onlineCoverArt;
        set => SetProperty(ref _onlineCoverArt, value);
    }

    public bool EnableSteamImport
    {
        get => _enableSteamImport;
        set => SetProperty(ref _enableSteamImport, value);
    }

    public bool EnableEpicImport
    {
        get => _enableEpicImport;
        set => SetProperty(ref _enableEpicImport, value);
    }

    public bool EnableXboxImport
    {
        get => _enableXboxImport;
        set => SetProperty(ref _enableXboxImport, value);
    }

    public bool EnableGogImport
    {
        get => _enableGogImport;
        set => SetProperty(ref _enableGogImport, value);
    }

    public bool EnableSteamAccountSync
    {
        get => _enableSteamAccountSync;
        set => SetProperty(ref _enableSteamAccountSync, value);
    }

    public string SteamApiKey
    {
        get => _steamApiKey;
        set => SetProperty(ref _steamApiKey, value);
    }

    public string SteamUserId
    {
        get => _steamUserId;
        set
        {
            if (SetProperty(ref _steamUserId, value))
            {
                UpdateSteamConnectionState();
            }
        }
    }

    public bool EnableEpicAccountSync
    {
        get => _enableEpicAccountSync;
        set => SetProperty(ref _enableEpicAccountSync, value);
    }

    public string EpicManifestPathOverride
    {
        get => _epicManifestPathOverride;
        set => SetProperty(ref _epicManifestPathOverride, value);
    }

    public string IgdbClientId
    {
        get => _igdbClientId;
        set => SetProperty(ref _igdbClientId, value);
    }

    public string IgdbClientSecret
    {
        get => _igdbClientSecret;
        set => SetProperty(ref _igdbClientSecret, value);
    }

    public bool IsSteamConnected
    {
        get => _isSteamConnected;
        private set
        {
            if (SetProperty(ref _isSteamConnected, value))
            {
                OnPropertyChanged(nameof(SteamConnectVisibility));
                OnPropertyChanged(nameof(SteamDisconnectVisibility));
            }
        }
    }

    public bool IsEpicConnected
    {
        get => _isEpicConnected;
        private set
        {
            if (SetProperty(ref _isEpicConnected, value))
            {
                OnPropertyChanged(nameof(EpicConnectVisibility));
                OnPropertyChanged(nameof(EpicDisconnectVisibility));
            }
        }
    }

    public Visibility SteamConnectVisibility => IsSteamConnected ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SteamDisconnectVisibility => IsSteamConnected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EpicConnectVisibility => IsEpicConnected ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EpicDisconnectVisibility => IsEpicConnected ? Visibility.Visible : Visibility.Collapsed;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool NeedsOnboarding => !_hasCompletedOnboarding;

    public ICommand SaveCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand ConnectSteamCommand { get; }

    public ICommand ConnectEpicCommand { get; }

    public ICommand DisconnectSteamCommand { get; }

    public ICommand DisconnectEpicCommand { get; }

    public ICommand ResetAppDataCommand { get; }

    public ICommand ResetDatabaseCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, IGameLibraryService gameLibraryService)
    {
        _settingsService = settingsService;
        _gameLibraryService = gameLibraryService;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ResetCommand = new RelayCommand(Reset);
        ConnectSteamCommand = new AsyncRelayCommand(ConnectSteamAsync);
        ConnectEpicCommand = new AsyncRelayCommand(ConnectEpicAsync);
        DisconnectSteamCommand = new AsyncRelayCommand(DisconnectSteamAsync);
        DisconnectEpicCommand = new AsyncRelayCommand(DisconnectEpicAsync);
        ResetAppDataCommand = new AsyncRelayCommand(ResetAppDataAsync);
        ResetDatabaseCommand = new AsyncRelayCommand(ResetDatabaseAsync);

        PropertyChanged += OnSettingsPropertyChanged;
    }

    public async Task InitializeAsync()
    {
        _isInitializing = true;
        try
        {
            var settings = await _settingsService.LoadAsync();

            UseDarkTheme = settings.UseDarkTheme;
            AutoScanGames = settings.AutoScanGames;
            AutoScanIntervalMinutes = settings.AutoScanIntervalMinutes;
            StartWithWindows = settings.StartWithWindows;
            OnlineCoverArt = settings.OnlineCoverArt;
            EnableSteamImport = settings.EnableSteamImport;
            EnableEpicImport = settings.EnableEpicImport;
            EnableXboxImport = settings.EnableXboxImport;
            EnableGogImport = settings.EnableGogImport;
            EnableSteamAccountSync = settings.EnableSteamAccountSync;
            SteamApiKey = settings.SteamApiKey;
            SteamUserId = settings.SteamUserId;
            EnableEpicAccountSync = settings.EnableEpicAccountSync;
            EpicManifestPathOverride = settings.EpicManifestPathOverride;
            IgdbClientId = settings.IgdbClientId;
            IgdbClientSecret = settings.IgdbClientSecret;
            _hasCompletedOnboarding = settings.HasCompletedOnboarding;
            OnPropertyChanged(nameof(NeedsOnboarding));
            UpdateSteamConnectionState();
            await RefreshEpicConnectionStateAsync();

            StatusMessage = "Settings loaded.";
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task SaveAsync()
    {
        CancelPendingAutoSave();
        var previous = await _settingsService.LoadAsync();
        var shouldRefreshCovers = ShouldRefreshIgdbCovers(previous);

        await PersistAsync("Settings saved. Run Library refresh to apply account sync changes.");

        if (!shouldRefreshCovers)
        {
            return;
        }

        try
        {
            var refreshed = await _gameLibraryService.RefreshCoverArtAsync();
            StatusMessage = refreshed > 0
                ? $"Settings saved. Refreshed {refreshed} cover(s) from IGDB."
                : "Settings saved. IGDB is linked and covers are already up to date.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Settings saved, but IGDB cover refresh failed: {ex.Message}";
        }
    }

    public async Task CompleteOnboardingAsync(bool steam, bool epic, bool xbox, bool gog)
    {
        EnableSteamImport = steam;
        EnableEpicImport = epic;
        EnableXboxImport = xbox;
        EnableGogImport = gog;
        _hasCompletedOnboarding = true;
        OnPropertyChanged(nameof(NeedsOnboarding));

        await PersistAsync("Onboarding complete. You can change import sources later in Settings.");
    }

    private async Task ConnectSteamAsync()
    {
        EnableSteamAccountSync = true;

        var steamUserId = ResolveSteamUserId(SteamUserId);
        if (string.IsNullOrWhiteSpace(steamUserId))
        {
            StatusMessage = "Steam connect failed: could not detect a local Steam account. Sign into Steam and try again.";
            return;
        }

        SteamUserId = steamUserId;
        UpdateSteamConnectionState();
        await PersistAsync($"Steam connected ({steamUserId}). Refresh Library to import owned games.");
    }

    private async Task DisconnectSteamAsync()
    {
        EnableSteamAccountSync = false;
        SteamApiKey = string.Empty;
        SteamUserId = string.Empty;
        UpdateSteamConnectionState();

        await PersistAsync("Steam account disconnected.");
    }

    private async Task ConnectEpicAsync()
    {
        var imported = await ConnectEpicViaImportAsync();
        if (!imported)
        {
            StatusMessage = "Epic launcher session not found. Use Connect Epic to sign in from the in-app popup.";
        }
    }

    public string GetEpicAuthorizationUrl()
    {
        return EpicAuthorizationUrl;
    }

    public async Task<bool> ConnectEpicViaImportAsync()
    {
        EnableEpicAccountSync = true;

        var legendaryExe = ResolveLegendaryExecutable();
        if (string.IsNullOrWhiteSpace(legendaryExe))
        {
            StatusMessage = "Epic connect failed: legendary was not found in PATH.";
            return false;
        }

        var imported = await RunProcessAsync(legendaryExe, "auth --import");
        if (imported)
        {
            IsEpicConnected = true;
            await PersistAsync("Epic connected via launcher session. Refresh Library to import owned games.");
            return true;
        }

        IsEpicConnected = false;
        StatusMessage = "Epic launcher session was not found. Sign in through the Connect Epic popup.";
        return false;
    }

    public async Task<bool> ConnectEpicWithAuthorizationCodeAsync(string authorizationCode)
    {
        EnableEpicAccountSync = true;

        var trimmedCode = authorizationCode.Trim();
        if (string.IsNullOrWhiteSpace(trimmedCode))
        {
            StatusMessage = "Epic connect failed: missing authorization code.";
            return false;
        }

        var legendaryExe = ResolveLegendaryExecutable();
        if (string.IsNullOrWhiteSpace(legendaryExe))
        {
            StatusMessage = "Epic connect failed: legendary was not found in PATH.";
            return false;
        }

        var connected = await RunProcessWithArgumentsAsync(legendaryExe, "auth", "--code", trimmedCode);
        if (!connected)
        {
            IsEpicConnected = false;
            StatusMessage = "Epic connect failed: authorization code was rejected.";
            return false;
        }

        IsEpicConnected = true;
        await PersistAsync("Epic account connected. Refresh Library to import owned games.");
        return true;
    }

    private async Task DisconnectEpicAsync()
    {
        EnableEpicAccountSync = false;
        EpicManifestPathOverride = string.Empty;

        var legendaryExe = ResolveLegendaryExecutable();
        if (!string.IsNullOrWhiteSpace(legendaryExe))
        {
            _ = await RunProcessAsync(legendaryExe, "auth --delete");
        }

        IsEpicConnected = false;
        await PersistAsync("Epic account disconnected.");
    }

    private async Task PersistAsync(string successMessage)
    {
        CancelPendingAutoSave();
        _suppressAutoSave = true;
        try
        {
            await _settingsService.SaveAsync(BuildSettings());
        }
        finally
        {
            _suppressAutoSave = false;
        }

        StatusMessage = successMessage;
    }

    private bool ShouldRefreshIgdbCovers(AppSettings previous)
    {
        if (!OnlineCoverArt)
        {
            return false;
        }

        var currentClientId = IgdbClientId.Trim();
        var currentClientSecret = IgdbClientSecret.Trim();
        if (string.IsNullOrWhiteSpace(currentClientId) || string.IsNullOrWhiteSpace(currentClientSecret))
        {
            return false;
        }

        var previousClientId = previous.IgdbClientId.Trim();
        var previousClientSecret = previous.IgdbClientSecret.Trim();

        if (string.IsNullOrWhiteSpace(previousClientId) || string.IsNullOrWhiteSpace(previousClientSecret))
        {
            return true;
        }

        if (!previous.OnlineCoverArt)
        {
            return true;
        }

        return !string.Equals(previousClientId, currentClientId, StringComparison.Ordinal) ||
               !string.Equals(previousClientSecret, currentClientSecret, StringComparison.Ordinal);
    }

    private void Reset()
    {
        UseDarkTheme = true;
        AutoScanGames = true;
        AutoScanIntervalMinutes = 30;
        StartWithWindows = false;
        OnlineCoverArt = true;
        EnableSteamImport = true;
        EnableEpicImport = true;
        EnableXboxImport = true;
        EnableGogImport = true;
        EnableSteamAccountSync = true;
        SteamApiKey = string.Empty;
        SteamUserId = string.Empty;
        EnableEpicAccountSync = true;
        EpicManifestPathOverride = string.Empty;
        IgdbClientId = string.Empty;
        IgdbClientSecret = string.Empty;
        _hasCompletedOnboarding = true;
        OnPropertyChanged(nameof(NeedsOnboarding));
        UpdateSteamConnectionState();
        IsEpicConnected = false;

        StatusMessage = "Defaults restored and queued for auto-save.";
    }

    private async Task ResetAppDataAsync()
    {
        try
        {
            await _gameLibraryService.ResetAppDataAsync();

            Reset();
            await PersistAsync("Local library cache reset. Refresh Library to rescan.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reset failed: {ex.Message}";
        }
    }

    private async Task ResetDatabaseAsync()
    {
        try
        {
            await _gameLibraryService.ResetDatabaseAsync();

            await PersistAsync("Local SQLite database reset. Refresh Library to rebuild it.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Database reset failed: {ex.Message}";
        }
    }

    private void UpdateSteamConnectionState()
    {
        // Connection state should reflect explicit account linking, not local auto-detection.
        IsSteamConnected = !string.IsNullOrWhiteSpace(NormalizeSteamUserId(SteamUserId));
    }

    private async Task RefreshEpicConnectionStateAsync()
    {
        var legendaryExe = ResolveLegendaryExecutable();
        if (string.IsNullOrWhiteSpace(legendaryExe))
        {
            IsEpicConnected = false;
            return;
        }

        var output = await RunProcessCaptureAsync(legendaryExe, "status");
        IsEpicConnected = !string.IsNullOrWhiteSpace(output) &&
                          !output.Contains("<not logged in>", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSteamUserId(string configuredUserId)
    {
        var normalized = NormalizeSteamUserId(configuredUserId);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        var steamPath = GetSteamInstallPath();
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            return string.Empty;
        }

        var userdataPath = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userdataPath))
        {
            return string.Empty;
        }

        var candidates = new List<(string UserId, DateTime LastWriteUtc)>();
        IEnumerable<string> folders;
        try
        {
            folders = Directory.EnumerateDirectories(userdataPath, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return string.Empty;
        }

        foreach (var folder in folders)
        {
            var userId = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(userId) ||
                userId.Length < 6 ||
                userId.Length > 20 ||
                !userId.All(char.IsDigit))
            {
                continue;
            }

            var localConfigPath = Path.Combine(folder, "config", "localconfig.vdf");
            var lastWriteUtc = File.Exists(localConfigPath)
                ? File.GetLastWriteTimeUtc(localConfigPath)
                : Directory.GetLastWriteTimeUtc(folder);

            candidates.Add((userId, lastWriteUtc));
        }

        return candidates
            .OrderByDescending(c => c.LastWriteUtc)
            .Select(c => c.UserId)
            .Select(ConvertToSteamId64IfNeeded)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty;
    }

    private static string NormalizeSteamUserId(string input)
    {
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.All(char.IsDigit))
        {
            return ConvertToSteamId64IfNeeded(value);
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        if (!uri.Host.Contains("steamcommunity.com", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 &&
            string.Equals(segments[0], "profiles", StringComparison.OrdinalIgnoreCase) &&
            segments[1].All(char.IsDigit))
        {
            return ConvertToSteamId64IfNeeded(segments[1]);
        }

        return string.Empty;
    }

    private static string ConvertToSteamId64IfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.All(char.IsDigit))
        {
            return string.Empty;
        }

        if (!ulong.TryParse(value, out var numeric))
        {
            return string.Empty;
        }

        if (numeric < 76561197960265728UL)
        {
            numeric += 76561197960265728UL;
        }

        return numeric.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetSteamInstallPath()
    {
        var candidates = new[]
        {
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath"),
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam"
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string ResolveLegendaryExecutable()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(entry.Trim(), "legendary.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var commonCandidates = new[]
        {
            Path.Combine(localAppData, "Programs", "Python", "Python314", "Scripts", "legendary.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python313", "Scripts", "legendary.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python312", "Scripts", "legendary.exe")
        };

        return commonCandidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static async Task<bool> RunProcessAsync(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunProcessCaptureAsync(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return string.Empty;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;
            return string.IsNullOrWhiteSpace(output) ? error : output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<bool> RunProcessWithArgumentsAsync(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_isInitializing || _suppressAutoSave)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(args.PropertyName) || !AutoSavePropertyNames.Contains(args.PropertyName))
        {
            return;
        }

        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        CancelPendingAutoSave();
        _autoSaveCts = new CancellationTokenSource();
        _ = AutoSaveAsync(_autoSaveCts.Token);
    }

    private async Task AutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(700, cancellationToken);
            _suppressAutoSave = true;
            await _settingsService.SaveAsync(BuildSettings());
            if (!cancellationToken.IsCancellationRequested)
            {
                StatusMessage = "Settings auto-saved.";
            }
        }
        catch (OperationCanceledException)
        {
            // Debounced autosave canceled by newer edits.
        }
        catch (Exception ex)
        {
            StatusMessage = $"Settings auto-save failed: {ex.Message}";
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    private void CancelPendingAutoSave()
    {
        if (_autoSaveCts is null)
        {
            return;
        }

        _autoSaveCts.Cancel();
        _autoSaveCts.Dispose();
        _autoSaveCts = null;
    }

    private AppSettings BuildSettings()
    {
        return new AppSettings
        {
            UseDarkTheme = UseDarkTheme,
            AutoScanGames = AutoScanGames,
            AutoScanIntervalMinutes = Math.Max(5, AutoScanIntervalMinutes),
            StartWithWindows = StartWithWindows,
            OnlineCoverArt = OnlineCoverArt,
            EnableSteamImport = EnableSteamImport,
            EnableEpicImport = EnableEpicImport,
            EnableXboxImport = EnableXboxImport,
            EnableGogImport = EnableGogImport,
            EnableSteamAccountSync = EnableSteamAccountSync,
            SteamApiKey = SteamApiKey.Trim(),
            SteamUserId = SteamUserId.Trim(),
            EnableEpicAccountSync = EnableEpicAccountSync,
            EpicManifestPathOverride = EpicManifestPathOverride.Trim(),
            IgdbClientId = IgdbClientId.Trim(),
            IgdbClientSecret = IgdbClientSecret.Trim(),
            HasCompletedOnboarding = _hasCompletedOnboarding
        };
    }
}
