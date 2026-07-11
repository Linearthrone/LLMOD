@echo off

REM House Victoria — delegates to unified start.ps1

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start.ps1" %*

exit /b %ERRORLEVEL%

