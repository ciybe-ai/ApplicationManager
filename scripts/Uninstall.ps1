#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Entfernt ApplicationManager wieder vollstaendig von diesem PC
    (geplante Aufgaben, installierte Dateien, Datenordner).
#>

[CmdletBinding()]
param(
    [string]$SystemInstallDir = "C:\Program Files\ApplicationManager",
    [string]$UserInstallDir   = "$env:LocalAppData\ApplicationManager",
    [switch]$KeepLogs
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host ">> Entferne geplante Aufgaben" -ForegroundColor Cyan
schtasks /Delete /TN "ApplicationManager-SystemAgent" /F | Out-Null
schtasks /Delete /TN "ApplicationManager-UserAgent" /F | Out-Null

Write-Host ">> Entferne installierte Dateien" -ForegroundColor Cyan
Remove-Item -Path $SystemInstallDir -Recurse -Force
Remove-Item -Path $UserInstallDir -Recurse -Force

if (-not $KeepLogs) {
    Write-Host ">> Entferne Datenordner (Logs/State)" -ForegroundColor Cyan
    Remove-Item -Path "C:\ProgramData\ApplicationManager" -Recurse -Force
    Remove-Item -Path "$env:LocalAppData\ApplicationManager" -Recurse -Force
}

Write-Host "Deinstallation abgeschlossen." -ForegroundColor Green
