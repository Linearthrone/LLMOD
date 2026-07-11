# House Victoria - one-command startup for the full local stack.
param(
    [switch]$Rebuild,
    [switch]$ServicesOnly,
    [switch]$SkipComfy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:HV_FORCE_BUILD -eq '1') {
    $Rebuild = $true
}

$RepoRoot = if ($PSScriptRoot -match 'scripts$') {
    Split-Path -Parent $PSScriptRoot
} else {
    $PSScriptRoot
}

. (Join-Path $RepoRoot 'scripts\HV-StackCommon.ps1')

Write-Host ''
Write-Host '=== House Victoria - Start ===' -ForegroundColor Cyan
Write-Host "Repo: $RepoRoot"
Write-Host ''

$primary = Get-HVPrimaryLlm -RepoRoot $RepoRoot
Write-Host "Primary LLM: $primary" -ForegroundColor DarkGray
Write-Host ''

# Core backends
if ($primary -eq 'hermes' -or $primary -eq 'ollama') {
    Start-HVOllama -RepoRoot $RepoRoot | Out-Null
}
if ($primary -eq 'hermes') {
    Start-HVHermesGateway -RepoRoot $RepoRoot | Out-Null
} elseif ($primary -eq 'lmstudio') {
    Start-HVLmStudio -RepoRoot $RepoRoot | Out-Null
} elseif ($primary -eq 'anythingllm') {
    Write-HVServiceLine 'Anything LLM' 'start desktop app or System Monitor' 'warn'
} elseif ($primary -ne 'ollama') {
    Write-HVServiceLine 'Primary LLM' "unknown '$primary' - defaulting to Ollama" 'warn'
    Start-HVOllama -RepoRoot $RepoRoot | Out-Null
}

# Always-on House Victoria services
Start-HVMcpServer -RepoRoot $RepoRoot | Out-Null
Start-HVSttServer -RepoRoot $RepoRoot | Out-Null
Start-HVChatterbox -RepoRoot $RepoRoot | Out-Null
Start-HVBrowserCaptureBridge -RepoRoot $RepoRoot | Out-Null

if (-not $SkipComfy) {
    Start-HVComfyUI -RepoRoot $RepoRoot | Out-Null
}

if (-not $ServicesOnly) {
    $env:HV_REMOTE_COMPANION_ONLY = $null
    Start-HVApp -RepoRoot $RepoRoot -Rebuild:$Rebuild | Out-Null
}

Write-Host ''
Write-Host '=== Stack status ===' -ForegroundColor Cyan
Get-HVStackStatus -RepoRoot $RepoRoot

Write-Host ''
Write-Host 'One-time browser setup (after first install or extension update):' -ForegroundColor DarkGray
$extDir = Join-Path $RepoRoot 'BrowserCaptureExtension'
Write-Host "  chrome://extensions -> Load unpacked -> $extDir"
Write-Host ''
Write-Host 'Stop everything:  .\stop.ps1' -ForegroundColor DarkGray
Write-Host 'Rebuild + start:  .\start.ps1 -Rebuild' -ForegroundColor DarkGray
Write-Host ''
