# ShotPin — Windows 悬浮截图工具

一个轻量的 Windows 截图 + 悬浮参考图工具：快捷键框选截图，图片钉在桌面上，可同时悬浮多张，图片之间可以拉连线。单文件 exe，无安装、无第三方依赖，使用系统自带的 .NET Framework 4.8 运行。

## 功能

- 全局快捷键区域截图、当前屏幕截图、全部屏幕截图。
- 截图以无边框、置顶窗口悬浮在桌面上，可同时存在任意多张。
- 拖动移动、滚轮缩放（10%–800%）、双击复位 100%。
- 悬停显示工具条：复制、连线、关闭。
- 右键菜单：复制、另存为 PNG、适应屏幕、100%、连线到、断开连线、关闭、关闭全部。
- 图片之间画连线；拖动/缩放图片时连线实时跟随；连线经过图片区域会被裁掉，视觉上位于图片下方。
- 关闭任一端图片时自动删除相关连线；重复连接去重、禁止自连。
- 截图后默认自动复制到剪贴板；可选手动保存或自动保存 PNG。
- 托盘常驻，可一键隐藏/显示全部图片、关闭全部图片。
- 单实例；开机自启可在托盘菜单开关。

## 系统要求

- Windows 10 1809+ / Windows 11（64 位或 32 位）。
- .NET Framework 4.8（Windows 10 1903+/Windows 11 自带，无需单独安装）。
- 无需管理员权限。
- 开发/构建也不需要 .NET SDK 或 Visual Studio，使用系统自带的 `csc.exe`（C# 5）。

当前版本在 Windows 11（Build 26200，单显示器 2560×1600，150% 缩放）上验证：截图输出为 2560×1600 物理像素，选区裁剪尺寸精确，连线层鼠标穿透。

## 运行

直接双击：

```
dist\ShotPin.exe
```

程序启动后只显示托盘图标，不创建主窗口。首次运行会生成默认配置：

```
%APPDATA%\ShotPin\settings.ini
```

日志：

```
%APPDATA%\ShotPin\logs\app-yyyyMMdd.log
```

## 默认快捷键

| 功能 | 快捷键 |
| --- | --- |
| 区域截图 | `Ctrl+Shift+A` |
| 当前屏幕截图（光标所在显示器） | `Ctrl+Alt+F` |
| 显示/隐藏全部图片 | `Ctrl+Alt+H` |
| 关闭全部图片 | `Ctrl+Alt+Shift+Q` |
| 取消框选 / 关闭当前图 / 取消连线 | `Esc` |

> 区域截图默认使用 `Ctrl+Shift+A`。若被其它程序占用，托盘会提示，其他功能不受影响；修改 `settings.ini` 后点托盘“重新加载设置”即可。

## 操作说明

### 区域截图

1. 按 `Ctrl+Shift+A`，屏幕冻结并压暗。
2. 拖动鼠标框选；右下角显示选区尺寸。
3. 松开鼠标生成悬浮图；`Esc` 取消。
4. 截图会暂时隐藏已有的悬浮图和连线，避免“截图里套截图”，完成后自动恢复。

### 悬浮图

| 操作 | 效果 |
| --- | --- |
| 左键拖动 | 移动图片 |
| 滚轮 | 围绕窗口中心缩放 |
| 双击 | 恢复 100% |
| `Ctrl+0` | 恢复 100% |
| 悬停右上角 | 复制 / 连线 / 关闭按钮 |
| 右键 | 上下文菜单 |
| `Ctrl+C` | 复制图片到剪贴板 |
| `Ctrl+S` | 保存 PNG 到默认目录 |
| 方向键 / `Shift`+方向键 | 移动 1px / 10px |
| `Esc` / `Delete` | 关闭当前图 |

默认保存目录：`%USERPROFILE%\Pictures\ShotPin`。

### 连线

1. 点图片右上角的“连线”按钮（两个圆点的小图标）。
2. 所有图片进入连线模式，光标变为十字；源图顶部显示提示。
3. 点击另一张图片完成连线；`Esc` 取消；10 秒无操作自动取消。
4. 连线层是鼠标穿透的，删除连线请右键任一端图片：
   - “断开所有连线”
   - 或直接关闭图片，相关连线自动消失。

## 配置

