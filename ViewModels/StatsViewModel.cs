using System.Collections.ObjectModel;
using System.Windows.Input;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;

namespace Mystical.WinUI.ViewModels;

public sealed class StatsViewModel : ViewModelBase
{
    private readonly IGameLibraryService _gameLibraryService;
    private readonly AsyncRelayCommand _refreshCommand;

    private bool _isLoading;
    private string _statusMessage = "Ready";
    private int _totalGames;
    private int _installedGames;
    private int _notInstalledGames;
    private int _favoriteGames;
    private string _totalSizeText = "0 GB";
    private string _totalPlaytimeText = "0h";
    private string _totalPlaytimeInfoText = "Counts cumulative tracked playtime from all games currently in your library.";
    private string _mostPlayedText = "None";
    private string _gamesActivitySummaryText = "No recent play activity.";
    private string _timeActivitySummaryText = "No recent playtime activity.";

    public ObservableCollection<PlatformStatItem> PlatformBreakdown { get; } = new();
    public ObservableCollection<ActivityHeatCell> GamesActivityCells { get; } = new();
    public ObservableCollection<ActivityHeatCell> TimeActivityCells { get; } = new();

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

    public int TotalGames
    {
        get => _totalGames;
        private set => SetProperty(ref _totalGames, value);
    }

    public int InstalledGames
    {
        get => _installedGames;
        private set => SetProperty(ref _installedGames, value);
    }

    public int NotInstalledGames
    {
        get => _notInstalledGames;
        private set => SetProperty(ref _notInstalledGames, value);
    }

    public int FavoriteGames
    {
        get => _favoriteGames;
        private set => SetProperty(ref _favoriteGames, value);
    }

    public string TotalSizeText
    {
        get => _totalSizeText;
        private set => SetProperty(ref _totalSizeText, value);
    }

    public string TotalPlaytimeText
    {
        get => _totalPlaytimeText;
        private set => SetProperty(ref _totalPlaytimeText, value);
    }

    public string TotalPlaytimeInfoText
    {
        get => _totalPlaytimeInfoText;
        private set => SetProperty(ref _totalPlaytimeInfoText, value);
    }

    public string MostPlayedText
    {
        get => _mostPlayedText;
        private set => SetProperty(ref _mostPlayedText, value);
    }

    public string GamesActivitySummaryText
    {
        get => _gamesActivitySummaryText;
        private set => SetProperty(ref _gamesActivitySummaryText, value);
    }

    public string TimeActivitySummaryText
    {
        get => _timeActivitySummaryText;
        private set => SetProperty(ref _timeActivitySummaryText, value);
    }

    public ICommand RefreshCommand => _refreshCommand;

