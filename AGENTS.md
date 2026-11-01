# ShotPin 项目记忆

## 提交信息

- **所有 Git 提交信息必须使用中文**（标题和正文都要用中文），不要写英文提交说明。
- 提交前检查 `git log -1` 或 `git status`，确认这次提交的描述是中文。
- 提交标题简短说明改动，例如：`截图选区支持内联文字编辑`、`修复挂线高度保存`。

## 项目约定

- 源码按 C# 5 编写，使用系统自带的 `csc.exe` 构建；不要引入字符串插值、`?.`、`nameof`、元组等 C# 6+ 语法。
- 构建和核心测试：`powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test`。
- 构建产物 `dist\ShotPin.exe`、`dist\ShotPin.exe.config` 默认不提交；`dist\settings.ini` 是便携配置，需要提交。
- 截图相关 UI 行为改动后，尽量实际启动 `dist\ShotPin.exe` 做一次 GUI 冒烟验证，而不仅依赖编译。
