@echo off
setlocal

:: Iterate through directories starting with LabSync.Modules.
for /d %%i in (LabSync.Modules.*) do (
    echo [BUILDING] %%i
    dotnet build "%%i"
    
    :: Check exit code
    if %errorlevel% neq 0 (
        echo [ERROR] Build failed for %%i
    )
    echo.
)

pause