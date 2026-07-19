# Shared helpers for House Victoria stack start/stop scripts.
# Dot-source: . (Join-Path $PSScriptRoot 'HV-StackCommon.ps1')

Set-StrictMode -Version Latest

function Get-HVRepoRoot {
    param([string]$CallerPath = $PSScriptRoot)
    if (Test-Path (Join-Path $CallerPath 'HouseVictoria.sln')) {
        return (Resolve-Path $CallerPath).Path
    }
    $parent = Split-Path -Parent $CallerPath
    if (Test-Path (Join-Path $parent 'HouseVictoria.sln')) {
        return (Resolve-Path $parent).Path
    }
    throw "Could not locate House Victoria repo root from $CallerPath"
}

function Test-HVPortListening {
    param([int]$Port)
    try {
        return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
    } catch {
        return $false
    }
}

function Stop-HVByPort {
    param(
        [int]$Port,
        [string]$Label
    )
    $stopped = $false
    try {
        $conns = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
        foreach ($pid in ($conns.OwningProcess | Sort-Object -Unique)) {
            $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "  stopping $Label (PID $pid - $($proc.ProcessName))"
                Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
                $stopped = $true
            }
        }
    } catch {
        # ignore
    }
    return $stopped
}

function Wait-HVHttpOk {
    param(
        [string]$Url,
        [int]$TimeoutSec = 20
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) {
                return $true
            }
        } catch {
            # still starting
        }
        Start-Sleep -Milliseconds 600
    }
    return $false
}

function Write-HVServiceLine {
    param(
        [string]$Name,
        [string]$Detail,
        [ValidateSet('ok', 'skip', 'warn', 'fail')]
        [string]$Status = 'ok'
    )
    $tag = switch ($Status) {
        'ok' { '[OK]  ' }
        'skip' { '[--]  ' }
        'warn' { '[WARN]' }
        'fail' { '[FAIL]' }
    }
    $color = switch ($Status) {
        'ok' { 'Green' }
        'skip' { 'DarkGray' }
        'warn' { 'Yellow' }
        'fail' { 'Red' }
    }
    Write-Host "$tag $Name" -NoNewline -ForegroundColor $color
    if ($Detail) {
        Write-Host "  $Detail"
    } else {
        Write-Host ''
    }
}

function Get-HVPrimaryLlm {
    param([string]$RepoRoot)
    $path = Join-Path $RepoRoot 'primary-llm.txt'
    if (Test-Path $path) {
        $val = (Get-Content $path -Raw).Trim().ToLowerInvariant()
        if ($val) { return $val }
    }
    return 'hermes'
}

function Start-HVOllama {
    param([string]$RepoRoot)
    if (Test-HVPortListening -Port 11434) {
        Write-HVServiceLine 'Ollama' ':11434 (already running)'
        return $true
    }
    $ollama = Get-Command ollama -ErrorAction SilentlyContinue
    if (-not $ollama) {
        Write-HVServiceLine 'Ollama' 'not in PATH' 'warn'
        return $false
    }
    Start-Process -FilePath $ollama.Source -ArgumentList 'serve' -WindowStyle Hidden | Out-Null
    Start-Sleep -Seconds 2
    if (Test-HVPortListening -Port 11434) {
        Write-HVServiceLine 'Ollama' ':11434'
        return $true
    }
    Write-HVServiceLine 'Ollama' 'failed to bind :11434' 'fail'
    return $false
}

function Start-HVLoggedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [string]$Args = '',
        [Parameter(Mandatory = $true)][string]$WorkDir,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [string]$CmdPrefix = ''
    )

    $logDir = Split-Path -Parent $LogPath
    if ($logDir -and -not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }

    # Start-Process cannot redirect stdout and stderr to the same file; use cmd merge instead.
    # The full /c command must be one ArgumentList string — separate '/c' + command breaks >> redirects.
    $inner = if ($CmdPrefix) { "$CmdPrefix && " } else { '' }
    $inner += "`"$Exe`" $Args >> `"$LogPath`" 2>&1"
    return Start-Process -FilePath 'cmd.exe' -ArgumentList "/c `"$inner`"" `
        -WorkingDirectory $WorkDir -WindowStyle Hidden -PassThru
}

