using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services.Detection;

public interface IPlatformDetector
{
    string PlatformName { get; }

    Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default);
}
