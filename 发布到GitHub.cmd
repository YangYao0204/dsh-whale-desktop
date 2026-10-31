@echo off
cd /d "%~dp0"
echo.
echo   DSH Whale Pet  ^|  发布到 GitHub
echo   ==========================================
echo.
set "PATH=%PATH%;%ProgramFiles%\GitHub CLI"

where gh >nul 2>&1
if errorlevel 1 (
    echo   [1/2] 没检测到 GitHub CLI，正在安装 ...
    echo.
    winget install --id GitHub.cli -e --accept-source-agreements --accept-package-agreements
    echo.
    echo   安装完了，请重新双击本文件。
    pause
    exit /b 1
)

gh auth status >nul 2>&1
if errorlevel 1 (
    echo   [1/2] 需要先登录 GitHub（会打开浏览器）...
    echo.
    gh auth login
)

echo   [2/2] 创建仓库并推送 ...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\publish.ps1" -Visibility public

echo.
pause
