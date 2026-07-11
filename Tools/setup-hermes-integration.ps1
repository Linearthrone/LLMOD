# House Victoria - Hermes Agent integration setup (Windows)
# Installs Hermes (if missing), writes ~/.hermes/.env + MCP wiring for House Victoria MCP server.
param(
    [string]$ApiKey = "house-victoria-local-dev",
    [string]$McpEndpoint = "http://127.0.0.1:8080",
    [string]$OllamaEndpoint = "http://127.0.0.1:11434",
    [switch]$SkipComputerUse
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'HermesShared.ps1')
$DefaultToolsets = @('hermes-cli', 'web')
$ComputerUseMcpPackage = 'computer-use-mcp'
$ComputerUseMcpServerKey = 'computer_use'

Write-Host "=== House Victoria + Hermes integration ===" -ForegroundColor Cyan

function Test-HermesInstalled {
    return [bool](Get-HermesExe)
}

function Ensure-HermesToolsets {
    param(
        [string]$Path,
        [string[]]$Required
    )

    if (-not (Test-Path $Path)) { return $false }

    $lines = Get-Content $Path
    $out = New-Object System.Collections.Generic.List[string]
    $inToolsets = $false
    $existing = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $toolsetIndent = '    '

    foreach ($line in $lines) {
        if ($line -match '^\s*toolsets:\s*$') {
            $inToolsets = $true
            $toolsetIndent = if ($line -match '^(\s*)toolsets:') { $Matches[1] + '    ' } else { '    ' }
            [void]$out.Add($line)
            continue
        }

        if ($inToolsets) {
            if ($line -match '^\s*-\s+(.+?)\s*$') {
                $name = $Matches[1].Trim().Trim('"').Trim("'")
                if (-not [string]::IsNullOrWhiteSpace($name)) {
                    [void]$existing.Add($name)
                    [void]$out.Add($line)
                }
                continue
            }

            if ($line -match '^\S') {
                foreach ($req in $Required) {
                    if (-not $existing.Contains($req)) {
                        [void]$out.Add("$toolsetIndent- $req")
                        [void]$existing.Add($req)
                    }
                }
                $inToolsets = $false
            }
        }

        [void]$out.Add($line)
    }

    if ($inToolsets) {
        foreach ($req in $Required) {
            if (-not $existing.Contains($req)) {
                [void]$out.Add("$toolsetIndent- $req")
            }
        }
    } elseif (-not ($lines -join "`n" -match '(?m)^\s*toolsets:\s*$')) {
        [void]$out.Add('')
        [void]$out.Add('toolsets:')
        foreach ($req in $Required) {
            [void]$out.Add("    - $req")
        }
    }

    Set-Content -Path $Path -Value $out -Encoding UTF8
    return $true
}

function Remove-HermesToolset {
    param(
        [string]$Path,
        [string]$Name
    )

    if (-not (Test-Path $Path)) { return $false }

    $lines = Get-Content $Path
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line -match '^\s*-\s+(.+?)\s*$') {
            $item = $Matches[1].Trim().Trim('"').Trim("'")
            if ($item -eq $Name) { continue }
        }
        [void]$out.Add($line)
    }

    Set-Content -Path $Path -Value $out -Encoding UTF8
    return $true
}

