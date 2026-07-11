# install_praat.ps1
# Automates the downloading and extraction of Praat for Windows

$ErrorActionPreference = "Stop"

$praatDir = Join-Path $PSScriptRoot "praat"
$zipPath = Join-Path $env:TEMP "praat_download.zip"
$downloadUrl = "https://www.fon.hum.uva.nl/praat/praat6630_win-x64v3.zip"

Write-Host "Creating target folder: $praatDir"
if (-not (Test-Path $praatDir)) {
    New-Item -ItemType Directory -Path $praatDir | Out-Null
}

Write-Host "Downloading Praat from $downloadUrl ..."
Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath

Write-Host "Extracting Praat to $praatDir ..."
Expand-Archive -Path $zipPath -DestinationPath $praatDir -Force

Write-Host "Cleaning up temporary files..."
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath
}

$exePath = Join-Path $praatDir "praat.exe"
if (Test-Path $exePath) {
    Write-Host "Praat installed successfully at: $exePath"
    Write-Host "Run .\praat\praat.exe to open the GUI or execute it from the CLI."
} else {
    Write-Error "Praat executable was not found after extraction."
}
