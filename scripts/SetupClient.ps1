param(
    [ValidateSet('user', 'system')]
    [string]$Scope = 'user',
    [string]$BootstrapPassword,
    [switch]$SkipPrompt
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($BootstrapPassword) -and -not $SkipPrompt) {
    $BootstrapPassword = Read-Host "Bitte Initial-Passwort für Scope '$Scope' eingeben"
}

if ([string]::IsNullOrWhiteSpace($BootstrapPassword)) {
    throw 'Initial-Passwort darf nicht leer sein.'
}

$baseDirectory = if ($Scope -eq 'system') {
    [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
} else {
    [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
}

$dir = Join-Path $baseDirectory 'ApplicationManager'
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$secretFile = Join-Path $dir 'secrets.json'
$dictionary = @{}

if (Test-Path $secretFile) {
    try {
        $dictionary = Get-Content -Path $secretFile -Raw | ConvertFrom-Json -AsHashtable
    } catch {
        $dictionary = @{}
    }
}

$protectedBytes = [System.Security.Cryptography.ProtectedData]::Protect(
    [System.Text.Encoding]::UTF8.GetBytes($BootstrapPassword),
    $null,
    if ($Scope -eq 'system') { [System.Security.Cryptography.DataProtectionScope]::LocalMachine } else { [System.Security.Cryptography.DataProtectionScope]::CurrentUser }
)

$dictionary['BootstrapPassword'] = [Convert]::ToBase64String($protectedBytes)
$dictionary['SetupComplete'] = $true

$dictionary | ConvertTo-Json -Depth 3 | Set-Content -Path $secretFile -Encoding utf8

Write-Host "Setup für Scope '$Scope' abgeschlossen."
Write-Host "Secret-Datei: $secretFile"
