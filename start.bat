@echo off
setlocal enabledelayedexpansion
REM House Victoria - start all services and the app. Run install.bat first if not done.

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "MCP_PATH=%SCRIPT_DIR%\MCPServer"
set "STT_PORT=8000"
cd /d "%SCRIPT_DIR%"

echo.
echo === House Victoria - Start ===
echo.

REM --- Primary LLM Server only (Ollama, LM Studio, or Anything LLM) ---
set "PRIMARY_LLM=ollama"
if exist "%SCRIPT_DIR%\primary-llm.txt" (
    set /p PRIMARY_LLM=<"%SCRIPT_DIR%\primary-llm.txt"
    set "PRIMARY_LLM=%PRIMARY_LLM: =%"
)
if /i "%PRIMARY_LLM%"=="" set "PRIMARY_LLM=ollama"

if /i "%PRIMARY_LLM%"=="ollama" (
    echo Starting primary LLM: Ollama...
    where ollama >nul 2>&1
    if not errorlevel 1 (
        start "" /B ollama serve >nul 2>&1
        timeout /t 2 /nobreak >nul
        echo [OK] Ollama - http://localhost:11434
    ) else (
        echo [INFO] Ollama not in PATH. Start manually from System Monitor if needed.
    )
) else if /i "%PRIMARY_LLM%"=="lmstudio" (
    echo Starting primary LLM: LM Studio server...
    where lms >nul 2>&1
    if errorlevel 1 (
        echo [INFO] LM Studio CLI ^(lms^) not found. Start manually from System Monitor if needed.
    ) else (
        netstat -an | findstr /C:":1234" | findstr /C:"LISTENING" >nul 2>&1
        if not errorlevel 1 (
            echo [INFO] LM Studio server already on port 1234. Skipping.
        ) else (
            if not exist "%SCRIPT_DIR%\Media" mkdir "%SCRIPT_DIR%\Media"
            start "LM Studio Server" /B /D "%SCRIPT_DIR%" cmd /c "lms server start --port 1234 >> Media\lmstudio-server.log 2>&1"
            timeout /t 2 /nobreak >nul
            echo [OK] LM Studio - http://localhost:1234
        )
    )
) else if /i "%PRIMARY_LLM%"=="anythingllm" (
    echo [INFO] Anything LLM: Start manually from System Monitor or launch AnythingLLM desktop app.
) else if /i "%PRIMARY_LLM%"=="hermes" (
    echo Starting primary LLM: Hermes Agent ^(Ollama backend + gateway^)...
    where ollama >nul 2>&1
    if not errorlevel 1 (
        start "" /B ollama serve >nul 2>&1
        timeout /t 2 /nobreak >nul
        echo [OK] Ollama backend - http://localhost:11434
    ) else (
        echo [WARN] Ollama not in PATH. Hermes needs a local LLM backend.
    )
    where hermes >nul 2>&1
    if errorlevel 1 (
        echo [WARN] Hermes CLI not in PATH. Run Tools\setup-hermes-integration.ps1 first.
    ) else (
        netstat -an | findstr /C:":8642" | findstr /C:"LISTENING" >nul 2>&1
        if not errorlevel 1 (
            echo [INFO] Hermes gateway already on port 8642. Skipping.
        ) else (
            if not exist "%SCRIPT_DIR%\Media" mkdir "%SCRIPT_DIR%\Media"
            start "Hermes Gateway" /B /D "%SCRIPT_DIR%" cmd /c "hermes gateway >> Media\hermes-gateway.log 2>&1"
            timeout /t 3 /nobreak >nul
            echo [OK] Hermes gateway - http://127.0.0.1:8642/v1
        )
    )
) else (
    echo [INFO] Unknown primary LLM '%PRIMARY_LLM%'. Defaulting to Ollama.
    where ollama >nul 2>&1
    if not errorlevel 1 (
        start "" /B ollama serve >nul 2>&1
        timeout /t 2 /nobreak >nul
        echo [OK] Ollama - http://localhost:11434
    )
)
echo.

REM --- MCP Server (port 8080) ---
echo Starting MCP Server...
if not exist "%MCP_PATH%\.venv\Scripts\python.exe" (
    echo [WARN] MCP venv missing. Run install.bat first.
) else (
    netstat -an | findstr /C:":8080" | findstr /C:"LISTENING" >nul 2>&1
    if not errorlevel 1 (
        echo [INFO] MCP Server already on port 8080. Skipping.
    ) else (
        if not exist "%MCP_PATH%\logs" mkdir "%MCP_PATH%\logs"
        start "MCP Server" /B /D "%MCP_PATH%" cmd /c ".venv\Scripts\python.exe http_server.py >> logs\http_server.log 2>&1"
        timeout /t 3 /nobreak >nul
        powershell -NoProfile -Command "try { (Invoke-WebRequest -Uri 'http://127.0.0.1:8080/health' -UseBasicParsing -TimeoutSec 5).StatusCode } catch { exit 1 }" >nul 2>&1
        if errorlevel 1 (
            echo [WARN] MCP Server failed health check on port 8080. See MCPServer\logs\http_server.log
        ) else (
            echo [OK] MCP Server - http://localhost:8080
        )
    )
)
echo.

