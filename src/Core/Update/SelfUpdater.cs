using System.Security.Cryptography;
using System.Text.Json;
using ApplicationManager.Core.Logging;
using ApplicationManager.Core.Models;

namespace ApplicationManager.Core.Update;

/// <summary>
/// Domänenunabhängiges Selbst-Update über ein HTTPS-Manifest.
///
/// Ablauf:
///  1. Manifest laden (JSON), Version vergleichen.
///  2. Bei neuerer Version: passende EXE herunterladen + SHA256 prüfen.
///  3. Laufende EXE per Rename beiseiteschieben (funktioniert unter Windows
///     auch während der Prozess läuft - "Rename statt Löschen"-Trick),
///     neue EXE an die ursprüngliche Stelle kopieren.
///  4. Beim nächsten Start (Service-Neustart / nächster Login-Task-Lauf)
///     ist automatisch die neue Version aktiv. Alte Datei wird beim
///     naechsten Start aufgeraeumt.
/// </summary>
public class SelfUpdater
{
    private readonly HttpClient _http;
    private readonly FileLogger _log;
    private readonly bool _isSystemAgent;

    public SelfUpdater(HttpClient http, FileLogger log, bool isSystemAgent)
    {
        _http = http;
        _log = log;
        _isSystemAgent = isSystemAgent;
    }

    /// <summary>
    /// Räumt eine von einem vorherigen Update übrig gebliebene .old-Datei auf.
    /// Sollte bei jedem Programmstart als erstes aufgerufen werden.
    /// </summary>
    public void CleanupPreviousUpdate()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;
        var oldPath = exePath + ".old";
        if (File.Exists(oldPath))
        {
            try { File.Delete(oldPath); }
            catch { /* wird beim naechsten Mal erneut versucht */ }
        }
    }

    /// <returns>true, wenn ein Update eingespielt wurde (Neustart empfohlen/nötig)</returns>
    public async Task<bool> CheckAndApplyUpdateAsync(string manifestUrl, Version currentVersion)
    {
        UpdateManifest? manifest;
        try
        {
            var json = await _http.GetStringAsync(manifestUrl);
            manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _log.Warn($"Update-Manifest nicht erreichbar: {ex.Message}");
            return false;
        }

        if (manifest == null || !Version.TryParse(manifest.Version, out var remoteVersion))
        {
            _log.Warn("Update-Manifest ungueltig.");
            return false;
        }

        if (remoteVersion <= currentVersion)
        {
            _log.Info($"Kein Update noetig (lokal: {currentVersion}, remote: {remoteVersion}).");
            return false;
        }

        var (downloadUrl, sha256) = _isSystemAgent
            ? (manifest.SystemAgentUrl, manifest.SystemAgentSha256)
            : (manifest.UserAgentUrl, manifest.UserAgentSha256);

        if (string.IsNullOrWhiteSpace(downloadUrl) || string.IsNullOrWhiteSpace(sha256))
        {
            _log.Warn("Manifest enthaelt keine Download-Infos fuer diese Komponente.");
            return false;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            _log.Warn("Eigener Prozesspfad konnte nicht ermittelt werden.");
            return false;
        }

        var tempPath = exePath + ".new";
        var oldPath = exePath + ".old";

        try
        {
            _log.Info($"Lade Update {remoteVersion} von {downloadUrl} ...");
            var bytes = await _http.GetByteArrayAsync(downloadUrl);

            var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(actualHash, sha256.Replace("-", ""), StringComparison.OrdinalIgnoreCase))
            {
                _log.Error("SHA256-Pruefsumme des Updates stimmt nicht - Update abgebrochen.");
                return false;
            }

            await File.WriteAllBytesAsync(tempPath, bytes);

            // Laufende EXE beiseiteschieben (Rename ist bei einer laufenden
            // .NET-EXE unter Windows i.d.R. moeglich), neue EXE einsetzen.
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(exePath, oldPath);
            File.Move(tempPath, exePath);

            _log.Info($"Update auf Version {remoteVersion} eingespielt. Wird beim naechsten Start aktiv.");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Update fehlgeschlagen: {ex.Message}");
            // Aufräumen, falls halb fertig
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            return false;
        }
    }
}
