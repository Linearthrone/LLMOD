param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== House Victoria - Stop All Services ==="
Write-Host

# Resolve script and repo paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Split-Path -Parent $scriptDir

function Stop-ByPort {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $found = $false

    try {
        $conn = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
        if ($conn) {
            $portPids = $conn.OwningProcess | Sort-Object -Unique
            foreach ($portPid in $portPids) {
                $proc = Get-Process -Id $portPid -ErrorAction SilentlyContinue
                if ($proc) {
                    Write-Host "Stopping $Label (PID $portPid - $($proc.ProcessName))..."
                    Stop-Process -Id $portPid -Force -ErrorAction SilentlyContinue
                    $found = $true
                }
            }
        }
    } catch {
        Write-Host "[$Label] Error during Get-NetTCPConnection: $_"
    }

    if (-not $found) {
        # Fallback: netstat parsing
        try {
            $netstat = netstat -ano
            $line = $netstat | Select-String ":$Port\s"
            if ($line) {
                $parts = ($line -replace '\s+', ' ').ToString().Trim().Split(' ')
                $portPid = $parts[-1]
                if ($portPid -match '^\d+$') {
                    Write-Host "Stopping $Label via netstat PID $portPid..."
                    try {
                        Stop-Process -Id $portPid -Force -ErrorAction Stop
                        $found = $true
                    } catch {
                        Write-Host "[$Label] Could not stop PID $portPid. Try running PowerShell as Administrator."
                    }
                }
            }
        } catch {
            Write-Host "[$Label] Error during netstat fallback: $_"
        }
    }

    if ($found) {
        Write-Host "$Label stopped."
    } else {
        Write-Host "No process found listening on port $Port for $Label (it may already be stopped)."
    }
}

#
# Kokoro TTS (port 8880)
#
try {
    $kokoroScript = Join-Path $scriptDir "stop-kokoro.ps1"
    if (Test-Path $kokoroScript) {
        Write-Host "Stopping Kokoro TTS via stop-kokoro.ps1..."
        & $kokoroScript
        Write-Host
    } else {
        Stop-ByPort -Port 8880 -Label "Kokoro TTS"
        Write-Host
    }
} catch {
    Write-Host "Error while stopping Kokoro TTS: $_"
    Write-Host
}

#
# Piper TTS (port 5000) and STT server (port 8000) - reuse existing scripts
#
try {
    $piperScript = Join-Path $scriptDir "stop-piper.ps1"
    if (Test-Path $piperScript) {
        Write-Host "Stopping Piper TTS via stop-piper.ps1..."
        & $piperScript
        Write-Host
    } else {
        Write-Host "stop-piper.ps1 not found; falling back to port-based stop."
        Stop-ByPort -Port 5000 -Label "Piper TTS"
        Write-Host
    }
} catch {
    Write-Host "Error while stopping Piper TTS: $_"
    Write-Host
}

try {
    $sttScript = Join-Path $scriptDir "stop-stt.ps1"
    if (Test-Path $sttScript) {
        Write-Host "Stopping STT Server via stop-stt.ps1..."
        & $sttScript
        Write-Host
    } else {
        Write-Host "stop-stt.ps1 not found; falling back to port-based stop."
        Stop-ByPort -Port 8000 -Label "STT Server"
        Write-Host
    }
} catch {
    Write-Host "Error while stopping STT Server: $_"
    Write-Host
}

#
# MCP Server (port 8080)
#
Write-Host "Stopping MCP Server (port 8080)..."
Stop-ByPort -Port 8080 -Label "MCP Server"
Write-Host

#
# LM Studio server (default port 1234)
#
Write-Host "Stopping LM Studio server (port 1234)..."
Stop-ByPort -Port 1234 -Label "LM Studio server"
Write-Host

#
# Ollama (default port 11434, process 'ollama')
#
Write-Host "Stopping Ollama server (port 11434 / process ollama)..."
Stop-ByPort -Port 11434 -Label "Ollama server"
try {
    $ollamaProc = Get-Process -Name "ollama" -ErrorAction SilentlyContinue
    if ($ollamaProc) {
        foreach ($p in $ollamaProc) {
            Write-Host "Stopping Ollama process (PID $($p.Id))..."
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        }
        Write-Host "Ollama processes stopped (if any were running)."
    }
} catch {
    Write-Host "Error while stopping Ollama processes: $_"
}
Write-Host

#
# ComfyUI (default port 8188)
#
Write-Host "Stopping ComfyUI (port 8188)..."
Stop-ByPort -Port 8188 -Label "ComfyUI"
Write-Host

#
# House Victoria desktop app (HouseVictoria.App.exe)
#
Write-Host "Stopping House Victoria app (HouseVictoria.App.exe)..."
try {
    $hvProcs = Get-Process -Name "HouseVictoria.App" -ErrorAction SilentlyContinue
    if ($hvProcs) {
        foreach ($p in $hvProcs) {
            Write-Host "Stopping House Victoria app (PID $($p.Id))..."
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        }
        Write-Host "House Victoria app stopped."
    } else {
        Write-Host "No HouseVictoria.App process found (app may already be closed)."
    }
} catch {
    Write-Host "Error while stopping House Victoria app: $_"
}
Write-Host

#
# Stability Matrix (best-effort, based on window title)
#
Write-Host "Stopping Stability Matrix (best-effort)..."
try {
    $stabProcs = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.MainWindowTitle -like "*Stability Matrix*"
    }
    if ($stabProcs) {
        foreach ($p in $stabProcs) {
            Write-Host "Stopping Stability Matrix process (PID $($p.Id) - $($p.ProcessName))..."
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        }
        Write-Host "Stability Matrix processes stopped (if any were running)."
    } else {
        Write-Host "No Stability Matrix windowed process found."
    }
} catch {
    Write-Host "Error while stopping Stability Matrix: $_"
}
Write-Host

Write-Host "=== Stop All complete ==="

