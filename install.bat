@echo off
setlocal
REM House Victoria - one-time setup. Run from repo root. Use start.bat to run services.

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "MCP_PATH=%SCRIPT_DIR%\MCPServer"
cd /d "%SCRIPT_DIR%"

echo.
echo === House Victoria - Install ===
echo.

REM --- Prerequisites ---
echo Checking .NET 8 SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found. Install .NET 8 from https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)
for /f "tokens=*" %%v in ('dotnet --version') do set DOTNET_VER=%%v
echo [OK] .NET %DOTNET_VER%

echo Checking Python...
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found. Install Python 3.10+ from https://www.python.org/downloads/
    exit /b 1
)
for /f "tokens=*" %%v in ('python --version') do set PY_VER=%%v
echo [OK] %PY_VER%

echo Checking pip...
pip --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] pip not found. Install pip or reinstall Python with pip.
    exit /b 1
)
echo [OK] pip found
echo.

REM --- .NET build ---
echo Restoring and building solution...
dotnet restore HouseVictoria.sln
if errorlevel 1 ( echo [ERROR] dotnet restore failed. & exit /b 1 )
dotnet build HouseVictoria.sln -c Release --no-restore
if errorlevel 1 ( echo [ERROR] dotnet build failed. & exit /b 1 )
echo [OK] Solution built.
echo.

REM --- MCP Server venv ---
echo Setting up MCP Server...
if not exist "%MCP_PATH%" (
    echo [ERROR] MCPServer folder not found: %MCP_PATH%
    exit /b 1
)
cd /d "%MCP_PATH%"

REM Prevent pip from hanging on prompts (e.g. version check, overwrite)
set "PIP_DISABLE_PIP_VERSION_CHECK=1"
set "PIP_NO_INPUT=1"

if not exist ".venv" (
    echo Creating virtual environment...
    python -m venv .venv
    if errorlevel 1 ( echo [ERROR] Failed to create venv. & exit /b 1 )
)
call .venv\Scripts\activate.bat
python -m pip install --upgrade pip -q
echo Installing MCP and dependencies...
pip install -e . -q
if errorlevel 1 ( echo [ERROR] pip install -e . failed. & exit /b 1 )
echo [OK] MCP Server dependencies installed.

REM Optional: STT server (faster-whisper) in same venv
if exist "%SCRIPT_DIR%\STTServer\requirements.txt" (
    echo Installing STT server dependencies...
    pip install -r "%SCRIPT_DIR%\STTServer\requirements.txt" -q
    if errorlevel 1 ( echo [WARN] STT deps failed. Later: pip install -r STTServer\requirements.txt ) else ( echo [OK] STT deps installed. )
)

REM Optional: Piper server uses same venv (piper-tts already in MCP deps)
if exist "%SCRIPT_DIR%\PiperServer\requirements.txt" (
    pip install -r "%SCRIPT_DIR%\PiperServer\requirements.txt" -q 2>nul
)

if not exist "data" mkdir data
if not exist "data\banks" mkdir data\banks
if not exist "data\projects" mkdir data\projects
if not exist "logs" mkdir logs

if not exist ".env" (
    (
        echo SERVER_PORT=8080
        echo SERVER_HOST=localhost
        echo DATABASE_PATH=.\data\memory.db
        echo DATA_BANKS_PATH=.\data\banks
        echo PROJECTS_PATH=.\data\projects
        echo LOG_LEVEL=INFO
        echo LOG_FILE=.\logs\server.log
        echo MEMORY_MAX_ENTRIES=10000
        echo MEMORY_RETENTION_DAYS=365
    ) > .env
    echo [OK] .env created.
)
cd /d "%SCRIPT_DIR%"

if not exist "Media" mkdir Media
if not exist "Media\PiperVoices" mkdir Media\PiperVoices
echo [OK] Media\PiperVoices ready. Add Piper .onnx voice files here if needed.
echo.

echo === Install complete ===
echo Run start.bat to start Ollama, MCP Server, Piper TTS, STT, and the app.
echo.
