@echo off
REM House Victoria Complete Installer (Batch Version)
REM This script installs and starts all components including the MCP server

setlocal enabledelayedexpansion

echo.
echo ============================================================
echo         House Victoria Complete Installation Script
echo ============================================================
echo.

REM Get script directory
set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

REM ============================================================
REM Step 1: Check Prerequisites
REM ============================================================
echo === Checking Prerequisites ===
echo.

echo Checking for .NET 8.0 SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found. Please install .NET 8.0 SDK.
    echo Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo [OK] .NET SDK found: %DOTNET_VERSION%
echo.

echo Checking for Python 3.10+...
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found. Please install Python 3.10+.
    echo Download from: https://www.python.org/downloads/
    pause
    exit /b 1
)
for /f "tokens=*" %%i in ('python --version') do set PYTHON_VERSION=%%i
echo [OK] Python found: %PYTHON_VERSION%
echo.

echo Checking for pip...
pip --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] pip not found. Please install pip
        pause
    exit /b 1
)
echo [OK] pip found
echo.

REM ============================================================
REM Step 2: Install .NET Dependencies and Build
REM ============================================================
echo === Installing .NET Dependencies and Building Solution ===
echo.

echo Restoring NuGet packages...
dotnet restore HouseVictoria.sln
if errorlevel 1 (
    echo [ERROR] Failed to restore NuGet packages.
    pause
    exit /b 1
)
echo [OK] NuGet packages restored.
echo.

echo Building solution...
dotnet build HouseVictoria.sln --configuration Release --no-restore
if errorlevel 1 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)
echo [OK] Solution built successfully.
echo.

REM ============================================================
REM Step 3: Setup MCP Server
REM ============================================================
echo === Setting Up MCP Server ===
echo.

set "MCP_PATH=%SCRIPT_DIR%\MCPServer"

if not exist "%MCP_PATH%" (
    echo [ERROR] MCP Server directory not found: %MCP_PATH%
    pause
    exit /b 1
)

cd /d "%MCP_PATH%"

REM Create virtual environment if it doesn't exist
if not exist ".venv" (
    echo Creating Python virtual environment...
    python -m venv .venv
    if errorlevel 1 (
        echo [ERROR] Failed to create virtual environment.
        pause
        exit /b 1
    )
    echo [OK] Virtual environment created.
) else (
    echo Virtual environment already exists.
)
echo.

REM Activate virtual environment
echo Activating virtual environment...
call .venv\Scripts\activate.bat
if errorlevel 1 (
    echo [ERROR] Failed to activate virtual environment.
    pause
    exit /b 1
)
echo.

REM Upgrade pip
echo Upgrading pip...
python -m pip install --upgrade pip --quiet
echo.

REM Install dependencies
echo Installing Python dependencies...
pip install -e . --quiet
if errorlevel 1 (
    echo [ERROR] Failed to install Python dependencies.
    pause
    exit /b 1
)
echo [OK] Python dependencies installed.
echo.

REM Create necessary directories
echo Creating data directories...
if not exist "data" mkdir data
if not exist "data\banks" mkdir data\banks
if not exist "data\projects" mkdir data\projects
if not exist "logs" mkdir logs
echo [OK] Data directories created.
echo.

REM Create Media directories (Piper TTS voices)
echo Creating Media directories...
if not exist "%SCRIPT_DIR%Media" mkdir "%SCRIPT_DIR%Media"
if not exist "%SCRIPT_DIR%Media\PiperVoices" mkdir "%SCRIPT_DIR%Media\PiperVoices"
echo [OK] Media\PiperVoices created (place Piper voice .onnx/.onnx.json here or use piper.download_voices).
echo.

