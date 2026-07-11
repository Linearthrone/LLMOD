# House Victoria - one-command shutdown for the full local stack.
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$RepoRoot = if ($PSScriptRoot -match 'scripts$') {
    Split-Path -Parent $PSScriptRoot
} else {
    $PSScriptRoot
}

. (Join-Path $RepoRoot 'scripts\HV-StackCommon.ps1')

Write-Host ''
Write-Host '=== House Victoria - Stop ===' -ForegroundColor Cyan
Write-Host ''

# House Victoria app first
Get-Process -Name 'HouseVictoria.App' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  stopping House Victoria app (PID $($_.Id))"
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}

# Ports used by the stack (newest first)
$ports = @(
    @{ Port = 17891; Label = 'Browser cast bridge' }
    @{ Port = 17890; Label = 'Remote companion API' }
    @{ Port = 8642;  Label = 'Hermes gateway' }
    @{ Port = 8881;  Label = 'Chatterbox TTS' }
    @{ Port = 8000;  Label = 'STT server' }
    @{ Port = 8080;  Label = 'MCP server' }
    @{ Port = 8188;  Label = 'ComfyUI' }
    @{ Port = 1234;  Label = 'LM Studio' }
    @{ Port = 11434; Label = 'Ollama' }
)

foreach ($entry in $ports) {
    if (Stop-HVByPort -Port $entry.Port -Label $entry.Label) {
        Write-Host "  $($entry.Label) stopped."
    }
}

# Legacy stop script (extra cleanup)
$legacy = Join-Path $RepoRoot '.ps1 scripts\stop-all.ps1'
if (Test-Path $legacy) {
    Write-Host ''
    Write-Host 'Running legacy stop-all for any remaining processes...' -ForegroundColor DarkGray
    & $legacy
}

Write-Host ''
Write-Host '=== Stop complete ===' -ForegroundColor Cyan
Write-Host 'Start again:  .\start.ps1' -ForegroundColor DarkGray
Write-Host ''
