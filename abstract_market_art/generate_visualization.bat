@echo off
echo Abstract Market Dynamics Visualization Generator
echo ==============================================
echo.
echo Generating abstract visual art from MT4 data...
echo.

REM Check if Python is installed
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: Python is not installed or not in PATH
    echo Please install Python 3.7 or later and try again
    pause
    exit /b 1
)

REM Check if required Python packages are installed
echo Checking for required Python packages...
python -c "import matplotlib" >nul 2>&1
if %errorlevel% neq 0 (
    echo Installing matplotlib...
    pip install matplotlib
)

python -c "import numpy" >nul 2>&1
if %errorlevel% neq 0 (
    echo Installing numpy...
    pip install numpy
)

REM Run the visualization script
echo.
echo Running visualization script...
python market_dynamics_visualizer.py

if %errorlevel% neq 0 (
    echo.
    echo Error: Failed to generate visualization
    pause
    exit /b 1
)

echo.
echo Visualization generated successfully!
echo Check the generated PNG file in this directory
echo.
pause