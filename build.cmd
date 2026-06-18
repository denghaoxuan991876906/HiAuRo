@echo off
:: HiAuRo local dev build (Windows CMD -> bash with dotnet fallback)
set "ROOT=%~dp0"
set "SH=%ROOT%build.sh"
set "SH=%SH:\=/%"
set "GIT_BASH=%ProgramFiles%\Git\bin\bash.exe"
if exist "%GIT_BASH%" (
    "%GIT_BASH%" "%SH%"
    if %errorlevel% equ 0 exit /b 0
)
echo [build] bash not found, fallback to dotnet build
dotnet build "%ROOT%HiAuRo.slnx" -c Debug -nologo
exit /b %errorlevel%
