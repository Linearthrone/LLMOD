# Stop the Kokoro TTS server (process listening on port 8880).
# Run PowerShell as Administrator if you get "Access denied".

$port = 8880
$found = $false

try {
    $conn = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($conn) {
        $pids = $conn.OwningProcess | Sort-Object -Unique
        foreach ($pid in $pids) {
            $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "Stopping Kokoro TTS (PID $pid - $($proc.ProcessName))..."
                Stop-Process -Id $pid -Force
                $found = $true
            }
        }
    }
} catch {
    Write-Host "Error: $_"
}

if (-not $found) {
    $netstat = netstat -ano
    $line = $netstat | Select-String ":$port\s"
    if ($line) {
        $parts = ($line -replace '\s+', ' ').ToString().Trim().Split(' ')
        $pid = $parts[-1]
        if ($pid -match '^\d+$') {
            Write-Host "Stopping process PID $pid..."
            try {
                Stop-Process -Id $pid -Force -ErrorAction Stop
                $found = $true
            } catch {
                Write-Host "Could not stop PID $pid. Try: Right-click PowerShell -> Run as Administrator, then run this script again."
            }
        }
    }
}

if ($found) {
    Write-Host "Kokoro TTS stopped."
} else {
    Write-Host "No process found listening on port $port (Kokoro may already be stopped)."
}
