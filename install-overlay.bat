@echo off
echo ========================================
echo LLMOD Desktop Overlay Installer
echo ========================================
echo.

echo 1. Installing Electron dependencies...
cd desktop-overlay
npm install
if %errorlevel% neq 0 (
    echo Failed to install Electron dependencies
    pause
    exit /b 1
)
echo.

echo 2. Installing Node.js dependencies for modules...
cd ..
npm install
if %errorlevel% neq 0 (
    echo Failed to install Node.js dependencies
    pause
    exit /b 1
)
echo.

echo 3. Installing Python MCP servers...
pip install -e "Central Core/MainAccessToMcpServer"
if %errorlevel% neq 0 (
    echo Failed to install Python dependencies
    pause
    exit /b 1
)
echo.

echo 4. Starting module servers...
start "LLMOD Modules" cmd /k "npm start"
timeout /t 5 /nobreak > nul
echo.

echo 5. Starting Desktop Overlay...
echo.
echo ========================================
echo   LLMOD Desktop Overlay Starting...
echo ========================================
echo.
echo Your modules will now appear as floating
echo panels on your desktop!
echo.
echo Controls:
echo - Ctrl+Shift+A: Show all modules
echo - Ctrl+Shift+H: Hide all modules
echo - Ctrl+R: Reload all modules
echo - Ctrl+Q: Quit overlay
echo.
echo ========================================
echo.

cd desktop-overlay
npm start

pause