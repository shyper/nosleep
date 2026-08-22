@echo off
setlocal
echo =======================================================
echo               NoSleep - Build Script
echo =======================================================
echo.

powershell -Command "$p = [System.Diagnostics.Process]::GetProcessesByName('NoSleep'); if ($p.Count -gt 0) { $p | ForEach-Object { $_.Kill(); $_.WaitForExit(3000) }; Start-Sleep -Milliseconds 500 }" >nul 2>&1

set CSC=

if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) else if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set CSC="%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) else if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set CSC="%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) else if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set CSC="%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if "%CSC%"=="" (
    echo [ERROR] C# compiler csc.exe was not found in %%WINDIR%%\Microsoft.NET\Framework!
    echo Please make sure Microsoft .NET Framework 4.x is installed.
    if "%~1"=="" pause
    exit /b 1
)

echo Compiling NoSleep.exe using %CSC%...
%CSC% /target:winexe /out:NoSleep.exe /win32icon:app.ico /win32manifest:app.manifest /platform:anycpu /optimize+ /nologo /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll src\*.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] NoSleep.exe has been built successfully!
    echo You can now run NoSleep.exe directly.
) else (
    echo.
    echo [ERROR] An error occurred during compilation.
)

echo.
if "%~1"=="" pause