function Install-HermesComputerUseMac {
    Write-Host "Installing cua-driver (Hermes computer_use, macOS)..." -ForegroundColor Cyan
    try {
        & hermes computer-use install
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[WARN] hermes computer-use install exited with code $LASTEXITCODE" -ForegroundColor Yellow
        } else {
            Write-Host "[OK] cua-driver install completed"
        }
    } catch {
        Write-Host "[WARN] computer-use install failed: $($_.Exception.Message)" -ForegroundColor Yellow
        return
    }

    try {
        & hermes computer-use status
    } catch {
        Write-Host "[WARN] hermes computer-use status failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

function Install-ComputerUseMcpWindows {
    Write-Host "Configuring desktop control via $ComputerUseMcpPackage (Windows)..." -ForegroundColor Cyan
    if (-not (Get-Command npx -ErrorAction SilentlyContinue)) {
        Write-Host "[WARN] npx not found. Install Node.js 18+ for computer-use MCP." -ForegroundColor Yellow
        return
    }

    try {
        npm view $ComputerUseMcpPackage version | Out-Null
        Write-Host "[OK] $ComputerUseMcpPackage is reachable via npm"
    } catch {
        Write-Host "[WARN] Could not resolve $ComputerUseMcpPackage on npm: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

function Ensure-McpServerBlock {
    param(
        [string]$Path,
        [string]$ServerKey,
        [string]$YamlBlock
    )

    if (-not (Test-Path $Path)) { return $false }
    $existing = Get-Content $Path -Raw
    if ($existing -match "(?m)^\s*$([regex]::Escape($ServerKey)):\s*$") {
        Write-Host "[INFO] $ServerKey MCP already in config.yaml"
        return $false
    }

    if ($existing -notmatch 'mcp_servers:') {
        Add-Content -Path $Path -Value "`nmcp_servers:"
    }
    Add-Content -Path $Path -Value $YamlBlock
    Write-Host "[OK] Appended $ServerKey MCP to config.yaml"
    return $true
}

function Install-DesktopControl {
    $isMac = $false
    try {
        $isMac = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::OSX)
    } catch { }

    if ($isMac) {
        Install-HermesComputerUseMac
        Ensure-HermesToolsets -Path $ConfigFile -Required @('computer_use') | Out-Null
        return
    }

    Install-ComputerUseMcpWindows
    $mcpBlock = @"

  ${ComputerUseMcpServerKey}:
    command: npx
    args: ["-y", "$ComputerUseMcpPackage"]
    timeout: 120
    enabled: true
"@
    Ensure-McpServerBlock -Path $ConfigFile -ServerKey $ComputerUseMcpServerKey -YamlBlock $mcpBlock | Out-Null
}

if (-not (Test-HermesInstalled)) {
    Write-Host "Hermes not found. Installing via official Windows installer..." -ForegroundColor Yellow
    Invoke-RestMethod https://raw.githubusercontent.com/NousResearch/hermes-agent/main/scripts/install.ps1 | Invoke-Expression
}

if (-not (Test-HermesInstalled)) {
    Write-Error "Hermes install failed or 'hermes' is not on PATH. Open a new terminal and re-run this script."
}

$HermesDir = Get-HermesDir
$EnvFile = Join-Path $HermesDir ".env"
$ConfigFile = Join-Path $HermesDir "config.yaml"
Write-Host "[INFO] Hermes config dir: $HermesDir" -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path $HermesDir | Out-Null

function Set-HermesEnvValue {
    param(
        [string]$Path,
        [string]$Key,
        [string]$Value
    )

    $lines = if (Test-Path $Path) { Get-Content $Path } else { @() }
    $found = $false
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line -match "^\s*$([regex]::Escape($Key))\s*=") {
            [void]$out.Add("$Key=$Value")
            $found = $true
        } else {
            [void]$out.Add($line)
        }
    }
    if (-not $found) {
        if ($out.Count -gt 0 -and $out[$out.Count - 1] -ne '') {
            [void]$out.Add('')
        }
        [void]$out.Add("$Key=$Value")
    }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($pair in @(
    @{ Key = 'API_SERVER_ENABLED'; Value = 'true' },
    @{ Key = 'API_SERVER_HOST'; Value = '127.0.0.1' },
    @{ Key = 'API_SERVER_PORT'; Value = '8642' },
    @{ Key = 'API_SERVER_KEY'; Value = $ApiKey },
    @{ Key = 'API_SERVER_MODEL_NAME'; Value = 'hermes-agent' }
)) {
    Set-HermesEnvValue -Path $EnvFile -Key $pair.Key -Value $pair.Value
}
Write-Host "[OK] Updated API server keys in $EnvFile"

$repoData = Join-Path $RepoRoot "HouseVictoria.App\Data"
if (-not (Test-Path $repoData)) { $repoData = Join-Path $RepoRoot "Data" }
$repoDataEscaped = $repoData -replace '\\', '/'

$mcpPython = Join-Path $RepoRoot "MCPServer\.venv\Scripts\python.exe"
$mcpPythonEscaped = $mcpPython -replace '\\', '/'
$mt4Path = "C:\Program Files (x86)\MetaTrader 4 FOREX.com US"
$appConfigPath = Join-Path $RepoRoot "HouseVictoria.App\App.config"
if (Test-Path $appConfigPath) {
    [xml]$appXml = Get-Content $appConfigPath
    $mt4Node = $appXml.configuration.appSettings.add | Where-Object { $_.key -eq "MT4DataPath" }
    if ($mt4Node -and $mt4Node.value) { $mt4Path = $mt4Node.value }
}
$mt4PathEscaped = $mt4Path -replace '\\', '/'

$fileRetrievalPath = Join-Path $RepoRoot "HouseVictoria.App\bin\Release\net8.0-windows\Media\GeneratedFiles"
if (-not (Test-Path $fileRetrievalPath)) {
    $fileRetrievalPath = Join-Path $RepoRoot "Media\GeneratedFiles"
}
New-Item -ItemType Directory -Force -Path $fileRetrievalPath | Out-Null
$fileRetrievalEscaped = $fileRetrievalPath -replace '\\', '/'

$mcpFragment = @"

# --- House Victoria (auto-merged by setup-hermes-integration.ps1) ---
# HTTP :8080 is used by WPF personas (MCPServerEndpoint). Hermes needs stdio MCP for tool loops.
mcp_servers:
  house_victoria:
    command: $mcpPythonEscaped
    args: ["-m", "house_victoria_mcp"]
    env:
      MT4_DATA_PATH: "$mt4PathEscaped"
      FILE_RETRIEVAL_PATH: "$fileRetrievalEscaped"
    timeout: 300
    enabled: true
  house_victoria_data:
    command: npx
    args: ["-y", "@modelcontextprotocol/server-filesystem", "$repoDataEscaped"]
    timeout: 120
    enabled: true
  ${ComputerUseMcpServerKey}:
    command: npx
    args: ["-y", "$ComputerUseMcpPackage"]
    timeout: 120
    enabled: true
"@

if (Test-Path $ConfigFile) {
    $existing = Get-Content $ConfigFile -Raw
    $updated = $false
    if ($existing -notmatch 'house_victoria:') {
        if ($existing -notmatch 'mcp_servers:') {
            Add-Content -Path $ConfigFile -Value "`nmcp_servers:"
        }
        Add-Content -Path $ConfigFile -Value @"

  house_victoria:
    command: $mcpPythonEscaped
    args: ["-m", "house_victoria_mcp"]
    env:
      MT4_DATA_PATH: "$mt4PathEscaped"
      FILE_RETRIEVAL_PATH: "$fileRetrievalEscaped"
    timeout: 300
    enabled: true
"@
        Write-Host "[OK] Appended house_victoria MCP (MT4 tools) to config.yaml"
        $updated = $true
    } else {
        Write-Host "[INFO] house_victoria MCP already in config.yaml"
    }
    if ($existing -notmatch "house_victoria_data:") {
        Add-Content -Path $ConfigFile -Value @"

  house_victoria_data:
    command: npx
    args: ["-y", "@modelcontextprotocol/server-filesystem", "$repoDataEscaped"]
    timeout: 120
    enabled: true
"@
        Write-Host "[OK] Appended house_victoria_data MCP to config.yaml"
        $updated = $true
    } else {
        Write-Host "[INFO] house_victoria_data MCP already in config.yaml"
    }
    if (-not $updated) { }
} else {
    $initial = @"
# Hermes config for House Victoria
# Run: hermes setup --portal   OR configure a local Ollama provider below

toolsets:
    - hermes-cli
    - web

providers:
  ollama_local:
    type: openai_compatible
    base_url: $($OllamaEndpoint.TrimEnd('/'))/v1
    api_key: ollama
    model: llama3.2

default_provider: ollama_local
$mcpFragment
"@
    Set-Content -Path $ConfigFile -Value $initial -Encoding UTF8
    Write-Host "[OK] Created $ConfigFile"
}

if (Test-Path $ConfigFile) {
  $configText = Get-Content $ConfigFile -Raw
  if ($configText -notmatch '(?m)^\s*platform_toolsets:\s*$') {
    if (Ensure-HermesToolsets -Path $ConfigFile -Required $DefaultToolsets) {
      Write-Host "[OK] Ensured toolsets in config.yaml: $($DefaultToolsets -join ', ')"
    }

    $isMac = $false
    try {
      $isMac = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
          [System.Runtime.InteropServices.OSPlatform]::OSX)
    } catch { }
    if (-not $isMac) {
      if (Remove-HermesToolset -Path $ConfigFile -Name 'computer_use') {
        Write-Host "[OK] Removed macOS-only computer_use toolset (Windows uses computer_use MCP)"
      }
    }
  } else {
    Write-Host "[INFO] Native Hermes config uses platform_toolsets; skipping legacy toolsets merge"
  }
}

