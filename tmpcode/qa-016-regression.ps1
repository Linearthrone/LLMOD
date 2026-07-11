<#
  QA-01 independent regression harness for ISSUE-20260515-001 / TASK-20260708-016.
  Drives the source-verified mock (mock-remote-companion.ps1) with the REAL App.config
  token+port, then runs the actual scripts/Verify-HouseVictoriaStack.ps1 against it.
  Scenario A: no contact (mirrors repo config) -> valid-token should PASS via downstream 400.
  Scenario B: contact set                       -> valid-token should PASS via 200 reply.
  tmpcode/ is gitignored; not product code. QA does NOT modify any code under test.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo   = Split-Path -Parent $PSScriptRoot
$mock   = Join-Path $repo 'tmpcode\mock-remote-companion.ps1'
$verify = Join-Path $repo 'scripts\Verify-HouseVictoriaStack.ps1'

[xml]$xml = Get-Content -LiteralPath (Join-Path $repo 'HouseVictoria.App\App.config') -Raw
$cfg = @{}; foreach ($n in $xml.configuration.appSettings.add) { $cfg[$n.key] = $n.value }
$port  = [int]$cfg['RemoteCompanionListenPort']
$token = $cfg['RemoteCompanionApiToken']

function Invoke-Scenario {
    param([string]$Name, [string]$ContactId)
    Write-Host "`n========== QA SCENARIO: $Name (contact='$ContactId') =========="
    $args = @('-NoProfile','-ExecutionPolicy','Bypass','-File', $mock, '-Port', $port, '-Token', $token)
    if ($ContactId) { $args += @('-ContactId', $ContactId) }
    $proc = Start-Process -FilePath 'powershell' -ArgumentList $args -PassThru -WindowStyle Hidden
    try {
        $up = $false
        for ($i = 0; $i -lt 20; $i++) {
            try {
                $h = Invoke-WebRequest -Uri "http://127.0.0.1:$port/api/remote/v1/health" -UseBasicParsing -TimeoutSec 2
                if ($h.StatusCode -eq 200) { $up = $true; break }
            } catch { Start-Sleep -Milliseconds 300 }
        }
        if (-not $up) { Write-Host "[harness] mock did not come up on $port"; return }
        & powershell -NoProfile -ExecutionPolicy Bypass -File $verify
    }
    finally {
        if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    }
}

Invoke-Scenario -Name 'A-no-contact (repo config)' -ContactId ''
Invoke-Scenario -Name 'B-contact-configured'       -ContactId 'qa-contact-001'
Write-Host "`n[harness] done."
