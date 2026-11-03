@echo off
REM Compila BDM-500 Supervisor como ejecutable x86 autocontenido en .\publish
REM Requiere .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
cd /d "%~dp0"
dotnet publish BdmSupervisor.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o publish
if errorlevel 1 (echo. & echo *** ERROR de compilacion *** & pause & exit /b 1)
copy /y appsettings.json publish\ >nul
echo.
echo Listo: publish\BdmSupervisor.exe
pause
