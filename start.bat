@echo off
setlocal enabledelayedexpansion
REM House Victoria - start all services and the app. Run install.bat first if not done.

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "MCP_PATH=%SCRIPT_DIR%\MCPServer"
set "PIPER_DATA=%SCRIPT_DIR%\Media\PiperVoices"
set "PIPER_MODEL=en_US-amy-medium"
set "KOKORO_PORT=8880"
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
        timeout /t 2 /nobreak >nul
        echo [OK] MCP Server - http://localhost:8080
    )
)
echo.

REM --- Kokoro TTS (port 8880) ---
REM kokoro-fastapi is NOT on PyPI; use Docker or a local clone (see https://github.com/remsky/Kokoro-FastAPI).
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
echo.

REM --- Piper TTS (port 5000) ---
echo Starting Piper TTS...
if not exist "%SCRIPT_DIR%\PiperServer\piper_server.py" (
    echo [INFO] PiperServer\piper_server.py not found. Skipping.
) else if not exist "%MCP_PATH%\.venv\Scripts\python.exe" (
    echo [INFO] MCP venv missing. Run install.bat. Skipping Piper.
) else (
    netstat -an | findstr /C:":5000" | findstr /C:"LISTENING" >nul 2>&1
    if not errorlevel 1 (
        echo [INFO] Piper TTS already on port 5000. Skipping.
    ) else (
        if not exist "%SCRIPT_DIR%\Media" mkdir "%SCRIPT_DIR%\Media"
        start "Piper TTS" /B /D "%SCRIPT_DIR%" cmd /c "%MCP_PATH%\.venv\Scripts\python.exe PiperServer\piper_server.py --model %PIPER_MODEL% --port 5000 --data-dir "%PIPER_DATA%" --debug >> Media\piper.log 2>&1"
        timeout /t 2 /nobreak >nul
        echo [OK] Piper TTS - http://localhost:5000
    )
)
echo.

REM --- STT (port 8000) ---
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
set "COMFYUI_STARTED=0"
if defined COMFYUI_PORTABLE_PATH (
    echo Starting ComfyUI...
    REM Ensure D:\ComfyUI\models is loaded as extra models (copy config to ComfyUI root)
    if exist "%SCRIPT_DIR%\extra_model_paths_d_comfyui.yaml" (
        copy /Y "%SCRIPT_DIR%\extra_model_paths_d_comfyui.yaml" "%COMFYUI_PORTABLE_PATH%\extra_model_paths.yaml" >nul 2>&1
    )
    if exist "%COMFYUI_PORTABLE_PATH%\run_nvidia_gpu.bat" (
        start "ComfyUI" /B /D "%COMFYUI_PORTABLE_PATH%" cmd /c "run_nvidia_gpu.bat"
        timeout /t 2 /nobreak >nul
        echo [OK] ComfyUI starting - http://127.0.0.1:8188
        set "COMFYUI_STARTED=1"
    ) else if exist "%COMFYUI_PORTABLE_PATH%\run_cpu.bat" (
        start "ComfyUI" /B /D "%COMFYUI_PORTABLE_PATH%" cmd /c "run_cpu.bat"
        timeout /t 2 /nobreak >nul
        echo [OK] ComfyUI ^(CPU^) starting - http://127.0.0.1:8188
        set "COMFYUI_STARTED=1"
    ) else (
        echo [WARN] run_nvidia_gpu.bat / run_cpu.bat not found in %COMFYUI_PORTABLE_PATH%
    )
)
if "!COMFYUI_STARTED!" == "0" (
    set "COMFYUI_FOUND=0"
    set "COMFYUI_PATH="
    set "COMFYUI_IS_EXE=0"
    if exist "C:\Users\kurtw\AppData\Local\Programs\ComfyUI\ComfyUI.exe" (
        set "COMFYUI_PATH=C:\Users\kurtw\AppData\Local\Programs\ComfyUI\ComfyUI.exe"
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
            start "" "!COMFYUI_PATH!"
        ) else (
            REM Ensure D:\ComfyUI\models is loaded as extra models
            if exist "%SCRIPT_DIR%\extra_model_paths_d_comfyui.yaml" (
                copy /Y "%SCRIPT_DIR%\extra_model_paths_d_comfyui.yaml" "!COMFYUI_PATH!\extra_model_paths.yaml" >nul 2>&1
            )
            pushd "!COMFYUI_PATH!"
            start "ComfyUI" /B cmd /c "python main.py --port 8188"
            popd
        )
        timeout /t 2 /nobreak >nul
        echo [OK] ComfyUI - http://localhost:8188
        set "COMFYUI_STARTED=1"
    ) else (
        echo [INFO] ComfyUI not found. Set COMFYUI_PORTABLE_PATH in Settings or install to default locations.
    )
)
echo.

REM --- House Victoria App ---
echo Starting House Victoria App...
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
