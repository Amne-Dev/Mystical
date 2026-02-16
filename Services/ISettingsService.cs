using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync();

    Task SaveAsync(AppSettings settings);
}
