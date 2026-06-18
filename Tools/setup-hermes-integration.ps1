# House Victoria - Hermes Agent integration setup (Windows)
# Installs Hermes (if missing), writes ~/.hermes/.env + MCP wiring for House Victoria MCP server.
param(
    [string]$ApiKey = "house-victoria-local-dev",
    [string]$McpEndpoint = "http://127.0.0.1:8080",
    [string]$OllamaEndpoint = "http://127.0.0.1:11434"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$HermesDir = Join-Path $env:USERPROFILE ".hermes"
$EnvFile = Join-Path $HermesDir ".env"
$ConfigFile = Join-Path $HermesDir "config.yaml"

Write-Host "=== House Victoria + Hermes integration ===" -ForegroundColor Cyan

function Test-HermesInstalled {
    if (Get-Command hermes -ErrorAction SilentlyContinue) { return $true }
    $fallback = Join-Path $env:LOCALAPPDATA "hermes\hermes-agent\.venv\Scripts\hermes.exe"
    return (Test-Path $fallback)
}

if (-not (Test-HermesInstalled)) {
    Write-Host "Hermes not found. Installing via official Windows installer..." -ForegroundColor Yellow
    Invoke-RestMethod https://raw.githubusercontent.com/NousResearch/hermes-agent/main/scripts/install.ps1 | Invoke-Expression
}

if (-not (Test-HermesInstalled)) {
    Write-Error "Hermes install failed or 'hermes' is not on PATH. Open a new terminal and re-run this script."
}

New-Item -ItemType Directory -Force -Path $HermesDir | Out-Null

$envLines = @(
    "API_SERVER_ENABLED=true",
    "API_SERVER_HOST=127.0.0.1",
    "API_SERVER_PORT=8642",
    "API_SERVER_KEY=$ApiKey",
    "API_SERVER_MODEL_NAME=hermes-agent"
)
Set-Content -Path $EnvFile -Value ($envLines -join "`n") -Encoding UTF8
Write-Host "[OK] Wrote $EnvFile"

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

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Green
Write-Host '  1. Ensure Ollama is running: ollama serve'
Write-Host '  2. Ensure House Victoria MCP is running: start.bat (port 8080)'
Write-Host '  3. Start Hermes gateway: hermes gateway'
Write-Host '  4. Launch House Victoria - chat routes through Hermes with tools.'
Write-Host '  5. Persona MCP + MT4: run Tools\setup-persona-mcp.ps1 if you add personas later.'
Write-Host ""
Write-Host 'Per-persona Hermes (without changing primary): set AdditionalServers["hermes"]="true" on the AI contact.'
