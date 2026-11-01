using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using ShotPin.Core;

namespace ShotPin.App
{
    internal sealed class AppSettings
    {
        public bool AutoCopy = true;
        public bool AutoSave = false;
        public string SaveDir = string.Empty;
        public int LineYPercent = 12;
        public Color LineColor = Color.FromArgb(201, 185, 154);
        public int LineWidth = 2;
        public HotkeySpec RegionHotkey;
        public HotkeySpec FullscreenHotkey;
        public HotkeySpec ToggleBoardHotkey;
        public HotkeySpec CloseAllHotkey;
        public readonly List<string> Warnings = new List<string>();
    }

    internal sealed class SettingsStore
    {
        public string FilePath { get; private set; }
        public AppSettings Settings { get; private set; }

        public SettingsStore()
        {
            FilePath = AppPaths.SettingsPath;
            Settings = new AppSettings();
        }

        public void Load()
        {
            Directory.CreateDirectory(Log.DataDir);
            if (!File.Exists(FilePath))
            {
                // 兼容旧版本放在 AppData 的配置，首次升级时迁移到 exe 同目录。
                string legacy = Path.Combine(AppPaths.LegacyDir, "settings.ini");
                if (!string.Equals(legacy, FilePath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(legacy))
                {
                    try
                    {
                        File.Copy(legacy, FilePath, false);
                        Log.Info("migrated settings from " + legacy);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("migrate settings failed", ex);
                    }
                }
            }
            if (!File.Exists(FilePath))
            {
                WriteDefaultFile();
            }

            IniFile ini = IniFile.Load(FilePath);
            AppSettings settings = new AppSettings();

            settings.AutoCopy = ini.GetBool("general", "auto_copy", true);
            settings.AutoSave = ini.GetBool("general", "auto_save", false);
            settings.LineYPercent = ini.GetInt("general", "line_y_percent", 12, 5, 60);
            settings.LineWidth = ini.GetInt("general", "line_width", 2, 1, 6);

            string colorText = ini.Get("general", "line_color", "#C9B99A");
            settings.LineColor = ParseColor(colorText, settings.Warnings);

            string saveDir = ini.Get("general", "save_dir", DefaultSaveDir());
            saveDir = Environment.ExpandEnvironmentVariables(saveDir).Trim();
            if (saveDir.Length == 0)
            {
                saveDir = DefaultSaveDir();
            }
            if (!Path.IsPathRooted(saveDir))
            {
                saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), saveDir);
            }
            settings.SaveDir = saveDir;

            settings.RegionHotkey = ParseHotkey(ini, "region", "Ctrl+Shift+A", settings.Warnings);
            settings.FullscreenHotkey = ParseHotkey(ini, "fullscreen", "Ctrl+Alt+F", settings.Warnings);
            settings.ToggleBoardHotkey = ParseHotkey(ini, "toggle_board", "Ctrl+Alt+H", settings.Warnings);
            settings.CloseAllHotkey = ParseHotkey(ini, "close_all", "Ctrl+Alt+Shift+Q", settings.Warnings);

            Settings = settings;
        }

        public static string DefaultSaveDir()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrEmpty(pictures))
            {
                return Path.Combine(Log.DataDir, "shots");
            }
            return Path.Combine(pictures, "ShotPin");
        }

        /// <summary>
        /// 拖动挂线绳结后，把高度百分比写回配置文件。保留其它键和注释。
        /// </summary>
        public void SaveLineYPercent(int percent)
        {
            if (percent < 5)
            {
                percent = 5;
            }
            if (percent > 60)
            {
                percent = 60;
            }
            if (Settings != null)
            {
                Settings.LineYPercent = percent;
            }
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                bool replaced = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("line_y_percent=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "line_y_percent=" + percent;
                        replaced = true;
                    }
                }

                if (!replaced)
                {
                    List<string> list = new List<string>(lines);
                    int generalIndex = -1;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (string.Equals(list[i].Trim(), "[general]", StringComparison.OrdinalIgnoreCase))
                        {
                            generalIndex = i;
                            break;
                        }
                    }
                    if (generalIndex >= 0)
                    {
                        int insertAt = generalIndex + 1;
                        while (insertAt < list.Count && !list[insertAt].TrimStart().StartsWith("["))
                        {
                            insertAt++;
                        }
                        list.Insert(insertAt, "line_y_percent=" + percent);
                    }
                    else
                    {
                        list.Add("[general]");
                        list.Add("line_y_percent=" + percent);
                    }
                    lines = list.ToArray();
                }

                File.WriteAllLines(FilePath, lines, new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                Log.Error("save line position failed", ex);
            }
        }

        private static HotkeySpec ParseHotkey(IniFile ini, string key, string fallback, List<string> warnings)
        {
            string text = ini.Get("hotkeys", key, fallback);
            HotkeySpec spec;
            if (HotkeySpec.TryParse(text, out spec))
            {
                return spec;
            }
            warnings.Add("快捷键配置无效: " + key + "=" + text + "，已回退到 " + fallback);
            return HotkeySpec.Parse(fallback);
        }

        private static Color ParseColor(string text, List<string> warnings)
        {
            try
            {
                Color color = ColorTranslator.FromHtml(text);
                if (color.A == 0)
                {
                    color = Color.FromArgb(255, color);
                }
                return color;
            }
            catch
            {
                warnings.Add("挂线颜色无效: " + text + "，已回退到 #C9B99A");
                return Color.FromArgb(201, 185, 154);
            }
        }

        private void WriteDefaultFile()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; ShotPin 设置文件");
            sb.AppendLine("; 修改后可在托盘菜单点“重新加载设置”，无需重启。");
            sb.AppendLine();
            sb.AppendLine("[general]");
            sb.AppendLine("; 截图后自动复制到剪贴板");
            sb.AppendLine("auto_copy=1");
            sb.AppendLine("; 截图后自动保存 PNG 到 save_dir");
            sb.AppendLine("auto_save=0");
            sb.AppendLine("save_dir=%USERPROFILE%\\Pictures\\ShotPin");
            sb.AppendLine("; 桌面装饰挂线：高度百分比（5-60）、颜色、粗细（1-6）");
            sb.AppendLine("; 也可以运行时拖动挂线最左端的小绳结调整高度");
            sb.AppendLine("line_y_percent=12");
            sb.AppendLine("line_color=#C9B99A");
            sb.AppendLine("line_width=2");
            sb.AppendLine();
            sb.AppendLine("[hotkeys]");
            sb.AppendLine("; 格式：Ctrl+Shift+A、Ctrl+Shift+F12、Alt+PrintScreen");
            sb.AppendLine("; 如快捷键被其它程序占用，可改成其它组合后重新加载设置。");
            sb.AppendLine("region=Ctrl+Shift+A");
            sb.AppendLine("fullscreen=Ctrl+Alt+F");
            sb.AppendLine("toggle_board=Ctrl+Alt+H");
            sb.AppendLine("close_all=Ctrl+Alt+Shift+Q");
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(true));
        }
    }
}