function Start-HVLmStudio {
    param([string]$RepoRoot)

    if (Test-HVPortListening -Port 1234) {
        Write-HVServiceLine 'LM Studio' ':1234 (already running)'
        return $true
    }

    $lms = Get-Command lms -ErrorAction SilentlyContinue
    if (-not $lms) {
        Write-HVServiceLine 'LM Studio' 'lms CLI not in PATH - start from System Monitor' 'warn'
        return $false
    }

    $mediaDir = Join-Path $RepoRoot 'Media'
    if (-not (Test-Path $mediaDir)) { New-Item -ItemType Directory -Path $mediaDir -Force | Out-Null }
    $log = Join-Path $mediaDir 'lmstudio-server.log'

    Start-HVLoggedProcess -Exe $lms.Source -Args 'server start --port 1234' -WorkDir $RepoRoot -LogPath $log | Out-Null
    Start-Sleep -Seconds 2

    if (Test-HVPortListening -Port 1234) {
        Write-HVServiceLine 'LM Studio' ':1234'
        return $true
    }
    Write-HVServiceLine 'LM Studio' 'failed to start' 'warn'
    return $false
}

function Start-HVHermesGateway {
    param([string]$RepoRoot)

    . (Join-Path $RepoRoot 'Tools\HermesShared.ps1')
    $hermesExe = Get-HermesExe
    if (-not $hermesExe) {
        Write-HVServiceLine 'Hermes gateway' 'hermes CLI not found - run Tools\setup-hermes-integration.ps1' 'fail'
        return $false
    }

    if (Test-HVPortListening -Port 8642) {
        if (Wait-HVHttpOk 'http://127.0.0.1:8642/health' -TimeoutSec 3) {
            Write-HVServiceLine 'Hermes gateway' ':8642 (already running)'
            return $true
        }
        Stop-HVByPort -Port 8642 -Label 'stale Hermes gateway' | Out-Null
        Start-Sleep -Seconds 1
    }

    $mediaDir = Join-Path $RepoRoot 'Media'
    if (-not (Test-Path $mediaDir)) {
        New-Item -ItemType Directory -Path $mediaDir -Force | Out-Null
    }
    $log = Join-Path $mediaDir 'hermes-gateway.log'

    $p = Start-HVLoggedProcess -Exe $hermesExe -Args 'gateway' -WorkDir $RepoRoot -LogPath $log `
        -CmdPrefix 'set PYTHONPATH=&& set VIRTUAL_ENV=&& set VIRTUAL_ENV_PROMPT='
    Start-Sleep -Seconds 2

    if (Wait-HVHttpOk 'http://127.0.0.1:8642/health' -TimeoutSec 25) {
        Write-HVServiceLine 'Hermes gateway' ":8642 (PID $($p.Id))"
        return $true
    }

    Write-HVServiceLine 'Hermes gateway' "not healthy - see Media\hermes-gateway.log" 'fail'
    return $false
}

function Start-HVMcpServer {
    param([string]$RepoRoot)

    if (Test-HVPortListening -Port 8080) {
        if (Wait-HVHttpOk 'http://127.0.0.1:8080/health' -TimeoutSec 3) {
            Write-HVServiceLine 'MCP server' ':8080 (already running)'
            return $true
        }
        Stop-HVByPort -Port 8080 -Label 'stale MCP' | Out-Null
        Start-Sleep -Seconds 1
    }

    $python = Join-Path $RepoRoot 'MCPServer\.venv\Scripts\python.exe'
    $server = Join-Path $RepoRoot 'MCPServer\http_server.py'
    if (-not (Test-Path $python)) {
        Write-HVServiceLine 'MCP server' 'venv missing - run install.bat' 'fail'
        return $false
    }

    $logDir = Join-Path $RepoRoot 'MCPServer\logs'
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    $log = Join-Path $logDir 'http_server.log'

    Start-HVLoggedProcess -Exe $python -Args "`"$server`"" -WorkDir (Join-Path $RepoRoot 'MCPServer') -LogPath $log | Out-Null
    Start-Sleep -Seconds 2

    if (Wait-HVHttpOk 'http://127.0.0.1:8080/health' -TimeoutSec 15) {
        Write-HVServiceLine 'MCP server' ':8080'
        return $true
    }
    Write-HVServiceLine 'MCP server' 'health check failed - see MCPServer\logs' 'fail'
    return $false
}

