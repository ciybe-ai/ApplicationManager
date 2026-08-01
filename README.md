# ApplicationManager – .NET-Basisprogramm (Task-basiert)

Zwei eigenständige, schlanke Programme, beide als **geplante Aufgabe mit
Trigger "Bei Anmeldung"** – dadurch laufen beide grundsätzlich **nur, wenn
tatsächlich ein Nutzer angemeldet ist/sich anmeldet**, nie im Leerlauf ohne
jede Sitzung. Kein Windows-Dienst mehr im Einsatz.

| Komponente | Läuft als | Installationsort | Datenordner |
|---|---|---|---|
| `ApplicationManager.SystemAgent` | SYSTEM (Task, "Bei Anmeldung") | `C:\Program Files\ApplicationManager\` | `C:\ProgramData\ApplicationManager\` |
| `ApplicationManager.UserAgent` | angemeldeter Nutzer (Task, "Bei Anmeldung") | `%LocalAppData%\ApplicationManager\` | `%LocalAppData%\ApplicationManager\` |

**SystemAgent**: HKLM (systemweite Uninstalls), Default-Profil-Bereinigung
(damit künftige Erstanmeldungen nichts erben), winget `--scope machine`.

**UserAgent**: HKCU des gerade angemeldeten Nutzers (kein Hive-Laden nötig,
da er direkt in dessen Sitzung läuft), winget `--scope user`.

Beide Programme:
- laufen **einmal pro Trigger** und beenden sich danach (kein Dauerprozess)
- bearbeiten pro Lauf maximal `BatchSize` Aktionen (Drosselung)
- prüfen periodisch ein Update-Manifest und tauschen sich bei Bedarf selbst aus

## 1. Warum diese Architektur?

- **"Nur wenn angemeldet"** ist mit einem Task von Natur aus erfüllt – ein
  Dienst würde auch ohne jede Sitzung laufen, das hättest du künstlich
  einschränken müssen.
- **Kein Fremd-Hive-Laden mehr nötig**: Jeder Nutzer triggert bei seinem
  eigenen Login automatisch seinen eigenen UserAgent, der direkt in seinem
  HKCU arbeitet. Nutzer, die sich nie anmelden, brauchen auch keine
  Bereinigung – deckt sich mit "läuft nur bei Anmeldung".
- **Getrennte Installationsorte** entsprechen der Windows-Konvention:
  maschinenweite Software gehört nach `Program Files` (nur von Admins
  änderbar), rein nutzerbezogene Ablagen nach `%LocalAppData%` (schreibbar
  durch den jeweiligen Nutzer selbst, kein Adminrecht nötig).
- **Update ohne Lock-Risiko**: Da beide Programme kurzlebig sind (starten,
  arbeiten, beenden sich), ist beim Selbst-Update-Rename nie eine
  dauerhaft offene Datei im Weg – anders als bei einem lange laufenden
  Dienstprozess.
- **Gemeinsamer Code**: Der komplette Ablauf (Update-Check, Blacklist/
  Wishlist laden, gedrosseltes Abarbeiten, State speichern) lebt einmalig
  in `Core/Agent/AgentRunner.cs`. Beide `Program.cs`-Dateien sind nur noch
  ca. 15 Zeilen Konfiguration (`AgentOptions`) – der einzige Unterschied
  ist die Registry-Quelle (HKLM vs. HKCU), der winget-Scope, ob das
  Default-Profil bereinigt wird, und der Datenpfad. Wer künftig Logik
  ändert (z. B. Retry-Verhalten, neue Aktionstypen), tut das an genau
  einer Stelle für beide Programme.

## 2. Voraussetzungen zum Bauen

- .NET 8 SDK (nur auf der Build-Maschine nötig – die Ziel-PCs brauchen
  dank `--self-contained` kein .NET installiert)

```powershell
cd ApplicationManager
dotnet restore
dotnet publish src/SystemAgent -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish src/UserAgent   -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Ergebnis jeweils unter `src/<Projekt>/bin/Release/net8.0-windows/win-x64/publish/`.