REM Create .env file if it doesn't exist
if not exist ".env" (
    echo Creating .env configuration file...
    (
        echo # Server Configuration
        echo SERVER_PORT=8080
        echo SERVER_HOST=localhost
        echo(
        echo # Database Configuration
        echo DATABASE_PATH=.\data\memory.db
        echo(
        echo # Data Banks
        echo DATA_BANKS_PATH=.\data\banks
        echo PROJECTS_PATH=.\data\projects
        echo(
        echo # Logging
        echo LOG_LEVEL=INFO
        echo LOG_FILE=.\logs\server.log
        echo(
        echo # Memory
        echo MEMORY_MAX_ENTRIES=10000
        echo MEMORY_RETENTION_DAYS=365
    ) > .env
    echo [OK] .env file created.
) else (
    echo .env file already exists.
)
echo.

cd /d "%SCRIPT_DIR%"
echo [OK] MCP Server setup complete.
echo.

REM ============================================================
REM Step 4: Start Services
REM ============================================================
echo === Starting Services ===
echo.

REM Start Ollama service
echo Starting Ollama service...
where ollama >nul 2>&1
if not errorlevel 1 (
    echo Launching Ollama server...
    start "" /B ollama serve >nul 2>&1
    timeout /t 2 /nobreak >nul
    echo [OK] Ollama server launch attempted. It may take a few seconds to initialize.
    echo      Service should be available at http://localhost:11434
) else (
    echo [INFO] Ollama command not found in PATH.
    echo        If Ollama is installed, ensure it's in your PATH or start it manually.
    echo        Download from: https://ollama.ai if needed.
    echo        Note: On Windows, Ollama may run as a service - check Task Manager.
)
echo.

REM Start MCP Server (HTTP API on port 8080)
echo Starting MCP Server...
if exist "%MCP_PATH%\.venv\Scripts\python.exe" (
    if not exist "%MCP_PATH%\logs" mkdir "%MCP_PATH%\logs"
    start "MCP Server" /B cmd /c "cd /d "%MCP_PATH%" && .venv\Scripts\python.exe http_server.py >> logs\http_server.log 2>&1"
    timeout /t 3 /nobreak >nul
    echo [OK] MCP Server launch attempted. Service should be available at http://localhost:8080
    echo      Logs: MCPServer\logs\http_server.log
) else (
    echo [WARN] MCP Server venv not found at %MCP_PATH%\.venv
    echo        Run install.bat again to complete MCP setup, or start manually: cd MCPServer ^&^& .venv\Scripts\python.exe http_server.py
)
echo.

REM Start Piper TTS (voice synthesis for AI contacts during calls)
set "PIPER_DATA=%SCRIPT_DIR%Media\PiperVoices"
set "PIPER_MODEL=en_US-amy-medium"
if exist "%MCP_PATH%\.venv\Scripts\python.exe" (
    if not exist "%SCRIPT_DIR%Media" mkdir "%SCRIPT_DIR%Media"
    start "Piper TTS" /B cmd /c "cd /d "%SCRIPT_DIR%" && "%MCP_PATH%\.venv\Scripts\python.exe" -m piper --model %PIPER_MODEL% --port 5000 --data-dir "%PIPER_DATA%" >> Media\piper.log 2>&1"
    timeout /t 2 /nobreak >nul
    echo [OK] Piper TTS - http://localhost:5000 (voice: %PIPER_MODEL%)
    echo      Logs: Media\piper.log. Download voices: python -m piper.download_voices
) else (
    echo [INFO] Piper TTS: MCP venv not found. Run install.bat to set up MCP (includes piper-tts).
)
echo.

REM Start ComfyUI if available
echo Starting ComfyUI...
set "COMFYUI_FOUND=0"
set "COMFYUI_PATH="
set "COMFYUI_IS_EXE=0"

REM Check common ComfyUI locations (exe vs directory)
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
    echo Launching ComfyUI from: !COMFYUI_PATH!
    if !COMFYUI_IS_EXE! == 1 (
        start "" "!COMFYUI_PATH!"
    ) else (
        pushd "!COMFYUI_PATH!"
        start "" python main.py --port 8188
        popd
    )
    timeout /t 3 /nobreak >nul
    echo [OK] ComfyUI launch attempted. Service should be available at http://localhost:8188
) else (
    echo [INFO] ComfyUI not found in common locations.
    echo        Skipping Stable Diffusion startup. Start manually if installed elsewhere.
)
echo.



REM Start WPF Application
echo Starting House Victoria Application...
set "APP_PATH=%SCRIPT_DIR%\HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe"

if exist "%APP_PATH%" (
    start "" "%APP_PATH%"
    echo [OK] House Victoria Application started.
    ) else (
        REM Try Debug build if Release doesn't exist
        set "APP_PATH_DEBUG=%SCRIPT_DIR%\HouseVictoria.App\bin\Debug\net8.0-windows\HouseVictoria.App.exe"
    if exist "%APP_PATH_DEBUG%" (
        start "" "%APP_PATH_DEBUG%"
        echo [OK] House Victoria Application started from Debug build.
    ) else (
        echo [ERROR] House Victoria Application executable not found.
        echo Expected locations:
        echo   - %APP_PATH%
        echo   - %APP_PATH_DEBUG%
        echo Please build the solution first.
    )
)
echo.

REM ============================================================
REM Summary
REM ============================================================
echo ============================================================
echo                    Installation Complete
echo ============================================================
echo.
echo [OK] .NET Solution: Built and ready
echo [OK] MCP Server: Installed and configured (includes Piper TTS)
echo [OK] Media/PiperVoices: Created for TTS voice models
echo [OK] Services: Started
echo.
echo Started Services:
echo   - Ollama (LLM Server) - http://localhost:11434
echo   - MCP Server - http://localhost:8080
echo   - Piper TTS - http://localhost:5000 (voice synthesis)
echo   - ComfyUI - http://localhost:8188 (if available)
echo   - House Victoria Application
echo.
echo To start services later (if already installed):
echo   Run: start.bat
echo.
echo To manually start services individually:
echo   Ollama: ollama serve
echo   MCP Server: cd MCPServer ^&^& .venv\Scripts\python.exe http_server.py
echo   Piper TTS: python -m piper --model en_US-amy-medium --port 5000 --data-dir Media\PiperVoices
echo   ComfyUI: cd ComfyUI ^&^& python main.py --port 8188
echo   Application: HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe
echo.
echo Note: Services are running in the background (no windows visible).
echo       Use Task Manager to verify processes are running if needed.
echo.
echo All services are now running and ready to use!
echo.
pause
