# 一键发布到 GitHub（需要先装 GitHub CLI 并登录）
#   1) winget install GitHub.cli
#   2) 重新打开终端，执行  gh auth login
#   3) 运行本脚本：powershell -ExecutionPolicy Bypass -File tools\publish.ps1 -Visibility public

param(
    [string]$Name = "",
    [ValidateSet("public", "private")]
    [string]$Visibility = "public",
    [string]$Description = "DSH 小鲸鱼桌面挂件：把 DeepSeek Harness 右下角的小鲸鱼搬到 Windows 桌面（透明置顶、余额/峰谷、点击对话、吸附翻转）"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $Name) { $Name = Split-Path -Leaf $root }

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    Write-Host ""
    Write-Host "没有找到 GitHub CLI（gh）。两条路：" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "A. 装 gh 后一键推送：" -ForegroundColor Cyan
    Write-Host "   winget install GitHub.cli"
    Write-Host "   gh auth login"
    Write-Host "   powershell -ExecutionPolicy Bypass -File tools\publish.ps1"
    Write-Host ""
    Write-Host "B. 手动建仓库再推：" -ForegroundColor Cyan
    Write-Host "   1) 在 https://github.com/new 新建空仓库（不要勾 README）"
    Write-Host "   2) 执行："
    Write-Host "      git remote add origin https://github.com/<你的用户名>/$Name.git"
    Write-Host "      git push -u origin main"
    Write-Host ""
    exit 1
}

$auth = & gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "gh 还没登录，先执行：gh auth login" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path (Join-Path $root ".git"))) {
    Write-Host "当前目录不是 git 仓库" -ForegroundColor Red
    exit 1
}

$existing = & git remote 2>$null
if ($existing -contains "origin") {
    Write-Host "已经有 origin，直接推送……"
    git push -u origin main
    exit $LASTEXITCODE
}

Write-Host "创建仓库 $Name（$Visibility）并推送……" -ForegroundColor Cyan
& gh repo create $Name "--$Visibility" --source . --remote origin --push --description "$Description"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "发布完成 🎉" -ForegroundColor Green
    & gh repo view --web
}
