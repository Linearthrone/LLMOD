@echo off
REM Setup script for House Victoria MCP Server

echo House Victoria MCP Server Setup
echo ================================
echo.

REM Create virtual environment if it doesn't exist
if not exist ".venv" (
    echo Creating virtual environment...
    python -m venv .venv
) else (
    echo Virtual environment already exists.
)

REM Activate virtual environment
echo Activating virtual environment...
call .venv\Scripts\activate.bat

REM Install dependencies
echo.
echo Installing dependencies...
pip install -e .

REM Copy example environment file if .env doesn't exist
if not exist ".env" (
    echo.
    echo Creating .env file from example...
    copy .env.example .env
) else (
    echo .env file already exists.
)

REM Create necessary directories
echo.
echo Creating data directories...
if not exist "data" mkdir data
if not exist "data\banks" mkdir data\banks
if not exist "data\projects" mkdir data\projects
if not exist "logs" mkdir logs

echo.
echo =====================================
echo Setup complete!
echo =====================================
echo.
echo To start the server, run:
echo   python -m house_victoria_mcp
echo.
echo Or configure in your MCP client (Claude Desktop, VS Code, etc.)
echo using the settings in .vscode\mcp.json
echo.
pause
