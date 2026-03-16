param(
    [string]$Text = "This is a test of the House Victoria text to speech service.",
    [string]$OutputPath = "Media\Test-TTS.wav"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== House Victoria TTS Smoke Test ==="

# Resolve repo root based on script location
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
Write-Host "Repo root: $repoRoot"

# Load App.config to get TTSEndpoint
$appConfigPath = Join-Path $repoRoot "HouseVictoria.App\App.config"
if (-not (Test-Path $appConfigPath)) {
    Write-Warning "App.config not found at $appConfigPath. Falling back to default TTS endpoint http://localhost:5000"
    $ttsEndpoint = "http://localhost:5000"
} else {
    [xml]$configXml = Get-Content $appConfigPath
    $settings = $configXml.configuration.appSettings.add
    $ttsSetting = $settings | Where-Object { $_.key -eq "TTSEndpoint" }
    $ttsEndpoint = if ($ttsSetting -and $ttsSetting.value) { $ttsSetting.value } else { "http://localhost:5000" }
}

$ttsEndpoint = $ttsEndpoint.TrimEnd("/")
Write-Host "Using TTSEndpoint: $ttsEndpoint"

try {
    # Health check
    Write-Host "Checking TTS health at $ttsEndpoint/health ..."
    $healthResponse = Invoke-WebRequest -UseBasicParsing -Uri "$ttsEndpoint/health" -TimeoutSec 5
    Write-Host "Health status: $($healthResponse.StatusCode)"
} catch {
    Write-Warning "Health check failed: $($_.Exception.Message). Continuing with synthesis attempt."
}

try {
    Write-Host "Requesting synthesis..."
    $body = @{ text = $Text } | ConvertTo-Json -Depth 3
    $response = Invoke-WebRequest -UseBasicParsing -Uri "$ttsEndpoint/" -Method Post -ContentType "application/json" -Body $body -TimeoutSec 30
    if (-not $response.Content) {
        throw "TTS response had no content."
    }

    $outPathFull = Join-Path $repoRoot $OutputPath
    $outDir = Split-Path -Parent $outPathFull
    if (-not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    }

    [IO.File]::WriteAllBytes($outPathFull, $response.Content)
    $lengthKb = [Math]::Round((Get-Item $outPathFull).Length / 1KB, 2)
    Write-Host "TTS audio saved to $outPathFull ($lengthKb KB)"
    Write-Host "TTS smoke test completed successfully."
} catch {
    Write-Error "TTS smoke test failed: $($_.Exception.Message)"
    exit 1
}

