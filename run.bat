@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

echo ===================================================
echo Launching LabSync!
echo Base Folder: %SCRIPT_DIR%
echo ===================================================

:: Windows 1 – server (docker + dotnet)
start "LabSync Server" cmd /k "cd /d "%SCRIPT_DIR%" && docker compose down && timeout /t 5 /nobreak && docker compose up -d && timeout /t 5 /nobreak && dotnet run --project src/LabSync.Server"

timeout /t 1 /nobreak >nul

:: Window 2 – agent
start "LabSync Agent" cmd /k "cd /d "%SCRIPT_DIR%" && dotnet run --project src/LabSync.Agent"

timeout /t 1 /nobreak >nul

:: Window 3 – client (npm build -> preview)
start "LabSync Client" cmd /k "cd /d "%SCRIPT_DIR%\src\client" && npm run build && npm run preview"

echo.
echo All Windows are now open!. (client will run AFTER finishing build.)
pause