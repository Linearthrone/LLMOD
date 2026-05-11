<#
.SYNOPSIS
    Cross-repo integration verification (LLMOD + Unreal HouseVictoria).
.DESCRIPTION
    Runs:
      1) LLMOD stack smoke (remote health + chat + optional image lane)
      2) Unreal websocket readiness check (:8888)
      3) Unreal Lexie acceptance script
    Appends an evidence block to tmpcode/cross-repo-integration-evidence.txt.
.PARAMETER SkipUnrealLanes
    When set, only runs the LLMOD stack smoke (and parses qa-stack-evidence for critical failures). Skips :8888 wait and Unreal Lexie script.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$UnrealProjectRoot = 'C:\Users\kurtw\OneDrive\Documents\Unreal Projects\HouseVictoria',
    [int]$UnrealWsPort = 8888,
    [int]$WebSocketWaitSeconds = 30,
    [string]$EvidencePath = '',
    [switch]$SkipUnrealLanes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $EvidencePath = Join-Path (Join-Path $RepoRoot 'tmpcode') 'cross-repo-integration-evidence.txt'
}

$verifyStack = Join-Path (Join-Path $RepoRoot 'scripts') 'Verify-HouseVictoriaStack.ps1'
$unrealSmoke = Join-Path (Join-Path $UnrealProjectRoot 'Tools') 'lexie_acceptance_test.ps1'

if (-not (Test-Path -LiteralPath $verifyStack)) { throw "Missing script: $verifyStack" }
if (-not $SkipUnrealLanes -and -not (Test-Path -LiteralPath $unrealSmoke)) {
    throw "Missing script: $unrealSmoke (or use -SkipUnrealLanes)"
}

function Test-TcpPortOpen {
    param([string]$TargetHost, [int]$Port)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $iar = $client.BeginConnect($TargetHost, $Port, $null, $null)
        $ok = $iar.AsyncWaitHandle.WaitOne(1500, $false)
        if (-not $ok) { return $false }
        $client.EndConnect($iar) | Out-Null
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Get-LastQaStackEvidenceBlock {
    param([string]$QaEvidencePath)
    if (-not (Test-Path -LiteralPath $QaEvidencePath)) { return '' }
    $all = [System.IO.File]::ReadAllText($QaEvidencePath)
    $marker = '=== Verify-HouseVictoriaStack'
    $idx = $all.LastIndexOf($marker, [System.StringComparison]::Ordinal)
    if ($idx -lt 0) { return $all }
    return $all.Substring($idx)
}

function Test-QaStackEvidenceCriticalFailures {
    param([string]$Block)
    if ([string]::IsNullOrWhiteSpace($Block)) { return @('empty qa-stack evidence block') }
    $fails = New-Object System.Collections.Generic.List[string]
    if ($Block -match '(?m)^\[remote-health\] FAIL') { $null = $fails.Add('remote-health') }
    if ($Block -match '(?m)^\[remote-chat-short-token\] (FAIL|UNEXPECTED)') { $null = $fails.Add('remote-chat-short-token') }
    if ($Block -match '(?m)^\[remote-chat-valid-token\] FAIL') { $null = $fails.Add('remote-chat-valid-token') }
    return , $fails.ToArray()
}

function Add-Evidence {
    param([string[]]$Lines)
    $dir = Split-Path -Parent $EvidencePath
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $block = ($Lines -join "`n") + "`n"
    Add-Content -LiteralPath $EvidencePath -Encoding utf8 -Value $block
    Write-Host $block
}

$lines = New-Object System.Collections.Generic.List[string]
$null = $lines.Add("=== Verify-CrossRepoIntegration $(Get-Date -Format 'o') ===")
$null = $lines.Add("[inputs] RepoRoot=$RepoRoot UnrealProjectRoot=$UnrealProjectRoot UnrealWsPort=$UnrealWsPort")

# Lane A/B
try {
    $null = $lines.Add("[lane-stack] START")
    & powershell -NoProfile -ExecutionPolicy Bypass -File $verifyStack -RepoRoot $RepoRoot | Out-Null
    $qaEvidence = Join-Path (Join-Path $RepoRoot 'tmpcode') 'qa-stack-evidence.txt'
    $stackBlock = Get-LastQaStackEvidenceBlock -QaEvidencePath $qaEvidence
    $crit = Test-QaStackEvidenceCriticalFailures -Block $stackBlock
    if ($crit.Count -gt 0) {
        $null = $lines.Add("[lane-stack] FAIL critical checks: $($crit -join ', ')")
        Add-Evidence -Lines $lines
        exit 1
    }
    $null = $lines.Add("[lane-stack] PASS")
}
catch {
    $null = $lines.Add("[lane-stack] FAIL $($_.Exception.Message)")
    Add-Evidence -Lines $lines
    exit 1
}

if ($SkipUnrealLanes) {
    $null = $lines.Add("[lane-unreal-ready] SKIPPED SkipUnrealLanes")
    $null = $lines.Add("[lane-unreal-smoke] SKIPPED SkipUnrealLanes")
    $null = $lines.Add("[result] PASS (stack only)")
    Add-Evidence -Lines $lines
    exit 0
}

# Unreal readiness
$deadline = (Get-Date).AddSeconds($WebSocketWaitSeconds)
$ready = $false
while ((Get-Date) -lt $deadline) {
    if (Test-TcpPortOpen -TargetHost '127.0.0.1' -Port $UnrealWsPort) {
        $ready = $true
        break
    }
    Start-Sleep -Milliseconds 500
}

if (-not $ready) {
    $null = $lines.Add("[lane-unreal-ready] FAIL ws://127.0.0.1:$UnrealWsPort not listening within ${WebSocketWaitSeconds}s")
    Add-Evidence -Lines $lines
    exit 1
}
$null = $lines.Add("[lane-unreal-ready] PASS ws://127.0.0.1:$UnrealWsPort")

# Lane C
try {
    $null = $lines.Add("[lane-unreal-smoke] START")
    & powershell -NoProfile -ExecutionPolicy Bypass -File $unrealSmoke | Out-Null
    $null = $lines.Add("[lane-unreal-smoke] PASS")
}
catch {
    $null = $lines.Add("[lane-unreal-smoke] FAIL $($_.Exception.Message)")
    Add-Evidence -Lines $lines
    exit 1
}

$null = $lines.Add("[result] PASS")
Add-Evidence -Lines $lines
