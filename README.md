# ShotPin — Windows 悬浮截图工具

一个轻量的 Windows 截图 + 参考图工具：快捷键截图后，图片会像夹在晾衣绳上一样挂在桌面的一条装饰挂线上；可以沿挂线滑动、取下、再挂回，随取随用。单个 exe（配置放在同目录的 ini 中），无安装、无第三方依赖，使用系统自带的 .NET Framework 4.8 运行。

## 功能

- 有图片挂在上面时，桌面上才显示一条**装饰挂线**，带轻微下垂的绳感和夹子效果；挂线为空时自动隐藏，下一次截图再出现。
- 全局快捷键区域截图、当前屏幕截图、全部屏幕截图。
- 新截图自动挂在挂线上，靠近截图时的水平位置，并用短绳和夹子连接。
- 挂着的图片可以沿挂线左右拖动；向下拖动会“取下”，变成普通悬浮图；拖回挂线附近松手可重新挂上。
- 也可通过图片工具条或右键菜单“挂到挂线 / 从挂线取下”。
- 挂线显示时，拖动最左端的小绳结可调整挂线高度，松手后自动写入配置。
- 图片支持拖动、滚轮缩放（10%–800%）、双击复位 100%。
- 悬停右上角：复制、挂/取、关闭；右键有完整菜单。
- 截图后默认自动复制到剪贴板；可选手动保存或自动保存 PNG。
- 托盘常驻，可一键隐藏/显示全部图片、关闭全部图片。
- 单实例；开机自启可在托盘菜单开关。

## 系统要求

- Windows 10 1809+ / Windows 11（64 位或 32 位）。
- .NET Framework 4.8（Windows 10 1903+/Windows 11 自带，无需单独安装）。
- 无需管理员权限。
- 开发/构建也不需要 .NET SDK 或 Visual Studio，使用系统自带的 `csc.exe`（C# 5）。

当前版本在 Windows 11（Build 26200，单显示器 2560×1600，150% 缩放）上验证：截图输出为 2560×1600 物理像素，选区裁剪尺寸精确，挂线层鼠标穿透。

## 运行

直接双击：

```
dist\ShotPin.exe
```

程序启动后只显示托盘图标，不创建主窗口；第一次截图挂上后，挂线才出现。配置、日志都放在 **exe 同目录**，跟着项目走：

```
dist\settings.ini
dist\logs\app-yyyyMMdd.log
```

首次运行如果 `dist\settings.ini` 不存在，会生成默认配置；旧版本放在 `%APPDATA%\ShotPin\settings.ini` 的配置会在第一次启动时自动迁移到 `dist\settings.ini`。只有 exe 所在目录不可写时，才回退到 `%APPDATA%\ShotPin`。

`settings.ini` 会随项目一起提交，可以直接在编辑器里改；日志和诊断文件已加入 `.gitignore`，不会被提交。

## 默认快捷键

| 功能 | 快捷键 |
| --- | --- |
| 区域截图 | `Ctrl+Shift+A` |
| 当前屏幕截图（光标所在显示器） | `Ctrl+Alt+F` |
| 显示/隐藏全部图片与挂线 | `Ctrl+Alt+H` |
| 关闭全部图片 | `Ctrl+Alt+Shift+Q` |
| 取消框选 / 关闭当前图 | `Esc` |

> 区域截图默认使用 `Ctrl+Shift+A`。若被其它程序占用，托盘会提示，其他功能不受影响；修改 `settings.ini` 后点托盘“重新加载设置”即可。

## 操作说明

### 区域截图

1. 按 `Ctrl+Shift+A`，屏幕冻结并压暗。
2. 拖动鼠标框选；右下角显示选区尺寸。
3. 松开鼠标生成悬浮图；`Esc` 取消。
4. 截图时会暂时隐藏已有的图片和挂线，避免“截图里套截图”，完成后自动恢复。
5. 新图会自动挂到挂线上，位置尽量靠近框选区域。

### 挂线与挂图

- 挂线只在有图片挂着时显示；最后一张图被取下或关闭后，挂线和左端绳结一起隐藏，直到下一次截图或重新挂图。
- 挂线横跨整个桌面，默认在屏幕高度约 12% 处。
- 有图片挂着、挂线显示时，拖动**最左端的小绳结**上下移动，可调整挂线高度；松手后写入 `settings.ini`。
- 挂着的图片顶部会显示短绳和夹子；拖动图片左右滑动时，夹子和绳子跟随。
- 把挂着的图片向**下**拖出一段距离，会从挂线取下，变成自由悬浮图。
- 当挂线仍然显示（至少还有一张图挂着）时，把自由悬浮图拖到挂线附近松手，会重新挂上并自动对齐到挂线下方。
- 如果挂线已经因为空线而隐藏，右键图片选择“挂到挂线”，或点工具条上的挂取按钮，挂线会重新出现。
- 也可以右键图片，选择“挂到挂线 / 从挂线取下”。

### 悬浮图

| 操作 | 效果 |
| --- | --- |
| 左键拖动 | 挂着的图沿挂线滑动；取下的图自由移动 |
| 向下拖出 | 从挂线取下 |
| 拖到挂线附近松手（挂线显示时） | 重新挂上；空线时请用右键/工具条 |
| 滚轮 | 围绕窗口中心缩放 |
| 双击 / `Ctrl+0` | 恢复 100% |
| 悬停右上角 | 复制 / 挂取 / 关闭按钮 |
| 右键 | 上下文菜单（复制、另存为、挂/取、适应屏幕、关闭等） |
| `Ctrl+C` | 复制图片到剪贴板 |
| `Ctrl+S` | 保存 PNG 到默认目录 |
| 方向键 / `Shift`+方向键 | 移动 1px / 10px（挂着的图只左右移动） |
| `Esc` / `Delete` | 关闭当前图 |

