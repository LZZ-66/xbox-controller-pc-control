# Xbox Controller PC Control（Xbox 手柄控制电脑）

用 Xbox 手柄（蓝牙连接）完全控制 Windows 桌面：鼠标移动/点击、滚轮、常用快捷键，以及一个可以用手柄直接操作的屏幕键盘方案。附带托盘菜单汉化、后台运行、关闭时询问等功能。

## 功能特性

- **鼠标控制**：左摇杆移动光标（无加速，速度均匀），LB/RB 为左右键，LT/RT 为后退/前进，右摇杆上下推滚动页面
- **屏幕键盘手柄导航**（`OskNavHelper`）：Windows 11 的屏幕键盘天生不接收键盘焦点，本项目用后台助手把十字键/A/B 转换成"网格选键 + 点击"，实现纯手柄打字
- **关闭时询问**（`JSMGuard`）：关闭手柄控制程序时弹窗询问"最小化到托盘继续运行"或"完全关闭"
- **托盘菜单汉化**：把 JoyShockMapper 托盘右键菜单全部本地化为中文
- **后台常驻**：控制台窗口自动隐藏，仅保留系统托盘图标

## 目录结构

```
├── config/OnStartup.txt    手柄按键映射配置（自动加载）
├── src/JSMGuard.cs         守护/启动程序（关闭询问、隐藏窗口、拉起组件）
├── src/OskNavHelper.cs     屏幕键盘手柄导航助手
├── tools/patch-jsm-zh.ps1  下载 JoyShockMapper 3.6.1 + 托盘菜单汉化 + 放入配置
├── tools/build.ps1         编译两个 C# 程序到 out/
├── LICENSE                 MIT（本项目代码）
└── THIRD-PARTY-NOTICES.md  JoyShockMapper 版权与许可证声明
```

## 环境要求

- Windows 10/11（x64），.NET Framework 4.x（系统自带）
- PowerShell 5.1+
- Xbox 手柄已通过蓝牙配对

## 安装步骤

```powershell
# 1. 下载并汉化 JoyShockMapper（解压到 vendor/，并写入桌面控制配置）
powershell -ExecutionPolicy Bypass -File tools\patch-jsm-zh.ps1

# 2. 编译 JSMGuard / OskNavHelper（输出到 out/）
powershell -ExecutionPolicy Bypass -File tools\build.ps1

# 3. 启动（自动拉起全部组件，隐藏窗口，托盘常驻）
out\JSMGuard.exe
```

可选：为 `out\JSMGuard.exe` 创建桌面快捷方式，之后双击即可启动；
程序已运行时再次双击 = 恢复后台运行（例如选择"完全关闭"之后）。

## 手柄按键布局

| 手柄 | 功能 |
|---|---|
| 左摇杆 | 移动鼠标光标 |
| 右摇杆 | 上推/下推 = 向上/向下滚动（按住连续滚动） |
| LB / RB | 鼠标右键 / 鼠标左键 |
| LT / RT | 浏览器后退 / 前进 |
| 十字键 | 方向键 |
| A / B / X / Y | 回车 / Esc / 空格 / 退格（长按 Y = Ctrl） |
| 按下左摇杆 / 右摇杆 | 鼠标中键 / Tab |
| View 键 | 打开屏幕键盘 → 十字键选键、A 按下、B 收起 |
| Menu 键 | 短按 Alt+Tab，长按开始菜单 |
| Xbox 键 | 开始菜单；双击 = 显示桌面 |

## 自定义

- **光标速度**：`config/OnStartup.txt` 中 `STICK_SENS`（默认 220，越大越快）
- **滚动速度**：`TURBO_PERIOD`（默认 90 毫秒/格，越小越快）
- **修改后重启 JoyShockMapper**（托盘右键 → 退出 → 弹窗选"是"）生效
- **组件路径**：`src/JSMGuard.cs` 顶部常量可改（默认从 exe 相对路径查找，找不到时回退到 D:\Tools 布局）

## 常见问题

- 玩游戏时建议退出本工具（否则会出现双输入），或针对游戏制作 AutoLoad 配置
- 按 Xbox 键弹出"游戏栏"：设置 → 游戏 → Xbox 游戏栏 → 关闭"控制器上的此按钮打开 Xbox 游戏栏"
- 托盘里的「自动加载文件夹」子菜单显示的是配置文件名，属正常现象

## 许可

本项目代码以 MIT 许可证发布（见 LICENSE）。JoyShockMapper 为 MIT 许可的第三方项目，
其版权声明与许可证全文见 THIRD-PARTY-NOTICES.md；托盘菜单汉化是**非官方**修改，
请勿向上游项目反馈本修改版的问题。
