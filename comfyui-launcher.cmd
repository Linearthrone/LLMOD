@echo off
REM Used by start.bat only. Args: mode (gpu|cpu|main) root_folder log_file
setlocal
set "MODE=%~1"
set "ROOT=%~2"
set "LOG=%~3"
if "%ROOT%"=="" exit /b 1
if "%LOG%"=="" exit /b 1
cd /d "%ROOT%"
if /i "%MODE%"=="gpu" (
    call run_nvidia_gpu.bat >> "%LOG%" 2>&1
    exit /b 0
)
if /i "%MODE%"=="cpu" (
    call run_cpu.bat >> "%LOG%" 2>&1
    exit /b 0
)
if /i "%MODE%"=="main" (
    python main.py --port 8188 >> "%LOG%" 2>&1
    exit /b 0
)
exit /b 1
