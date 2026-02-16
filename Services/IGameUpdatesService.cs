using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

public interface IGameUpdatesService
{
    Task<GameUpdatesSnapshot> CheckForUpdatesAsync(IReadOnlyList<GameInfo> games, CancellationToken cancellationToken = default);
}
