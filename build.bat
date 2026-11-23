@echo off
setlocal
echo ========================================================
echo   ZynCopy - Build System (ZynOps Solutions)
echo ========================================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] C# Compiler not found at: %CSC%
    exit /b 1
)

set BIN_DIR=%~dp0bin
set SRC_DIR=%~dp0src
set INSTALLER_DIR=%~dp0installer

if not exist "%BIN_DIR%" mkdir "%BIN_DIR%"

echo [1/3] Copying brand assets to bin...
copy /Y "%SRC_DIR%\logo.png" "%BIN_DIR%\logo.png" >nul
copy /Y "%SRC_DIR%\app.ico" "%BIN_DIR%\app.ico" >nul

echo.
echo [2/3] Compiling ZynCopy.exe (Application with Icon)...
"%CSC%" /target:winexe /optimize+ /platform:anycpu /win32icon:"%SRC_DIR%\app.ico" /out:"%BIN_DIR%\ZynCopy.exe" /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll "%SRC_DIR%\Program.cs" "%SRC_DIR%\MainForm.cs" "%SRC_DIR%\CopyEngine.cs" "%SRC_DIR%\ShellIntegrator.cs"

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to compile ZynCopy.exe!
    exit /b %ERRORLEVEL%
)
echo [OK] ZynCopy.exe compiled successfully!

echo.
echo [3/3] Compiling ZynCopy-Setup.exe (Installer with Icon)...
"%CSC%" /target:winexe /optimize+ /platform:anycpu /win32icon:"%SRC_DIR%\app.ico" /out:"%BIN_DIR%\ZynCopy-Setup.exe" /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,Microsoft.CSharp.dll "%INSTALLER_DIR%\Setup.cs"

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to compile ZynCopy-Setup.exe!
    exit /b %ERRORLEVEL%
)
echo [OK] ZynCopy-Setup.exe compiled successfully!

echo.
echo ========================================================
echo   Build Successful! Artifacts in: %BIN_DIR%
echo ========================================================
dir "%BIN_DIR%\*.exe"
