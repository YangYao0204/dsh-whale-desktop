@echo off
rem 编译 WhaleSignal.exe（.NET Framework 4.8 自带 csc，无需 SDK）
rem 产物：同目录 WhaleSignal.exe
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo 找不到 csc.exe，请安装 .NET Framework 4.8
    exit /b 1
)
"%CSC%" /nologo /target:exe /codepage:65001 /out:"%~dp0WhaleSignal.exe" /r:System.dll "%~dp0WhaleSignal.cs"
if errorlevel 1 (
    echo 编译失败
    exit /b 1
)
echo 完成：%~dp0WhaleSignal.exe
