using System.Diagnostics;
using System.Text.RegularExpressions;
using ApplicationManager.Core.Inventory;
using ApplicationManager.Core.Logging;
using ApplicationManager.Core.Models;

namespace ApplicationManager.Core.Actions;

public static class Uninstaller
{
    public static bool TryUninstall(InstalledApp app, BlacklistEntry entry, FileLogger log)
    {
        var command = !string.IsNullOrWhiteSpace(app.QuietUninstallString)
            ? app.QuietUninstallString
            : app.UninstallString;

        if (string.IsNullOrWhiteSpace(command))
        {
            log.Warn($"Kein Uninstall-Befehl fuer '{app.DisplayName}' gefunden.");
            return false;
        }

        try
        {
            if (command.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                var guidMatch = Regex.Match(command, @"\{[0-9A-Fa-f\-]+\}");
                var args = guidMatch.Success
                    ? $"/x {guidMatch.Value} /qn /norestart"
                    : Regex.Replace(command, @"^msiexec(\.exe)?", "", RegexOptions.IgnoreCase) + " /qn /norestart";

                RunAndWait("msiexec.exe", args, log);
            }
            else
            {
                var (exe, defaultArgs) = SplitCommand(command);
                var args = entry.SilentArgs ?? defaultArgs;
                RunAndWait(exe, args, log);
            }

            log.Info($"Deinstalliert: {app.DisplayName} (Scope: {app.Scope}{(app.Sid != null ? ", SID: " + app.Sid : "")})");
            return true;
        }
        catch (Exception ex)
        {
            log.Error($"Fehler bei Deinstallation von '{app.DisplayName}': {ex.Message}");
            return false;
        }
    }

    private static (string exe, string args) SplitCommand(string command)
    {
        var match = Regex.Match(command, "^\"([^\"]+)\"\\s*(.*)$");
        return match.Success
            ? (match.Groups[1].Value, match.Groups[2].Value)
            : (command, "");
    }

    private static void RunAndWait(string exe, string args, FileLogger log)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var proc = Process.Start(psi);
        proc?.WaitForExit(600_000); // Timeout: 10 Minuten
    }
}
