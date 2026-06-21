param(
    [int] $Port = 8881
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

Write-Host "Stopping Chatterbox TTS (port $Port)..."

$found = $false
try {
    $conn = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    if ($conn) {
        foreach ($pid in ($conn.OwningProcess | Sort-Object -Unique)) {
            $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "Stopping Chatterbox TTS (PID $pid - $($proc.ProcessName))..."
                Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
                $found = $true
            }
        }
    }
} catch {
    Write-Host "Error during Get-NetTCPConnection: $_"
}

if (-not $found) {
    Write-Host "No process found on port $Port (Chatterbox may already be stopped)."
} else {
    Write-Host "Chatterbox TTS stopped."
}
