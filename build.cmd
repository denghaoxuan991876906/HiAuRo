@echo off
:: HiAuRo local dev build (Windows CMD -> bash)
set "SH=%~dp0build.sh"
set "SH=%SH:\=/%"
where bash >nul 2>nul && bash "%SH%" && exit /b 0
echo [ERROR] bash not found. Install Git Bash or WSL.
exit /b 1
