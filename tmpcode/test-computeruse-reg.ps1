# Sandbox check of the computer_use registration logic used in setup-persona-mcp.ps1.
# Proves: (1) added to a clean config, (2) idempotent on re-run, (3) -SkipComputerUse skips.
# Does NOT touch the real Hermes config. tmpcode/ is gitignored.

$sandbox = Join-Path $PSScriptRoot 'sandbox-config.yaml'
$ComputerUseMcpPackage = 'computer-use-mcp'

function Register-ComputerUse {
    param([string]$HermesConfig, [switch]$SkipComputerUse)
    if (-not $SkipComputerUse) {
        $cuBlock = @"

  computer_use:
    command: npx
    args: ["-y", "$ComputerUseMcpPackage"]
    timeout: 120
    enabled: true
"@
        $cfgText = Get-Content $HermesConfig -Raw
        if ($cfgText -match '(?m)^\s*computer_use:\s*$') {
            Write-Host 'INFO: computer_use MCP already present in config.yaml'
        } else {
            if ($cfgText -notmatch 'mcp_servers:') {
                Add-Content -Path $HermesConfig -Value "`nmcp_servers:"
            }
            Add-Content -Path $HermesConfig -Value $cuBlock
            Write-Host 'OK: Registered computer_use MCP in config.yaml (desktop control)'
        }
    } else {
        Write-Host 'INFO: Skipped computer_use MCP registration (-SkipComputerUse)'
    }
}

# Clean config that already has house_victoria (as setup-persona-mcp writes it).
@"
mcp_servers:
  house_victoria:
    command: python
    args: ["-m", "house_victoria_mcp"]
    enabled: true
"@ | Set-Content -Path $sandbox -Encoding UTF8

Write-Host "--- Run 1 (clean config) ---"
Register-ComputerUse -HermesConfig $sandbox
Write-Host "--- Run 2 (idempotent) ---"
Register-ComputerUse -HermesConfig $sandbox
Write-Host "--- Verify both blocks present ---"
$final = Get-Content $sandbox -Raw
$hasHV = $final -match '(?m)^\s*house_victoria:\s*$'
$cuCount = ([regex]::Matches($final, '(?m)^\s*computer_use:\s*$')).Count
Write-Host ("house_victoria present: {0}" -f $hasHV)
Write-Host ("computer_use block count (want 1): {0}" -f $cuCount)

Remove-Item $sandbox -ErrorAction SilentlyContinue
Write-Host "--- Skip path ---"
"mcp_servers:`n  house_victoria:`n    enabled: true" | Set-Content -Path $sandbox -Encoding UTF8
Register-ComputerUse -HermesConfig $sandbox -SkipComputerUse
$skipHasCu = (Get-Content $sandbox -Raw) -match '(?m)^\s*computer_use:\s*$'
Write-Host ("computer_use present after skip (want False): {0}" -f $skipHasCu)
Remove-Item $sandbox -ErrorAction SilentlyContinue
