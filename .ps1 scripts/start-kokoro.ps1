# Start Kokoro TTS from a Kokoro-FastAPI clone (run their start-cpu.ps1).
# kokoro-fastapi is NOT on PyPI; use Docker or this script with a clone.
# Usage: .\start-kokoro.ps1 -ScriptDir "C:\path\to\repo" -KokoroCloneDir "C:\path\to\Kokoro-FastAPI" [-Port 8880]

param(
    [Parameter(Mandatory = $true)]
    [string] $ScriptDir,
    [Parameter(Mandatory = $true)]
    [string] $KokoroCloneDir,
    [int] $Port = 8880
)

$logPath = Join-Path $ScriptDir "Media\kokoro.log"
$mediaDir = Join-Path $ScriptDir "Media"
if (-not (Test-Path $mediaDir)) { New-Item -ItemType Directory -Path $mediaDir -Force | Out-Null }

"--- Kokoro start $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') (from clone) ---" | Add-Content -Path $logPath
"Clone: $KokoroCloneDir" | Add-Content -Path $logPath

$startPs1 = Join-Path $KokoroCloneDir "start-cpu.ps1"
if (-not (Test-Path -LiteralPath $startPs1)) {
    "start-cpu.ps1 not found at $startPs1" | Add-Content -Path $logPath
    exit 1
}

try {
    Set-Location -LiteralPath $KokoroCloneDir
    & $startPs1 *>> $logPath 2>&1
} catch {
    $_.ToString() | Add-Content -Path $logPath
}
