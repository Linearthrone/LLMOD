param(
    [Parameter(Mandatory = $true)]
    [string]$AudioPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== House Victoria STT Smoke Test ==="

if (-not (Test-Path $AudioPath)) {
    throw "Audio file not found: $AudioPath"
}

# Resolve repo root based on script location
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
Write-Host "Repo root: $repoRoot"

# Load App.config to get STTEndpoint
$appConfigPath = Join-Path $repoRoot "HouseVictoria.App\App.config"
if (-not (Test-Path $appConfigPath)) {
    Write-Warning "App.config not found at $appConfigPath. Falling back to default STT endpoint http://localhost:8000/transcribe"
    $sttEndpoint = "http://localhost:8000/transcribe"
} else {
    [xml]$configXml = Get-Content $appConfigPath
    $settings = $configXml.configuration.appSettings.add
    $sttSetting = $settings | Where-Object { $_.key -eq "STTEndpoint" }
    $sttEndpoint = if ($sttSetting -and $sttSetting.value) { $sttSetting.value } else { "http://localhost:8000/transcribe" }
}

$sttEndpoint = $sttEndpoint.TrimEnd("/")
Write-Host "Using STTEndpoint: $sttEndpoint"

try {
    # Derive /health URL from /transcribe-style endpoint
    $baseUrl = $sttEndpoint
    if ($baseUrl.ToLower().EndsWith("/transcribe")) {
        $baseUrl = $baseUrl.Substring(0, $baseUrl.Length - "/transcribe".Length).TrimEnd("/")
    }
    $healthUrl = "$baseUrl/health"
    Write-Host "Checking STT health at $healthUrl ..."
    $healthResponse = Invoke-WebRequest -UseBasicParsing -Uri $healthUrl -TimeoutSec 5
    Write-Host "Health status: $($healthResponse.StatusCode)"
} catch {
    Write-Warning "Health check failed: $($_.Exception.Message). Continuing with transcription attempt."
}

try {
    Write-Host "Sending audio for transcription..."
    $fileBytes = [IO.File]::ReadAllBytes((Resolve-Path $AudioPath))

    $boundary = [System.Guid]::NewGuid().ToString()
    $newline = "`r`n"
    $bodyBuilder = New-Object System.Text.StringBuilder

    $bodyBuilder.Append("--$boundary$newline") | Out-Null
    $bodyBuilder.Append("Content-Disposition: form-data; name=`"audio`"; filename=`"audio.wav`"$newline") | Out-Null
    $bodyBuilder.Append("Content-Type: audio/wav$newline$newline") | Out-Null
    $fileHeaderBytes = [System.Text.Encoding]::ASCII.GetBytes($bodyBuilder.ToString())

    $footer = "$newline--$boundary--$newline"
    $footerBytes = [System.Text.Encoding]::ASCII.GetBytes($footer)

    $stream = New-Object System.IO.MemoryStream
    $stream.Write($fileHeaderBytes, 0, $fileHeaderBytes.Length)
    $stream.Write($fileBytes, 0, $fileBytes.Length)
    $stream.Write($footerBytes, 0, $footerBytes.Length)
    $stream.Position = 0

    $contentType = "multipart/form-data; boundary=$boundary"
    $response = Invoke-WebRequest -UseBasicParsing -Uri $sttEndpoint -Method Post -ContentType $contentType -InFile $stream -TimeoutSec 60

    if (-not $response.Content) {
        throw "STT response had no content."
    }

    $text = $null
    try {
        $json = $response.Content | ConvertFrom-Json
        if ($json.text) { $text = $json.text }
        elseif ($json.transcription) { $text = $json.transcription }
    } catch {
        $text = $response.Content.Trim()
    }

    if (-not $text) {
        Write-Warning "STT returned empty transcription."
    } else {
        Write-Host "Transcription:"
        Write-Host "----------------------"
        Write-Host $text
        Write-Host "----------------------"
    }

    Write-Host "STT smoke test completed."
} catch {
    Write-Error "STT smoke test failed: $($_.Exception.Message)"
    exit 1
}