`%APPDATA%\ShotPin\settings.ini`（首次运行自动生成，含中文注释）：

```ini
[general]
auto_copy=1
auto_save=0
save_dir=%USERPROFILE%\Pictures\ShotPin
link_color=#4F8CFF
link_width=2

[hotkeys]
region=Ctrl+Shift+A
fullscreen=Ctrl+Alt+F
toggle_board=Ctrl+Alt+H
close_all=Ctrl+Alt+Shift+Q
```

- 热键格式：`Ctrl+Shift+A`、`Ctrl+Shift+F12`、`Alt+PrintScreen` 等；支持 Ctrl/Alt/Shift/Win、A–Z、0–9、F1–F24、PrintScreen、方向键等。
- 修改后托盘菜单 →“重新加载设置”，无需重启。
- 非法热键/颜色会回退到默认值并在日志和托盘提示。
- 图片和连线是**会话级**的：退出程序后不保留，重新启动从空白开始。

## 开机自启

托盘菜单 →“开机自启”勾选即可，对应注册表：

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ShotPin
```

默认关闭。

## 从源码构建

构建脚本使用系统自带的 C# 编译器，不需要安装 .NET SDK：

```powershell
build.cmd
```

或：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

产物：

```
dist\ShotPin.exe
dist\ShotPin.exe.config
```

说明：

- 构建脚本自动查找 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（缺失时回退 32 位目录）。
- 自动生成 `build\app.ico`、编译 `build\CoreTests.exe` 并运行纯逻辑测试（热键解析、INI、几何端点、级联摆放、连线图）。
- 源码按 C# 5 编写（系统 csc 只支持到 C# 5），请勿使用字符串插值、`?.`、`nameof`、元组等新语法。
- `src\app.manifest` 声明 PerMonitorV2 DPI 感知；`src\app.config` 让 WinForms 菜单也按 DPI 缩放。

### 诊断

```powershell
dist\ShotPin.exe --diag
```

会把环境报告写入 `%APPDATA%\ShotPin\diag.txt`，包括：系统版本、进程架构、DPI 感知级别、物理屏幕/工作区尺寸、四个默认热键的可用性。排查“高分屏截图尺寸不对”“热键被占用”时先看这个文件。

## 项目结构

```
ShotPin\
├─ build.cmd / build.ps1      # 构建 + 测试
├─ src\
│  ├─ Program.cs              # 入口、单实例、异常处理、--diag
│  ├─ Native.cs               # P/Invoke
│  ├─ AssemblyInfo.cs
│  ├─ app.manifest / app.config
│  ├─ Core\                   # 纯逻辑，可单测
│  ├─ App\                    # 设置、日志、热键、截图、托盘调度
│  └─ UI\                     # 选择层、悬浮图、连线层、主题
├─ tests\CoreTests.cs
├─ tools\make-icon.ps1
└─ dist\ShotPin.exe
```

## 已知限制

- exe 未签名，首次运行可能触发 Windows SmartScreen；选择“仍要运行”即可。
- 截图使用 GDI；HDR 广色域内容、DRM/受保护内容、独占全屏游戏可能偏色或黑屏，建议游戏使用无边框全屏。
- 「连线层」是置顶透明层，会出现在**其它程序**的截图中；ShotPin 自己截图时会先隐藏它。
- 图片始终置顶；没有单图取消置顶。用 `Ctrl+Alt+H` 一键隐藏全部。
- 不保存重启后的图片/连线布局。
- 不支持标注、OCR、滚动截图、录屏。
- 混合 DPI 多显示器为尽力支持，未在多屏混合缩放的机器上实测。
- Windows 7/8 不支持。

## 故障排查

1. **热键没反应**：确认程序在托盘运行；看日志 `%APPDATA%\ShotPin\logs\`；若提示被占用，改 `settings.ini` 后“重新加载设置”。
2. **截图模糊/尺寸不对**：运行 `ShotPin.exe --diag`，确认 `dpiAwareness=2`、`virtualScreen` 是物理分辨率；缩放截图时 100% 才是像素 1:1。
3. **连线看不到**：检查两张图是否重叠（重叠区域线会被裁掉）、托盘是否处于“隐藏所有图片”。
4. **无法复制**：剪贴板可能被其它程序占用，重试或稍后再试；日志会记录。
