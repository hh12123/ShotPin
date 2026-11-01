using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShotPin.Core
{
    /// <summary>
    /// 极简 INI 读写：支持 [section]、key=value、空行和 ; / # 注释。
    /// 不依赖任何第三方库。
    /// </summary>
    public sealed class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections;

        public IniFile()
        {
            _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        public static IniFile Load(string path)
        {
            IniFile ini = new IniFile();
            if (path == null || !File.Exists(path))
            {
                return ini;
            }

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            string section = string.Empty;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line == null)
                {
                    continue;
                }
                if (i == 0 && line.Length > 0 && line[0] == '\uFEFF')
                {
                    line = line.Substring(1);
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == ';' || line[0] == '#')
                {
                    continue;
                }
                if (line[0] == '[')
                {
                    int close = line.IndexOf(']');
                    if (close > 0)
                    {
                        section = line.Substring(1, close - 1).Trim();
                    }
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length == 0)
                {
                    continue;
                }
                ini.Set(section, key, value);
            }
            return ini;
        }

        public string Get(string section, string key, string fallback)
        {
            Dictionary<string, string> values;
            if (section == null)
            {
                section = string.Empty;
            }
            if (!_sections.TryGetValue(section, out values))
            {
                return fallback;
            }
            string value;
            if (!values.TryGetValue(key, out value))
            {
                return fallback;
            }
            if (value == null || value.Length == 0)
            {
                return fallback;
            }
            return value;
        }

        public int GetInt(string section, string key, int fallback, int min, int max)
        {
            string text = Get(section, key, null);
            int value;
            if (text == null || !int.TryParse(text, out value))
            {
                return fallback;
            }
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        public bool GetBool(string section, string key, bool fallback)
        {
            string text = Get(section, key, null);
            if (text == null)
            {
                return fallback;
            }
            text = text.Trim();
            if (text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "on", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (text == "0" || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "off", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return fallback;
        }

        public void Set(string section, string key, string value)
        {
            if (section == null)
            {
                section = string.Empty;
            }
            if (key == null)
            {
                return;
            }
            Dictionary<string, string> values;
            if (!_sections.TryGetValue(section, out values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _sections[section] = values;
            }
            values[key] = value == null ? string.Empty : value;
        }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, Dictionary<string, string>> section in _sections)
            {
                if (section.Key.Length > 0)
                {
                    sb.Append('[').Append(section.Key).Append(']').AppendLine();
                }
                foreach (KeyValuePair<string, string> pair in section.Value)
                {
                    sb.Append(pair.Key).Append('=').Append(pair.Value).AppendLine();
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
