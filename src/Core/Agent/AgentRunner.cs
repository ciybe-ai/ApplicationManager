using System.Reflection;
using ApplicationManager.Core.Actions;
using ApplicationManager.Core.Config;
using ApplicationManager.Core.Inventory;
using ApplicationManager.Core.Logging;
using ApplicationManager.Core.State;
using ApplicationManager.Core.Update;

namespace ApplicationManager.Core.Agent;

/// <summary>
/// Alles, worin sich SystemAgent und UserAgent unterscheiden. Der komplette
/// Ablauf (Update-Check, Blacklist/Wishlist laden, gedrosseltes Abarbeiten,
/// State speichern) ist identisch und lebt in <see cref="AgentRunner"/>.
/// </summary>
public class AgentOptions
{
    /// <summary>Kurzname für Log-/State-Dateinamen, z. B. "systemagent" oder "useragent".</summary>
    public required string AgentName { get; init; }

    /// <summary>Assembly des aufrufenden Programms (für Versionsermittlung).</summary>
    public required Assembly Assembly { get; init; }

    /// <summary>Registry-Quelle: HKLM (SystemAgent) oder HKCU (UserAgent).</summary>
    public required Func<List<InstalledApp>> GetInstalledApps { get; init; }

    /// <summary>winget --scope Wert: "machine" oder "user".</summary>
    public required string WingetScope { get; init; }

    /// <summary>Nur SystemAgent: Default-Profil-Autostart bereinigen.</summary>
    public bool CleanDefaultProfile { get; init; }

    /// <summary>Steuert, welches Feldpaar im Update-Manifest verwendet wird.</summary>
    public required bool IsSystemAgent { get; init; }

    /// <summary>Fallback-Datenpfad, falls appsettings.json keinen angibt.</summary>
    public required string DefaultDataDirectory { get; init; }
}

/// <summary>
/// Gemeinsamer Ablauf für beide Programme: laeuft einmal pro Aufruf
/// (kein Dauerprozess), gedrosselt auf maximal BatchSize Aktionen,
/// mit periodischem Selbst-Update-Check.
/// </summary>
public static class AgentRunner
{
    public static async Task RunAsync(AgentOptions options)
    {
        var baseDir = AppContext.BaseDirectory;
        var settings = ConfigLoader.LoadSettings(baseDir);
        var dataDir = string.IsNullOrWhiteSpace(settings.DataDirectory)
            ? options.DefaultDataDirectory
            : settings.DataDirectory;

        var log = new FileLogger(dataDir, $"{options.AgentName}.log");
        var stateStore = new StateStore(dataDir, $"{options.AgentName}.state.json");
        var http = new HttpClient();
        var updater = new SelfUpdater(http, log, options.IsSystemAgent);
        var currentVersion = options.Assembly.GetName().Version ?? new Version(1, 0, 0);

        updater.CleanupPreviousUpdate();
        log.Info($"{options.AgentName} gestartet (Version {currentVersion}).");

        var state = stateStore.Load();

        // 1) Update-Check (nur alle X Stunden)
        if (!string.IsNullOrWhiteSpace(settings.UpdateManifestUrl) &&
            (state.LastUpdateCheckUtc == null ||
             DateTime.UtcNow - state.LastUpdateCheckUtc > TimeSpan.FromHours(settings.UpdateCheckIntervalHours)))
        {
            await updater.CheckAndApplyUpdateAsync(settings.UpdateManifestUrl, currentVersion);
            state.LastUpdateCheckUtc = DateTime.UtcNow;
            stateStore.Save(state);
            // Kein Neustart noetig: der naechste Task-Trigger nutzt automatisch
            // die bereits per Rename ausgetauschte EXE.
        }

        // 2) Blacklist laden, ggf. Default-Profil bereinigen, unerwuenschte Apps entfernen
        if (!string.IsNullOrWhiteSpace(settings.BlacklistSource))
        {
            var blacklist = await ConfigLoader.LoadBlacklistAsync(settings.BlacklistSource, http);

            if (blacklist.Count == 0)
            {
                log.Warn("Blacklist leer/nicht erreichbar - ueberspringe Deinstallations-Teil.");
            }
            else
            {
                if (options.CleanDefaultProfile)
                    DefaultProfileCleaner.CleanAutostart(blacklist.Select(b => b.AppName), log);

                var blacklistNames = blacklist.Select(b => b.AppName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var toRemove = options.GetInstalledApps()
                    .Where(a => blacklistNames.Contains(a.DisplayName))
                    .Where(a => !state.ProcessedUninstalls.Contains(a.DisplayName))
                    .Take(settings.BatchSize)
                    .ToList();

                log.Info($"{toRemove.Count} Deinstallation(en) in diesem Lauf (Batch-Limit: {settings.BatchSize}).");

                for (int i = 0; i < toRemove.Count; i++)
                {
                    var app = toRemove[i];
                    var entry = blacklist.First(b => string.Equals(b.AppName, app.DisplayName, StringComparison.OrdinalIgnoreCase));
                    var ok = Uninstaller.TryUninstall(app, entry, log);
                    if (ok) state.ProcessedUninstalls.Add(app.DisplayName);

                    if (i < toRemove.Count - 1)
                        await Task.Delay(TimeSpan.FromSeconds(settings.DelayBetweenActionsSeconds));
                }
            }
        }

        // 3) Gewuenschte Software installieren (winget, passender Scope)
        if (!string.IsNullOrWhiteSpace(settings.WishlistSource) && WingetInstaller.IsWingetAvailable())
        {
            var wishlist = await ConfigLoader.LoadWishlistAsync(settings.WishlistSource, http);
            var toInstall = wishlist
                .Where(w => w.Scope == options.WingetScope)
                .Where(w => !state.ProcessedInstalls.Contains(w.PackageId))
                .Take(settings.BatchSize)
                .ToList();

            for (int i = 0; i < toInstall.Count; i++)
            {
                var ok = WingetInstaller.TryInstall(toInstall[i], log);
                if (ok) state.ProcessedInstalls.Add(toInstall[i].PackageId);

                if (i < toInstall.Count - 1)
                    await Task.Delay(TimeSpan.FromSeconds(settings.DelayBetweenActionsSeconds));
            }
        }

        stateStore.Save(state);
        log.Info($"{options.AgentName}-Lauf beendet.");
    }
}
