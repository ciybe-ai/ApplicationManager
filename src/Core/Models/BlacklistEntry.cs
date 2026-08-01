namespace ApplicationManager.Core.Models;

/// <summary>
/// Ein unerwünschtes Programm. Wird über die zentrale Blacklist (JSON) gepflegt.
/// </summary>
public class BlacklistEntry
{
    /// <summary>Muss exakt dem DisplayName in der Uninstall-Registry entsprechen.</summary>
    public string AppName { get; set; } = "";

    /// <summary>
    /// Optionale Silent-Argumente für EXE-Deinstaller, falls das Standard-
    /// QuietUninstallString fehlt (z. B. "/S", "/SILENT", "/VERYSILENT").
    /// </summary>
    public string? SilentArgs { get; set; }

    /// <summary>Freitext-Notiz, z. B. warum die App unerwünscht ist.</summary>
    public string? Note { get; set; }
}
