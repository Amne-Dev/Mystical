using System.Text.Json;
using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MysticalWinUI");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(_settingsPath);
        using var document = await JsonDocument.ParseAsync(stream);

        var settings = document.RootElement.Deserialize(AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();

        // Migrate existing users forward without forcing onboarding immediately.
        if (!document.RootElement.TryGetProperty("hasCompletedOnboarding", out _))
        {
            settings.HasCompletedOnboarding = true;
        }

        return settings;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, AppSettingsJsonContext.Default.AppSettings);
    }
}
