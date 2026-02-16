namespace Mystical.WinUI.Services.Detection.Internal;

internal static class FileSystemHelper
{
    public static long EstimateDirectorySize(string directoryPath, int maxFiles = 2000)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return 0;
        }

        long total = 0;
        var fileCount = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(file).Length;
                    fileCount++;

                    if (fileCount >= maxFiles)
                    {
                        break;
                    }
                }
                catch
                {
                    // skip inaccessible files.
                }
            }
        }
        catch
        {
            return total;
        }

        return total;
    }

    public static string FindLikelyExecutable(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
        {
            return string.Empty;
        }

        try
        {
            var candidateFiles = Directory
                .EnumerateFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in candidateFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (!name.Contains("unins") && !name.Contains("uninstall") && !name.Contains("setup") && !name.Contains("redist"))
                {
                    return file;
                }
            }

            return candidateFiles.FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
