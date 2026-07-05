# Meta Search and Control Center - Setup
# Als Administrator ausfuehren: Rechtsklick -> "Mit PowerShell ausfuehren"

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

$appName = "Meta Search and Control Center"
$appExe  = "MSCC.exe"
$appIcon = "app-icon.ico"
$publisher = "Dennis Michael Heine"
$version  = "1.0.0"

$installDir = Join-Path $env:ProgramFiles "MSCC"
$sourceDir  = Join-Path $PSScriptRoot "bin\Debug\net10.0-windows"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  $appName - Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Lizenz anzeigen
$licenseFile = Join-Path $PSScriptRoot "LICENSE.txt"
if (Test-Path $licenseFile) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Lizenzvereinbarung (Apache 2.0)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Get-Content $licenseFile | ForEach-Object { Write-Host $_ }
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    $accept = Read-Host "Akzeptieren Sie die Lizenz? (j/n)"
    if ($accept -notmatch '^[jJyY]') {
        Write-Host "Installation abgebrochen." -ForegroundColor Red
        pause
        exit 1
    }
    Write-Host ""
}

# Pruefe, ob Quellverzeichnis existiert
if (-not (Test-Path $sourceDir)) {
    Write-Host "FEHLER: Quellverzeichnis nicht gefunden:" -ForegroundColor Red
    Write-Host "  $sourceDir" -ForegroundColor Red
    Write-Host ""
    Write-Host "Bitte fuhren Sie dieses Skript aus dem Projekt-Root-Verzeichnis aus." -ForegroundColor Yellow
    pause
    exit 1
}

# Pruefe, ob .NET Desktop Runtime installiert ist
Write-Host "[1/5] Pruefe .NET Desktop Runtime..." -ForegroundColor White
try {
    $dotnetVersion = dotnet --list-runtimes 2>&1 | Select-String "Microsoft.WindowsDesktop.App 10\."
    if (-not $dotnetVersion) {
        Write-Host "  WARNUNG: .NET 10 Desktop Runtime wurde nicht gefunden." -ForegroundColor Yellow
        Write-Host "  Bitte installieren Sie diese von: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
    } else {
        Write-Host "  OK: $dotnetVersion" -ForegroundColor Green
    }
} catch {
    Write-Host "  WARNUNG: Konnte .NET Version nicht pruefen." -ForegroundColor Yellow
}

# Beende laufende Instanz
Write-Host ""
Write-Host "[2/5] Beende laufende Instanzen..." -ForegroundColor White
$running = Get-Process -Name "MSCC" -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Write-Host "  MSCC wurde beendet." -ForegroundColor Green
} else {
    Write-Host "  Keine laufende Instanz gefunden." -ForegroundColor Green
}

# Installationsverzeichnis erstellen
Write-Host ""
Write-Host "[3/5] Kopiere Dateien nach $installDir..." -ForegroundColor White

if (Test-Path $installDir) {
    Write-Host "  Entferne vorherige Installation..." -ForegroundColor Yellow
    Remove-Item -LiteralPath $installDir -Recurse -Force
}
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

# Dateien kopieren
$fileCount = 0
Get-ChildItem -LiteralPath $sourceDir -Recurse | ForEach-Object {
    $target = Join-Path $installDir ($_.FullName.Substring($sourceDir.Length + 1))
    $targetDir = Split-Path $target -Parent
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }
    if (-not $_.PSIsContainer) {
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
        $fileCount++
    }
}
Write-Host "  $fileCount Dateien kopiert." -ForegroundColor Green

# Startmenue-Verknuepfung
Write-Host ""
Write-Host "[4/5] Erstelle Verknuepfungen..." -ForegroundColor White

$startMenu = [Environment]::GetFolderPath("Programs")
$startMenuDir = Join-Path $startMenu $appName
if (-not (Test-Path $startMenuDir)) {
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
}

$wshShell = New-Object -ComObject WScript.Shell

# Startmenue
$shortcut = $wshShell.CreateShortcut((Join-Path $startMenuDir "$appName.lnk"))
$shortcut.TargetPath = Join-Path $installDir $appExe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = Join-Path $installDir $appIcon
$shortcut.Description = $appName
$shortcut.Save()
Write-Host "  Startmenue-Verknuepfung erstellt." -ForegroundColor Green

# Desktop
try {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $desktopShortcut = $wshShell.CreateShortcut((Join-Path $desktop "$appName.lnk"))
    $desktopShortcut.TargetPath = Join-Path $installDir $appExe
    $desktopShortcut.WorkingDirectory = $installDir
    $desktopShortcut.IconLocation = Join-Path $installDir $appIcon
    $desktopShortcut.Description = $appName
    $desktopShortcut.Save()
    Write-Host "  Desktop-Verknuepfung erstellt." -ForegroundColor Green
} catch {
    Write-Host "  Desktop-Verknuepfung wurde ausgelassen." -ForegroundColor Yellow
}

# Registry-Eintrag fuer "Programme und Funktionen"
Write-Host ""
Write-Host "[5/5] Registriere in Windows..." -ForegroundColor White
$uninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MSCC"
if (-not (Test-Path $uninstallKey)) {
    New-Item -Path $uninstallKey -Force | Out-Null
}
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value $appName -Type String
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value (Join-Path $installDir $appExe) -Type String
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value $version -Type String
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value $publisher -Type String
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $installDir -Type String
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`"" -Type String
Write-Host "  In Programme und Funktionen registriert." -ForegroundColor Green

# Uninstaller-Script in den Installationsordner kopieren
$uninstallScript = Join-Path $installDir "uninstall.ps1"
@'
# Meta Search and Control Center - Uninstall
#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"
$appName = "Meta Search and Control Center"
$installDir = Join-Path $env:ProgramFiles "MSCC"

Write-Host "Deinstalliere $appName..." -ForegroundColor Yellow

$running = Get-Process -Name "MSCC" -ErrorAction SilentlyContinue
if ($running) { $running | Stop-Process -Force }

$startMenu = Join-Path ([Environment]::GetFolderPath("Programs")) $appName
if (Test-Path $startMenu) { Remove-Item -LiteralPath $startMenu -Recurse -Force }

$desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "$appName.lnk"
if (Test-Path $desktop) { Remove-Item -LiteralPath $desktop -Force }

Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MSCC" -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path $installDir) { Remove-Item -LiteralPath $installDir -Recurse -Force }

Write-Host "$appName wurde deinstalliert." -ForegroundColor Green
pause
'@ | Set-Content -Path $uninstallScript -Encoding UTF8

# Lizenzdatei kopieren
if (Test-Path $licenseFile) {
    Copy-Item -LiteralPath $licenseFile -Destination $installDir -Force
}

# Cleanup COM
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($wshShell) | Out-Null
Remove-Variable wshShell

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Installation abgeschlossen!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Installiert nach: $installDir" -ForegroundColor White
Write-Host "  Starten ueber:   Startmenue -> $appName" -ForegroundColor White
Write-Host ""
pause
