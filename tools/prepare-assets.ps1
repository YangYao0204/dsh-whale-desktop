# 从本机已安装的 dsh-whale-widget 插件复制素材到 assets/
# 这些素材不随仓库分发（见 NOTICE.md），所以构建前必须先跑这个脚本。

param(
    [string]$PluginDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "assets"

if (-not $PluginDir) {
    $candidates = @(
        (Join-Path $env:USERPROFILE ".dsh\profiles\desktop\node_modules\dsh-whale-widget\assets"),
        (Join-Path $env:USERPROFILE ".dsh\profiles\web\node_modules\dsh-whale-widget\assets")
    )
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "DSniang1.png")) { $PluginDir = $c; break }
    }
}

if (-not $PluginDir -or -not (Test-Path (Join-Path $PluginDir "DSniang1.png"))) {
    Write-Host ""
    Write-Host "找不到 dsh-whale-widget 的 assets 目录。" -ForegroundColor Red
    Write-Host "先安装插件：dsh plugin --profile web add dsh-whale-widget" -ForegroundColor Yellow
    Write-Host "或者手动指定：tools\prepare-assets.ps1 -PluginDir <...\dsh-whale-widget\assets>" -ForegroundColor Yellow
    exit 1
}

New-Item -ItemType Directory -Force -Path $assets | Out-Null

$map = @{
    "DSniang1.png" = "whale.png"
    "Ya1.mp3"      = "Ya1.mp3"
    "Ya2.mp3"      = "Ya2.mp3"
    "D1.mp3"       = "D1.mp3"
    "D2.mp3"       = "D2.mp3"
}

foreach ($src in $map.Keys) {
    $from = Join-Path $PluginDir $src
    $to = Join-Path $assets $map[$src]
    if (-not (Test-Path $from)) {
        Write-Host "缺少 $src，跳过" -ForegroundColor Yellow
        continue
    }
    Copy-Item $from $to -Force
    Write-Host "复制 $src -> assets\$($map[$src])"
}

Write-Host ""
Write-Host "完成。接着运行 tools\build.ps1" -ForegroundColor Green
