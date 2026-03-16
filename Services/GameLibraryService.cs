using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services.Detection;

namespace Mystical.WinUI.Services;

public sealed class GameLibraryService : IGameLibraryService
{
    private static readonly HttpClient CoverLookupHttpClient = CreateCoverLookupHttpClient();

    private readonly ISettingsService _settingsService;
    private readonly IReadOnlyList<IPlatformDetector> _detectors;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, string?> _igdbCoverCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _databasePath;
    private readonly string _coversPath;
    private string _igdbAccessToken = string.Empty;
    private DateTimeOffset _igdbAccessTokenExpiresUtc = DateTimeOffset.MinValue;

    private bool _databaseInitialized;
    private List<GameInfo> _games = new();

    public event EventHandler? LibraryChanged;

    public GameLibraryService(IEnumerable<IPlatformDetector> detectors, ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _detectors = detectors.ToList();

        var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MysticalWinUI");
        Directory.CreateDirectory(appFolder);

        _databasePath = Path.Combine(appFolder, "mystical_library.db");
        _coversPath = Path.Combine(appFolder, "covers");
        Directory.CreateDirectory(_coversPath);
    }

    public async Task<IReadOnlyList<GameInfo>> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureDatabaseInitializedAsync();

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            _games = await LoadGamesFromDatabaseAsync(connection);
            _games = ApplyPlatformDuplicateRules(_games);
            return _games.ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<GameInfo>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var result = await ScanForChangesAsync(cancellationToken);
        return result.Games;
    }

    public async Task<LibraryScanResult> ScanForChangesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync();
        var detectedGames = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var detector in _detectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ShouldRunDetector(detector, settings))
            {
                continue;
            }

            IReadOnlyList<GameInfo> detectorGames;
            try
            {
                detectorGames = await detector.DetectGamesAsync(cancellationToken);
            }
            catch
            {
                detectorGames = Array.Empty<GameInfo>();
            }

            foreach (var game in detectorGames)
            {
                if (detectedGames.TryGetValue(game.Key, out var existing))
                {
                    detectedGames[game.Key] = MergeDetectedGame(existing, game);
                    continue;
                }

                detectedGames[game.Key] = game;
            }
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureDatabaseInitializedAsync();

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var existingGames = await LoadGamesFromDatabaseAsync(connection, cancellationToken);
            var mergedGames = MergeDetectedGames(existingGames, detectedGames.Values);
            mergedGames = ApplySourceFilters(mergedGames, settings);
            mergedGames = PruneStaleMicrosoftStoreGames(mergedGames, detectedGames.Values);
            mergedGames = ApplyPlatformDuplicateRules(mergedGames);
            SanitizeLegacyCoverUrls(mergedGames);
            await PopulateMissingCoverArtFromIgdbAsync(mergedGames, settings, cancellationToken);

            var diff = BuildLibraryDiff(existingGames, mergedGames);

            if (!diff.HasChanges)
            {
                _games = existingGames
                    .OrderByDescending(g => g.IsFavorite)
                    .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new LibraryScanResult
                {
                    Games = _games.ToList(),
                    HasChanges = false,
                    AddedCount = 0,
                    RemovedCount = 0,
                    UpdatedCount = 0
                };
            }

            foreach (var game in mergedGames)
            {
                await UpsertGameAsync(connection, game, cancellationToken);
            }

            var mergedKeys = mergedGames
                .Select(game => game.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var staleGame in existingGames.Where(game => !mergedKeys.Contains(game.Key)))
            {
                await DeleteGameAsync(connection, staleGame, cancellationToken);
            }

            _games = mergedGames
                .OrderByDescending(g => g.IsFavorite)
                .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            RaiseLibraryChanged();
            return new LibraryScanResult
            {
                Games = _games.ToList(),
                HasChanges = true,
                AddedCount = diff.AddedCount,
                RemovedCount = diff.RemovedCount,
                UpdatedCount = diff.UpdatedCount
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetFavoriteAsync(string gameKey, bool isFavorite)
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureDatabaseInitializedAsync();

            var game = _games.FirstOrDefault(g => g.Key == gameKey);
            if (game is null)
            {
                return;
            }

            game.IsFavorite = isFavorite;

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await UpsertGameAsync(connection, game);
            RaiseLibraryChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GameInfo?> MarkLaunchedAsync(string gameKey)
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureDatabaseInitializedAsync();

            var game = _games.FirstOrDefault(g => g.Key == gameKey);
            if (game is null)
            {
                return null;
            }

            game.LastPlayed = DateTimeOffset.UtcNow;
            game.Playtime = game.Playtime.Add(TimeSpan.FromMinutes(1));

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await UpsertGameAsync(connection, game);

            RaiseLibraryChanged();
            return Clone(game);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GameInfo?> ImportCoverArtAsync(string gameKey, string sourceImagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureDatabaseInitializedAsync();

            var game = _games.FirstOrDefault(g => g.Key == gameKey);
            if (game is null)
            {
                return null;
            }

            var extension = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var safeFileName = CreateSafeFileName(game.Key) + extension.ToLowerInvariant();
            var destinationPath = Path.Combine(_coversPath, safeFileName);

            File.Copy(sourceImagePath, destinationPath, overwrite: true);

            game.CoverArtPath = destinationPath;

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await UpsertGameAsync(connection, game, cancellationToken);

            RaiseLibraryChanged();
            return Clone(game);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RefreshCoverArtForGameAsync(string gameKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            return false;
        }

        var settings = await _settingsService.LoadAsync();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureDatabaseInitializedAsync();

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var games = await LoadGamesFromDatabaseAsync(connection, cancellationToken);
            var game = games.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, gameKey, StringComparison.OrdinalIgnoreCase));
            if (game is null)
            {
                return false;
            }

            var previousPath = game.CoverArtPath ?? string.Empty;
            var previousUrl = game.CoverArtUrl ?? string.Empty;

            SanitizeLegacyCoverUrls(new[] { game });

            if (settings.OnlineCoverArt &&
                TryResolveIgdbCredentials(settings, out var clientId, out var clientSecret))
            {
                var accessToken = await GetIgdbAccessTokenAsync(clientId, clientSecret, cancellationToken);
                if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(game.Title))
                {
                    var lookupTitle = NormalizeLookupTitle(game.Title);
                    var coverUrl = string.Empty;

                    if (!string.IsNullOrWhiteSpace(lookupTitle) &&
                        _igdbCoverCache.TryGetValue(lookupTitle, out var cachedCover) &&
                        !string.IsNullOrWhiteSpace(cachedCover))
                    {
                        coverUrl = cachedCover;
                    }

                    if (string.IsNullOrWhiteSpace(coverUrl))
                    {
                        coverUrl = await TryLookupIgdbCoverUrlWithRetryAsync(game.Title, clientId, accessToken, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(coverUrl) && !string.IsNullOrWhiteSpace(lookupTitle))
                        {
                            _igdbCoverCache[lookupTitle] = coverUrl;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(coverUrl))
                    {
                        ApplyIgdbCoverForDisplay(game, coverUrl);
                    }
                }
            }

            var changed = !string.Equals(previousPath, game.CoverArtPath ?? string.Empty, StringComparison.Ordinal) ||
                          !string.Equals(previousUrl, game.CoverArtUrl ?? string.Empty, StringComparison.Ordinal);

            if (!changed)
            {
                return false;
            }

            await UpsertGameAsync(connection, game, cancellationToken);
            _games = games
                .OrderByDescending(g => g.IsFavorite)
                .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            RaiseLibraryChanged();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureDatabaseInitializedAsync();

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM games;";
            await command.ExecuteNonQueryAsync(cancellationToken);

            _games.Clear();
            RaiseLibraryChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetAppDataAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureDatabaseInitializedAsync();

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM games;";
            await command.ExecuteNonQueryAsync(cancellationToken);

            _games.Clear();
            ResetCoverCacheBestEffort();
            RaiseLibraryChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> RefreshCoverArtAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureDatabaseInitializedAsync();

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var games = await LoadGamesFromDatabaseAsync(connection, cancellationToken);
            var beforeCoverState = games.ToDictionary(
                g => g.Key,
                g => (Path: g.CoverArtPath ?? string.Empty, Url: g.CoverArtUrl ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            SanitizeLegacyCoverUrls(games);
            await PopulateMissingCoverArtFromIgdbAsync(games, settings, cancellationToken);

            var updatedCount = 0;
            foreach (var game in games)
            {
                if (!beforeCoverState.TryGetValue(game.Key, out var previousState))
                {
                    previousState = (string.Empty, string.Empty);
                }

                var currentPath = game.CoverArtPath ?? string.Empty;
                var currentUrl = game.CoverArtUrl ?? string.Empty;

                if (string.Equals(previousState.Url, currentUrl, StringComparison.Ordinal) &&
                    string.Equals(previousState.Path, currentPath, StringComparison.Ordinal))
                {
                    continue;
                }

                await UpsertGameAsync(connection, game, cancellationToken);
                updatedCount++;
            }

            if (updatedCount > 0)
            {
                _games = games
                    .OrderByDescending(g => g.IsFavorite)
                    .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                RaiseLibraryChanged();
            }

            return updatedCount;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureDatabaseInitializedAsync()
    {
        if (_databaseInitialized)
        {
            return;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS games (
                game_id TEXT NOT NULL,
                platform INTEGER NOT NULL,
                title TEXT NOT NULL,
                description TEXT,
                install_path TEXT,
                executable_path TEXT,
                launch_uri TEXT,
                playtime_minutes INTEGER DEFAULT 0,
                last_played TEXT,
                install_date TEXT,
                is_installed INTEGER DEFAULT 1,
                is_favorite INTEGER DEFAULT 0,
                size_bytes INTEGER DEFAULT 0,
                cover_art_path TEXT,
                cover_art_url TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (game_id, platform)
            );

            CREATE INDEX IF NOT EXISTS idx_games_title ON games(title);
            CREATE INDEX IF NOT EXISTS idx_games_platform ON games(platform);
            CREATE INDEX IF NOT EXISTS idx_games_favorite ON games(is_favorite);
            """;

        await command.ExecuteNonQueryAsync();
        _databaseInitialized = true;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_databasePath}");
    }

    private static List<GameInfo> MergeDetectedGames(IEnumerable<GameInfo> existingGames, IEnumerable<GameInfo> detectedGames)
    {
        var merged = existingGames.ToDictionary(g => g.Key, Clone, StringComparer.OrdinalIgnoreCase);

        foreach (var detected in detectedGames)
        {
            if (merged.TryGetValue(detected.Key, out var existing))
            {
                existing.Title = detected.Title;
                existing.Description = detected.Description;
                existing.InstallPath = detected.InstallPath;
                existing.ExecutablePath = detected.ExecutablePath;
                existing.LaunchUri = detected.LaunchUri;
                existing.InstallDate = detected.InstallDate;
                existing.IsInstalled = detected.IsInstalled;
                existing.SizeBytes = detected.SizeBytes > 0 ? detected.SizeBytes : existing.SizeBytes;
                existing.CoverArtUrl = string.IsNullOrWhiteSpace(detected.CoverArtUrl) ? existing.CoverArtUrl : detected.CoverArtUrl;
                continue;
            }

            merged[detected.Key] = Clone(detected);
        }

        return merged.Values.ToList();
    }

    private static (bool HasChanges, int AddedCount, int RemovedCount, int UpdatedCount) BuildLibraryDiff(
        IReadOnlyList<GameInfo> existingGames,
        IReadOnlyList<GameInfo> mergedGames)
    {
        var existingByKey = existingGames.ToDictionary(g => g.Key, StringComparer.OrdinalIgnoreCase);
        var mergedByKey = mergedGames.ToDictionary(g => g.Key, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var removed = 0;
        var updated = 0;

        foreach (var key in mergedByKey.Keys)
        {
            if (!existingByKey.TryGetValue(key, out var existing))
            {
                added++;
                continue;
            }

            if (!AreEquivalent(existing, mergedByKey[key]))
            {
                updated++;
            }
        }

        foreach (var key in existingByKey.Keys)
        {
            if (!mergedByKey.ContainsKey(key))
            {
                removed++;
            }
        }

        return (added > 0 || removed > 0 || updated > 0, added, removed, updated);
    }

    private static bool AreEquivalent(GameInfo left, GameInfo right)
    {
        return string.Equals(left.GameId, right.GameId, StringComparison.Ordinal) &&
               left.Platform == right.Platform &&
               string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
               string.Equals(left.Description, right.Description, StringComparison.Ordinal) &&
               string.Equals(left.InstallPath, right.InstallPath, StringComparison.Ordinal) &&
               string.Equals(left.ExecutablePath, right.ExecutablePath, StringComparison.Ordinal) &&
               string.Equals(left.LaunchUri, right.LaunchUri, StringComparison.Ordinal) &&
               left.Playtime == right.Playtime &&
               left.LastPlayed == right.LastPlayed &&
               left.InstallDate == right.InstallDate &&
               left.IsInstalled == right.IsInstalled &&
               left.IsFavorite == right.IsFavorite &&
               left.SizeBytes == right.SizeBytes &&
               string.Equals(left.CoverArtPath, right.CoverArtPath, StringComparison.Ordinal) &&
               string.Equals(left.CoverArtUrl, right.CoverArtUrl, StringComparison.Ordinal);
    }

    private static GameInfo MergeDetectedGame(GameInfo existing, GameInfo incoming)
    {
        var primary = existing.IsInstalled && !incoming.IsInstalled
            ? Clone(existing)
            : Clone(incoming);

        var secondary = existing.IsInstalled && !incoming.IsInstalled
            ? incoming
            : existing;

        if (string.IsNullOrWhiteSpace(primary.Title))
        {
            primary.Title = secondary.Title;
        }

        if (string.IsNullOrWhiteSpace(primary.Description))
        {
            primary.Description = secondary.Description;
        }

        if (string.IsNullOrWhiteSpace(primary.InstallPath))
        {
            primary.InstallPath = secondary.InstallPath;
        }

        if (string.IsNullOrWhiteSpace(primary.ExecutablePath))
        {
            primary.ExecutablePath = secondary.ExecutablePath;
        }

        if (string.IsNullOrWhiteSpace(primary.LaunchUri))
        {
            primary.LaunchUri = secondary.LaunchUri;
        }

        if (primary.SizeBytes <= 0)
        {
            primary.SizeBytes = secondary.SizeBytes;
        }

        if (primary.InstallDate is null)
        {
            primary.InstallDate = secondary.InstallDate;
        }

        primary.IsInstalled = existing.IsInstalled || incoming.IsInstalled;
        primary.CoverArtUrl = PickPreferredDetectedCoverUrl(primary.CoverArtUrl, secondary.CoverArtUrl);
        return primary;
    }

    private static string PickPreferredDetectedCoverUrl(string primaryCoverUrl, string secondaryCoverUrl)
    {
        var primary = primaryCoverUrl ?? string.Empty;
        var secondary = secondaryCoverUrl ?? string.Empty;

        if (string.IsNullOrWhiteSpace(primary))
        {
            return secondary;
        }

        if (string.IsNullOrWhiteSpace(secondary))
        {
            return primary;
        }

        var primaryWeak = IsWeakDetectedCoverUrl(primary);
        var secondaryWeak = IsWeakDetectedCoverUrl(secondary);

        if (primaryWeak && !secondaryWeak)
        {
            return secondary;
        }

        if (!primaryWeak && secondaryWeak)
        {
            return primary;
        }

        return primary;
    }

    private static bool IsWeakDetectedCoverUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return true;
        }

        if (url.Contains("epicgames.com/offer/", StringComparison.OrdinalIgnoreCase) &&
            url.Contains("/wide/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsLegacyImdbUrl(url);
    }

    private static GameInfo Clone(GameInfo game)
    {
        return new GameInfo
        {
            GameId = game.GameId,
            Platform = game.Platform,
            Title = game.Title,
            Description = game.Description,
            InstallPath = game.InstallPath,
            ExecutablePath = game.ExecutablePath,
            LaunchUri = game.LaunchUri,
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

    private static async Task<List<GameInfo>> LoadGamesFromDatabaseAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        var games = new List<GameInfo>();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                game_id,
                platform,
                title,
                description,
                install_path,
                executable_path,
                launch_uri,
                playtime_minutes,
                last_played,
                install_date,
                is_installed,
                is_favorite,
                size_bytes,
                cover_art_path,
                cover_art_url
            FROM games
            ORDER BY title COLLATE NOCASE;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var game = new GameInfo
            {
                GameId = reader.GetString(0),
                Platform = (GamePlatform)reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                InstallPath = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                ExecutablePath = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                LaunchUri = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Playtime = TimeSpan.FromMinutes(reader.IsDBNull(7) ? 0 : reader.GetInt64(7)),
                LastPlayed = ParseDate(reader.IsDBNull(8) ? null : reader.GetString(8)),
                InstallDate = ParseDate(reader.IsDBNull(9) ? null : reader.GetString(9)),
                IsInstalled = !reader.IsDBNull(10) && reader.GetInt32(10) == 1,
                IsFavorite = !reader.IsDBNull(11) && reader.GetInt32(11) == 1,
                SizeBytes = reader.IsDBNull(12) ? 0 : reader.GetInt64(12),
                CoverArtPath = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                CoverArtUrl = reader.IsDBNull(14) ? string.Empty : reader.GetString(14)
            };

            games.Add(game);
        }

        return games;
    }

    private static async Task UpsertGameAsync(SqliteConnection connection, GameInfo game, CancellationToken cancellationToken = default)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO games (
                game_id,
                platform,
                title,
                description,
                install_path,
                executable_path,
                launch_uri,
                playtime_minutes,
                last_played,
                install_date,
                is_installed,
                is_favorite,
                size_bytes,
                cover_art_path,
                cover_art_url,
                updated_at
            )
            VALUES (
                $game_id,
                $platform,
                $title,
                $description,
                $install_path,
                $executable_path,
                $launch_uri,
                $playtime_minutes,
                $last_played,
                $install_date,
                $is_installed,
                $is_favorite,
                $size_bytes,
                $cover_art_path,
                $cover_art_url,
                CURRENT_TIMESTAMP
            )
            ON CONFLICT(game_id, platform) DO UPDATE SET
                title = excluded.title,
                description = excluded.description,
                install_path = excluded.install_path,
                executable_path = excluded.executable_path,
                launch_uri = excluded.launch_uri,
                playtime_minutes = excluded.playtime_minutes,
                last_played = excluded.last_played,
                install_date = excluded.install_date,
                is_installed = excluded.is_installed,
                is_favorite = excluded.is_favorite,
                size_bytes = excluded.size_bytes,
                cover_art_path = excluded.cover_art_path,
                cover_art_url = excluded.cover_art_url,
                updated_at = CURRENT_TIMESTAMP;
            """;

        AddParameter(command, "$game_id", game.GameId);
        AddParameter(command, "$platform", (int)game.Platform);
        AddParameter(command, "$title", game.Title);
        AddParameter(command, "$description", game.Description);
        AddParameter(command, "$install_path", game.InstallPath);
        AddParameter(command, "$executable_path", game.ExecutablePath);
        AddParameter(command, "$launch_uri", game.LaunchUri);
        AddParameter(command, "$playtime_minutes", (long)Math.Max(0, game.Playtime.TotalMinutes));
        AddParameter(command, "$last_played", FormatDate(game.LastPlayed));
        AddParameter(command, "$install_date", FormatDate(game.InstallDate));
        AddParameter(command, "$is_installed", game.IsInstalled ? 1 : 0);
        AddParameter(command, "$is_favorite", game.IsFavorite ? 1 : 0);
        AddParameter(command, "$size_bytes", game.SizeBytes);
        AddParameter(command, "$cover_art_path", game.CoverArtPath);
        AddParameter(command, "$cover_art_url", game.CoverArtUrl);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteGameAsync(SqliteConnection connection, GameInfo game, CancellationToken cancellationToken = default)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM games
            WHERE game_id = $game_id
              AND platform = $platform;
            """;

        AddParameter(command, "$game_id", game.GameId);
        AddParameter(command, "$platform", (int)game.Platform);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string CreateSafeFileName(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(Path.GetInvalidFileNameChars().Contains(c) ? '_' : c);
        }

        return sb.ToString();
    }

    private static string? FormatDate(DateTimeOffset? date)
    {
        return date?.ToUniversalTime().ToString("O");
    }

    private static DateTimeOffset? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;
    }

    private static bool ShouldRunDetector(IPlatformDetector detector, AppSettings settings)
    {
        return detector switch
        {
            SteamDetector => settings.EnableSteamImport,
            SteamAccountDetector => settings.EnableSteamImport,
            EpicDetector => settings.EnableEpicImport,
            EpicAccountDetector => settings.EnableEpicImport,
            XboxDetector => settings.EnableXboxImport,
            MicrosoftStoreDetector => settings.EnableXboxImport,
            GogDetector => settings.EnableGogImport,
            _ => true
        };
    }

    private static List<GameInfo> ApplyPlatformDuplicateRules(List<GameInfo> games)
    {
        return RemoveMicrosoftStoreDuplicatesWhenXboxExists(games);
    }

    private static List<GameInfo> RemoveMicrosoftStoreDuplicatesWhenXboxExists(List<GameInfo> games)
    {
        var xboxTitles = games
            .Where(game => game.Platform == GamePlatform.Xbox)
            .Select(game => NormalizeTitleForDuplicateDetection(game.Title))
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .ToHashSet(StringComparer.Ordinal);

        if (xboxTitles.Count == 0)
        {
            return games;
        }

        return games
            .Where(game =>
            {
                if (game.Platform != GamePlatform.MicrosoftStore)
                {
                    return true;
                }

                var normalizedTitle = NormalizeTitleForDuplicateDetection(game.Title);
                return string.IsNullOrWhiteSpace(normalizedTitle) || !xboxTitles.Contains(normalizedTitle);
            })
            .ToList();
    }

    private static List<GameInfo> ApplySourceFilters(List<GameInfo> games, AppSettings settings)
    {
        return games.Where(game => game.Platform switch
        {
            GamePlatform.Steam => settings.EnableSteamImport,
            GamePlatform.EpicGames => settings.EnableEpicImport,
            GamePlatform.Xbox => settings.EnableXboxImport,
            GamePlatform.MicrosoftStore => settings.EnableXboxImport,
            GamePlatform.GOG => settings.EnableGogImport,
            _ => true
        }).ToList();
    }

    private async Task PopulateMissingCoverArtFromIgdbAsync(
        IEnumerable<GameInfo> games,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.OnlineCoverArt)
        {
            return;
        }

        if (!TryResolveIgdbCredentials(settings, out var clientId, out var clientSecret))
        {
            return;
        }

        var accessToken = await GetIgdbAccessTokenAsync(clientId, clientSecret, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var missingCoverGames = games
            .Where(NeedsIgdbCoverEnrichment)
            .Where(game => !string.IsNullOrWhiteSpace(game.Title))
            .ToList();

        if (missingCoverGames.Count == 0)
        {
            return;
        }

        var attemptedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in missingCoverGames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lookupTitle = NormalizeLookupTitle(game.Title);
            if (string.IsNullOrWhiteSpace(lookupTitle))
            {
                continue;
            }

            if (_igdbCoverCache.TryGetValue(lookupTitle, out var cachedCoverUrl))
            {
                if (!string.IsNullOrWhiteSpace(cachedCoverUrl))
                {
                    ApplyIgdbCoverForDisplay(game, cachedCoverUrl);
                    continue;
                }
            }

            if (attemptedThisRun.Contains(lookupTitle))
            {
                continue;
            }

            attemptedThisRun.Add(lookupTitle);

            var coverUrl = await TryLookupIgdbCoverUrlWithRetryAsync(game.Title, clientId, accessToken, cancellationToken);

            if (!string.IsNullOrWhiteSpace(coverUrl))
            {
                _igdbCoverCache[lookupTitle] = coverUrl;
                ApplyIgdbCoverForDisplay(game, coverUrl);
            }
        }
    }

    private static bool TryResolveIgdbCredentials(AppSettings settings, out string clientId, out string clientSecret)
    {
        clientId = settings.IgdbClientId.Trim();
        clientSecret = settings.IgdbClientSecret.Trim();

        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = (Environment.GetEnvironmentVariable("IGDB_CLIENT_ID") ??
                        Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID") ??
                        string.Empty).Trim();
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            clientSecret = (Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET") ??
                            Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET") ??
                            string.Empty).Trim();
        }

        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
    }

    private async Task<string> GetIgdbAccessTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_igdbAccessToken) &&
            DateTimeOffset.UtcNow < _igdbAccessTokenExpiresUtc.AddMinutes(-1))
        {
            return _igdbAccessToken;
        }

        var tokenUrl =
            $"https://id.twitch.tv/oauth2/token?client_id={Uri.EscapeDataString(clientId)}&client_secret={Uri.EscapeDataString(clientSecret)}&grant_type=client_credentials";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            using var response = await CoverLookupHttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            using var document = JsonDocument.Parse(payload);
            var token = GetJsonString(document.RootElement, "access_token");
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            var expiresInSeconds = document.RootElement.TryGetProperty("expires_in", out var expiresValue) &&
                                   expiresValue.ValueKind == JsonValueKind.Number &&
                                   expiresValue.TryGetInt32(out var parsed)
                ? Math.Max(60, parsed)
                : 3600;

            _igdbAccessToken = token;
            _igdbAccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
            return _igdbAccessToken;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> TryLookupIgdbCoverUrlAsync(
        string title,
        string clientId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var normalizedRequested = NormalizeLookupTitle(title);
        if (normalizedRequested.Length < 2)
        {
            return string.Empty;
        }

        var escapedTitle = EscapeIgdbSearchText(title.Trim());
        if (string.IsNullOrWhiteSpace(escapedTitle))
        {
            return string.Empty;
        }

        var query = $"search \"{escapedTitle}\"; fields name,category,version_parent,cover.image_id; where cover != null & version_parent = null; limit 10;";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
            {
                Content = new StringContent(query, Encoding.UTF8, "text/plain")
            };

            request.Headers.Add("Client-ID", clientId);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            using var response = await CoverLookupHttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            var bestScore = int.MinValue;
            var bestImageId = string.Empty;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var imageId = TryGetNestedString(item, "cover", "image_id");
                if (string.IsNullOrWhiteSpace(imageId))
                {
                    continue;
                }

                var candidateTitle = GetJsonString(item, "name");
                var score = ComputeIgdbMatchScore(normalizedRequested, candidateTitle, item);

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestImageId = imageId;
            }

            return bestScore >= 5
                ? $"https://images.igdb.com/igdb/image/upload/t_cover_big_2x/{bestImageId}.jpg"
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> TryLookupIgdbCoverUrlWithRetryAsync(
        string title,
        string clientId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var delays = new[] { 0, 250, 600 };
        for (var attempt = 0; attempt < delays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (delays[attempt] > 0)
            {
                await Task.Delay(delays[attempt], cancellationToken);
            }

            var coverUrl = await TryLookupIgdbCoverUrlAsync(title, clientId, accessToken, cancellationToken);
            if (!string.IsNullOrWhiteSpace(coverUrl))
            {
                return coverUrl;
            }
        }

        return string.Empty;
    }

    private static int ComputeIgdbMatchScore(string normalizedRequested, string candidateTitle, JsonElement item)
    {
        var score = 0;
        var normalizedCandidate = NormalizeLookupTitle(candidateTitle);

        if (normalizedCandidate == normalizedRequested)
        {
            score += 8;
        }
        else if (normalizedCandidate.StartsWith(normalizedRequested, StringComparison.Ordinal) ||
                 normalizedRequested.StartsWith(normalizedCandidate, StringComparison.Ordinal))
        {
            score += 5;
        }
        else if (normalizedCandidate.Contains(normalizedRequested, StringComparison.Ordinal) ||
                 normalizedRequested.Contains(normalizedCandidate, StringComparison.Ordinal))
        {
            score += 2;
        }

        if (item.TryGetProperty("category", out var categoryElement) &&
            categoryElement.ValueKind == JsonValueKind.Number &&
            categoryElement.TryGetInt32(out var category))
        {
            if (category == 0 || category == 8 || category == 9)
            {
                score += 3;
            }
            else if (category == 1 || category == 5 || category == 6 || category == 7)
            {
                score -= 3;
            }
        }

        if (normalizedCandidate.Contains("soundtrack", StringComparison.Ordinal) ||
            normalizedCandidate.Contains("ost", StringComparison.Ordinal) ||
            normalizedCandidate.Contains("demo", StringComparison.Ordinal))
        {
            score -= 4;
        }

        return score;
    }

    private static string EscapeIgdbSearchText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string TryGetNestedString(JsonElement element, string parentProperty, string childProperty)
    {
        if (!element.TryGetProperty(parentProperty, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return GetJsonString(parent, childProperty);
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string NormalizeLookupTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray());

        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeTitleForDuplicateDetection(string value)
    {
        return NormalizeLookupTitle(value);
    }

    private static HttpClient CreateCoverLookupHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("MysticalWinUI/1.0");
        return client;
    }

    private static void SanitizeLegacyCoverUrls(IEnumerable<GameInfo> games)
    {
        foreach (var game in games)
        {
            if (IsLegacyImdbUrl(game.CoverArtUrl))
            {
                game.CoverArtUrl = string.Empty;
            }
        }
    }

    private static bool IsLegacyImdbUrl(string coverArtUrl)
    {
        if (string.IsNullOrWhiteSpace(coverArtUrl))
        {
            return false;
        }

        return coverArtUrl.Contains("imdb.com", StringComparison.OrdinalIgnoreCase) ||
               coverArtUrl.Contains("media-imdb.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsIgdbCoverEnrichment(GameInfo game)
    {
        if (ShouldPreferIgdbForPlatform(game.Platform))
        {
            // For Xbox/MS Store prefer IGDB portrait covers even if local package logos exist.
            if (IsIgdbCoverUrl(game.CoverArtUrl))
            {
                return !string.IsNullOrWhiteSpace(game.CoverArtPath);
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(game.CoverArtPath) && File.Exists(game.CoverArtPath))
        {
            return false;
        }

        var coverUrl = game.CoverArtUrl ?? string.Empty;
        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            return true;
        }

        if (IsLegacyImdbUrl(coverUrl))
        {
            return true;
        }

        // Epic URLs are often landscape or missing; IGDB gives us consistent portrait covers.
        if (game.Platform == GamePlatform.EpicGames &&
            coverUrl.Contains("epicgames.com/offer/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static void ApplyIgdbCoverForDisplay(GameInfo game, string igdbCoverUrl)
    {
        if (string.IsNullOrWhiteSpace(igdbCoverUrl))
        {
            return;
        }

        game.CoverArtUrl = igdbCoverUrl;

        if (ShouldPreferIgdbForPlatform(game.Platform))
        {
            // GameInfo.CoverArtUri prioritizes local path over URL.
            // Clear local icon path so IGDB portrait cover is actually displayed.
            game.CoverArtPath = string.Empty;
        }
    }

    private static bool ShouldPreferIgdbForPlatform(GamePlatform platform)
    {
        return platform == GamePlatform.Xbox || platform == GamePlatform.MicrosoftStore;
    }

    private static bool IsIgdbCoverUrl(string? coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            return false;
        }

        return coverUrl.Contains("images.igdb.com/igdb/image/upload/", StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseLibraryChanged()
    {
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    private static List<GameInfo> PruneStaleMicrosoftStoreGames(
        List<GameInfo> mergedGames,
        IEnumerable<GameInfo> detectedGames)
    {
        var detectedMicrosoftStoreKeys = detectedGames
            .Where(game => game.Platform == GamePlatform.MicrosoftStore)
            .Select(game => game.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Keep existing entries when detection returns no Microsoft Store data at all,
        // which usually means a transient API/read failure instead of "no games".
        if (detectedMicrosoftStoreKeys.Count == 0)
        {
            return mergedGames;
        }

        return mergedGames
            .Where(game => game.Platform != GamePlatform.MicrosoftStore || detectedMicrosoftStoreKeys.Contains(game.Key))
            .ToList();
    }

    private void ResetCoverCacheBestEffort()
    {
        Directory.CreateDirectory(_coversPath);

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(_coversPath, "*", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            TryDeleteFile(file);
        }

        var directories = Directory.EnumerateDirectories(_coversPath, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length)
            .ToList();

        foreach (var directory in directories)
        {
            TryDeleteDirectory(directory);
        }
    }

    private static void TryDeleteFile(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return;
            }
            catch when (attempt < 2)
            {
                Thread.Sleep(100);
            }
            catch
            {
                return;
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: false);
                return;
            }
            catch when (attempt < 2)
            {
                Thread.Sleep(100);
            }
            catch
            {
                return;
            }
        }
    }
}
