@echo off
cd /d "%~dp0"
dotnet build XfsCheck\XfsCheck.csproj -c Release -o XfsCheck\bin >nul || (echo *** ERROR de compilacion *** & pause & exit /b 1)
XfsCheck\bin\XfsCheck.exe %*
pause