if (-not $SkipComputerUse) {
    Install-DesktopControl
} else {
    Write-Host "[INFO] Skipped desktop control setup (-SkipComputerUse)"
}

# Sync App.config keys when present
$appConfig = Join-Path $RepoRoot "HouseVictoria.App\App.config"
if (Test-Path $appConfig) {
    [xml]$xml = Get-Content $appConfig
    $settings = $xml.configuration.appSettings
    function Set-AppSetting($key, $value) {
        $node = $settings.add | Where-Object { $_.key -eq $key }
        if ($node) { $node.value = $value }
        else {
            $new = $xml.CreateElement("add")
            $new.SetAttribute("key", $key)
            $new.SetAttribute("value", $value)
            [void]$settings.AppendChild($new)
        }
    }
    Set-AppSetting "PrimaryLLM" "hermes"
    Set-AppSetting "HermesEndpoint" "http://127.0.0.1:8642/v1"
    Set-AppSetting "HermesApiKey" $ApiKey
    Set-AppSetting "HermesModelName" "hermes-agent"
    Set-AppSetting "HermesAutoStart" "true"
    Set-AppSetting "MCPServerEndpoint" $McpEndpoint
    $xml.Save($appConfig)
    Write-Host "[OK] Updated HouseVictoria.App\App.config (PrimaryLLM=hermes, MCPServerEndpoint=$McpEndpoint)"
}

Ensure-HermesAgentLimits -ConfigFile $ConfigFile | Out-Null
Ensure-HermesLocalBrowserPolicy -ConfigFile $ConfigFile | Out-Null

Ensure-HermesOnPath | Out-Null

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Green
Write-Host '  1. Start the full stack: .\start.ps1'
Write-Host '  2. Launch House Victoria - chat routes through Hermes with tools.'
Write-Host '  3. Desktop control (Windows): restart hermes gateway; tools come from computer_use MCP.'
Write-Host '  4. Persona MCP + MT4: run Tools\setup-persona-mcp.ps1 if you add personas later.'
Write-Host ""
Write-Host 'Per-persona Hermes (without changing primary): set AdditionalServers["hermes"]="true" on the AI contact.'
