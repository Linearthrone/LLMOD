# Stop the Piper TTS server (process listening on port 5000).
# Run PowerShell as Administrator if you get "Access denied".

$port = 5000
$found = $false

try {
    $conn = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($conn) {
        $piperPids = $conn.OwningProcess | Sort-Object -Unique
        foreach ($piperPid in $piperPids) {
            $proc = Get-Process -Id $piperPid -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "Stopping Piper TTS (PID $piperPid - $($proc.ProcessName))..."
                Stop-Process -Id $piperPid -Force
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
        $piperPid = $parts[-1]
        if ($piperPid -match '^\d+$') {
            Write-Host "Stopping process PID $piperPid..."
            try {
                Stop-Process -Id $piperPid -Force -ErrorAction Stop
                $found = $true
            } catch {
                Write-Host "Could not stop PID $piperPid. Try: Right-click PowerShell -> Run as Administrator, then run this script again."
            }
        }
    }
}

if ($found) {
    Write-Host "Piper TTS stopped."
} else {
    Write-Host "No process found listening on port $port (Piper may already be stopped)."
}
