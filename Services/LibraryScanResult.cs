using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

public sealed class LibraryScanResult
{
    public IReadOnlyList<GameInfo> Games { get; init; } = Array.Empty<GameInfo>();

    public bool HasChanges { get; init; }

    public int AddedCount { get; init; }

    public int RemovedCount { get; init; }

    public int UpdatedCount { get; init; }
}
