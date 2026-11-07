# build.ps1
# 用系统自带的 .NET Framework csc 编译器编译 JSMGuard 与 OskNavHelper (无需安装任何 SDK)。
# 用法: powershell -ExecutionPolicy Bypass -File tools\build.ps1
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\out')
)

$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "未找到 csc.exe: $csc (需要 .NET Framework 4.x, Win10/11 自带)" }
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

$gac = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'
$uia1 = Join-Path $gac 'UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll'
$uia2 = Join-Path $gac 'UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll'
$wpb  = Join-Path $gac 'WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll'
foreach ($r in @($uia1, $uia2, $wpb)) { if (-not (Test-Path $r)) { throw "缺少系统程序集: $r" } }

$src = Join-Path $PSScriptRoot '..\src'

Write-Host "编译 JSMGuard..."
& $csc /nologo /target:winexe /optimize `
    "/out:$OutDir\JSMGuard.exe" `
    /reference:System.Windows.Forms.dll `
    (Join-Path $src 'JSMGuard.cs')
if ($LASTEXITCODE -ne 0) { throw 'JSMGuard 编译失败' }

Write-Host "编译 OskNavHelper..."
& $csc /nologo /target:winexe /optimize `
    "/out:$OutDir\OskNavHelper.exe" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    "/reference:$uia1" "/reference:$uia2" "/reference:$wpb" `
    (Join-Path $src 'OskNavHelper.cs')
if ($LASTEXITCODE -ne 0) { throw 'OskNavHelper 编译失败' }

Write-Host "完成, 输出目录: $OutDir"
Get-ChildItem $OutDir -Filter '*.exe' | ForEach-Object { "  $($_.Name) ($($_.Length) 字节)" }
