@echo off
cd /d "%~dp0"

echo ========================================
echo     LLMOD Desktop Overlay
echo ========================================
echo.
echo Starting floating desktop panels...
echo.

:: Start module servers in background
start /MIN "LLMOD Modules" cmd /c "npm start"

:: Wait for modules to start
timeout /t 5 /nobreak > nul

:: Start desktop overlay
cd desktop-overlay
start "LLMOD Desktop Overlay" cmd /c "npm start"

echo.
echo ✨ LLMOD Desktop Overlay is starting!
echo.
echo Your floating panels will appear in a few moments...
echo.
echo Controls:
echo   Ctrl+Shift+A: Show all panels
echo   Ctrl+Shift+H: Hide all panels  
echo   Ctrl+R: Reload all modules
echo   Ctrl+Q: Quit overlay
echo.

timeout /t 3 > nul
exit