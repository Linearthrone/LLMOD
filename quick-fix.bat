@echo off
echo ========================================
echo LLMOD Quick Fix Script
echo ========================================
echo.

echo 1. Pulling latest fixes from GitHub...
git pull origin master
echo.

echo 2. Installing Node.js dependencies...
npm install
echo.

echo 3. Fixing Python MCP Server...
pip install -e "Central Core/MainAccessToMcpServer"
echo.

echo 4. Starting LLMOD Application...
echo.
echo ========================================
echo   LLMOD is starting...
echo ========================================
echo.
echo Access modules at:
echo - App Tray: http://localhost:8085
echo - Chat Module: http://localhost:8080
echo - Contacts: http://localhost:8081
echo - ViewPort: http://localhost:8082
echo - Systems: http://localhost:8083
echo - Context Data: http://localhost:8084
echo.
echo Press Ctrl+C to stop all modules
echo ========================================
echo.

npm start
pause