@echo off
REM Stop Piper TTS server (process on port 5000).
REM If it "refuses", right-click this file -> Run as administrator.

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

echo Stopping Piper TTS (port 5000)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0.ps1 scripts\stop-piper.ps1"
if errorlevel 1 (
    echo.
    echo If you see "Access denied", right-click stop-piper.bat and choose "Run as administrator".
    pause
)
