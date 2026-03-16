using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

public interface IGameLibraryService
{
    event EventHandler? LibraryChanged;

    Task<IReadOnlyList<GameInfo>> LoadAsync();

    Task<IReadOnlyList<GameInfo>> ScanAsync(CancellationToken cancellationToken = default);

    Task<LibraryScanResult> ScanForChangesAsync(CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(string gameKey, bool isFavorite);

    Task<GameInfo?> MarkLaunchedAsync(string gameKey);

    Task<GameInfo?> ImportCoverArtAsync(string gameKey, string sourceImagePath, CancellationToken cancellationToken = default);

    Task<bool> RefreshCoverArtForGameAsync(string gameKey, CancellationToken cancellationToken = default);

    Task ResetDatabaseAsync(CancellationToken cancellationToken = default);

    Task ResetAppDataAsync(CancellationToken cancellationToken = default);

    Task<int> RefreshCoverArtAsync(CancellationToken cancellationToken = default);
}
