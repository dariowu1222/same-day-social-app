@echo off
title Today App - Dev Environment

start "Vite Dev Server" /D "C:\Users\cq4dario\source\¦PÀWTO~1\frontend" cmd /k npm run dev

start "" "C:\Program Files\Android\Android Studio\bin\studio64.exe"

timeout /t 5 /nobreak >nul
