using Microsoft.Win32;

namespace Mystical.WinUI.Services.Detection.Internal;

internal static class RegistryHelper
{
    public static string ReadString(string keyPath, string valueName)
    {
        if (!TryParseKeyPath(keyPath, out var hive, out var subKeyPath))
        {
            return string.Empty;
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32, RegistryView.Default })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subKeyPath);
                var value = key?.GetValue(valueName);

                if (value is string str && !string.IsNullOrWhiteSpace(str))
                {
                    return Environment.ExpandEnvironmentVariables(str.Trim());
                }
            }
            catch
            {
                // ignore inaccessible registry views.
            }
        }

        return string.Empty;
    }

    public static IReadOnlyList<string> GetSubKeyNames(string keyPath)
    {
        if (!TryParseKeyPath(keyPath, out var hive, out var subKeyPath))
        {
            return Array.Empty<string>();
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32, RegistryView.Default })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subKeyPath);
                if (key is null)
                {
                    continue;
                }

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    names.Add(subKeyName);
                }
            }
            catch
            {
                // ignore inaccessible registry views.
            }
        }

        return names.ToList();
    }

    private static bool TryParseKeyPath(string keyPath, out RegistryHive hive, out string subKeyPath)
    {
        hive = RegistryHive.LocalMachine;
        subKeyPath = string.Empty;

        var normalized = keyPath.Replace('/', '\\').Trim();

        if (normalized.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            subKeyPath = normalized["HKEY_LOCAL_MACHINE\\".Length..];
            return true;
        }

        if (normalized.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            subKeyPath = normalized["HKEY_CURRENT_USER\\".Length..];
            return true;
        }

        return false;
    }
}
