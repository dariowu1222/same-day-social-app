@echo off
rem === SameDay Today: one-click device test launcher ===
rem Double-click this file. It will:
rem   1. detect your PC's LAN IP and write frontend\.env.device
rem   2. open Windows Firewall for port 5000 (UAC prompt once)
rem   3. start the backend on 0.0.0.0:5000 in a new window
rem   4. build the frontend (device mode) and open Android Studio
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\device-test.ps1"
echo.
pause
