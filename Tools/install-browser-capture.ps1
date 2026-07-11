# Browser capture bridge + extension path reminder.
# Full stack startup: .\start.ps1  (includes bridge on :17891)
param(
    [switch]$StartBridgeOnly,
    [switch]$SkipBridgeStart
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $RepoRoot 'scripts\HV-StackCommon.ps1')

Write-Host "=== House Victoria Browser Capture ===" -ForegroundColor Cyan
Write-Host "Tip: use .\start.ps1 for the full stack (bridge is included)." -ForegroundColor DarkGray
Write-Host ""

$ExtensionDir = Join-Path $RepoRoot "BrowserCaptureExtension"

if (-not $SkipBridgeStart) {
    Start-HVBrowserCaptureBridge -RepoRoot $RepoRoot | Out-Null
}

Write-Host ""
Write-Host "Extension folder (load unpacked in Chrome/Edge):" -ForegroundColor Cyan
Write-Host "  $ExtensionDir"
Write-Host ""
Write-Host "Chrome: chrome://extensions  -> Developer mode -> Load unpacked"
Write-Host "Edge:   edge://extensions    -> Developer mode -> Load unpacked"
Write-Host ""
Write-Host "Desktop live preview: Instrument Stack > Desktop tab"
Write-Host "cast socket: ws://127.0.0.1:17891/ws/cast"
Write-Host ""

if ($StartBridgeOnly) { exit 0 }
