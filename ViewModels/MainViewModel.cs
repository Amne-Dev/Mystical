using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;

namespace Mystical.WinUI.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IGameLibraryService _gameLibraryService;
    private readonly IGameLauncherService _gameLauncherService;
    private readonly ISettingsService _settingsService;
    private readonly SemaphoreSlim _libraryReloadGate = new(1, 1);

    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _launchSelectedCommand;
    private readonly AsyncRelayCommand _toggleFavoriteCommand;

    private List<GameInfo> _allGames = new();

    private string _searchText = string.Empty;
    private string _selectedPlatform = "All";
    private string _selectedLibraryFilter = "All";
    private bool _isScanning;
    private bool _isListView;
    private string _statusMessage = "Ready";
    private GameInfo? _selectedGame;

    public ObservableCollection<GameInfo> Games { get; } = new();

    public ObservableCollection<string> PlatformFilters { get; } = new() { "All" };

    public ObservableCollection<string> LibraryFilters { get; } = new() { "All", "Installed", "Not Installed", "Favorite" };

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedPlatform
    {
        get => _selectedPlatform;
        set
        {
            if (SetProperty(ref _selectedPlatform, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedLibraryFilter
    {
        get => _selectedLibraryFilter;
        set
        {
            if (SetProperty(ref _selectedLibraryFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
        }
    }

    public bool IsListView
    {
        get => _isListView;
        set
        {
            if (SetProperty(ref _isListView, value))
            {
                OnPropertyChanged(nameof(GridViewVisibility));
                OnPropertyChanged(nameof(ListViewVisibility));
                OnPropertyChanged(nameof(IsGridViewSelected));
                OnPropertyChanged(nameof(IsListViewSelected));
            }
        }
    }

    public bool IsGridViewSelected
    {
        get => !IsListView;
        set
        {
            if (value)
            {
                IsListView = false;
            }
        }
    }

    public bool IsListViewSelected
    {
        get => IsListView;
        set
        {
            if (value)
            {
                IsListView = true;
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public GameInfo? SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (SetProperty(ref _selectedGame, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedGameSubtitle));
                OnPropertyChanged(nameof(PrimaryActionText));
                _launchSelectedCommand.NotifyCanExecuteChanged();
                _toggleFavoriteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedGame is not null;

    public bool HasGames => Games.Count > 0;

    public bool ShowEmptyState => !IsScanning && Games.Count == 0;

    public Visibility EmptyStateVisibility => ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GridViewVisibility => IsListView ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ListViewVisibility => IsListView ? Visibility.Visible : Visibility.Collapsed;

    public string SelectedGameSubtitle => SelectedGame is null
        ? "Select a game to see actions"
        : $"{SelectedGame.PlatformName} - {SelectedGame.InstallStateText}";

    public string PrimaryActionText => SelectedGame is not null && !SelectedGame.IsInstalled ? "Install" : "Launch";

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand LaunchSelectedCommand => _launchSelectedCommand;

    public ICommand ToggleFavoriteCommand => _toggleFavoriteCommand;

    public MainViewModel(IGameLibraryService gameLibraryService, IGameLauncherService gameLauncherService, ISettingsService settingsService)
    {
        _gameLibraryService = gameLibraryService;
        _gameLauncherService = gameLauncherService;
        _settingsService = settingsService;
        _gameLibraryService.LibraryChanged += OnLibraryChanged;

        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
        _launchSelectedCommand = new AsyncRelayCommand(LaunchSelectedAsync, () => HasSelection);
        _toggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync, () => HasSelection);
    }

    public async Task InitializeAsync()
    {
        _allGames = (await _gameLibraryService.LoadAsync()).ToList();
        RebuildPlatformFilters();
        ApplyFilters();

        if (_allGames.Count == 0)
        {
            await RefreshAsync();
            return;
        }

        StatusMessage = _allGames.Count == 1
            ? "Library ready. 1 game available."
            : $"Library ready. {_allGames.Count} games available.";
    }

    public async Task<bool> ImportCoverFromFileAsync(string filePath)
    {
        if (SelectedGame is null)
        {
            return false;
        }

        var updated = await _gameLibraryService.ImportCoverArtAsync(SelectedGame.Key, filePath);
        if (updated is null)
        {
            return false;
        }

        ApplyGameUpdate(updated);
        StatusMessage = $"Imported cover art for {updated.Title}.";
        return true;
    }

    private async Task RefreshAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning launchers and linked accounts...";

        try
        {
            _allGames = (await _gameLibraryService.ScanAsync()).ToList();
            RebuildPlatformFilters();
            ApplyFilters();
            var uninstalledCount = _allGames.Count(g => !g.IsInstalled);
            var steamUninstalled = _allGames.Count(g => g.Platform == GamePlatform.Steam && !g.IsInstalled);
            var epicUninstalled = _allGames.Count(g => g.Platform == GamePlatform.EpicGames && !g.IsInstalled);

            StatusMessage = $"Scan complete: {_allGames.Count} games ({uninstalledCount} not installed).";

            if (steamUninstalled == 0 || epicUninstalled == 0)
            {
                var settings = await _settingsService.LoadAsync();

                if (steamUninstalled == 0 && settings.EnableSteamImport && settings.EnableSteamAccountSync)
                {
                    StatusMessage += " Steam cloud titles were not returned (profile may be private). Use Connect Steam in Settings.";
                }

                if (epicUninstalled == 0 && settings.EnableEpicImport && settings.EnableEpicAccountSync)
                {
                    StatusMessage += " Epic cloud titles were not returned. Use Connect Epic in Settings.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task LaunchSelectedAsync()
    {
        if (SelectedGame is null)
        {
            return;
        }

        var action = SelectedGame.IsInstalled ? "launch" : "install";
        var didLaunch = await _gameLauncherService.LaunchAsync(SelectedGame);
        if (!didLaunch)
        {
            StatusMessage = $"{PrimaryActionText} unavailable for {SelectedGame.Title}.";
            return;
        }

        if (SelectedGame.IsInstalled)
        {
            var updated = await _gameLibraryService.MarkLaunchedAsync(SelectedGame.Key);
            if (updated is not null)
            {
                ApplyGameUpdate(updated);
            }
        }

        StatusMessage = action == "launch"
            ? $"Launching {SelectedGame.Title}..."
            : $"Opening install for {SelectedGame.Title}...";
    }

    private async Task ToggleFavoriteAsync()
    {
        await ToggleFavoriteForGameAsync(SelectedGame);
    }

    public async Task LaunchGameAsync(GameInfo? game)
    {
        if (game is null)
        {
            return;
        }

        SelectedGame = Games.FirstOrDefault(candidate => candidate.Key == game.Key) ?? game;
        await LaunchSelectedAsync();
    }

    public async Task ToggleFavoriteForGameAsync(GameInfo? game)
    {
        if (game is null)
        {
            return;
        }

        var target = _allGames.FirstOrDefault(candidate => candidate.Key == game.Key) ?? game;
        target.IsFavorite = !target.IsFavorite;
        await _gameLibraryService.SetFavoriteAsync(target.Key, target.IsFavorite);

        var title = target.Title;
        var isFavorite = target.IsFavorite;
        var key = target.Key;

        ApplyFilters();
        SelectedGame = Games.FirstOrDefault(candidate => candidate.Key == key);
        StatusMessage = isFavorite
            ? $"{title} added to favorites."
            : $"{title} removed from favorites.";
    }

    public async Task RefreshCoverForGameAsync(GameInfo? game)
    {
        if (game is null)
        {
            return;
        }

        var title = game.Title;
        var key = game.Key;

        var updated = await _gameLibraryService.RefreshCoverArtForGameAsync(key);
        if (!updated)
        {
            StatusMessage = $"No new cover art was found for {title}.";
            return;
        }

        _allGames = (await _gameLibraryService.LoadAsync()).ToList();
        RebuildPlatformFilters();
        ApplyFilters();
        SelectedGame = Games.FirstOrDefault(candidate => candidate.Key == key);
        StatusMessage = $"Cover art refreshed for {title}.";
    }

    private void ApplyGameUpdate(GameInfo updatedGame)
    {
        for (var i = 0; i < _allGames.Count; i++)
        {
            if (_allGames[i].Key == updatedGame.Key)
            {
                _allGames[i] = updatedGame;
                break;
            }
        }

        ApplyFilters();

        SelectedGame = Games.FirstOrDefault(g => g.Key == updatedGame.Key);
    }

    private async void OnLibraryChanged(object? sender, EventArgs e)
    {
        try
        {
            await _libraryReloadGate.WaitAsync();
            _allGames = (await _gameLibraryService.LoadAsync()).ToList();
            RebuildPlatformFilters();
            ApplyFilters();
        }
        catch
        {
            // Ignore transient reload errors triggered by background updates.
        }
        finally
        {
            _libraryReloadGate.Release();
        }
    }

    private void RebuildPlatformFilters()
    {
        var selected = SelectedPlatform;

        PlatformFilters.Clear();
        PlatformFilters.Add("All");

        foreach (var platform in _allGames
                     .Select(g => g.PlatformName)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            PlatformFilters.Add(platform);
        }

        SelectedPlatform = PlatformFilters.Contains(selected) ? selected : "All";
    }

    private void ApplyFilters()
    {
        var filtered = _allGames.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(g =>
                g.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                g.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedPlatform, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(g => string.Equals(g.PlatformName, SelectedPlatform, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(SelectedLibraryFilter, "Installed", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(g => g.IsInstalled);
        }
        else if (string.Equals(SelectedLibraryFilter, "Not Installed", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(g => !g.IsInstalled);
        }
        else if (string.Equals(SelectedLibraryFilter, "Favorite", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(g => g.IsFavorite);
        }

        var sorted = filtered
            .OrderByDescending(g => g.IsFavorite)
            .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Games.Clear();
        foreach (var game in sorted)
        {
            Games.Add(game);
        }

        OnPropertyChanged(nameof(HasGames));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyStateVisibility));

        if (SelectedGame is not null && Games.All(g => g.Key != SelectedGame.Key))
        {
            SelectedGame = null;
        }
    }
}
