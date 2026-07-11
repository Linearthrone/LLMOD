# Wire House Victoria MCP (memory + MetaTrader 4 tools) for all AI personas.
# - Ensures App.config MCPServerEndpoint points at the HTTP MCP wrapper (:8080)
# - Syncs persona databank config.json files
# - Registers stdio house_victoria MCP in Hermes (required when PrimaryLLM=hermes)
param(
    [string]$McpEndpoint = "http://127.0.0.1:8080",
    [switch]$SkipComputerUse
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'HermesShared.ps1')
$AppConfig = Join-Path $RepoRoot "HouseVictoria.App\App.config"
$HermesDir = Get-HermesDir
$HermesConfig = Join-Path $HermesDir "config.yaml"
$McpPython = Join-Path $RepoRoot "MCPServer\.venv\Scripts\python.exe"
$McpPythonEscaped = $McpPython -replace '\\', '/'
$ComputerUseMcpPackage = 'computer-use-mcp'

Write-Host "=== House Victoria persona MCP setup ===" -ForegroundColor Cyan
Write-Host ("[INFO] Hermes config dir: " + $HermesDir) -ForegroundColor Cyan

function Get-AppSetting($key) {
    if (-not (Test-Path $AppConfig)) { return $null }
    [xml]$xml = Get-Content $AppConfig
    $node = $xml.configuration.appSettings.add | Where-Object { $_.key -eq $key }
    return $node.value
}

function Set-AppSetting($key, $value) {
    [xml]$xml = Get-Content $AppConfig
    $settings = $xml.configuration.appSettings
    $node = $settings.add | Where-Object { $_.key -eq $key }
    if ($node) { $node.value = $value }
    else {
        $new = $xml.CreateElement("add")
        $new.SetAttribute("key", $key)
        $new.SetAttribute("value", $value)
        [void]$settings.AppendChild($new)
    }
    $xml.Save($AppConfig)
}

$mcpEndpoint = Get-AppSetting "MCPServerEndpoint"
if ([string]::IsNullOrWhiteSpace($mcpEndpoint)) {
    Set-AppSetting "MCPServerEndpoint" $McpEndpoint
    Write-Host ('OK: Set MCPServerEndpoint=' + $McpEndpoint + ' in App.config')
} else {
    Write-Host ('INFO: MCPServerEndpoint already ' + $mcpEndpoint)
    $McpEndpoint = $mcpEndpoint
}

$mt4Path = Get-AppSetting "MT4DataPath"
if ([string]::IsNullOrWhiteSpace($mt4Path)) {
    $mt4Path = "C:\Program Files (x86)\MetaTrader 4 FOREX.com US"
}
$mt4PathEscaped = $mt4Path -replace '\\', '/'
Write-Host ('INFO: MT4DataPath: ' + $mt4Path)

function Resolve-NewestFile($candidates) {
    $found = @()
    foreach ($p in $candidates) {
        if (Test-Path $p) { $found += Get-Item $p }
    }
    if ($found.Count -gt 0) {
        return ($found | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
    }
    return $null
}

$appDbCandidates = @(
    (Join-Path $RepoRoot "HouseVictoria.App\bin\Release\net8.0-windows\Data\Memory\HouseVictoria.db"),
    (Join-Path $RepoRoot "HouseVictoria.App\bin\Debug\net8.0-windows\Data\Memory\HouseVictoria.db"),
    (Join-Path $RepoRoot "Data\Memory\HouseVictoria.db"),
    (Join-Path $RepoRoot "Data\HouseVictoria.db"),
    (Join-Path $RepoRoot "HouseVictoria.App\Data\HouseVictoria.db")
)
$appDb = Resolve-NewestFile $appDbCandidates
if (-not $appDb) { $appDb = $appDbCandidates[0] }
$appDbEscaped = $appDb -replace '\\', '/'

$dataBankCandidates = @(
    (Join-Path $RepoRoot "HouseVictoria.App\bin\Release\net8.0-windows\Data\Databanks"),
    (Join-Path $RepoRoot "HouseVictoria.App\bin\Debug\net8.0-windows\Data\Databanks"),
    (Join-Path $RepoRoot "HouseVictoria.App\Data\Databanks"),
    (Join-Path $RepoRoot "Data\Databanks")
)
$dataBanks = $dataBankCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $dataBanks) { $dataBanks = $dataBankCandidates[0] }
$dataBanksEscaped = $dataBanks -replace '\\', '/'

$mcpMemoryDb = Join-Path $RepoRoot "MCPServer\data\memory.db"
$mcpMemoryDbEscaped = $mcpMemoryDb -replace '\\', '/'
$repoRootEscaped = $RepoRoot -replace '\\', '/'
$runtimeDataEscaped = (Join-Path $RepoRoot "HouseVictoria.App\bin\Release\net8.0-windows\Data") -replace '\\', '/'
$repoDataEscaped = (Join-Path $RepoRoot "Data") -replace '\\', '/'

