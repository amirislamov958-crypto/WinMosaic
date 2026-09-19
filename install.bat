@echo off
chcp 65001 >nul
title Установка темы начального экрана Windows 8.1
echo =====================================================================
echo    УСТАНОВКА АУТЕНТИЧНОЙ ТЕМЫ НАЧАЛЬНОГО ЭКРАНА WINDOWS 8.1
echo =====================================================================
echo.

set "INSTALL_DIR=%LOCALAPPDATA%\Programs\WinMosaic"
set "CONFIG_DIR=%LOCALAPPDATA%\Win8StartScreen"
set "EXE_PATH=%INSTALL_DIR%\WinMosaic.exe"

echo [1/5] Завершение запущенных экземпляров...
taskkill /f /im WinMosaic.exe >nul 2>&1
taskkill /f /im Win8StartScreen.exe >nul 2>&1

echo [2/5] Сборка и установка приложения в %INSTALL_DIR%...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
dotnet publish "%~dp0Win8StartScreen.csproj" -c Release -o "%INSTALL_DIR%" --no-self-contained
if %errorlevel% neq 0 (
    echo ОШИБКА: Сборка не удалась. Убедитесь, что установлен .NET 8 SDK.
    pause
    exit /b %errorlevel%
)

echo [3/5] Проверка конфигурации плиток...
if not exist "%CONFIG_DIR%" mkdir "%CONFIG_DIR%"

echo [4/5] Регистрация в автозагрузке Windows...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "Win8StartScreen" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "WinMosaic" /t REG_SZ /d "\"%EXE_PATH%\"" /f >nul
if %errorlevel% neq 0 (
    echo ПРЕДУПРЕЖДЕНИЕ: Не удалось записать ключ автозагрузки в реестр.
) else (
    echo [OK] Тема успешно добавлена в автозагрузку (HKCU\...\Run).
)

echo [5/5] Запуск темы начального экрана...
start "" "%EXE_PATH%"

echo.
echo =====================================================================
echo    ГОТОВО! ТЕМА WINDOWS 8.1 УСПЕШНО УСТАНОВЛЕНА И АКТИВНА!
echo =====================================================================
echo  - Нажмите клавишу Win для открытия экрана Пуск Windows 8.1.
echo  - Иконка темы всегда находится в системном трее (возле часов).
echo  - Тема будет автоматически запускаться при каждом перезапуске ПК.
echo  - Перетаскивайте плитки левой кнопкой мыши для изменения порядка.
echo  - Нажимайте правой кнопкой мыши на плитку для изменения её размера.
echo =====================================================================
echo.
pause
