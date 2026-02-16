using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

public interface IGameLauncherService
{
    Task<bool> LaunchAsync(GameInfo game);
}
