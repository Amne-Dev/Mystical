using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;

namespace Mystical.WinUI.ViewModels;

public sealed class UpdatesViewModel : ViewModelBase
{
    private readonly IGameLibraryService _gameLibraryService;
    private readonly IGameUpdatesService _gameUpdatesService;
    private readonly IGameLauncherService _gameLauncherService;
    private readonly AsyncRelayCommand _refreshCommand;

    private bool _isLoading;
    private string _statusMessage = "Ready";
    private string _lastCheckedText = "Never";
    private int _installedGamesChecked;

    public ObservableCollection<GameUpdateInfo> UpdateItems { get; } = new();

    public ObservableCollection<string> Notes { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LastCheckedText
    {
        get => _lastCheckedText;
        private set => SetProperty(ref _lastCheckedText, value);
    }

    public int InstalledGamesChecked
    {
        get => _installedGamesChecked;
        private set => SetProperty(ref _installedGamesChecked, value);
    }

    public int UpdatesFound => UpdateItems.Count;

    public Visibility EmptyStateVisibility => UpdateItems.Count == 0 && !IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NotesVisibility => Notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RefreshCommand => _refreshCommand;

    public UpdatesViewModel(
        IGameLibraryService gameLibraryService,
        IGameUpdatesService gameUpdatesService,
        IGameLauncherService gameLauncherService)
    {
        _gameLibraryService = gameLibraryService;
        _gameUpdatesService = gameUpdatesService;
        _gameLauncherService = gameLauncherService;
        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = "Checking launcher update status...";

        try
        {
            var games = await _gameLibraryService.LoadAsync();
            var snapshot = await _gameUpdatesService.CheckForUpdatesAsync(games);

            ReplaceItems(UpdateItems, snapshot.Updates);
            ReplaceItems(Notes, snapshot.Notes);

            InstalledGamesChecked = snapshot.InstalledGamesChecked;
            LastCheckedText = snapshot.CheckedAt.LocalDateTime.ToString("g");
            StatusMessage = snapshot.Updates.Count > 0
                ? $"Found {snapshot.Updates.Count} game update(s)."
                : "All checked games appear up to date.";

            OnPropertyChanged(nameof(UpdatesFound));
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(NotesVisibility));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update check failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    public async Task ApplyUpdateAsync(GameUpdateInfo? update)
    {
        if (update is null)
        {
            return;
        }

        try
        {
            var games = await _gameLibraryService.LoadAsync();
            var game = games.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, update.GameKey, StringComparison.OrdinalIgnoreCase));

            if (game is null)
            {
                StatusMessage = $"Cannot open update flow for {update.Title}: game not found in library.";
                return;
            }

            var launchTarget = BuildUpdateLaunchTarget(game, update.Platform);
            var opened = await _gameLauncherService.LaunchAsync(launchTarget);
            StatusMessage = opened
                ? $"Opened {update.PlatformName} update flow for {update.Title}."
                : $"Unable to open update flow for {update.Title}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start update for {update.Title}: {ex.Message}";
        }
    }

    private static GameInfo BuildUpdateLaunchTarget(GameInfo game, GamePlatform platform)
    {
        var updateUri = ResolveUpdateLaunchUri(game, platform);
        if (string.IsNullOrWhiteSpace(updateUri))
        {
            return game;
        }

        return new GameInfo
        {
            GameId = game.GameId,
            Platform = game.Platform,
            Title = game.Title,
            Description = game.Description,
            InstallPath = game.InstallPath,
            ExecutablePath = game.ExecutablePath,
            LaunchUri = updateUri,
            Playtime = game.Playtime,
            LastPlayed = game.LastPlayed,
            InstallDate = game.InstallDate,
            IsInstalled = game.IsInstalled,
            IsFavorite = game.IsFavorite,
            SizeBytes = game.SizeBytes,
            CoverArtPath = game.CoverArtPath,
            CoverArtUrl = game.CoverArtUrl
        };
    }

    private static string ResolveUpdateLaunchUri(GameInfo game, GamePlatform platform)
    {
        if (platform == GamePlatform.Steam && TryExtractSteamAppId(game.GameId, out var steamAppId))
        {
            // For already-installed titles this reliably opens the Steam page
            // where update/download can be triggered.
            return $"steam://nav/games/details/{steamAppId}";
        }

        if (platform == GamePlatform.EpicGames && !string.IsNullOrWhiteSpace(game.GameId))
        {
            return $"com.epicgames.launcher://apps/{game.GameId}?action=install";
        }

        return game.LaunchUri;
    }

    private static bool TryExtractSteamAppId(string gameId, out string appId)
    {
        appId = string.Empty;
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return false;
        }

        var digits = new string(gameId.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return false;
        }

        appId = digits;
        return true;
    }
}