默认保存目录：`%USERPROFILE%\Pictures\ShotPin`。

## 配置

`dist\settings.ini`（与 exe 同目录，首次运行自动生成，含中文注释）：

```ini
[general]
auto_copy=1
auto_save=0
save_dir=%USERPROFILE%\Pictures\ShotPin

; 桌面装饰挂线：高度百分比（5-60）、颜色、粗细（1-6）
line_y_percent=12
line_color=#C9B99A
line_width=2

[hotkeys]
region=Ctrl+Shift+A
fullscreen=Ctrl+Alt+F
toggle_board=Ctrl+Alt+H
close_all=Ctrl+Alt+Shift+Q
```

- `line_y_percent` 是挂线高度占屏幕高度的百分比；运行时拖动左边小绳结会自动更新。
- `line_color` 使用 `#RRGGBB`；`line_width` 为 1–6 像素。
- 热键格式：`Ctrl+Shift+A`、`Ctrl+Shift+F12`、`Alt+PrintScreen` 等；支持 Ctrl/Alt/Shift/Win、A–Z、0–9、F1–F24、PrintScreen、方向键等。
- 修改后托盘菜单 →“重新加载设置”，无需重启。
- 非法热键/颜色会回退到默认值并在日志和托盘提示。
- 图片是**会话级**的：退出程序后不保留；挂线高度会保存在配置里。

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

> 这两个是构建产物，默认已被 `.gitignore` 忽略。发布二进制时可以 `git add -f dist\ShotPin.exe dist\ShotPin.exe.config`；`dist\settings.ini` 则是需要提交的便携配置。

说明：

- 构建脚本自动查找 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（缺失时回退 32 位目录）。
- 自动生成 `build\app.ico`、编译 `build\CoreTests.exe` 并运行纯逻辑测试（热键解析、INI、挂线几何、缩放与摆放）。
- 构建前请先从托盘退出正在运行的 ShotPin，否则 `dist\ShotPin.exe` 被占用会导致编译失败。
- 构建脚本只更新 `dist\ShotPin.exe` 和 `.config`，不会覆盖 `dist\settings.ini`。
- 源码按 C# 5 编写（系统 csc 只支持到 C# 5），请勿使用字符串插值、`?.`、`nameof`、元组等新语法。
- `src\app.manifest` 声明 PerMonitorV2 DPI 感知；`src\app.config` 让 WinForms 菜单也按 DPI 缩放。

### 诊断

```powershell
dist\ShotPin.exe --diag
```

会把环境报告写入 `dist\diag.txt`，包括：系统版本、进程架构、DPI 感知级别、物理屏幕/工作区尺寸、四个默认热键的可用性。排查“高分屏截图尺寸不对”“热键被占用”时先看这个文件。

## 项目结构

```
ShotPin\
├─ .gitignore                  # 忽略日志、构建产物、临时文件
├─ build.cmd / build.ps1       # 构建 + 测试
├─ src\
│  ├─ Program.cs               # 入口、单实例、异常处理、--diag
│  ├─ Native.cs                # P/Invoke
│  ├─ AssemblyInfo.cs
│  ├─ app.manifest / app.config
│  ├─ Core\                    # 纯逻辑，可单测（含挂线几何）
│  ├─ App\                     # 路径、设置、日志、热键、截图、托盘调度
│  └─ UI\                      # 挂线层、绳结、悬浮图、选择层、主题
├─ tests\CoreTests.cs
├─ tools\make-icon.ps1
└─ dist\
   ├─ ShotPin.exe              # 构建产物（默认不入库）
   ├─ ShotPin.exe.config       # 构建产物（默认不入库）
   └─ settings.ini             # 便携配置（跟随项目，需要提交）
```

`dist\logs\`、`dist\diag.txt`、`build\` 都由 `.gitignore` 忽略；提交时只需要保证 `dist\settings.ini` 被加入 Git。

## 已知限制

- exe 未签名，首次运行可能触发 Windows SmartScreen；选择“仍要运行”即可。
- 截图使用 GDI；HDR 广色域内容、DRM/受保护内容、独占全屏游戏可能偏色或黑屏，建议游戏使用无边框全屏。
- 挂线是置顶透明层，会出现在**其它程序**的截图中；ShotPin 自己截图时会先隐藏它。
- 挂线层本身鼠标穿透，只有最左端的小绳结可点击拖动。
- 图片始终置顶；没有单图取消置顶。用 `Ctrl+Alt+H` 一键隐藏全部。
- 不保存重启后的图片布局；挂线高度会保存。
- 不支持标注、OCR、滚动截图、录屏。
- 混合 DPI 多显示器为尽力支持，未在多屏混合缩放的机器上实测。
- Windows 7/8 不支持。

## 故障排查

1. **热键没反应**：确认程序在托盘运行；看日志 `dist\logs\`；若提示被占用，改 `dist\settings.ini` 后“重新加载设置”。
2. **截图模糊/尺寸不对**：运行 `ShotPin.exe --diag`，确认 `dpiAwareness=2`、`virtualScreen` 是物理分辨率；缩放截图时 100% 才是像素 1:1。
3. **看不到挂线**：如果当前没有挂着的图片，挂线会故意隐藏；截一张新图或把图片重新挂上即可。也可能处于“隐藏所有图片”状态，按 `Ctrl+Alt+H` 恢复；还可以调大 `line_y_percent`。
4. **图片没挂上**：右键图片选择“挂到挂线”，或把图片顶部拖到挂线附近再松手。
5. **无法复制**：剪贴板可能被其它程序占用，重试或稍后再试；日志会记录。