## 3. Konfiguration

`appsettings.json` liegt neben der jeweiligen EXE:

```json
{
  "BlacklistSource": "https://.../app-blacklist.json",
  "WishlistSource": "https://.../app-wishlist.json",
  "UpdateManifestUrl": "https://.../update-manifest.json",
  "BatchSize": 1,
  "DelayBetweenActionsSeconds": 30,
  "UpdateCheckIntervalHours": 12,
  "DataDirectory": null
}
```

`DataDirectory: null` (Standard) lässt jedes Programm seinen passenden
Standardpfad selbst wählen (siehe Tabelle oben). Nur setzen, wenn ihr
explizit einen anderen Ort wollt.

`BlacklistSource`/`WishlistSource` akzeptieren `https://`-URLs oder UNC-Pfade
(`\\domain.local\netlogon\...`) – funktioniert also domänengebunden UND
domänenunabhängig übers Internet mit derselben Konfigurationsstruktur.

Für maschinen-/nutzerspezifische Overrides ohne die zentrale Datei
anzufassen: `appsettings.Local.json` daneben legen.

Beispieldateien unter `examples/`:
- `app-blacklist.example.json`
- `app-wishlist.example.json`
- `update-manifest.example.json`

Die tatsächlich **live genutzten** Listen liegen unter `config/` im Repo
(`app-blacklist.json`, `app-wishlist.json`) und werden von den Programmen
direkt über `raw.githubusercontent.com` geladen – siehe Abschnitt 6.2.

## 4. Deployment: SystemAgent

Dateien nach `C:\Program Files\ApplicationManager\` bringen (z. B. per
GPO-Computer-Startskript, das die publish-Dateien kopiert, oder über eure
bestehende Softwareverteilung).

GPO-Scheduled-Task (Computerkonfiguration):

```
Computerkonfiguration
 → Einstellungen (Preferences)
   → Systemsteuerungseinstellungen
     → Geplante Aufgaben → Neu → Geplante Aufgabe

Allgemein:
  Ausführen als: NT AUTHORITY\SYSTEM
  ✅ Ausgeblendet

Trigger:
  Bei Anmeldung (eines beliebigen Nutzers)
  + Wiederholend alle 15 Min für z. B. 8 Std (deckt lange Sitzungen ab)

Aktionen:
  Programm: C:\Program Files\ApplicationManager\ApplicationManager.SystemAgent.exe
```

## 5. Deployment: UserAgent

Dateien nach `%LocalAppData%\ApplicationManager\` **des jeweiligen Nutzers**
bringen. Das geht sauber per GPO:

```
Benutzerkonfiguration
 → Einstellungen (Preferences)
   → Windows-Einstellungen
     → Dateien → Neu → Datei
       Quelle: \\domain.local\netlogon\it\releases\ApplicationManager.UserAgent.exe
       Ziel:   %LocalAppData%\ApplicationManager\ApplicationManager.UserAgent.exe
     (gleiches Prinzip für appsettings.json)
```

Anschließend die geplante Aufgabe (Benutzerkonfiguration):

```
Benutzerkonfiguration
 → Einstellungen (Preferences)
   → Systemsteuerungseinstellungen
     → Geplante Aufgaben → Neu → Geplante Aufgabe

Allgemein:
  Ausführen als: (leer lassen -> läuft als der sich anmeldende Nutzer)
  ✅ Ausgeblendet

Trigger:
  Bei Anmeldung
  + Wiederholend alle 15 Min für z. B. 8 Std

Aktionen:
  Programm: %LocalAppData%\ApplicationManager\ApplicationManager.UserAgent.exe
