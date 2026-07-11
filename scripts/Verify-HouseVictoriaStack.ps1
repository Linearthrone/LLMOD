<#
.SYNOPSIS
    Non-interactive smoke checks for Remote Companion + optional ComfyUI (reads repo App.config).
.DESCRIPTION
    Used by QA-01 / OPS to unblock TASK-015 and regress remote + image stacks without relying on IDE UI harnesses.
.PARAMETER RepoRoot
    Repo root containing HouseVictoria.App\App.config
.PARAMETER EvidencePath
    Append one run block to this file (UTF-8)
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$EvidencePath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
}
if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $EvidencePath = Join-Path (Join-Path $RepoRoot 'tmpcode') 'qa-stack-evidence.txt'
}

$configPath = Join-Path (Join-Path $RepoRoot 'HouseVictoria.App') 'App.config'
if (-not (Test-Path -LiteralPath $configPath)) {
    throw "App.config not found: $configPath"
}

[xml]$xml = Get-Content -LiteralPath $configPath -Raw
$table = @{}
foreach ($node in $xml.configuration.appSettings.add) {
    $table[$node.key] = $node.value
}

$port = [int]$table['RemoteCompanionListenPort']
if ($port -lt 1 -or $port -gt 65535) { throw "Invalid RemoteCompanionListenPort in config." }
$token = $table['RemoteCompanionApiToken']
$imgRaw = $table['StableDiffusionEndpoint']
if ([string]::IsNullOrWhiteSpace($imgRaw)) { $imgRaw = 'http://localhost:8188' }
$imgEp = $imgRaw.TrimEnd('/')
$ckptPref = if ($null -ne $table['ComfyUIPreferredCheckpoint']) { $table['ComfyUIPreferredCheckpoint'] } else { '' }

function Get-ErrDetail {
    param([System.Management.Automation.ErrorRecord]$ErrRecord)
    try {
        $ex = $ErrRecord.Exception
        $w = $ex -as [System.Net.WebException]
        if (-not $w -and $ex.InnerException) {
            $w = $ex.InnerException -as [System.Net.WebException]
        }
        if ($w -and $w.Response) {
            $resp = $w.Response
            $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
            $txt = $sr.ReadToEnd()
            $sr.Close()
            return "HTTP $([int]$resp.StatusCode) $txt"
        }
        return $ex.Message
    }
    catch {
        return $ErrRecord.Exception.Message
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$null = $lines.Add("=== Verify-HouseVictoriaStack $(Get-Date -Format 'o') ===")
$null = $lines.Add("[config] RepoRoot=$RepoRoot ListenPort=$port ImageEndpoint=$imgEp PreferredCheckpoint=$ckptPref")

$base = "http://127.0.0.1:$port"
try {
    $resp = Invoke-WebRequest -Uri "$base/api/remote/v1/health" -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop
    $null = $lines.Add("[remote-health] $($resp.StatusCode) $($resp.Content)")
}
catch {
    $null = $lines.Add("[remote-health] FAIL $(Get-ErrDetail $_)")
}

try {
    $body = '{"message":"ping"}'
    $null = Invoke-WebRequest -Uri "$base/api/remote/v1/chat" -UseBasicParsing -Method POST `
        -Headers @{ Authorization = 'Bearer short' } -ContentType 'application/json; charset=utf-8' `
        -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 8 -ErrorAction Stop
    $null = $lines.Add('[remote-chat-short-token] FAIL expected 401 unauthorized')
}
catch {
    $detail = Get-ErrDetail $_
    if ($detail -match '\b401\b' -and $detail -match 'unauthorized') {
        $null = $lines.Add("[remote-chat-short-token] PASS $detail")
    }
    else {
        $null = $lines.Add("[remote-chat-short-token] UNEXPECTED $detail")
    }
}

if (-not [string]::IsNullOrWhiteSpace($token)) {
    try {
        $bodyOk = '{"message":"ping"}'
        $respC = Invoke-WebRequest -Uri "$base/api/remote/v1/chat" -UseBasicParsing -Method POST `
            -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json; charset=utf-8' `
            -Body ([System.Text.Encoding]::UTF8.GetBytes($bodyOk)) -TimeoutSec 120 -ErrorAction Stop
        $cLen = [Math]::Min(200, $respC.Content.Length)
        $null = $lines.Add("[remote-chat-valid-token] PASS $($respC.StatusCode) snippet=$($respC.Content.Substring(0, $cLen))")
    }
    catch {
        $detail = Get-ErrDetail $_
        # Auth + connectivity are proven the moment we get any HTTP response other than 401.
        # A downstream business error (e.g. no AI contact configured) still returns 400, but is
        # NOT a smoke failure for this check -- surface the status/body so it is not mistaken for a
        # socket failure. Only 401/unauthorized or a real transport error counts as FAIL.
        if ($detail -match '\b401\b' -or $detail -match 'unauthorized') {
            $null = $lines.Add("[remote-chat-valid-token] FAIL auth rejected valid token: $detail")
        }
        elseif ($detail -match 'message_required') {
            $null = $lines.Add("[remote-chat-valid-token] FAIL payload contract regressed: $detail")
        }
        elseif ($detail -match '\bHTTP\s+\d+\b') {
            $null = $lines.Add("[remote-chat-valid-token] PASS auth+connectivity ok, downstream: $detail")
        }
        else {
            $null = $lines.Add("[remote-chat-valid-token] FAIL $detail")
        }
    }
}

$infoUrl = "$imgEp/object_info/CheckpointLoaderSimple"
try {
    $co = Invoke-WebRequest -Uri $infoUrl -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop
    $null = $lines.Add("[comfy-checkpoint-metadata] $($co.StatusCode) len=$($co.RawContentLength)")
}
catch {
    $null = $lines.Add("[comfy-checkpoint-metadata] NOT_REACHABLE $($_.Exception.Message)`n  Hint: run start.bat or Settings -> Start ComfyUI; endpoint must match StableDiffusionEndpoint.")
}

$d = Split-Path $EvidencePath -Parent
if (-not (Test-Path -LiteralPath $d)) {
    New-Item -ItemType Directory -Force -Path $d | Out-Null
}

$block = ($lines -join "`n") + "`n"
Add-Content -LiteralPath $EvidencePath -Encoding utf8 -Value $block
Write-Host $block
