#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Ein-Klick-Installer fuer ApplicationManager (SystemAgent + UserAgent).
    Laedt die jeweils neuesten Release-Assets direkt von GitHub, kopiert sie
    an die richtigen Orte und richtet die noetigen geplanten Aufgaben ein.

.HINWEIS
    - Muss als Administrator ausgefuehrt werden (Program Files + SYSTEM-Task).
    - Installiert den UserAgent-Teil fuer den Nutzer, der dieses Skript
      ausfuehrt (bzw. fuer $env:USERNAME). Fuer die Ausrollung an ALLE
      Nutzer eines PCs siehe README Abschnitt 5 (GPO-Dateiverteilung) -
      dieses Skript ist gedacht fuer Einzelinstallation/Tests bzw. als
      Vorlage fuer eine GPO-Startskript-Version.

.PARAMETER Repo
    GitHub-Repo im Format "orgname/reponame".

.BEISPIEL
    .\Install.ps1 -Repo "meine-firma/ApplicationManager"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [string]$SystemInstallDir = "C:\Program Files\ApplicationManager",
    [string]$UserInstallDir   = "$env:LocalAppData\ApplicationManager"
)

$ErrorActionPreference = "Stop"
$baseUrl = "https://github.com/$Repo/releases/latest/download"

function Write-Step($msg) {
    Write-Host ">> $msg" -ForegroundColor Cyan
}

function Download($url, $destination) {
    Write-Host "   Lade $url" -ForegroundColor DarkGray
    Invoke-WebRequest -Uri $url -OutFile $destination -UseBasicParsing
}

# ---------------------------------------------------------------------------
# 0) .NET 8 Runtime sicherstellen (framework-dependent EXEs brauchen sie)
# ---------------------------------------------------------------------------
function Test-DotNet8RuntimeInstalled {
    try {
        $runtimes = & dotnet --list-runtimes 2>$null
        return ($runtimes -match '^Microsoft\.NETCore\.App 8\.')
    } catch {
        return $false
    }
}

Write-Step "Pruefe .NET 8 Runtime"
if (Test-DotNet8RuntimeInstalled) {
    Write-Host "   Bereits installiert." -ForegroundColor DarkGray
}
else {
    Write-Host "   Nicht gefunden - installiere per winget ..." -ForegroundColor Yellow

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "winget ist auf diesem PC nicht verfuegbar. Bitte .NET 8 Runtime manuell " +
              "installieren (https://dotnet.microsoft.com/download/dotnet/8.0, 'Runtime' " +
              "unter 'Run console apps', x64) und Install.ps1 danach erneut ausfuehren."
    }

    # Microsoft.DotNet.Runtime.8 = normale .NET-Runtime (ausreichend, da wir
    # kein WinForms/WPF nutzen). NICHT DesktopRuntime.8 - waere groesser als noetig.
    winget install --id Microsoft.DotNet.Runtime.8 -e --silent `
        --accept-package-agreements --accept-source-agreements

    if (-not (Test-DotNet8RuntimeInstalled)) {
        throw ".NET 8 Runtime konnte nicht automatisch installiert werden. " +
              "Bitte manuell installieren und Install.ps1 erneut ausfuehren."
    }
    Write-Host "   Installiert." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# 1) SystemAgent installieren (Program Files + ProgramData)
# ---------------------------------------------------------------------------
Write-Step "Installiere SystemAgent nach $SystemInstallDir"
New-Item -ItemType Directory -Force -Path $SystemInstallDir | Out-Null

Download "$baseUrl/ApplicationManager.SystemAgent.exe" "$SystemInstallDir\ApplicationManager.SystemAgent.exe"
Download "$baseUrl/SystemAgent.appsettings.json"       "$SystemInstallDir\appsettings.json"

# ---------------------------------------------------------------------------
# 2) UserAgent installieren (nutzerlokal unter %LocalAppData%)
# ---------------------------------------------------------------------------
Write-Step "Installiere UserAgent nach $UserInstallDir (Nutzer: $env:USERNAME)"
New-Item -ItemType Directory -Force -Path $UserInstallDir | Out-Null

Download "$baseUrl/ApplicationManager.UserAgent.exe" "$UserInstallDir\ApplicationManager.UserAgent.exe"
Download "$baseUrl/UserAgent.appsettings.json"       "$UserInstallDir\appsettings.json"

# ---------------------------------------------------------------------------
# 3) Geplante Aufgabe: SystemAgent (SYSTEM, "Bei Anmeldung")
# ---------------------------------------------------------------------------
Write-Step "Richte geplante Aufgabe fuer SystemAgent ein"

$sysTaskName = "ApplicationManager-SystemAgent"
Get-ScheduledTask -TaskName $sysTaskName -ErrorAction SilentlyContinue |
    Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue

$sysAction  = New-ScheduledTaskAction -Execute "$SystemInstallDir\ApplicationManager.SystemAgent.exe"
$sysTrigger = New-ScheduledTaskTrigger -AtLogOn
$sysTrigger.Repetition = (New-ScheduledTaskTrigger -Once -At (Get-Date) `
    -RepetitionInterval (New-TimeSpan -Minutes 15) `
    -RepetitionDuration (New-TimeSpan -Hours 8)).Repetition
$sysPrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$sysSettings  = New-ScheduledTaskSettingsSet -Hidden -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName $sysTaskName -Action $sysAction -Trigger $sysTrigger `
    -Principal $sysPrincipal -Settings $sysSettings -Force | Out-Null

# ---------------------------------------------------------------------------
# 4) Geplante Aufgabe: UserAgent (aktueller Nutzer, "Bei Anmeldung")
# ---------------------------------------------------------------------------
Write-Step "Richte geplante Aufgabe fuer UserAgent ein"

$userTaskName = "ApplicationManager-UserAgent"
Get-ScheduledTask -TaskName $userTaskName -ErrorAction SilentlyContinue |
    Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue

$userAction  = New-ScheduledTaskAction -Execute "$UserInstallDir\ApplicationManager.UserAgent.exe"
$userTrigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$userTrigger.Repetition = (New-ScheduledTaskTrigger -Once -At (Get-Date) `
    -RepetitionInterval (New-TimeSpan -Minutes 15) `
    -RepetitionDuration (New-TimeSpan -Hours 8)).Repetition
$userSettings = New-ScheduledTaskSettingsSet -Hidden -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName $userTaskName -Action $userAction -Trigger $userTrigger `
    -Settings $userSettings -Force | Out-Null
# Kein -Principal angegeben -> laeuft automatisch im Kontext des Nutzers,
# der beim Trigger "Bei Anmeldung" angemeldet ist.

# ---------------------------------------------------------------------------
# 5) Ersten Lauf sofort anstossen (optional, fuer sofortiges Feedback)
# ---------------------------------------------------------------------------
Write-Step "Starte ersten Testlauf beider Agents"
Start-ScheduledTask -TaskName $sysTaskName
Start-ScheduledTask -TaskName $userTaskName

Write-Host ""
Write-Host "Installation abgeschlossen." -ForegroundColor Green
Write-Host "Logs pruefen unter:"
Write-Host "  C:\ProgramData\ApplicationManager\systemagent.log"
Write-Host "  $env:LocalAppData\ApplicationManager\useragent.log"