REM --- STT (port 8000) — chat dictation + remote companion only; voice calls use the streaming engine ---
echo Starting STT Server...
if not exist "%SCRIPT_DIR%\STTServer\app.py" (
    echo [INFO] STTServer\app.py not found. Skipping.
) else if not exist "%MCP_PATH%\.venv\Scripts\python.exe" (
    echo [INFO] MCP venv missing. Run install.bat. Skipping STT.
) else (
    netstat -an | findstr /C:":%STT_PORT%" | findstr /C:"LISTENING" >nul 2>&1
    if not errorlevel 1 (
        echo [INFO] STT already on port %STT_PORT%. Skipping.
    ) else (
        if not exist "%SCRIPT_DIR%\Media" mkdir "%SCRIPT_DIR%\Media"
        start "STT Server" /B /D "%SCRIPT_DIR%" cmd /c "%MCP_PATH%\.venv\Scripts\python.exe -m uvicorn STTServer.app:app --host 127.0.0.1 --port %STT_PORT% >> Media\stt.log 2>&1"
        timeout /t 2 /nobreak >nul
        echo [OK] STT - http://localhost:%STT_PORT%/transcribe
    )
)
echo.

REM --- Stability Matrix (optional: set STABILITY_MATRIX_PATH to exe path) ---
if defined STABILITY_MATRIX_PATH (
    echo Starting Stability Matrix...
    if exist "%STABILITY_MATRIX_PATH%" (
        start "Stability Matrix" "" "%STABILITY_MATRIX_PATH%"
        timeout /t 2 /nobreak >nul
        echo [OK] Stability Matrix started.
    ) else (
        echo [WARN] STABILITY_MATRIX_PATH not found: %STABILITY_MATRIX_PATH%
    )
) else (
    echo [INFO] STABILITY_MATRIX_PATH not set. Start from Settings if needed.
)
echo.

