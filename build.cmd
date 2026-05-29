@echo off
:: HiAuRo local dev build script
:: Usage: build.cmd

dotnet build %~dp0HiAuRo.slnx -c Debug -nologo
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] HiAuRo build failed
    exit /b %ERRORLEVEL%
)

:: 清理 NuGet 缓存，确保 ACR 拿到最新的本地包
rmdir /s /q "%USERPROFILE%\.nuget\packages\hiauro.sdk" 2>nul

echo Build succeeded
