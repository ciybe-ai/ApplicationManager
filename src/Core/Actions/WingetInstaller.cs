using System.Diagnostics;
using ApplicationManager.Core.Logging;
using ApplicationManager.Core.Models;

namespace ApplicationManager.Core.Actions;

public static class WingetInstaller
{
    public static bool IsWingetAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("winget.exe", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryInstall(WishlistEntry entry, FileLogger log)
    {
        try
        {
            var args = $"install --id {entry.PackageId} --silent " +
                       $"--accept-source-agreements --accept-package-agreements " +
                       $"--scope {(entry.Scope == "user" ? "user" : "machine")}";

            var psi = new ProcessStartInfo("winget.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(600_000);

            if (proc?.ExitCode == 0)
            {
                log.Info($"Installiert: {entry.PackageId} (Scope: {entry.Scope})");
                return true;
            }

            log.Warn($"winget-Exitcode {proc?.ExitCode} bei {entry.PackageId}");
            return false;
        }
        catch (Exception ex)
        {
            log.Error($"Fehler bei Installation von '{entry.PackageId}': {ex.Message}");
            return false;
        }
    }
}
