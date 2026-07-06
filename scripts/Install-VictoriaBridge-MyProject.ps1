# Copies HouseVictoriaBridge into the My Project UE 5.8 tree and enables WebSocketNetworking hint.
param(
    [string]$MyProjectPath = "$env:USERPROFILE\OneDrive\Documents\Unreal Projects\MyProject",
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$sourcePlugin = Join-Path $RepoRoot "Unreal\Plugins\HouseVictoriaBridge"
$targetPlugins = Join-Path $MyProjectPath "Plugins"
$targetPlugin = Join-Path $targetPlugins "HouseVictoriaBridge"
$uproject = Join-Path $MyProjectPath "MyProject.uproject"

if (-not (Test-Path $sourcePlugin)) {
    throw "Source plugin not found: $sourcePlugin"
}
if (-not (Test-Path $MyProjectPath)) {
    throw "My Project folder not found: $MyProjectPath"
}

New-Item -ItemType Directory -Force -Path $targetPlugins | Out-Null

if (Test-Path $targetPlugin) {
    Write-Host "Removing existing plugin at $targetPlugin"
    Remove-Item -Recurse -Force $targetPlugin
}

Write-Host "Copying HouseVictoriaBridge -> $targetPlugin"
Copy-Item -Recurse -Force $sourcePlugin $targetPlugin

if (-not (Test-Path $uproject)) {
    Write-Warning "uproject not found at $uproject - copy plugin manually and enable in editor."
}
else {
    $json = Get-Content $uproject -Raw | ConvertFrom-Json
    if (-not $json.Plugins) {
        $json | Add-Member -NotePropertyName Plugins -NotePropertyValue @()
    }

    $names = @($json.Plugins | ForEach-Object { $_.Name })
    $changed = $false
    $pluginEntries = @(
        @{ Name = "WebSocketNetworking"; Enabled = $true },
        @{ Name = "HouseVictoriaBridge"; Enabled = $true }
    )

    foreach ($entry in $pluginEntries) {
        if ($names -notcontains $entry.Name) {
            $json.Plugins += [PSCustomObject]$entry
            $changed = $true
            Write-Host "Added plugin entry: $($entry.Name)"
        }
    }

    if ($changed) {
        $json | ConvertTo-Json -Depth 10 | Set-Content $uproject -Encoding UTF8
        Write-Host "Updated $uproject"
    }
    else {
        Write-Host "Plugins already listed in uproject."
    }
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Open My Project in UE 5.8 and build C++ if prompted."
Write-Host "  2. Implement WebSocket server on port 8888 (see Docs/MyProject_Victoria_Setup.md)."
Write-Host "  3. Wire BP_MHC_Victoria to parsed verbs (walk/talk/see/touch)."
Write-Host "  4. python Tools/unreal_mock_ws.py  OR  PIE with your server."
Write-Host "  5. Start House Victoria - chat with Victoria persona."
