@echo off
chcp 65001 >nul
title Удаление темы начального экрана Windows 8.1
echo =====================================================================
echo    УДАЛЕНИЕ ТЕМЫ НАЧАЛЬНОГО ЭКРАНА WINDOWS 8.1 ИЗ АВТОЗАГРУЗКИ
echo =====================================================================
echo.

echo [1/2] Завершение процесса WinMosaic.exe...
taskkill /f /im WinMosaic.exe >nul 2>&1
taskkill /f /im Win8StartScreen.exe >nul 2>&1

echo [2/2] Удаление записи из автозагрузки Windows...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "WinMosaic" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "Win8StartScreen" /f >nul 2>&1

echo.
echo =====================================================================
echo    Тема Windows 8.1 отключена и успешно удалена из автозагрузки.
echo =====================================================================
echo.
pause
