@echo off
REM Stop STT server (process on port 8000).
REM If it "refuses", right-click this file -> Run as administrator.

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

echo Stopping STT Server (port 8000)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0.ps1 scripts\stop-stt.ps1"
if errorlevel 1 (
    echo.
    echo If you see "Access denied", right-click stop-stt.bat and choose "Run as administrator".
    pause
)
