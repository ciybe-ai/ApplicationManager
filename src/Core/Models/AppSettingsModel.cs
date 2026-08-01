namespace ApplicationManager.Core.Models;

/// <summary>
/// Zentrale Laufzeit-Konfiguration. Wird aus appsettings.json geladen und
/// kann durch appsettings.Local.json (nicht eingecheckt) überschrieben werden.
/// </summary>
public class AppSettingsModel
{
    /// <summary>
    /// Quelle der Blacklist (unerwünschte Apps). Http(s)-URL ODER lokaler/UNC-Pfad.
    /// Beispiel: "https://intranet.example.com/it/app-blacklist.json"
    /// Beispiel: "\\\\domain.local\\netlogon\\it\\app-blacklist.json"
    /// </summary>
    public string BlacklistSource { get; set; } = "";

    /// <summary>
    /// Quelle der Wishlist (gewünschte Apps, winget-Paket-IDs). Optional.
    /// </summary>
    public string? WishlistSource { get; set; }

    /// <summary>
    /// Manifest-URL für Selbst-Updates, siehe UpdateManifest.
    /// </summary>
    public string? UpdateManifestUrl { get; set; }

    /// <summary>
    /// Wie viele Aktionen (Uninstalls/Installs) maximal pro Zyklus.
    /// </summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>
    /// Wartezeit zwischen zwei Aktionen innerhalb eines Zyklus (Sekunden).
    /// </summary>
    public int DelayBetweenActionsSeconds { get; set; } = 30;

    /// <summary>
    /// Wie oft auf Updates geprüft wird (Stunden). Beide Programme laufen
    /// nur "one-shot" pro Task-Trigger (kein Dauerprozess) - die Wiederholung
    /// und damit auch die Drosselung übernimmt der Scheduled-Task-Trigger
    /// (z. B. "Bei Anmeldung" + Wiederholung alle 15 Min).
    /// </summary>
    public int UpdateCheckIntervalHours { get; set; } = 12;

    /// <summary>
    /// Lokales Verzeichnis für Logs/State/Update-Zwischendateien.
    /// Leer lassen, um den programmspezifischen Standardpfad zu verwenden:
    /// SystemAgent -> C:\ProgramData\ApplicationManager
    /// UserAgent   -> %LocalAppData%\ApplicationManager
    /// </summary>
    public string? DataDirectory { get; set; }
}
