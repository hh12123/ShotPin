using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ShotPin.Core;

namespace ShotPin.App
{
    internal static class Diagnostics
    {
        public static void WriteReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ShotPin diagnostic report");
            sb.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("exe=" + Application.ExecutablePath);
            sb.AppendLine("os=" + Environment.OSVersion.VersionString);
            sb.AppendLine("os64=" + Environment.Is64BitOperatingSystem + " process64=" + Environment.Is64BitProcess);
            sb.AppendLine("clr=" + Environment.Version);
            sb.AppendLine();

            try
            {
                int awareness;
                try
                {
                    Native.GetProcessDpiAwareness(Native.GetCurrentProcess(), out awareness);
                    sb.AppendLine("dpiAwareness=" + awareness + " (0=unaware, 1=system, 2=permonitor)");
                }
                catch (Exception ex)
                {
                    sb.AppendLine("dpiAwareness=unavailable (" + ex.GetType().Name + ")");
                }

                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    sb.AppendLine("graphicsDpi=" + g.DpiX + "x" + g.DpiY);
                }
                IntPtr dc = Native.GetDC(IntPtr.Zero);
                if (dc != IntPtr.Zero)
                {
                    try
                    {
                        int dpiX = Native.GetDeviceCaps(dc, Native.LOGPIXELSX);
                        sb.AppendLine("deviceDpiX=" + dpiX);
                    }
                    finally
                    {
                        Native.ReleaseDC(IntPtr.Zero, dc);
                    }
                }

                sb.AppendLine("virtualScreen=" + SystemInformation.VirtualScreen);
                Screen[] screens = Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    sb.AppendLine("screen[" + i + "] device=" + screens[i].DeviceName +
                                  " primary=" + screens[i].Primary +
                                  " bounds=" + screens[i].Bounds +
                                  " work=" + screens[i].WorkingArea);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("screenProbeError=" + ex);
            }

            sb.AppendLine();
            HotkeySpec[] specs = new HotkeySpec[]
            {
                HotkeySpec.Parse("Ctrl+Shift+A"),
                HotkeySpec.Parse("Ctrl+Alt+F"),
                HotkeySpec.Parse("Ctrl+Alt+H"),
                HotkeySpec.Parse("Ctrl+Alt+Shift+Q")
            };
            string[] names = new string[] { "region", "fullscreen", "toggle_board", "close_all" };
            for (int i = 0; i < specs.Length; i++)
            {
                bool ok;
                string error;
                TryRegister(specs[i], i + 500, out ok, out error);
                sb.AppendLine("hotkey." + names[i] + "=" + specs[i].Text + " -> " + (ok ? "OK" : "FAILED " + error));
            }

            sb.AppendLine();
            sb.AppendLine("dataDir=" + Log.DataDir);
            sb.AppendLine("settings=" + Path.Combine(Log.DataDir, "settings.ini"));

            try
            {
                Directory.CreateDirectory(Log.DataDir);
                string path = Path.Combine(Log.DataDir, "diag.txt");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Log.Info("diagnostics written to " + path);
            }
            catch
            {
            }
        }

        private static void TryRegister(HotkeySpec spec, int id, out bool ok, out string error)
        {
            ok = false;
            error = string.Empty;
            try
            {
                ok = Native.RegisterHotKey(IntPtr.Zero, id, spec.Modifiers | Native.MOD_NOREPEAT, spec.VirtualKey);
                if (ok)
                {
                    Native.UnregisterHotKey(IntPtr.Zero, id);
                }
                else
                {
                    error = "Win32Error=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
            }
        }
    }
}
