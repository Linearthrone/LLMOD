@echo off
REM Stop all House Victoria services and servers.
REM If it "refuses", right-click this file -> Run as administrator.

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

echo.
echo === House Victoria - Stop All ===
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0.ps1 scripts\stop-all.ps1"
if errorlevel 1 (
    echo.
    echo If you see "Access denied", right-click stop-all.bat and choose "Run as administrator".
    pause
)

