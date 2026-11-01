using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using ShotPin.Core;

namespace ShotPin.Tests
{
    internal static class CoreTests
    {
        private static int _passed;
        private static int _failed;

        private static void Check(bool condition, string name)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("PASS  " + name);
            }
            else
            {
                _failed++;
                Console.WriteLine("FAIL  " + name);
            }
        }

        private static bool Near(float a, float b, float tolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("ShotPin core tests");
            Console.WriteLine("------------------");

            TestHotkeys();
            TestIni();
            TestGeometry();
            TestClothesline();

            Console.WriteLine("------------------");
            Console.WriteLine("passed=" + _passed + " failed=" + _failed);
            return _failed == 0 ? 0 : 1;
        }

        private static void TestHotkeys()
        {
            HotkeySpec spec;
            Check(HotkeySpec.TryParse("Ctrl+Alt+A", out spec) &&
                  spec.Modifiers == (HotkeySpec.MOD_CONTROL | HotkeySpec.MOD_ALT) &&
                  spec.VirtualKey == (uint)'A' &&
                  spec.Text == "Ctrl+Alt+A", "hotkey Ctrl+Alt+A");

            Check(HotkeySpec.TryParse("control+shift+f12", out spec) &&
                  spec.Modifiers == (HotkeySpec.MOD_CONTROL | HotkeySpec.MOD_SHIFT) &&
                  spec.VirtualKey == 0x7B &&
                  spec.Text == "Ctrl+Shift+F12", "hotkey control+shift+f12 normalizes");

            Check(HotkeySpec.TryParse("Alt+PrintScreen", out spec) &&
                  spec.VirtualKey == 0x2C && spec.Text == "Alt+PrintScreen", "hotkey PrintScreen");

            Check(HotkeySpec.TryParse("Ctrl+Alt+Shift+Win+Q", out spec) &&
                  spec.Text == "Ctrl+Alt+Shift+Win+Q", "hotkey all modifiers");

            Check(!HotkeySpec.TryParse("", out spec), "reject empty hotkey");
            Check(!HotkeySpec.TryParse(null, out spec), "reject null hotkey");
            Check(!HotkeySpec.TryParse("Ctrl+", out spec), "reject dangling modifier");
            Check(!HotkeySpec.TryParse("Ctrl", out spec), "reject modifier only");
            Check(!HotkeySpec.TryParse("A", out spec), "reject unmodified letter");
            Check(!HotkeySpec.TryParse("Ctrl+Alt", out spec), "reject no key");
            Check(!HotkeySpec.TryParse("Ctrl+Alt+A+B", out spec), "reject two keys");
            Check(!HotkeySpec.TryParse("Ctrl+Alt+ZZ", out spec), "reject unknown key");
            Check(!HotkeySpec.TryParse("Hyper+A", out spec), "reject unknown modifier");

            Check(HotkeySpec.Format(HotkeySpec.MOD_ALT | HotkeySpec.MOD_CONTROL, 0x41) == "Ctrl+Alt+A",
                  "format modifier order");
        }

        private static void TestIni()
        {
            string path = Path.Combine(Path.GetTempPath(), "shotpin_test_" + Guid.NewGuid().ToString("N") + ".ini");
            string roundtripPath = null;
            try
            {
                string content =
                    "\uFEFF; 注释\r\n" +
                    "# 另一种注释\r\n" +
                    "[general]\r\n" +
                    "auto_copy = 1\r\n" +
                    "auto_save=0\r\n" +
                    "name=hello world\r\n" +
                    "\r\n" +
                    "[hotkeys]\r\n" +
                    "region=Ctrl+Alt+A\r\n" +
                    "same=first\r\n" +
                    "same=second\r\n";
                File.WriteAllText(path, content, Encoding.UTF8);

                IniFile ini = IniFile.Load(path);
                Check(ini.GetBool("general", "auto_copy", false), "ini bool true");
                Check(!ini.GetBool("general", "auto_save", true), "ini bool false");
                Check(ini.Get("general", "name", "") == "hello world", "ini value with space");
                Check(ini.Get("hotkeys", "region", "") == "Ctrl+Alt+A", "ini hotkey value");
                Check(ini.Get("hotkeys", "same", "") == "second", "ini duplicate key last wins");
                Check(ini.Get("missing", "key", "fallback") == "fallback", "ini fallback");
                Check(ini.GetInt("general", "auto_copy", 0, 0, 9) == 1, "ini int");
                Check(ini.GetInt("general", "auto_copy", 5, 2, 6) == 2, "ini int clamps low");

                ini.Set("extra", "k", "v");
                roundtripPath = WriteTemp(ini.Serialize());
                IniFile roundtrip = IniFile.Load(roundtripPath);
                Check(roundtrip.Get("extra", "k", "") == "v", "ini serialize roundtrip");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                if (roundtripPath != null && File.Exists(roundtripPath))
                {
                    File.Delete(roundtripPath);
                }
            }
        }

        private static string WriteTemp(string text)
        {
            string path = Path.Combine(Path.GetTempPath(), "shotpin_rt_" + Guid.NewGuid().ToString("N") + ".ini");
            File.WriteAllText(path, text, Encoding.UTF8);
            return path;
        }

        private static void TestGeometry()
        {
            Check(GeometryUtil.ClampZoom(0.01f) == GeometryUtil.MinZoom, "zoom clamp min");
            Check(GeometryUtil.ClampZoom(100f) == GeometryUtil.MaxZoom, "zoom clamp max");
            Check(GeometryUtil.ClampZoom(float.NaN) == 1f, "zoom clamp NaN");

            Check(Near(GeometryUtil.FitZoom(new Size(2000, 1000), new Rectangle(0, 0, 1000, 800)), 0.425f, 0.001f),
                  "fit zoom large width");

            Size size = GeometryUtil.WindowSizeForZoom(new Size(100, 50), 1.5f, 2);
            Check(size == new Size(154, 79), "window size for zoom");

            Rectangle current = new Rectangle(100, 100, 154, 79);
            Rectangle zoomed = GeometryUtil.ZoomedBounds(current, new Size(100, 50), 2.0f, 2);
            Check(zoomed.Width == 204 && zoomed.Height == 104, "zoomed bounds size");
            Check(zoomed.Left + zoomed.Width / 2 == current.Left + current.Width / 2, "zoomed bounds center X");
            Check(zoomed.Top + zoomed.Height / 2 == current.Top + current.Height / 2, "zoomed bounds center Y");

            List<Rectangle> existing = new List<Rectangle>();
            Rectangle work = new Rectangle(0, 0, 1000, 800);
            Rectangle placed = GeometryUtil.CascadePlacement(new Rectangle(0, 0, 100, 100), work, existing);
            Check(placed.Location == new Point(0, 0), "cascade first");

            existing.Add(new Rectangle(0, 0, 100, 100));
            placed = GeometryUtil.CascadePlacement(new Rectangle(0, 0, 100, 100), work, existing);
            Check(placed.Location == new Point(24, 24), "cascade second offset");

            Rectangle reachable = GeometryUtil.KeepReachable(new Rectangle(-500, -500, 100, 100), new Rectangle(0, 0, 1920, 1080), 48);
            Check(reachable.X == -52 && reachable.Y == 0, "keep reachable clamps");

            Rectangle clamp = GeometryUtil.ClampToWorkArea(new Rectangle(2000, 2000, 100, 100), work);
            Check(clamp.Right <= work.Right && clamp.Bottom <= work.Bottom, "clamp to work area");
        }

        private static void TestClothesline()
        {
            Check(ClotheslineLayout.LineYAt(0, 0, 100, 100, 8) == 100, "clothesline y at left");
            Check(ClotheslineLayout.LineYAt(50, 0, 100, 100, 8) == 108, "clothesline y sag center");
            Check(ClotheslineLayout.LineYAt(100, 0, 100, 100, 8) == 100, "clothesline y at right");
            Check(ClotheslineLayout.LineYAt(-10, 0, 100, 100, 8) == 100, "clothesline y clamps left");

            Check(ClotheslineLayout.ClampLineY(10, 40, 200) == 40, "line y clamp min");
            Check(ClotheslineLayout.ClampLineY(300, 40, 200) == 200, "line y clamp max");
            Check(ClotheslineLayout.ClampLineY(120, 40, 200) == 120, "line y clamp middle");

            List<Rectangle> existing = new List<Rectangle>();
            Check(ClotheslineLayout.PlaceOnLine(120, 80, 0, 1000, existing, 8) == 120,
                "place on empty line");

            existing.Add(new Rectangle(100, 0, 100, 100));
            Check(ClotheslineLayout.PlaceOnLine(120, 80, 0, 1000, existing, 8) == 208,
                "place on line avoids overlap");

            existing.Add(new Rectangle(400, 0, 100, 100));
            int placed = ClotheslineLayout.PlaceOnLine(380, 80, 0, 1000, existing, 8);
            Check(placed == 312, "place on line uses nearest free gap");

            Check(ClotheslineLayout.NearLine(200, 190, 15), "near line within threshold");
            Check(!ClotheslineLayout.NearLine(200, 190, 5), "near line outside threshold");
        }
    }
}
