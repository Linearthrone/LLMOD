# Shared Hermes path resolution for House Victoria setup scripts.
# Dot-source this file:  . (Join-Path $PSScriptRoot 'HermesShared.ps1')
#
# Resolution order MUST match HouseVictoria.Core/Utils/HermesPaths.cs so the C# gateway and
# these scripts always agree on one Hermes config directory:
#   1. `hermes config path` (authoritative)
#   2. %LOCALAPPDATA%\hermes  (if a config.yaml already lives there)
#   3. %USERPROFILE%\.hermes  (legacy default)

function Get-HermesExe {
    $cmd = Get-Command hermes -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    foreach ($venvName in @('.venv', 'venv')) {
        $fallback = Join-Path $env:LOCALAPPDATA "hermes\hermes-agent\$venvName\Scripts\hermes.exe"
        if (Test-Path $fallback) { return $fallback }
    }

    return $null
}

function Get-HermesScriptsDir {
    $exe = Get-HermesExe
    if ($exe) {
        return Split-Path -Parent $exe
    }
    return $null
}

function Ensure-HermesOnPath {
    $scriptsDir = Get-HermesScriptsDir
    if (-not $scriptsDir) {
        Write-Warning 'Hermes executable not found. Run Tools\setup-hermes-integration.ps1 first.'
        return $false
    }

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ($userPath -split ';' | Where-Object { $_ -eq $scriptsDir }) {
        return $true
    }

    $newPath = if ([string]::IsNullOrWhiteSpace($userPath)) { $scriptsDir } else { "$userPath;$scriptsDir" }
    [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
    $env:Path = "$env:Path;$scriptsDir"
    Write-Host "Added Hermes to user PATH: $scriptsDir" -ForegroundColor Green
    return $true
}

function Get-HermesDir {
    $exe = Get-HermesExe
    if ($exe) {
        try {
            $cfgPath = (& $exe config path 2>$null | Out-String).Trim()
            if ($cfgPath -and (Test-Path $cfgPath)) {
                return Split-Path -Parent $cfgPath
            }
        } catch { }
    }

    $localHermes = Join-Path $env:LOCALAPPDATA "hermes"
    if (Test-Path (Join-Path $localHermes "config.yaml")) {
        return $localHermes
    }

    return Join-Path $env:USERPROFILE ".hermes"
}

function Set-HermesYamlScalar {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigText,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $pattern = "(?m)^(\s{0,4}$Key\s*:\s*).+$"
    if ($ConfigText -match $pattern) {
        return [regex]::Replace($ConfigText, $pattern, "`${1}$Value", 1)
    }
    return $ConfigText
}

function Ensure-HermesAgentLimits {
    param(
        [string]$ConfigFile = $(Join-Path (Get-HermesDir) 'config.yaml'),
        [int]$MaxTurns = 250,
        [int]$CodeExecutionMaxToolCalls = 150,
        [int]$DelegationMaxIterations = 100
    )

    if (-not (Test-Path $ConfigFile)) {
        Write-Warning "Hermes config not found: $ConfigFile"
        return $false
    }

    $text = Get-Content $ConfigFile -Raw
    $updated = $text
    $updated = Set-HermesYamlScalar -ConfigText $updated -Key 'max_turns' -Value $MaxTurns
    $updated = Set-HermesYamlScalar -ConfigText $updated -Key 'max_tool_calls' -Value $CodeExecutionMaxToolCalls
    $updated = Set-HermesYamlScalar -ConfigText $updated -Key 'max_iterations' -Value $DelegationMaxIterations

    if ($updated -ne $text) {
        Set-Content -Path $ConfigFile -Value $updated -Encoding UTF8 -NoNewline
        Write-Host "[OK] Hermes tool limits: max_turns=$MaxTurns, code_execution.max_tool_calls=$CodeExecutionMaxToolCalls, delegation.max_iterations=$DelegationMaxIterations" -ForegroundColor Green
    } else {
        Write-Host "[OK] Hermes tool limits already at or above House Victoria defaults" -ForegroundColor Green
    }

    return $true
}

function Set-HermesMcpServerEnabled {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigText,
        [Parameter(Mandatory = $true)][string]$ServerName,
        [bool]$Enabled = $false
    )

    $value = if ($Enabled) { 'true' } else { 'false' }
    $lines = $ConfigText -split "\r?\n"
    $inServer = $false
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line -match "^  $([regex]::Escape($ServerName)):\s*$") {
            $inServer = $true
            [void]$out.Add($line)
            continue
        }
        if ($inServer) {
            if ($line -match '^  \S') {
                $inServer = $false
            } elseif ($line -match '^    enabled:\s*') {
                [void]$out.Add("    enabled: $value")
                continue
            }
        }
        [void]$out.Add($line)
    }
    return ($out -join "`n")
}

function Remove-HermesPlatformToolset {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigText,
        [Parameter(Mandatory = $true)][string]$ToolsetName
    )

    $escaped = [regex]::Escape($ToolsetName)
    $pattern = "(?m)^    - $escaped\s*$\r?\n"
    return [regex]::Replace($ConfigText, $pattern, '')
}

function Ensure-HermesLocalBrowserPolicy {
    param(
        [string]$ConfigFile = $(Join-Path (Get-HermesDir) 'config.yaml')
    )

    if (-not (Test-Path $ConfigFile)) {
        Write-Warning "Hermes config not found: $ConfigFile"
        return $false
    }

    $text = Get-Content $ConfigFile -Raw
    $updated = $text
    $updated = Set-HermesMcpServerEnabled -ConfigText $updated -ServerName 'puppeteer' -Enabled $false
    $updated = Remove-HermesPlatformToolset -ConfigText $updated -ToolsetName 'browser'

    if ($updated -ne $text) {
        Set-Content -Path $ConfigFile -Value $updated -Encoding UTF8 -NoNewline
        Write-Host '[OK] Hermes browser policy: puppeteer MCP disabled; Hermes browser toolset removed from cli' -ForegroundColor Green
    } else {
        Write-Host '[OK] Hermes browser policy already applied (no ghost-browser spawns)' -ForegroundColor Green
    }

    return $true
}
