using System.Diagnostics;
using Microsoft.Win32;
using ApplicationManager.Core.Logging;

namespace ApplicationManager.Core.Inventory;

/// <summary>
/// Bereinigt das Default-Profil (C:\Users\Default), damit KÜNFTIGE
/// Erstanmeldungen unerwünschte Autostart-Einträge nicht mehr "erben".
///
/// Hinweis: Die frühere Logik zum Laden fremder User-Hives (für Nutzer, die
/// gerade NICHT angemeldet sind) entfällt bewusst - da SystemAgent und
/// UserAgent beide nur per "Bei Anmeldung"-Trigger laufen, deckt der
/// UserAgent jeden Nutzer automatisch bei dessen eigenem Login ab. Ein
/// Nutzer, der sich nie anmeldet, braucht auch keine Bereinigung.
/// </summary>
public static class DefaultProfileCleaner
{
    public static void CleanAutostart(IEnumerable<string> blacklistedAppNames, FileLogger log)
    {
        const string defaultHive = @"C:\Users\Default\NTUSER.DAT";
        if (!File.Exists(defaultHive)) return;
        if (Registry.Users.OpenSubKey("DefaultTemp") != null) return; // schon geladen -> nichts anfassen

        if (!RunReg("load", @"HKU\DefaultTemp", defaultHive))
        {
            log.Warn("Default-Profil-Hive konnte nicht geladen werden.");
            return;
        }

        try
        {
            using var runKey = Registry.Users.OpenSubKey(
                @"DefaultTemp\Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (runKey == null) return;

            foreach (var valueName in runKey.GetValueNames())
            {
                var value = runKey.GetValue(valueName) as string ?? "";
                if (blacklistedAppNames.Any(name =>
                        valueName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                        value.Contains(name, StringComparison.OrdinalIgnoreCase)))
                {
                    runKey.DeleteValue(valueName, throwOnMissingValue: false);
                    log.Info($"Default-Profil: Autostart-Eintrag '{valueName}' entfernt.");
                }
            }
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            RunReg("unload", @"HKU\DefaultTemp", null);
        }
    }

    private static bool RunReg(string action, string key, string? path)
    {
        var args = path != null ? $"{action} \"{key}\" \"{path}\"" : $"{action} \"{key}\"";
        var psi = new ProcessStartInfo("reg.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit(15000);
        return proc?.ExitCode == 0;
    }
}