    public StatsViewModel(IGameLibraryService gameLibraryService)
    {
        _gameLibraryService = gameLibraryService;
        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = "Calculating library statistics...";

        try
        {
            var games = (await _gameLibraryService.LoadAsync()).ToList();

            TotalGames = games.Count;
            InstalledGames = games.Count(g => g.IsInstalled);
            NotInstalledGames = games.Count(g => !g.IsInstalled);
            FavoriteGames = games.Count(g => g.IsFavorite);

            var totalSizeBytes = games.Sum(g => Math.Max(0, g.SizeBytes));
            TotalSizeText = FormatSize(totalSizeBytes);
            TotalPlaytimeText = FormatPlaytime(TimeSpan.FromMinutes(games.Sum(g => Math.Max(0, g.Playtime.TotalMinutes))));

            var mostPlayed = games
                .OrderByDescending(g => g.Playtime)
                .FirstOrDefault(g => g.Playtime > TimeSpan.Zero);
            MostPlayedText = mostPlayed is null
                ? "None"
                : $"{mostPlayed.Title} ({mostPlayed.FormattedPlaytime})";

            BuildActivityHeatmaps(games);

            PlatformBreakdown.Clear();
            foreach (var group in games
                         .GroupBy(g => g.PlatformName)
                         .OrderByDescending(g => g.Count())
                         .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                PlatformBreakdown.Add(new PlatformStatItem(group.Key, group.Count()));
            }

            StatusMessage = $"Updated {DateTime.Now:g}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stats failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        const double kb = 1024d;
        const double mb = kb * 1024d;
        const double gb = mb * 1024d;
        const double tb = gb * 1024d;

        if (bytes >= tb)
        {
            return $"{bytes / tb:0.00} TB";
        }

        if (bytes >= gb)
        {
            return $"{bytes / gb:0.00} GB";
        }

        if (bytes >= mb)
        {
            return $"{bytes / mb:0.0} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / kb:0} KB";
        }

        return $"{bytes} B";
    }

    private static string FormatPlaytime(TimeSpan playtime)
    {
        if (playtime <= TimeSpan.Zero)
        {
            return "0h";
        }

        if (playtime.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)playtime.TotalMinutes)} min";
        }

        var totalHours = (int)playtime.TotalHours;
        var days = totalHours / 24;
        var hours = totalHours % 24;

        if (days > 0)
        {
            return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
        }

        return $"{totalHours}h {playtime.Minutes}m";
    }

    private void BuildActivityHeatmaps(IReadOnlyList<GameInfo> games)
    {
        GamesActivityCells.Clear();
        TimeActivityCells.Clear();

        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-181);
        while (startDate.DayOfWeek != DayOfWeek.Sunday)
        {
            startDate = startDate.AddDays(-1);
        }

        var dayGameCounts = new Dictionary<DateTime, int>();
        var dayPlaytimeMinutes = new Dictionary<DateTime, int>();

        foreach (var game in games)
        {
            if (game.LastPlayed is null)
            {
                continue;
            }

            var day = game.LastPlayed.Value.LocalDateTime.Date;
            if (day < startDate || day > endDate)
            {
                continue;
            }

            dayGameCounts[day] = dayGameCounts.TryGetValue(day, out var currentCount) ? currentCount + 1 : 1;

            var minutes = (int)Math.Max(0, Math.Round(game.Playtime.TotalMinutes));
            dayPlaytimeMinutes[day] = dayPlaytimeMinutes.TryGetValue(day, out var currentMinutes)
                ? currentMinutes + minutes
                : minutes;
        }

        var maxGameCount = dayGameCounts.Count == 0 ? 0 : dayGameCounts.Values.Max();
        var maxMinutes = dayPlaytimeMinutes.Count == 0 ? 0 : dayPlaytimeMinutes.Values.Max();
        var activeGameDays = dayGameCounts.Count(pair => pair.Value > 0);
        var activeTimeDays = dayPlaytimeMinutes.Count(pair => pair.Value > 0);

        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            var gameCount = dayGameCounts.TryGetValue(day, out var dayGames) ? dayGames : 0;
            var gameOpacity = ComputeHeatOpacity(gameCount, maxGameCount);
            GamesActivityCells.Add(new ActivityHeatCell(
                day,
                gameCount,
                gameOpacity,
                $"{day:ddd, MMM d}: {(gameCount == 0 ? "No games played" : $"{gameCount} game{(gameCount == 1 ? string.Empty : "s")} played")}"
            ));

            var minutes = dayPlaytimeMinutes.TryGetValue(day, out var dayMinutes) ? dayMinutes : 0;
            var timeOpacity = ComputeHeatOpacity(minutes, maxMinutes);
            TimeActivityCells.Add(new ActivityHeatCell(
                day,
                minutes,
                timeOpacity,
                $"{day:ddd, MMM d}: {(minutes == 0 ? "No tracked playtime" : $"{minutes} min tracked playtime")}"
            ));
        }

        GamesActivitySummaryText = activeGameDays == 0
            ? "No recent play activity in the last 26 weeks."
            : $"{activeGameDays} active day{(activeGameDays == 1 ? string.Empty : "s")} in the last 26 weeks.";

        TimeActivitySummaryText = activeTimeDays == 0
            ? "No tracked playtime activity in the last 26 weeks."
            : $"{activeTimeDays} day{(activeTimeDays == 1 ? string.Empty : "s")} with tracked playtime in the last 26 weeks.";
    }

    private static double ComputeHeatOpacity(int count, int maxCount)
    {
        if (count <= 0 || maxCount <= 0)
        {
            return 0.12;
        }

        var normalized = Math.Clamp((double)count / maxCount, 0, 1);
        return 0.28 + (Math.Sqrt(normalized) * 0.72);
    }
}

public sealed record PlatformStatItem(string Platform, int Count);

public sealed record ActivityHeatCell(DateTime Date, int Value, double HeatOpacity, string TooltipText);
