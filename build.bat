@echo off
REM ==============================================================================
REM   Light Engine - Cross-Platform Build & Test Automation Script (Windows)
REM ==============================================================================

set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

echo [LightEngine] Solution derleniyor (Release)...
dotnet build LightEngine.slnx -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [HATA] Derleme basarisiz oldu!
    exit /b %ERRORLEVEL%
)

echo [LightEngine] 9 Asamali Otomasyon Test Paketi calistiriliyor...
dotnet run --project LightEngine.Tests -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [HATA] Testler basarisiz oldu!
    exit /b %ERRORLEVEL%
)

echo.
echo ==============================================================================
echo   [BASARILI] Light Engine derlendi ve tum 9 dogrulama asamasi gecti!
echo ==============================================================================

if "%1"=="publish" goto do_publish
if "%1"=="dist" goto do_publish
if "%1"=="all" goto do_publish
goto finish

:do_publish
echo.
echo [LightEngine] Standalone Windows ve Linux paketleri yayinlaniyor...

REM Windows Publish
dotnet publish LightEngine.Editor\LightEngine.Editor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\windows
dotnet publish Game\Game.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\windows
copy /y LightEngine.Editor\bin\Release\net9.0\runtimes\win-x64\native\* dist\windows\ >nul 2>&1
copy /y Icon.ico dist\windows\ >nul 2>&1
copy /y Icon.png dist\windows\ >nul 2>&1

REM Linux Publish
dotnet publish LightEngine.Editor\LightEngine.Editor.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\linux
dotnet publish Game\Game.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\linux
copy /y LightEngine.Editor\bin\Release\net9.0\runtimes\linux-x64\native\* dist\linux\ >nul 2>&1
copy /y Icon.png dist\linux\ >nul 2>&1

echo [LightEngine] Yayinlama tamamlandi: dist\windows ve dist\linux hazir!

:finish
exit /b 0
