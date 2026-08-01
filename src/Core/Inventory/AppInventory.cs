using Microsoft.Win32;

namespace ApplicationManager.Core.Inventory;

public class InstalledApp
{
    public string DisplayName { get; set; } = "";
    public string? UninstallString { get; set; }
    public string? QuietUninstallString { get; set; }
    public string Scope { get; set; } = "Machine"; // "Machine" oder "User"
    public string? Sid { get; set; } // gesetzt bei Scope == "User" und Fremd-Hive-Zugriff
}

/// <summary>
/// Liest installierte Programme aus den bekannten Uninstall-Registry-Pfaden.
/// </summary>
public static class AppInventory
{
    private static readonly string[] UninstallSubKeys =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    /// <summary>Systemweite Installationen (HKLM). Für den Service (SYSTEM).</summary>
    public static List<InstalledApp> GetMachineApps()
    {
        var result = new List<InstalledApp>();
        foreach (var subKey in UninstallSubKeys)
        {
            using var root = Registry.LocalMachine.OpenSubKey(subKey);
            if (root == null) continue;
            result.AddRange(ReadEntries(root, "Machine", null));
        }
        return result;
    }

    /// <summary>
    /// Installationen im aktuellen Nutzerkontext (HKCU). Für den UserAgent,
    /// der direkt in der Sitzung des angemeldeten Nutzers läuft - dadurch ist
    /// HKCU automatisch der richtige Hive, kein manuelles Laden nötig.
    /// </summary>
    public static List<InstalledApp> GetCurrentUserApps()
    {
        var result = new List<InstalledApp>();
        foreach (var subKey in UninstallSubKeys)
        {
            using var root = Registry.CurrentUser.OpenSubKey(subKey);
            if (root == null) continue;
            result.AddRange(ReadEntries(root, "User", null));
        }
        return result;
    }

    private static List<InstalledApp> ReadEntries(RegistryKey root, string scope, string? sid)
    {
        var list = new List<InstalledApp>();
        foreach (var subKeyName in root.GetSubKeyNames())
        {
            using var sub = root.OpenSubKey(subKeyName);
            var displayName = sub?.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName)) continue;

            list.Add(new InstalledApp
            {
                DisplayName = displayName,
                UninstallString = sub?.GetValue("UninstallString") as string,
                QuietUninstallString = sub?.GetValue("QuietUninstallString") as string,
                Scope = scope,
                Sid = sid
            });
        }
        return list;
    }
}
