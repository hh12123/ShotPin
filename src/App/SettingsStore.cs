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
        public Color LinkColor = Color.FromArgb(79, 140, 255);
        public int LinkWidth = 2;
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
            FilePath = Path.Combine(Log.DataDir, "settings.ini");
            Settings = new AppSettings();
        }

        public void Load()
        {
            Directory.CreateDirectory(Log.DataDir);
            if (!File.Exists(FilePath))
            {
                WriteDefaultFile();
            }

            IniFile ini = IniFile.Load(FilePath);
            AppSettings settings = new AppSettings();

            settings.AutoCopy = ini.GetBool("general", "auto_copy", true);
            settings.AutoSave = ini.GetBool("general", "auto_save", false);
            settings.LinkWidth = ini.GetInt("general", "link_width", 2, 1, 8);

            string colorText = ini.Get("general", "link_color", "#4F8CFF");
            settings.LinkColor = ParseColor(colorText, settings.Warnings);

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
                warnings.Add("连线颜色无效: " + text + "，已回退到 #4F8CFF");
                return Color.FromArgb(79, 140, 255);
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
            sb.AppendLine("; 连线颜色（#RRGGBB）与线宽（1-8）");
            sb.AppendLine("link_color=#4F8CFF");
            sb.AppendLine("link_width=2");
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
