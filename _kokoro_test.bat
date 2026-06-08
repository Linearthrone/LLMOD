@echo off
setlocal enabledelayedexpansion
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "KOKORO_PORT=8880"

echo Starting Kokoro TTS...
netstat -an | findstr /C:":%KOKORO_PORT%" | findstr /C:"LISTENING" >nul 2>&1
if not errorlevel 1 (
    echo [INFO] Kokoro TTS already on port %KOKORO_PORT%. Skipping.
) else (
    set "KOKORO_STARTED=0"
    where docker >nul 2>&1
    if not errorlevel 1 (
        echo [INFO] Starting Kokoro TTS via Docker...
        start "Kokoro TTS" /B /D "%SCRIPT_DIR%" cmd /c "docker run --rm -p %KOKORO_PORT%:%KOKORO_PORT% ghcr.io/remsky/kokoro-fastapi-cpu:latest >> Media\kokoro.log 2>&1"
        set "KOKORO_STARTED=1"
    )
    if "!KOKORO_STARTED!"=="0" (
        set "KOKORO_CLONE=%SCRIPT_DIR%\Kokoro-FastAPI"
        if exist "!KOKORO_CLONE!\start-cpu.ps1" (
            echo [INFO] Starting Kokoro TTS from clone...
            set "KOKORO_PS1=%SCRIPT_DIR%\.ps1 scripts\start-kokoro.ps1"
            start "Kokoro TTS" /B /D "%SCRIPT_DIR%" powershell -NoProfile -ExecutionPolicy Bypass -File "!KOKORO_PS1!" -ScriptDir "%SCRIPT_DIR%" -KokoroCloneDir "!KOKORO_CLONE!" -Port %KOKORO_PORT%
            set "KOKORO_STARTED=1"
        )
    )
    if "!KOKORO_STARTED!"=="1" (
        timeout /t 2 /nobreak >nul
        echo [OK] Kokoro TTS - http://localhost:%KOKORO_PORT%
    ) else (
        echo [INFO] Kokoro TTS skipped. kokoro-fastapi is not on PyPI. Use: Docker ^(docker run -p 8880:8880 ghcr.io/remsky/kokoro-fastapi-cpu:latest^) or clone https://github.com/remsky/Kokoro-FastAPI and run start-cpu.ps1 from the clone.
    )
)
echo DONE
