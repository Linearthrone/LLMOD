@echo off
REM House Victoria — delegates to unified stop.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0stop.ps1" %*
exit /b %ERRORLEVEL%
