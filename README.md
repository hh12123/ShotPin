# ShotPin — Windows 悬浮截图工具

轻量的 Windows 截图 + 参考图工具：快捷键框选后，截图像夹在晾衣绳上一样挂在桌面挂线上，可以多张并存、沿挂线滑动、取下和重新挂上；框选后还能先加文字、彩色线、矩形或椭圆，再确认截图。单个 exe，配置放在同目录 ini 中，无安装、无第三方依赖，使用系统自带的 .NET Framework 4.8。

## 功能

- 区域截图、当前屏幕截图、全部屏幕截图；选区可移动、缩放和用方向键微调。
- 标注：文字、直线、矩形、椭圆，红/橙/绿/蓝/紫五色，支持撤销和清空。
- 文字直接在选区里输入，不弹独立对话框；无滚动条，文本框可调整大小，写完后可拖动文字位置。
- 截图自动挂到桌面装饰挂线；挂线自动避让图片，空线时隐藏。
- 图片沿挂线左右滑动，向下拖取下，拖回挂线附近重新挂上；也可用工具条或右键菜单切换。
- 图片支持拖动、滚轮缩放、复制、保存、置顶。
- 托盘常驻、单实例、可开机自启。

## 系统要求

- Windows 10 1809+ / Windows 11。
- .NET Framework 4.8（Windows 10 1903+/Windows 11 自带）。
- 无需管理员权限；构建只需系统自带的 `csc.exe`（C# 5）。

## 运行

双击 `dist\ShotPin.exe`。程序启动后只显示托盘图标，第一次截图挂上后挂线才出现。

配置和日志都在 exe 同目录：

```text
dist\settings.ini
dist\logs\app-yyyyMMdd.log
```

`dist\settings.ini` 随项目提交；日志、诊断文件和 `pictures\` 截图目录不会提交。

## 快捷键

| 功能 | 快捷键 |
| --- | --- |
| 区域截图 | `Ctrl+Shift+A` |
| 当前屏幕截图 | `Ctrl+Alt+F` |
| 显示/隐藏全部图片与挂线 | `Ctrl+Alt+H` |
| 关闭全部图片 | `Ctrl+Alt+Shift+Q` |
| 完成内联文字输入 | `Ctrl+Enter` |
| 取消框选/当前绘制/关闭当前图 | `Esc` |

热键可在 `settings.ini` 中修改，改完点托盘“重新加载设置”。

## 使用

1. 按 `Ctrl+Shift+A` 框选；松开鼠标后选区保留，可拖动内部移动、拖四角/四边缩放。
2. 在选区上方选“文字 / 线 / 矩形 / 椭圆”和颜色；文字工具在选区内点击后直接输入，`Ctrl+Enter` 或点击框外完成，文字可再拖动换位置。
3. 点选区下方“截图”、按 `Enter` 或双击选区确认；标注会画进最终截图。
4. 新截图自动挂到挂线。拖动挂线左端的小绳结可调整挂线高度；拖动图片左右滑动，向下拖取下，拖回挂线附近松手重新挂上。

## 配置

`dist\settings.ini` 默认值：

```ini
[general]
auto_copy=1
auto_save=0
save_dir=..\pictures
line_y_percent=9
line_color=#C9B99A
line_width=2

[hotkeys]
region=Ctrl+Shift+A
fullscreen=Ctrl+Alt+F
toggle_board=Ctrl+Alt+H
close_all=Ctrl+Alt+Shift+Q
```

- `auto_copy=1`：截图后复制到剪贴板。
- `auto_save=0`：不自动保存；改为 `1` 后，每张截图确认时自动保存 PNG 到 `save_dir`。
- `save_dir`：截图保存目录，默认项目内 `pictures`；该目录已被 `.gitignore` 忽略。
- `line_y_percent`：挂线高度百分比（5–60），拖动绳结会自动更新。
- `line_color` / `line_width`：挂线颜色和粗细（1–6 像素）。
- 修改配置后托盘菜单 →“重新加载设置”，无需重启。

## 从源码构建

```powershell
build.cmd
```

或：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

产物是 `dist\ShotPin.exe` 和 `dist\ShotPin.exe.config`（默认不入库）。`dist\settings.ini` 需要随项目提交。

## 已知限制

- exe 未签名，首次运行可能触发 SmartScreen。
- 截图使用 GDI；HDR、DRM/受保护内容、独占全屏游戏可能偏色或黑屏。
- 挂线是置顶透明层，会出现在其它程序的截图中；ShotPin 自己截图时会先隐藏。
- 图片是会话级的，退出程序后不保留；只保存挂线高度。
- 标注确认后已画进位图，不能再修改；不支持 OCR、滚动截图、录屏。
