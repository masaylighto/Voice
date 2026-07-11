# run_praat_analysis.ps1
# Wrapper script to execute Praat voice analysis
# Usage: .\run_praat_analysis.ps1 -InputFile "path_to_audio.wav"

param (
    [Parameter(Mandatory=$true)]
    [string]$InputFile
)

$ErrorActionPreference = "Stop"

# Absolute path resolutions
$inputPath = Resolve-Path $InputFile
$praatExe = Join-Path $PSScriptRoot "praat\praat.exe"
$praatScript = Join-Path $PSScriptRoot "praat\analyze_voice.praat"

if (-not (Test-Path $praatExe)) {
    Write-Error "Praat executable not found at: $praatExe. Run .\install_praat.ps1 first."
}
if (-not (Test-Path $praatScript)) {
    Write-Error "Praat script not found at: $praatScript."
}

Write-Host "Analyzing voice file: $inputPath" -ForegroundColor Cyan
Write-Host "Running Praat in headless mode..." -ForegroundColor Gray

# Run Praat redirecting stdout/stderr
$arguments = "--run `"$praatScript`" `"$inputPath`""
$processInfo = New-Object System.Diagnostics.ProcessStartInfo
$processInfo.FileName = $praatExe
$processInfo.Arguments = $arguments
$processInfo.RedirectStandardOutput = $true
$processInfo.RedirectStandardError = $true
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
$processInfo.StandardOutputEncoding = [System.Text.Encoding]::Unicode
$processInfo.StandardErrorEncoding = [System.Text.Encoding]::Unicode

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $processInfo

$process.Start() | Out-Null

# Read output streams
$stdoutStr = $process.StandardOutput.ReadToEnd()
$stderrStr = $process.StandardError.ReadToEnd()

# Clean UTF-16 null bytes
$stdoutStr = $stdoutStr.Replace("`0", "")
$stderrStr = $stderrStr.Replace("`0", "")

$process.WaitForExit()

if ($process.ExitCode -ne 0) {
    Write-Error "Praat analysis failed with exit code $($process.ExitCode). Error details:`n$stderrStr"
}

if ($stderrStr.Trim()) {
    Write-Warning "Praat warning output:`n$stderrStr"
}

Write-Host "`n=== ANALYSIS COMPLETED ===" -ForegroundColor Green
Write-Output $stdoutStr
