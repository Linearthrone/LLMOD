# Stop the STT server (process listening on port 8000).
# Run PowerShell as Administrator if you get "Access denied".

$port = 8000
$found = $false

try {
    $conn = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($conn) {
        $pids = $conn.OwningProcess | Sort-Object -Unique
        foreach ($pid in $pids) {
            $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "Stopping STT Server (PID $pid - $($proc.ProcessName))..."
                Stop-Process -Id $pid -Force
                $found = $true
            }
        }
    }
} catch {
    Write-Host "Error: $_"
}

if (-not $found) {
    # Fallback: try netstat (last column is PID)
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
    Write-Host "STT Server stopped."
} else {
    Write-Host "No process found listening on port $port (STT Server may already be stopped)."
}
