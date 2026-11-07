# patch-jsm-zh.ps1
# 下载 JoyShockMapper 官方发布包, 将托盘右键菜单本地化为中文, 并写入桌面控制配置。
# 用法: powershell -ExecutionPolicy Bypass -File tools\patch-jsm-zh.ps1
param(
    [string]$Version = 'v3.6.1',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\vendor')
)

$ErrorActionPreference = 'Stop'
$url = "https://github.com/Electronicks/JoyShockMapper/releases/download/$Version/JoyShockMapper_x64.zip"
$zip = Join-Path $env:TEMP "jsm_$Version.zip"

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

Write-Host "下载 $url"
Invoke-WebRequest -Uri $url -OutFile $zip

Write-Host "解压到 $OutDir"
Expand-Archive -Path $zip -DestinationPath $OutDir -Force
Remove-Item $zip -Force

$exe = Get-ChildItem $OutDir -Recurse -Filter 'JoyShockMapper.exe' | Select-Object -First 1
if (-not $exe) { throw '未找到 JoyShockMapper.exe, 解压失败或版本号错误' }

# 托盘菜单汉化 (UTF-16 字符串替换, 中文比英文短, 剩余字节补零)
Write-Host "汉化托盘菜单: $($exe.FullName)"
$pairs = @(
    @('Show Console', '显示控制台'),
    @('Reconnect controllers', '重新连接手柄'),
    @('AutoLoad', '自动加载配置'),
    @('AutoConnect', '自动连接手柄'),
    @('Whitelist', '白名单'),
    @('Calibrate all devices', '校准所有设备'),
    @('AutoLoad folder', '自动加载文件夹'),
    @('GyroConfigs folder', '陀螺仪配置文件夹'),
    @('Calculate RWC', '计算校准值'),
    @('Hide when minimized', '最小化时隐藏'),
    @('Quit', '退出')
)
$bytes = [System.IO.File]::ReadAllBytes($exe.FullName)
$s = [System.Text.Encoding]::Unicode.GetString($bytes)
foreach ($p in $pairs) {
    $en = [System.Text.Encoding]::Unicode.GetBytes($p[0])
    $zh = [System.Text.Encoding]::Unicode.GetBytes($p[1])
    if ($zh.Length -gt $en.Length) { Write-Warning "跳过 '$($p[0])': 中文比英文长"; continue }
    $idx = $s.IndexOf($p[0]); $n = 0
    while ($idx -ge 0) {
        $off = $idx * 2
        for ($i = 0; $i -lt $en.Length; $i++) { $bytes[$off + $i] = $(if ($i -lt $zh.Length) { $zh[$i] } else { 0 }) }
        $n++; $idx = $s.IndexOf($p[0], $idx + 1)
    }
    Write-Host "  '$($p[0])' -> '$($p[1])': $n 处"
}
[System.IO.File]::WriteAllBytes($exe.FullName, $bytes)

# 写入桌面控制配置 (OnStartup.txt 会被 JSM 自动加载)
$cfg = Join-Path $PSScriptRoot '..\config\OnStartup.txt'
if (Test-Path $cfg) {
    Copy-Item $cfg (Join-Path $exe.DirectoryName 'OnStartup.txt') -Force
    Write-Host "已写入配置: $(Join-Path $exe.DirectoryName 'OnStartup.txt')"
}
Write-Host '完成'
