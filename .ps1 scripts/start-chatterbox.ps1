# Start Chatterbox Turbo TTS (port 8881) using the MCP venv.
param(
    [string] $ScriptDir = "",
    [int] $Port = 8881
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ScriptDir)) {
    $ScriptDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

$repoRoot = $ScriptDir
$mediaDir = Join-Path $repoRoot "Media"
$logPath = Join-Path $mediaDir "chatterbox.log"
$errLogPath = Join-Path $mediaDir "chatterbox.err.log"
$voicesDir = Join-Path $repoRoot "Media\ChatterboxVoices"
$python = Join-Path $repoRoot "MCPServer\.venv\Scripts\python.exe"
$server = Join-Path $repoRoot "ChatterboxServer\chatterbox_server.py"

if (-not (Test-Path $mediaDir)) {
    New-Item -ItemType Directory -Path $mediaDir -Force | Out-Null
}
if (-not (Test-Path $voicesDir)) {
    New-Item -ItemType Directory -Path $voicesDir -Force | Out-Null
}

function Test-PortListening {
    param([int]$LocalPort)
    try {
        $conn = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue
        return [bool]$conn
    } catch {
        return $false
    }
}

if (Test-PortListening -LocalPort $Port) {
    Write-Host "[INFO] Chatterbox TTS already listening on port $Port."
    exit 0
}

if (-not (Test-Path $python)) {
    Write-Error "Python venv not found at $python. Run install.bat first."
    exit 1
}
if (-not (Test-Path $server)) {
    Write-Error "Server script not found at $server"
    exit 1
}

"--- Chatterbox start $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ---" | Add-Content -Path $logPath

$env:CHATTERBOX_PORT = "$Port"
$env:CHATTERBOX_VOICES_DIR = $voicesDir

Start-Process -FilePath $python `
    -ArgumentList @("`"$server`"") `
    -WorkingDirectory $repoRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput $logPath `
    -RedirectStandardError $errLogPath

Start-Sleep -Seconds 2
Write-Host "[OK] Chatterbox TTS starting - http://127.0.0.1:$Port (log: Media\chatterbox.log)"
