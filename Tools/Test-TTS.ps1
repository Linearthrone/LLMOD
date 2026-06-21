param(
    [string]$Text = "This is a test of the House Victoria Chatterbox text to speech service.",
    [string]$OutputPath = "Media/Test-TTS.wav",
    [string]$Voice = "default"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== House Victoria TTS Smoke Test ==="

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
Write-Host "Repo root: $repoRoot"

$appConfigPath = Join-Path $repoRoot "HouseVictoria.App\App.config"
$ttsEndpoint = "http://localhost:8881"
if (Test-Path $appConfigPath) {
    [xml]$configXml = Get-Content $appConfigPath
    $settings = $configXml.configuration.appSettings.add
    $ttsSetting = $settings | Where-Object { $_.key -eq "TTSEndpoint" }
    if ($ttsSetting -and $ttsSetting.value) {
        $ttsEndpoint = $ttsSetting.value
    }
}

$ttsEndpoint = $ttsEndpoint.TrimEnd("/")
Write-Host "Using TTSEndpoint: $ttsEndpoint"

try {
    Write-Host "Checking TTS health at $ttsEndpoint/health ..."
    $healthResponse = Invoke-WebRequest -UseBasicParsing -Uri "$ttsEndpoint/health" -TimeoutSec 10
    Write-Host "Health status: $($healthResponse.StatusCode)"
} catch {
    Write-Warning "Health check failed: $($_.Exception.Message). Is Chatterbox running? Run install.bat then start.bat."
}

try {
    Write-Host "Requesting synthesis (voice=$Voice)..."
    $body = @{ text = $Text; voice = $Voice } | ConvertTo-Json -Depth 3
    $response = Invoke-WebRequest -UseBasicParsing -Uri "$ttsEndpoint/" -Method Post -ContentType "application/json" -Body $body -TimeoutSec 120
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
