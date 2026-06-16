<#
.SYNOPSIS
    Wire House Victoria remote companion to your Tailscale tailnet.
.DESCRIPTION
    Keeps RemoteCompanionListenOnLan=false (loopback-only) and exposes the API via
    `tailscale serve`, which proxies HTTPS on your MagicDNS hostname to http://127.0.0.1:<port>.

    Prerequisites:
    - Tailscale installed and logged in on this PC
    - House Victoria running with RemoteCompanionEnabled=true and API token >= 16 chars

    Android app base URL: https://<your-pc-hostname>.<tailnet>.ts.net  (no port suffix)
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [int]$Port = 0,
    [switch]$StopServe,
    [switch]$StatusOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
}

function Get-TailscaleExe {
    $candidates = @(
        'C:\Program Files\Tailscale\tailscale.exe',
        "${env:ProgramFiles(x86)}\Tailscale\tailscale.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) { return $path }
    }
    $fromPath = Get-Command tailscale -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }
    return $null
}

function Read-RemoteCompanionPort {
    param([string]$Root)
    $configPath = Join-Path (Join-Path $Root 'HouseVictoria.App') 'App.config'
    if (-not (Test-Path -LiteralPath $configPath)) {
        throw "App.config not found: $configPath"
    }
    [xml]$xml = Get-Content -LiteralPath $configPath -Raw
    $node = $xml.configuration.appSettings.add | Where-Object { $_.key -eq 'RemoteCompanionListenPort' } | Select-Object -First 1
    if (-not $node -or [string]::IsNullOrWhiteSpace($node.value)) {
        return 17890
    }
    return [int]$node.value
}

$ts = Get-TailscaleExe
if (-not $ts) {
    Write-Host ''
    Write-Host 'Tailscale CLI not found.' -ForegroundColor Yellow
    Write-Host 'Install: https://tailscale.com/download/windows'
    Write-Host 'Or run (elevated):'
    Write-Host '  Invoke-WebRequest -Uri https://pkgs.tailscale.com/stable/tailscale-setup-latest-amd64.msi -OutFile $env:TEMP\tailscale.msi'
    Write-Host '  msiexec /i $env:TEMP\tailscale.msi /qn TS_NOLAUNCH=1'
    exit 1
}

if ($Port -lt 1) {
    $Port = Read-RemoteCompanionPort -Root $RepoRoot
}

Write-Host "=== Tailscale remote companion setup ===" -ForegroundColor Cyan
Write-Host "Tailscale: $ts"
Write-Host "Local API port: $Port (loopback)"

if ($StopServe) {
    & $ts serve --bg=false 2>$null
    & $ts serve reset 2>$null
    Write-Host 'Tailscale serve configuration cleared.' -ForegroundColor Green
    exit 0
}

$statusJson = & $ts status --json 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($statusJson)) {
    Write-Host ''
    Write-Host 'Tailscale is not connected. Log in first:' -ForegroundColor Yellow
    Write-Host "  & `"$ts`" login"
    Write-Host 'Then re-run this script.'
    exit 2
}

try {
    $health = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/api/remote/v1/health" -UseBasicParsing -TimeoutSec 8
    Write-Host "Local health: $($health.StatusCode) $($health.Content)" -ForegroundColor Green
}
catch {
    Write-Host ''
    Write-Host 'House Victoria remote companion is not reachable on loopback.' -ForegroundColor Red
    Write-Host "Start House Victoria with RemoteCompanionEnabled=true, then retry."
    Write-Host "Expected: http://127.0.0.1:$Port/api/remote/v1/health"
    exit 3
}

if ($StatusOnly) {
    Write-Host ''
    & $ts status
    Write-Host ''
    & $ts serve status
    exit 0
}

Write-Host ''
Write-Host 'Enabling tailscale serve (HTTPS on tailnet -> localhost)...' -ForegroundColor Cyan
& $ts serve --bg $Port
if ($LASTEXITCODE -ne 0) {
    throw "tailscale serve failed (exit $LASTEXITCODE)"
}

Write-Host ''
& $ts serve status

$dnsName = (& $ts status --json | ConvertFrom-Json).Self.DNSName
if ([string]::IsNullOrWhiteSpace($dnsName)) {
    $dnsName = '(check: tailscale status — use HTTPS MagicDNS hostname)'
}

Write-Host ''
Write-Host '=== Android app configuration ===' -ForegroundColor Green
Write-Host "Base URL:  https://$dnsName"
Write-Host 'API token: same value as House Victoria Settings -> Remote companion API token (16+ chars)'
Write-Host ''
Write-Host 'On your phone:'
Write-Host '  1. Install Tailscale from Play Store and sign in to the same account'
Write-Host '  2. Open AndroidRemoteCompanion, paste the Base URL above'
Write-Host '  3. Tap Check Health, then send a test message'
Write-Host ''
Write-Host 'To remove serve proxy:  .\scripts\Setup-TailscaleRemoteCompanion.ps1 -StopServe'
