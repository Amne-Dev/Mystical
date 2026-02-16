using Mystical.WinUI.Models;
using Mystical.WinUI.Services.Detection.Internal;

namespace Mystical.WinUI.Services.Detection;

public sealed class MinecraftDetector : IPlatformDetector
{
    private const string CoverUrl = "https://www.minecraft.net/content/dam/games/minecraft/key-art/Homepage_Discovery_Default_1280x720.jpg";

    public string PlatformName => "Minecraft";

    public Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var launcherPath = GetLauncherPath();
        if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
        {
            return Task.FromResult<IReadOnlyList<GameInfo>>(Array.Empty<GameInfo>());
        }

        if (IsMinecraftCoveredByXboxOrStore())
        {
            return Task.FromResult<IReadOnlyList<GameInfo>>(Array.Empty<GameInfo>());
        }

        var installPath = Path.GetDirectoryName(launcherPath) ?? string.Empty;

        var game = new GameInfo
        {
            GameId = "minecraft_launcher",
            Platform = GamePlatform.Minecraft,
            Title = "Minecraft Launcher",
            Description = "Detected from Minecraft Launcher installation",
            InstallPath = installPath,
            ExecutablePath = launcherPath,
            LaunchUri = string.Empty,
            IsInstalled = true,
            SizeBytes = !string.IsNullOrWhiteSpace(installPath)
                ? FileSystemHelper.EstimateDirectorySize(installPath, maxFiles: 1200)
                : 0,
            CoverArtUrl = CoverUrl
        };

        return Task.FromResult<IReadOnlyList<GameInfo>>(new[] { game });
    }

    private static bool IsMinecraftCoveredByXboxOrStore()
    {
        if (!string.IsNullOrWhiteSpace(GetBedrockInstallPath()))
        {
            return true;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var storePackageCandidates = new[]
        {
            Path.Combine(localAppData, "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe"),
            Path.Combine(localAppData, "Packages", "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe"),
            Path.Combine(localAppData, "Packages", "Microsoft.MinecraftEducationEdition_8wekyb3d8bbwe"),
            Path.Combine(localAppData, "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe")
        };

        if (storePackageCandidates.Any(Directory.Exists))
        {
            return true;
        }

        return HasXboxMinecraftInstall();
    }

    private static bool HasXboxMinecraftInstall()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                {
                    continue;
                }

                roots.Add(Path.Combine(drive.RootDirectory.FullName, "XboxGames"));
                roots.Add(Path.Combine(drive.RootDirectory.FullName, "ModifiableWindowsApps"));
            }
        }
        catch
        {
            // Ignore drive access failures.
        }

        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XboxGames"));
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ModifiableWindowsApps"));

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                var name = Path.GetFileName(directory);
                if (!string.IsNullOrWhiteSpace(name) &&
                    name.Contains("minecraft", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetLauncherPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Minecraft Launcher", "MinecraftLauncher.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Minecraft Launcher", "MinecraftLauncher.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Minecraft", "MinecraftLauncher.exe"),
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "MinecraftLauncher.exe"),
            RegistryHelper.ReadString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\MinecraftLauncher.exe", "")
        };

        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
               ?? string.Empty;
    }

    private static string GetBedrockInstallPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidates = new[]
        {
            Path.Combine(localAppData, "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe"),
            Path.Combine(localAppData, "Packages", "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe"),
            Path.Combine(localAppData, "Packages", "Microsoft.MinecraftEducationEdition_8wekyb3d8bbwe")
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }
}