function Start-HVSttServer {
    param([string]$RepoRoot)

    if (Test-HVPortListening -Port 8000) {
        Write-HVServiceLine 'STT server' ':8000 (already running)'
        return $true
    }

    $python = Join-Path $RepoRoot 'MCPServer\.venv\Scripts\python.exe'
    $sttApp = Join-Path $RepoRoot 'STTServer\app.py'
    if (-not (Test-Path $sttApp)) {
        Write-HVServiceLine 'STT server' 'STTServer\app.py missing' 'skip'
        return $false
    }
    if (-not (Test-Path $python)) {
        Write-HVServiceLine 'STT server' 'venv missing' 'skip'
        return $false
    }

    $mediaDir = Join-Path $RepoRoot 'Media'
    if (-not (Test-Path $mediaDir)) { New-Item -ItemType Directory -Path $mediaDir -Force | Out-Null }
    $log = Join-Path $mediaDir 'stt.log'

    Start-HVLoggedProcess -Exe $python `
        -Args '-m uvicorn STTServer.app:app --host 127.0.0.1 --port 8000' `
        -WorkDir $RepoRoot -LogPath $log | Out-Null
    Start-Sleep -Seconds 2

    if (Test-HVPortListening -Port 8000) {
        Write-HVServiceLine 'STT server' ':8000/transcribe'
        return $true
    }
    Write-HVServiceLine 'STT server' 'failed to start' 'warn'
    return $false
}

function Start-HVChatterbox {
    param([string]$RepoRoot)

    $script = Join-Path $RepoRoot '.ps1 scripts\start-chatterbox.ps1'
    if (-not (Test-Path $script)) {
        Write-HVServiceLine 'Chatterbox TTS' 'script missing' 'skip'
        return $false
    }
    if (Test-HVPortListening -Port 8881) {
        Write-HVServiceLine 'Chatterbox TTS' ':8881 (already running)'
        return $true
    }

    & $script -ScriptDir $RepoRoot | Out-Null
    Start-Sleep -Seconds 2
    if (Test-HVPortListening -Port 8881) {
        Write-HVServiceLine 'Chatterbox TTS' ':8881'
        return $true
    }
    Write-HVServiceLine 'Chatterbox TTS' 'failed to start - see Media\chatterbox.log' 'warn'
    return $false
}

function Start-HVBrowserCaptureBridge {
    param([string]$RepoRoot)

    $python = Join-Path $RepoRoot 'MCPServer\.venv\Scripts\python.exe'
    $bridge = Join-Path $RepoRoot 'BrowserCaptureBridge\bridge_server.py'
    if (-not (Test-Path $python) -or -not (Test-Path $bridge)) {
        Write-HVServiceLine 'Browser cast bridge' 'files missing' 'skip'
        return $false
    }

    if (Test-HVPortListening -Port 17891) {
        Stop-HVByPort -Port 17891 -Label 'browser cast bridge' | Out-Null
        Start-Sleep -Seconds 1
    }

    $mediaDir = Join-Path $RepoRoot 'Media'
    if (-not (Test-Path $mediaDir)) { New-Item -ItemType Directory -Path $mediaDir -Force | Out-Null }
    $log = Join-Path $mediaDir 'browser-capture-bridge.log'
    Start-HVLoggedProcess -Exe $python -Args "`"$bridge`"" -WorkDir $RepoRoot -LogPath $log | Out-Null
    Start-Sleep -Seconds 2

    if (Wait-HVHttpOk 'http://127.0.0.1:17891/health' -TimeoutSec 10) {
        Write-HVServiceLine 'Browser cast bridge' 'ws://127.0.0.1:17891/ws/cast'
        return $true
    }
    Write-HVServiceLine 'Browser cast bridge' 'health check failed' 'warn'
    return $false
}

function Start-HVComfyUI {
    param([string]$RepoRoot)

    if (Test-HVPortListening -Port 8188) {
        Write-HVServiceLine 'ComfyUI' ':8188 (already running)'
        return $true
    }

    $portablePath = $env:COMFYUI_PORTABLE_PATH
    if (-not $portablePath) {
        $pathFile = Join-Path $RepoRoot 'comfyui-portable-path.txt'
        if (Test-Path $pathFile) {
            $portablePath = (Get-Content $pathFile -Raw).Trim()
        }
    }

    $mediaDir = Join-Path $RepoRoot 'Media'
    if (-not (Test-Path $mediaDir)) { New-Item -ItemType Directory -Path $mediaDir -Force | Out-Null }
    $log = Join-Path $mediaDir 'comfyui.log'
    $launcher = Join-Path $RepoRoot 'comfyui-launcher.cmd'

    $extraModels = Join-Path $RepoRoot 'extra_model_paths_d_comfyui.yaml'

    if ($portablePath -and (Test-Path $portablePath)) {
        if (Test-Path $extraModels) {
            Copy-Item $extraModels (Join-Path $portablePath 'extra_model_paths.yaml') -Force
        }
        $mode = if (Test-Path (Join-Path $portablePath 'run_nvidia_gpu.bat')) { 'gpu' }
                elseif (Test-Path (Join-Path $portablePath 'run_cpu.bat')) { 'cpu' }
                else { $null }
        if ($mode) {
            Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', "`"$launcher`" $mode `"$portablePath`" `"$log`"" `
                -WorkingDirectory $RepoRoot -WindowStyle Hidden | Out-Null
            Start-Sleep -Seconds 3
            if (Test-HVPortListening -Port 8188) {
                Write-HVServiceLine 'ComfyUI' ':8188'
                return $true
            }
        }
    }

    $candidates = @(
        'C:\StabilityMatrix\Data\Packages\ComfyUI'
        (Join-Path $env:USERPROFILE 'ComfyUI')
        (Join-Path $env:LOCALAPPDATA 'ComfyUI')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate 'main.py')) {
            if (Test-Path $extraModels) {
                Copy-Item $extraModels (Join-Path $candidate 'extra_model_paths.yaml') -Force
            }
            Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', "`"$launcher`" main `"$candidate`" `"$log`"" `
                -WorkingDirectory $RepoRoot -WindowStyle Hidden | Out-Null
            Start-Sleep -Seconds 3
            if (Test-HVPortListening -Port 8188) {
                Write-HVServiceLine 'ComfyUI' ":8188 ($candidate)"
                return $true
            }
        }
    }

    $desktopExe = Join-Path $env:LOCALAPPDATA 'Programs\ComfyUI\ComfyUI.exe'
    if (Test-Path $desktopExe) {
        Start-Process -FilePath $desktopExe -WorkingDirectory (Split-Path $desktopExe) | Out-Null
        Start-Sleep -Seconds 3
        if (Test-HVPortListening -Port 8188) {
            Write-HVServiceLine 'ComfyUI' ':8188 (desktop app)'
            return $true
        }
    }

    Write-HVServiceLine 'ComfyUI' 'not configured (set comfyui-portable-path.txt or install desktop app)' 'skip'
    return $false
}

