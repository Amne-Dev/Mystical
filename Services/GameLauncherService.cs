using System.Diagnostics;
using Mystical.WinUI.Models;
using Windows.System;

namespace Mystical.WinUI.Services;

public sealed class GameLauncherService : IGameLauncherService
{
    public async Task<bool> LaunchAsync(GameInfo game)
    {
        if (!string.IsNullOrWhiteSpace(game.LaunchUri) &&
            game.LaunchUri.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
        {
            var shellStartInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = game.LaunchUri,
                UseShellExecute = true
            };

            Process.Start(shellStartInfo);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(game.LaunchUri) && Uri.TryCreate(game.LaunchUri, UriKind.Absolute, out var uri))
        {
            try
            {
                var launched = await Launcher.LaunchUriAsync(uri);
                if (launched)
                {
                    return true;
                }
            }
            catch
            {
                // Fallback to shell execution below.
            }

            try
            {
                var shellStartInfo = new ProcessStartInfo
                {
                    FileName = game.LaunchUri,
                    UseShellExecute = true
                };

                Process.Start(shellStartInfo);
                return true;
            }
            catch
            {
                // Fall through to executable path fallback.
            }
        }

        if (!string.IsNullOrWhiteSpace(game.ExecutablePath) && File.Exists(game.ExecutablePath))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = game.ExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return true;
        }

        return false;
    }
}
