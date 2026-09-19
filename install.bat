@echo off
setlocal
chcp 65001 >nul
title WinMosaic 8.1 Start Screen - Setup

echo =====================================================================
echo    WinMosaic - Windows 8.1 Start Screen Setup
echo =====================================================================
echo.

set "INSTALL_DIR=%LOCALAPPDATA%\Programs\WinMosaic"
set "CONFIG_DIR=%LOCALAPPDATA%\Win8StartScreen"
set "EXE_PATH=%INSTALL_DIR%\WinMosaic.exe"

echo [1/4] Stopping running instances...
taskkill /f /im WinMosaic.exe >nul 2>&1
taskkill /f /im Win8StartScreen.exe >nul 2>&1

echo [2/4] Installing application files...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
if not exist "%CONFIG_DIR%" mkdir "%CONFIG_DIR%"

if exist "%~dp0WinMosaic.exe" (
    echo Copying pre-built release files...
    xcopy "%~dp0*" "%INSTALL_DIR%\" /E /Y /I >nul
) else (
    echo Compiling from source code via dotnet...
    dotnet publish "%~dp0Win8StartScreen.csproj" -c Release -o "%INSTALL_DIR%" --no-self-contained
    if errorlevel 1 (
        echo.
        echo [ERROR] Build failed! Please install .NET 8 SDK: https://dotnet.microsoft.com
        echo.
        pause
        exit /b 1
    )
)

echo Updating tile layout configuration...
if exist "%CONFIG_DIR%\layout_config.json" copy /y "%CONFIG_DIR%\layout_config.json" "%CONFIG_DIR%\layout_config.json.bak" >nul 2>&1
if exist "%INSTALL_DIR%\default_layout.json" copy /y "%INSTALL_DIR%\default_layout.json" "%CONFIG_DIR%\layout_config.json" >nul
if exist "%~dp0default_layout.json" copy /y "%~dp0default_layout.json" "%CONFIG_DIR%\layout_config.json" >nul
if exist "%INSTALL_DIR%\Assets\default_layout.json" copy /y "%INSTALL_DIR%\Assets\default_layout.json" "%CONFIG_DIR%\layout_config.json" >nul

echo [3/4] Registering Windows Autostart...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "Win8StartScreen" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "WinMosaic" /t REG_SZ /d "\"%EXE_PATH%\"" /f >nul

echo [4/4] Starting WinMosaic...
start "" "%EXE_PATH%"

echo.
echo =====================================================================
echo    SUCCESS! WinMosaic has been installed and launched!
echo    - Press Windows key to open Start Screen.
echo    - Icon is available in system tray.
echo    - Autostart with Windows is enabled.
echo =====================================================================
echo.
pause
