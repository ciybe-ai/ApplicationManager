# ApplicationManager – Projektkontext für Claude Code

Dieses Dokument fasst zusammen, was bereits entschieden und umgesetzt wurde,
damit du (Claude Code) nahtlos weiterarbeiten kannst, ohne dass der Nutzer
alles nochmal erklären muss.

## Was das Projekt ist

Internes IT-Tool für einen Windows-Domänen-Admin (Rainer Batz). Zwei
Windows-Programme, die auf Firmen-PCs laufen:

- **unerwünschte Software automatisch deinstallieren** (Blacklist-gesteuert)
- **gewünschte Standardsoftware automatisch installieren** (winget-gesteuert)
- **sich selbst automatisch aktualisieren** (ohne Domänenbezug, übers Internet)
- künftig: **Konfiguration von Programmen** (noch nicht implementiert, daher
  bewusst "ApplicationManager" genannt statt z. B. "AppCleanupService" –
  der Name war ursprünglich enger gefasst und wurde extra umbenannt, um
  Raum für mehr als nur Cleanup zu lassen)

Repo: `https://github.com/ciybe-ai/ApplicationManager` (öffentlich)

## Architektur (WICHTIG, nicht ohne Grund ändern)

Zwei separate .NET-8-Programme + eine gemeinsame Core-Bibliothek:

```
src/Core/          - komplette Ablauflogik (AgentRunner), Registry-Zugriff,
                      Uninstall/winget-Aktionen, Self-Updater, State-Speicherung
src/SystemAgent/    - läuft als SYSTEM, HKLM + Default-Profil-Bereinigung,
                      winget --scope machine
src/UserAgent/      - läuft im Kontext des angemeldeten Nutzers, HKCU,
                      winget --scope user
```

**Warum zwei Programme statt eins:** unterschiedliche Registry-Ebenen
(HKLM vs. HKCU) und Installationsorte (Program Files vs. %LocalAppData%).

**Warum Task statt Windows-Dienst:** Der Nutzer wollte explizit, dass beide
Programme NUR laufen, wenn tatsächlich ein Nutzer angemeldet ist/sich
anmeldet – ein Dienst würde auch ohne jede Sitzung laufen. Beide sind daher
als geplante Aufgabe mit Trigger "Bei Anmeldung" eingerichtet (siehe
`scripts/Install.ps1`), laufen **einmal pro Trigger** und beenden sich
danach (kein Dauerprozess). Wiederholung übernimmt der Task-Trigger
(alle 15 Min für 8 Std), nicht eine Sleep-Schleife im Programm.

**Warum kein Fremd-Hive-Laden mehr:** Frühere Version lud HKCU-Hives nicht
angemeldeter Nutzer manuell (reg load/unload). Das wurde entfernt, weil
jeder Nutzer durch sein eigenes "Bei Anmeldung"-Trigger automatisch seinen
eigenen UserAgent startet – deckt sich mit der Anforderung.

**Warum gemeinsamer AgentRunner:** SystemAgent und UserAgent unterscheiden
sich nur in 4 Punkten (Registry-Quelle, winget-Scope, Default-Profil-
Bereinigung ja/nein, Datenpfad). Die komplette Ablauflogik lebt daher
EINMAL in `Core/Agent/AgentRunner.cs`; beide `Program.cs` sind nur noch
~15 Zeilen `AgentOptions`-Konfiguration. Änderungen an der Kernlogik
IMMER dort vornehmen, nie in den Program.cs-Dateien duplizieren.

## Deployment & Distribution

- **Framework-dependent** (NICHT self-contained) – EXEs sind ~192 KB statt
  ~64 MB. Voraussetzung: .NET 8 Runtime auf dem Ziel-PC. `Install.ps1`
  prüft das automatisch und installiert sie bei Bedarf per winget
  (`Microsoft.DotNet.Runtime.8` – NICHT DesktopRuntime, da kein WinForms/WPF
  genutzt wird).
- **CI/CD**: `.github/workflows/release.yml` baut bei jedem Tag `vX.Y.Z`
  beide EXEs, berechnet SHA256, erzeugt `update-manifest.json` und
  veröffentlicht ein GitHub Release. Die URLs im Manifest sind FEST
  (`.../releases/latest/download/...`), zeigen also immer automatisch aufs
  neueste Release.
- **Selbst-Update**: Beide Programme prüfen periodisch das Manifest und
  tauschen sich per Rename-Trick selbst aus (`Core/Update/SelfUpdater.cs`).
