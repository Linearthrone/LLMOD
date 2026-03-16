# Stop the STT server (process listening on port 8000).
# Run PowerShell as Administrator if you get "Access denied".

$port = 8000
$found = $false

try {
    $conn = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($conn) {
        $sttPids = $conn.OwningProcess | Sort-Object -Unique
        foreach ($sttPid in $sttPids) {
            $proc = Get-Process -Id $sttPid -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "Stopping STT Server (PID $sttPid - $($proc.ProcessName))..."
                Stop-Process -Id $sttPid -Force
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
        $sttPid = $parts[-1]
        if ($sttPid -match '^\d+$') {
            Write-Host "Stopping process PID $sttPid..."
            try {
                Stop-Process -Id $sttPid -Force -ErrorAction Stop
                $found = $true
            } catch {
                Write-Host "Could not stop PID $sttPid. Try: Right-click PowerShell -> Run as Administrator, then run this script again."
            }
        }
    }
}

if ($found) {
    Write-Host "STT Server stopped."
} else {
    Write-Host "No process found listening on port $port (STT Server may already be stopped)."
}