Write-Host ('INFO: App DB: ' + $appDb)
Write-Host ('INFO: Data banks: ' + $dataBanks)

# Sync persona databank config.json files
$dataRoots = @(
    (Join-Path $RepoRoot "HouseVictoria.App\Data\Databanks"),
    (Join-Path $RepoRoot "Data\Databanks")
)
$synced = 0
foreach ($root in $dataRoots) {
    if (-not (Test-Path $root)) { continue }
    Get-ChildItem -Path $root -Directory | ForEach-Object {
        $cfg = Join-Path $_.FullName "config.json"
        if (-not (Test-Path $cfg)) { return }
        try {
            $json = Get-Content $cfg -Raw | ConvertFrom-Json
            if ($json.MCPServerEndpoint -ne $McpEndpoint) {
                $json | Add-Member -NotePropertyName MCPServerEndpoint -NotePropertyValue $McpEndpoint -Force
                $json | ConvertTo-Json -Compress | Set-Content $cfg -Encoding UTF8
                $synced++
                Write-Host ('OK: Updated ' + $_.Name + '/config.json -> ' + $McpEndpoint)
            }
        } catch {
            Write-Host ('WARN: Could not update ' + $cfg + ' : ' + $_) -ForegroundColor Yellow
        }
    }
}
if ($synced -eq 0) {
    Write-Host ('INFO: Persona databank configs already use ' + $McpEndpoint + ' (or none found)')
}

if (-not (Test-Path $McpPython)) {
    Write-Host ('WARN: MCP venv not found at ' + $McpPython + ' — run install.bat first.') -ForegroundColor Yellow
}

# Hermes: stdio MCP (memory search, databanks, MT4) + filesystem roots she can actually read
$hermesVictoriaBlock = @"
  house_victoria:
    command: $McpPythonEscaped
    args: ["-m", "house_victoria_mcp"]
    env:
      MT4_DATA_PATH: "$mt4PathEscaped"
      APP_DATABASE_PATH: "$appDbEscaped"
      DATABASE_PATH: "$mcpMemoryDbEscaped"
      DATA_BANKS_PATH: "$dataBanksEscaped"
    timeout: 300
    enabled: true
  house_victoria_data:
    command: npx
    args: ["-y", "@modelcontextprotocol/server-filesystem", "$repoRootEscaped", "$runtimeDataEscaped", "$repoDataEscaped", "$dataBanksEscaped"]
    timeout: 120
    enabled: true
"@

function Update-HermesMcpBlock {
    param([string]$ConfigPath, [string]$Block)
    $content = Get-Content $ConfigPath -Raw
    if ($content -match '(?s)# --- House Victoria MCP \(persona setup\) ---.*?# --- end House Victoria MCP ---') {
        $content = [regex]::Replace($content, '(?s)# --- House Victoria MCP \(persona setup\) ---.*?# --- end House Victoria MCP ---', $Block.Trim())
    } elseif ($content -match '(?s)mcp_servers:\s*\r?\n(?:  .*\r?\n)*') {
        # Replace existing house_victoria* entries inside mcp_servers
        $content = [regex]::Replace($content, '(?s)  house_victoria_data:.*?(?=\r?\n  [a-z_]+:|\r?\n[a-z#]|\z)', '')
        $content = [regex]::Replace($content, '(?s)  house_victoria:\s*\r?\n(?:    .*\r?\n)*', '')
        $content = $content.TrimEnd() + "`n" + $Block.Trim() + "`n"
    } else {
        $content = $content.TrimEnd() + "`n`nmcp_servers:`n" + $Block.Trim() + "`n"
    }
    Set-Content -Path $ConfigPath -Value $content -Encoding UTF8
}

$hermesManagedBlock = @"
# --- House Victoria MCP (persona setup) ---
$hermesVictoriaBlock
# --- end House Victoria MCP ---
"@

if (Test-Path $HermesConfig) {
    Update-HermesMcpBlock -ConfigPath $HermesConfig -Block $hermesManagedBlock
    Write-Host ('OK: Updated Hermes MCP block in ' + $HermesConfig)

    # Desktop control: keep computer_use registered so a persona-only setup run never drops it.
    # Mirrors setup-hermes-integration.ps1; opt out with -SkipComputerUse.
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
} else {
    Write-Host 'INFO: Hermes config not found — run Tools\setup-hermes-integration.ps1 first, then re-run this script.'
}

Write-Host ""
Write-Host "Persona MCP is configured:" -ForegroundColor Green
Write-Host ('  App + personas -> ' + $McpEndpoint + ' (HTTP: memory, context, MT4 tools via /command)')
Write-Host '  Hermes chat    -> house_victoria stdio MCP (MT4 tools when PrimaryLLM=hermes)'
Write-Host ""
Write-Host 'Next: start.bat (MCP on :8080), attach HouseVictoriaBridge EA in MT4, run: hermes gateway restart'