```

Nach dem allerersten Kopieren übernimmt der eingebaute Self-Updater
künftige Aktualisierungen automatisch – die GPO-Dateiverteilung ist danach
nur noch für neue/noch nie angemeldete Nutzer relevant.

## 6. Kompletter Round-Trip: GitHub-Repo → Release-Pipeline → Installation

Dieser Abschnitt beschreibt den Weg von "Code committen" bis "läuft auf dem PC".

### 6.1 Repo einrichten (einmalig)

```powershell
cd ApplicationManager
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/ciybe-ai/ApplicationManager.git
git push -u origin main
```

Repo kann privat oder öffentlich sein – öffentliche Releases sind später
ohne Token abrufbar (einfacher für die Ziel-PCs). Bei einem privaten Repo
bräuchten Service/Installer zusätzlich ein Auth-Token, was den
Update-Mechanismus verkompliziert; für ein internes IT-Tool ist ein
öffentliches Repo meist unproblematisch, notfalls mit unauffälligem Namen.

**Platzhalter ersetzen:** In `src/SystemAgent/appsettings.json`,
`src/UserAgent/appsettings.json` und überall sonst `ciybe-ai` durch euren
echten GitHub-Org-/User-Namen ersetzen, bevor ihr committet.

### 6.2 Blacklist/Wishlist pflegen (laufender Betrieb, unabhängig von Releases)

Liegen in `config/app-blacklist.json` und `config/app-wishlist.json` im
`main`-Branch. Beide Programme laden sie live über
`raw.githubusercontent.com` – eine Änderung wird einfach committet/gepusht
und ist beim nächsten Task-Lauf aktiv, **ohne neues Release**:

```powershell
# app-blacklist.json bearbeiten, dann:
git add config/app-blacklist.json
git commit -m "Neue unerwünschte App hinzugefügt"
git push
```

### 6.3 Release-Pipeline (GitHub Actions)

`.github/workflows/release.yml` ist bereits enthalten. Sie greift bei jedem
Tag im Format `vX.Y.Z`:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Die Pipeline (läuft auf `windows-latest`):
1. Baut `SystemAgent` und `UserAgent` self-contained für win-x64
2. Berechnet SHA256 für beide EXEs
3. Erzeugt `update-manifest.json` mit der Tag-Version und den SHA256-Werten
   (die URLs darin bleiben **fest** – sie zeigen auf `.../releases/latest/download/...`,
   was GitHub automatisch auf das jeweils neueste Release umleitet)
4. Veröffentlicht ein GitHub Release mit allen Assets: beide EXEs, beide
   appsettings-Dateien, `update-manifest.json`, `Install.ps1`, `Uninstall.ps1`

Ergebnis: `https://github.com/ciybe-ai/ApplicationManager/releases/latest`
enthält immer den aktuellen Stand, ohne dass Konfiguration/Links angepasst
werden müssen.

### 6.4 Installation auf einem PC

Als Administrator in PowerShell:

```powershell
irm https://github.com/ciybe-ai/ApplicationManager/releases/latest/download/Install.ps1 -OutFile Install.ps1
.\Install.ps1 -Repo "ciybe-ai/ApplicationManager"
```

