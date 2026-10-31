# 编译 WhalePet.exe（.NET Framework 4.8 自带的 csc，无需安装 SDK）
# 产物：dist\WhalePet.exe（素材已嵌入）

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    Write-Host "找不到 csc.exe，请安装 .NET Framework 4.8" -ForegroundColor Red
    exit 1
}

$whale = Join-Path $root "assets\whale.png"
if (-not (Test-Path $whale)) {
    Write-Host "assets\whale.png 不存在，先运行 tools\prepare-assets.ps1" -ForegroundColor Red
    exit 1
}

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$src = Join-Path $root "src\WhalePet.cs"
$out = Join-Path $dist "WhalePet.exe"
$resource = "/resource:`"$whale`",whale.png"

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    "/out:$out" "$resource" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "$src"

if ($LASTEXITCODE -ne 0) {
    Write-Host "编译失败" -ForegroundColor Red
    exit $LASTEXITCODE
}

# 音效在运行时从磁盘读，跟 exe 放一起
Copy-Item (Join-Path $root "assets\*.mp3") $dist -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "完成：$out" -ForegroundColor Green
Write-Host "把 dist 整个目录拷走即可使用（WhalePet.exe + *.mp3）" -ForegroundColor Gray
