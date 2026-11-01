using System;

namespace ShotPin.Core
{
    /// <summary>
    /// 解析与格式化全局快捷键字符串，例如 "Ctrl+Alt+A"。
    /// 纯逻辑，不依赖 WinForms，方便单元测试。
    /// </summary>
    public sealed class HotkeySpec
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public uint Modifiers { get; private set; }
        public uint VirtualKey { get; private set; }
        public string Text { get; private set; }

        private HotkeySpec()
        {
        }

        public static HotkeySpec Parse(string text)
        {
            HotkeySpec spec;
            if (!TryParse(text, out spec))
            {
                throw new FormatException("无法解析快捷键: " + (text == null ? "(null)" : text));
            }
            return spec;
        }

        public static bool TryParse(string text, out HotkeySpec spec)
        {
            spec = null;
            if (text == null)
            {
                return false;
            }
            text = text.Trim();
            if (text.Length == 0)
            {
                return false;
            }

            string[] parts = text.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            uint modifiers = 0;
            string keyToken = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.Length == 0)
                {
                    return false;
                }
                uint modifier;
                if (TryParseModifier(token, out modifier))
                {
                    modifiers |= modifier;
                }
                else
                {
                    if (keyToken != null)
                    {
                        return false;
                    }
                    keyToken = token;
                }
            }

            if (keyToken == null)
            {
                return false;
            }

            uint vk;
            if (!TryParseKey(keyToken, out vk))
            {
                return false;
            }

            // 不允许无修饰键的普通按键，否则会吞掉系统输入。
            // PrintScreen / Pause 是例外。
            if (modifiers == 0 && vk != 0x2C && vk != 0x13)
            {
                return false;
            }

            spec = new HotkeySpec();
            spec.Modifiers = modifiers;
            spec.VirtualKey = vk;
            spec.Text = Format(modifiers, vk);
            return true;
        }

        public static string Format(uint modifiers, uint virtualKey)
        {
            string result = string.Empty;
            if ((modifiers & MOD_CONTROL) != 0)
            {
                result += "Ctrl+";
            }
            if ((modifiers & MOD_ALT) != 0)
            {
                result += "Alt+";
            }
            if ((modifiers & MOD_SHIFT) != 0)
            {
                result += "Shift+";
            }
            if ((modifiers & MOD_WIN) != 0)
            {
                result += "Win+";
            }
            return result + KeyName(virtualKey);
        }

        private static bool TryParseModifier(string token, out uint modifier)
        {
            modifier = 0;
            string upper = token.ToUpperInvariant();
            if (upper == "CTRL" || upper == "CONTROL")
            {
                modifier = MOD_CONTROL;
                return true;
            }
            if (upper == "ALT")
            {
                modifier = MOD_ALT;
                return true;
            }
            if (upper == "SHIFT")
            {
                modifier = MOD_SHIFT;
                return true;
            }
            if (upper == "WIN" || upper == "WINDOWS" || upper == "SUPER")
            {
                modifier = MOD_WIN;
                return true;
            }
            return false;
        }

        private static bool TryParseKey(string token, out uint vk)
        {
            vk = 0;
            if (token.Length == 1)
            {
                char c = char.ToUpperInvariant(token[0]);
                if (c >= 'A' && c <= 'Z')
                {
                    vk = (uint)c;
                    return true;
                }
                if (c >= '0' && c <= '9')
                {
                    vk = (uint)c;
                    return true;
                }
            }

            if (token.Length >= 2 && (token[0] == 'F' || token[0] == 'f'))
            {
                int number;
                if (int.TryParse(token.Substring(1), out number) && number >= 1 && number <= 24)
                {
                    vk = (uint)(0x70 + number - 1);
                    return true;
                }
            }

            string upper = token.ToUpperInvariant();
            switch (upper)
            {
                case "PRINTSCREEN":
                case "PRTSC":
                case "PRTSCN":
                    vk = 0x2C;
                    return true;
                case "PAUSE":
                    vk = 0x13;
                    return true;
                case "INSERT":
                    vk = 0x2D;
                    return true;
                case "DELETE":
                case "DEL":
                    vk = 0x2E;
                    return true;
                case "HOME":
                    vk = 0x24;
                    return true;
                case "END":
                    vk = 0x23;
                    return true;
                case "PAGEUP":
                case "PGUP":
                    vk = 0x21;
                    return true;
                case "PAGEDOWN":
                case "PGDN":
                    vk = 0x22;
                    return true;
                case "SPACE":
                    vk = 0x20;
                    return true;
                case "TAB":
                    vk = 0x09;
                    return true;
                case "ENTER":
                case "RETURN":
                    vk = 0x0D;
                    return true;
                case "BACKSPACE":
                    vk = 0x08;
                    return true;
                case "ESC":
                case "ESCAPE":
                    vk = 0x1B;
                    return true;
                case "LEFT":
                    vk = 0x25;
                    return true;
                case "UP":
                    vk = 0x26;
                    return true;
                case "RIGHT":
                    vk = 0x27;
                    return true;
                case "DOWN":
                    vk = 0x28;
                    return true;
                default:
                    return false;
            }
        }

        private static string KeyName(uint vk)
        {
            if (vk >= 0x41 && vk <= 0x5A)
            {
                return ((char)vk).ToString();
            }
            if (vk >= 0x30 && vk <= 0x39)
            {
                return ((char)vk).ToString();
            }
            if (vk >= 0x70 && vk <= 0x87)
            {
                return "F" + (vk - 0x70 + 1).ToString();
            }
            switch (vk)
            {
                case 0x2C: return "PrintScreen";
                case 0x13: return "Pause";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x08: return "Backspace";
                case 0x1B: return "Esc";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                default: return "0x" + vk.ToString("X2");
            }
        }
    }
}
