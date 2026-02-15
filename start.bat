@echo off
REM Start House Victoria services (Ollama, MCP Server, ComfyUI, App)
REM Run install.bat first if you haven't set up the project yet.

setlocal enabledelayedexpansion
set "SCRIPT_DIR=%~dp0"
set "MCP_PATH=%SCRIPT_DIR%MCPServer"
cd /d "%SCRIPT_DIR%"

echo.
echo === Starting House Victoria Services ===
echo.

REM Start Ollama
echo Starting Ollama...
where ollama >nul 2>&1
if not errorlevel 1 (
    start "" /B ollama serve >nul 2>&1
    timeout /t 2 /nobreak >nul
    echo [OK] Ollama - http://localhost:11434
) else (
    echo [INFO] Ollama not in PATH. Start manually if needed.
)
echo.

REM Start MCP Server
echo Starting MCP Server...
if exist "%MCP_PATH%\.venv\Scripts\python.exe" (
    if not exist "%MCP_PATH%\logs" mkdir "%MCP_PATH%\logs"
    start "MCP Server" /B cmd /c "cd /d "%MCP_PATH%" && .venv\Scripts\python.exe http_server.py >> logs\http_server.log 2>&1"
    timeout /t 3 /nobreak >nul
    echo [OK] MCP Server - http://localhost:8080
    echo      Logs: MCPServer\logs\http_server.log
) else (
    echo [WARN] MCP Server venv not found. Run install.bat first, or: cd MCPServer ^&^& .venv\Scripts\python.exe http_server.py
)
echo.

REM Start Piper TTS (voice synthesis for AI contacts during calls)
set "PIPER_DATA=%SCRIPT_DIR%Media\PiperVoices"
set "PIPER_MODEL=en_US-amy-medium"
if not exist "%SCRIPT_DIR%Media" mkdir "%SCRIPT_DIR%Media"
if not exist "%PIPER_DATA%" mkdir "%PIPER_DATA%"
if exist "%SCRIPT_DIR%.venv\Scripts\python.exe" (
    start "Piper TTS" /B cmd /c "cd /d "%SCRIPT_DIR%" && .venv\Scripts\python.exe -m piper --model %PIPER_MODEL% --port 5000 --data-dir "%PIPER_DATA%" >> Media\piper.log 2>&1"
    timeout /t 2 /nobreak >nul
    echo [OK] Piper TTS - http://localhost:5000 (voice: %PIPER_MODEL%)
) else if exist "%MCP_PATH%\.venv\Scripts\python.exe" (
    start "Piper TTS" /B cmd /c "cd /d "%SCRIPT_DIR%" && "%MCP_PATH%\.venv\Scripts\python.exe" -m piper --model %PIPER_MODEL% --port 5000 --data-dir "%PIPER_DATA%" >> Media\piper.log 2>&1"
    timeout /t 2 /nobreak >nul
    echo [OK] Piper TTS - http://localhost:5000 (voice: %PIPER_MODEL%)
) else (
    echo [INFO] Piper TTS: No Python venv found. Run install.bat or: python -m piper --model %PIPER_MODEL% --port 5000 --data-dir "%PIPER_DATA%"
)
echo.

REM Start ComfyUI if available (exe or directory with main.py)
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
    if !COMFYUI_IS_EXE! == 1 (
        start "" "!COMFYUI_PATH!"
    ) else (
        pushd "!COMFYUI_PATH!"
        start "" python main.py --port 8188
        popd
    )
    timeout /t 2 /nobreak >nul
    echo [OK] ComfyUI - http://localhost:8188
) else (
    echo [INFO] ComfyUI not found. Skipping.
)
echo.

REM Start WPF Application
echo Starting House Victoria Application...
set "APP_PATH=%SCRIPT_DIR%HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe"
if not exist "%APP_PATH%" set "APP_PATH=%SCRIPT_DIR%HouseVictoria.App\bin\Debug\net8.0-windows\HouseVictoria.App.exe"
if exist "%APP_PATH%" (
    start "" "%APP_PATH%"
    echo [OK] House Victoria Application started.
) else (
    echo [WARN] App executable not found. Build the solution first (install.bat or dotnet build).
)
echo.
echo === House Victoria Services ===
echo   Ollama: http://localhost:11434  ^| MCP Server: http://localhost:8080
echo   Piper TTS: http://localhost:5000  ^| ComfyUI: http://localhost:8188 (if found)
echo   App: House Victoria (WPF). MCP must be running for the app to connect.
echo.
pause