function Start-HVApp {
    param(
        [string]$RepoRoot,
        [switch]$Rebuild
    )

    Get-Process -Name 'HouseVictoria.App' -ErrorAction SilentlyContinue | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1

    $exe = Join-Path $RepoRoot 'HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe'
    $needsBuild = $Rebuild -or -not (Test-Path $exe)

    if ($needsBuild) {
        Write-Host ''
        Write-Host 'Building House Victoria (Release)...' -ForegroundColor Cyan
        dotnet build (Join-Path $RepoRoot 'HouseVictoria.sln') -c Release --nologo -v q
        if ($LASTEXITCODE -ne 0) {
            Write-HVServiceLine 'House Victoria app' 'Release build failed' 'fail'
            return $false
        }
    }

    if (-not (Test-Path $exe)) {
        $exe = Join-Path $RepoRoot 'HouseVictoria.App\bin\Debug\net8.0-windows\HouseVictoria.App.exe'
    }

    if (Test-Path $exe) {
        $p = Start-Process -FilePath $exe -PassThru
        Write-HVServiceLine 'House Victoria app' "PID $($p.Id)"
        return $true
    }

    Write-HVServiceLine 'House Victoria app' 'no built exe - run install.bat' 'fail'
    return $false
}

function Get-HVStackStatus {
    param([string]$RepoRoot)

    $rows = @(
        @{ Name = 'Ollama'; Port = 11434; Url = $null }
        @{ Name = 'Hermes gateway'; Port = 8642; Url = 'http://127.0.0.1:8642/health' }
        @{ Name = 'MCP server'; Port = 8080; Url = 'http://127.0.0.1:8080/health' }
        @{ Name = 'STT server'; Port = 8000; Url = $null }
        @{ Name = 'Chatterbox TTS'; Port = 8881; Url = $null }
        @{ Name = 'Browser cast'; Port = 17891; Url = 'http://127.0.0.1:17891/health' }
        @{ Name = 'ComfyUI'; Port = 8188; Url = $null }
        @{ Name = 'Remote companion'; Port = 17890; Url = 'http://127.0.0.1:17890/health' }
    )

    foreach ($row in $rows) {
        $up = Test-HVPortListening -Port $row.Port
        if ($up -and $row.Url) {
            $up = Wait-HVHttpOk $row.Url -TimeoutSec 2
        }
        $status = if ($up) { 'ok' } else { 'skip' }
        Write-HVServiceLine $row.Name (":$($row.Port)") $status
    }
}