- **Installation auf einem PC**: `scripts/Install.ps1` (lädt neuestes
  Release, installiert Runtime falls nötig, richtet beide Scheduled Tasks
  ein). `scripts/Uninstall.ps1` macht das Gegenteil.
- **Blacklist/Wishlist** (`config/app-blacklist.json`, `config/app-wishlist.json`)
  liegen aktuell im selben (öffentlichen) Repo, werden per
  `raw.githubusercontent.com` vom `main`-Branch geladen – Änderungen wirken
  sofort beim nächsten Task-Lauf, OHNE neuen Release/Tag.

## Bereits gelöste Stolpersteine (nicht nochmal reinlaufen)

1. `schtasks /Delete ... 2>$null` unter `$ErrorActionPreference = "Stop"`
   wirft trotzdem einen terminierenden Fehler, wenn die Aufgabe noch nicht
   existiert (Erstinstallation). Fix: `Get-ScheduledTask -ErrorAction
   SilentlyContinue | Unregister-ScheduledTask` statt `schtasks.exe`
   verwenden (in `Install.ps1`/`Uninstall.ps1` bereits umgesetzt).
2. `EnableCompressionInSingleFile` funktioniert NUR bei self-contained
   Publishes. Bei framework-dependent (aktueller Stand) darf das Property
   nicht gesetzt sein – Build bricht sonst mit einem klaren Fehler ab.
3. Git auf Windows meldet beim ersten `git add .` LF→CRLF-Warnungen – das
   ist normal (`core.autocrlf`) und unkritisch, kein Fix nötig.

## Aktueller Stand (Stand: 2026-08-02)

- ✅ Grundarchitektur und Ablauf sind umgesetzt: SystemAgent/UserAgent mit gemeinsamer Core-Logik
- ✅ GitHub-Token-Unterstützung für private Config-Sources ist implementiert
- ✅ `appsettings.Local.json` und `APPLICATIONMANAGER_GITHUB_TOKEN` werden als sichere Override-Mechanismen unterstützt
- ✅ Projektkontext ist auf Deutsch dokumentiert und README/CLAUDE werden parallel gepflegt
- ⚠️ Die eigentliche Produktiv-Validierung auf mehreren PCs steht noch aus
- ⚠️ Blacklist/Wishlist enthalten noch nicht die finalen echten Einträge

## Nächste Schritte (in sinnvoller Reihenfolge)

1. **Privates Config-Repo einrichten.** Die Konfiguration soll in einem separaten, privaten Repository liegen, während das Code-Repo öffentlich bleiben kann.
   - neues Repo wie `ApplicationManager-Config` anlegen
   - `config/` aus dem privaten Repo laden statt aus dem öffentlichen Repository
   - PAT-Mechanismus in `ConfigLoader` einsetzen und sicher auf den Ziel-PCs hinterlegen
2. **Blacklist mit echten Einträgen füllen** statt Platzhaltern.
3. **Pilot-Rollout** auf 2–3 Test-PCs mit dem Install-Skript und echten Logs.
4. **Breiter Rollout** über GPO oder manuelle Verteilung, wenn der Pilot sauber läuft.
5. Perspektivisch: Code-Signierung, zentrales Log-Reporting, erweiterte Konfigurations-Verteilung.

## Arbeits- und Entwicklungs-Konventionen

- Änderungen an der Kernlogik immer in `src/Core/...` vornehmen, nicht in den Program.cs-Dateien duplizieren.
- Kommentare und Doku-Strings bleiben auf Deutsch.
- `README.md` ist die primäre Nutzerdokumentation; `CLAUDE.md` dient als Arbeitskontext für Agenten und muss kurz und aktuell gehalten werden.
- Beim Arbeiten mit Git: kleine, nachvollziehbare Commits je sinnvoller Arbeitsblock; keine großen "WIP"-Commits ohne klare Bedeutung.
- Remote-Operations wie Push/Repo-Erstellung werden nur mit ausdrücklicher Freigabe bzw. nach geplanter Aktivität durchgeführt.

## Konventionen

- Kommentare und Doku-Strings im Code sind auf Deutsch (durchgängig so
  gehalten, bitte beibehalten).
- README.md ist die primäre Nutzerdokumentation, auf Deutsch, mit
  nummerierten Abschnitten – bei strukturellen Änderungen bitte dort
  nachziehen, nicht nur im Code kommentieren.
- Versionierung per Git-Tag `vX.Y.Z`, jeder Tag löst automatisch einen
  Release-Build aus.