REM --- ComfyUI (portable path from Settings or comfyui-portable-path.txt, or auto-discover) ---
REM Read ComfyUI path from file written by Settings (like primary-llm.txt)
if not defined COMFYUI_PORTABLE_PATH (
    if exist "%SCRIPT_DIR%\comfyui-portable-path.txt" (
        set /p COMFYUI_PORTABLE_PATH=<"%SCRIPT_DIR%\comfyui-portable-path.txt"
        set "COMFYUI_PORTABLE_PATH=!COMFYUI_PORTABLE_PATH: =!"
    )
)
if not exist "%SCRIPT_DIR%\Media" mkdir "%SCRIPT_DIR%\Media"
set "COMFYUI_LOG=%SCRIPT_DIR%\Media\comfyui.log"
set "COMFYUI_STARTED=0"
if defined COMFYUI_PORTABLE_PATH (
    echo Starting ComfyUI ^(portable^)...
    REM Ensure D:\ComfyUI\models and V:\models are loaded as extra models (copy config to ComfyUI root)
    if exist "%SCRIPT_DIR%\extra_model_paths_d_comfyui.yaml" (
        copy /Y "%SCRIPT_DIR%\extra_model_paths_d_comfyui.yaml" "%COMFYUI_PORTABLE_PATH%\extra_model_paths.yaml" >nul 2>&1
    )
    if exist "%COMFYUI_PORTABLE_PATH%\run_nvidia_gpu.bat" (
        start "ComfyUI" /B "%SCRIPT_DIR%\comfyui-launcher.cmd" gpu "!COMFYUI_PORTABLE_PATH!" "!COMFYUI_LOG!"
        timeout /t 2 /nobreak >nul
        echo [OK] ComfyUI starting - http://127.0.0.1:8188
        echo [INFO] ComfyUI log: Media\comfyui.log
        set "COMFYUI_STARTED=1"
    ) else if exist "%COMFYUI_PORTABLE_PATH%\run_cpu.bat" (
        start "ComfyUI" /B "%SCRIPT_DIR%\comfyui-launcher.cmd" cpu "!COMFYUI_PORTABLE_PATH!" "!COMFYUI_LOG!"
        timeout /t 2 /nobreak >nul
        echo [OK] ComfyUI ^(CPU^) starting - http://127.0.0.1:8188
        echo [INFO] ComfyUI log: Media\comfyui.log
        set "COMFYUI_STARTED=1"
    ) else (
        echo [WARN] run_nvidia_gpu.bat / run_cpu.bat not found in %COMFYUI_PORTABLE_PATH%
    )
)
if "!COMFYUI_STARTED!" == "0" (
    set "COMFYUI_FOUND=0"
    set "COMFYUI_PATH="
    set "COMFYUI_IS_EXE=0"
    set "COMFYUI_DESKTOP_EXE=%LOCALAPPDATA%\Programs\ComfyUI\ComfyUI.exe"
    if exist "!COMFYUI_DESKTOP_EXE!" (
        set "COMFYUI_PATH=!COMFYUI_DESKTOP_EXE!"
        set "COMFYUI_FOUND=1"
        set "COMFYUI_IS_EXE=1"
    ) else if exist "C:\StabilityMatrix\Data\Packages\ComfyUI\main.py" (
        set "COMFYUI_PATH=C:\StabilityMatrix\Data\Packages\ComfyUI"
        set "COMFYUI_FOUND=1"
    ) else if exist "%USERPROFILE%\ComfyUI\main.py" (
        set "COMFYUI_PATH=%USERPROFILE%\ComfyUI"
        set "COMFYUI_FOUND=1"
    ) else if exist "%LOCALAPPDATA%\ComfyUI\main.py" (
        set "COMFYUI_PATH=%LOCALAPPDATA%\ComfyUI"
        set "COMFYUI_FOUND=1"
    )
    if !COMFYUI_FOUND! == 1 (
        echo Starting ComfyUI...
        if !COMFYUI_IS_EXE! == 1 (
            REM ComfyUI desktop: ensure common custom-node deps (opencv-python, imageio-ffmpeg) in its venv
            set "COMFYUI_DESKTOP_VENV=%USERPROFILE%\Documents\ComfyUI\.venv\Scripts\python.exe"
            if exist "!COMFYUI_DESKTOP_VENV!" (
                "!COMFYUI_DESKTOP_VENV!" -c "import cv2; import imageio_ffmpeg" 2>nul
                if errorlevel 1 (
                    echo [INFO] One-time: installing OpenCV + imageio-ffmpeg for ComfyUI custom nodes...
                    "!COMFYUI_DESKTOP_VENV!" -m pip install opencv-python imageio-ffmpeg --disable-pip-version-check -q
                )
            )
            REM Start-Process detaches from this console so the Electron/Python backend does not flood this terminal
            powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%LOCALAPPDATA%\Programs\ComfyUI\ComfyUI.exe' -WorkingDirectory '%LOCALAPPDATA%\Programs\ComfyUI'"
        ) else (
            REM Ensure D:\ComfyUI\models and V:\models are loaded as extra models
            if exist "%SCRIPT_DIR%\extra_model_paths_d_comfyui.yaml" (
                copy /Y "%SCRIPT_DIR%\extra_model_paths_d_comfyui.yaml" "!COMFYUI_PATH!\extra_model_paths.yaml" >nul 2>&1
            )
            start "ComfyUI" /B "%SCRIPT_DIR%\comfyui-launcher.cmd" main "!COMFYUI_PATH!" "!COMFYUI_LOG!"
        )
        timeout /t 2 /nobreak >nul
        echo [OK] ComfyUI - http://localhost:8188
        if !COMFYUI_IS_EXE! == 1 (
            echo [INFO] Desktop app logs also go to: %USERPROFILE%\Documents\ComfyUI\user\comfyui.log
        ) else (
            echo [INFO] ComfyUI log: Media\comfyui.log
        )
        set "COMFYUI_STARTED=1"
    ) else (
        echo [INFO] ComfyUI not found. Set COMFYUI_PORTABLE_PATH in Settings or install to default locations.
    )
)
echo.

REM --- House Victoria App ---
echo Starting House Victoria App...
REM Ensure normal app launches are never forced into headless remote-only mode
REM by inherited shell environment variables.
set "HV_REMOTE_COMPANION_ONLY="
REM Ensure stale app instances don't keep old click-through behavior.
taskkill /IM HouseVictoria.App.exe /F >nul 2>&1
timeout /t 1 /nobreak >nul
echo Building House Victoria App (Release)...
dotnet build "%SCRIPT_DIR%\HouseVictoria.sln" -c Release
if errorlevel 1 (
    echo [ERROR] Release build failed. Fix errors above, then run start.bat again.
    echo [ERROR] Will attempt to start existing exe — fixes may be missing until build succeeds.
)
set "APP_EXE=%SCRIPT_DIR%\HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe"
if not exist "%APP_EXE%" set "APP_EXE=%SCRIPT_DIR%\HouseVictoria.App\bin\Debug\net8.0-windows\HouseVictoria.App.exe"
if exist "%APP_EXE%" (
    start "" "%APP_EXE%"
    echo [OK] House Victoria started.
) else (
    echo [INFO] No built exe. Starting with dotnet run...
    start "House Victoria" /D "%SCRIPT_DIR%" cmd /k "dotnet run --project HouseVictoria.App\HouseVictoria.App.csproj"
    timeout /t 3 /nobreak >nul
    echo [OK] House Victoria ^(dotnet run^) started.
)
echo.

echo === Services ===
echo   Ollama: http://localhost:11434  ^| MCP: http://localhost:8080
echo   Kokoro TTS: http://localhost:%KOKORO_PORT%  ^| Piper TTS: http://localhost:5000  ^| STT: http://localhost:%STT_PORT%/transcribe
echo   ComfyUI: http://localhost:8188 (if started)
echo   App: House Victoria
echo.