Das Skript (`scripts/Install.ps1`):
- lädt die aktuellsten EXEs + appsettings direkt von GitHub
- kopiert SystemAgent nach `C:\Program Files\ApplicationManager\`
- kopiert UserAgent nach `%LocalAppData%\ApplicationManager\` (für den
  Nutzer, der das Skript ausführt)
- richtet beide geplanten Aufgaben ein (Trigger "Bei Anmeldung",
  Wiederholung alle 15 Min, ausgeblendet)
- stößt einen ersten Testlauf an

Für die Ausrollung an **alle** Nutzer eines PCs (nicht nur den, der
installiert hat) bleibt der GPO-Weg aus Abschnitt 5 relevant – `Install.ps1`
ist gedacht für Einzelinstallation, Tests, oder als Vorlage für ein
GPO-Computerstartskript, das denselben Ablauf für jeden PC automatisiert.

Deinstallieren:
```powershell
irm https://github.com/ciybe-ai/ApplicationManager/releases/latest/download/Uninstall.ps1 -OutFile Uninstall.ps1
.\Uninstall.ps1
```

### 6.5 Der Kreis schließt sich: automatische Updates

Ab jetzt läuft alles von selbst:
1. Ihr ändert Code → neuer Tag → Pipeline baut & veröffentlicht Release
2. Beide Agents prüfen (alle `UpdateCheckIntervalHours` Std.) das feste
   Manifest unter `.../releases/latest/download/update-manifest.json`
3. Neue Version erkannt → EXE wird heruntergeladen, SHA256 geprüft, per
   Rename-Trick eingespielt
4. Beim nächsten Task-Trigger läuft automatisch die neue Version

Blacklist-Änderungen (Abschnitt 6.2) wirken sogar noch schneller, da sie
gar keinen Release-Zyklus durchlaufen müssen.

## 7. Selbst-Update: technischer Hintergrund

Beide Programme laden bei jedem Lauf (gedrosselt auf alle
`UpdateCheckIntervalHours` Stunden) `UpdateManifestUrl` (JSON). Steht dort
eine höhere Version, wird die passende EXE heruntergeladen, per SHA256
verifiziert und die aktuell laufende EXE per **Rename-Trick** ausgetauscht
(unter Windows lässt sich eine laufende EXE umbenennen, auch wenn sie nicht
gelöscht werden kann – danach wird die neue Version an ihre Stelle kopiert
und ist beim nächsten Task-Trigger aktiv).

Das Hosting läuft wie in Abschnitt 6 beschrieben komplett über GitHub
Releases – kein eigener Server nötig. Falls ihr später doch auf eigenes
HTTPS-Hosting wechseln wollt (z. B. Firmen-Website, Azure Blob Storage):
einfach `UpdateManifestUrl` in der Konfiguration anpassen, der Rest des
Mechanismus bleibt identisch.

**Sicherheitshinweis:** Der SHA256-Check verhindert nur beschädigte/
manipulierte Downloads bei Übertragungsfehlern, ersetzt aber keine echte
Code-Signatur. Für den produktiven Einsatz die EXEs zusätzlich mit einem
Firmenzertifikat signieren.

## 8. Bekannte Grenzen / nächste Schritte

- EXE-Deinstaller ohne bekannten Silent-Switch: `silentArgs` in der
  Blacklist-JSON pro App ergänzen.
- Programme, die einen Neustart zum vollständigen Entfernen brauchen,
  tauchen ggf. im nächsten Lauf erneut auf.
- Code-Signierung der EXEs für den produktiven Rollout empfehlenswert.
- Kein zentrales Reporting bisher – Logs liegen nur lokal
  (`DataDirectory\*.log`). Kann bei Bedarf um einen einfachen HTTP-Log-
  Upload erweitert werden.
- Nutzer, die sich sehr selten anmelden, werden entsprechend selten
  bearbeitet – das ist hier aber ausdrücklich gewünschtes Verhalten.
- `Install.ps1` installiert den UserAgent-Teil nur für den ausführenden
  Nutzer. Für Mehrbenutzer-PCs ohne GPO bräuchte es entweder mehrfaches
  Ausführen pro Nutzer oder eine Erweiterung, die alle lokalen Profile
  durchläuft.

## 9. Vor dem Rollout testen

1. Auf 1–2 Testgeräten/-nutzern installieren – entweder manuell
   (Abschnitt 4/5) oder per `Install.ps1` (Abschnitt 6.4).
2. Blacklist/Wishlist zunächst klein halten, Logs prüfen
   (`C:\ProgramData\ApplicationManager\systemagent.log` bzw.
   `%LocalAppData%\ApplicationManager\useragent.log`).
3. Update-Mechanismus bewusst testen: neuen Tag pushen, prüfen, dass sich
   beide Programme beim nächsten Task-Lauf korrekt selbst austauschen.
4. Erst danach breiter ausrollen (GPO für ganze OUs, oder `Install.ps1`
   als Grundlage für ein Softwareverteilungs-Paket).
