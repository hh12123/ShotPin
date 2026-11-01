using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShotPin.App;

namespace ShotPin.UI
{
    internal static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(79, 140, 255);
        public static readonly Color AccentSoft = Color.FromArgb(140, 79, 140, 255);
        public static readonly Color WindowBackground = Color.FromArgb(24, 26, 32);
        public static readonly Color Border = Color.FromArgb(90, 96, 108);
        public static readonly Color Panel = Color.FromArgb(225, 20, 22, 28);
        public static readonly Color PanelSoft = Color.FromArgb(160, 20, 22, 28);
        public static readonly Color Text = Color.FromArgb(240, 243, 250);
        public static readonly Color Muted = Color.FromArgb(180, 186, 198);
        public static readonly Color Shadow = Color.FromArgb(110, 0, 0, 0);

        private static Font _uiFont;
        private static Font _smallFont;

        public static Font UiFont
        {
            get
            {
                if (_uiFont == null)
                {
                    _uiFont = CreateFont(9.0f, FontStyle.Regular);
                }
                return _uiFont;
            }
        }

        public static Font SmallFont
        {
            get
            {
                if (_smallFont == null)
                {
                    _smallFont = CreateFont(8.0f, FontStyle.Regular);
                }
                return _smallFont;
            }
        }

        private static Font CreateFont(float size, FontStyle style)
        {
            try
            {
                Font font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
                if (font.Name != "Microsoft YaHei UI")
                {
                    font.Dispose();
                    return new Font("Segoe UI", size, style, GraphicsUnit.Point);
                }
                return font;
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
            }
        }

        public static int Scale(Control control, int value)
        {
            int dpi = 96;
            if (control != null)
            {
                dpi = control.DeviceDpi;
                try
                {
                    // .NET Framework 4.8 在部分 PMv2 组合下 DeviceDpi 会返回 96，
                    // 直接问窗口的真实 DPI 更可靠。
                    uint windowDpi = Native.GetDpiForWindow(control.Handle);
                    if (windowDpi >= 72 && windowDpi <= 480)
                    {
                        dpi = (int)windowDpi;
                    }
                }
                catch
                {
                }
            }
            return (int)Math.Round(value * (float)dpi / 96.0f);
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics graphics, Rectangle bounds, int radius, Color color)
        {
            using (GraphicsPath path = RoundedRect(bounds, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                graphics.FillPath(brush, path);
            }
        }

        public static void DrawButtonIcon(Graphics graphics, Rectangle bounds, int action, Color color)
        {
            float scale = Math.Min(bounds.Width, bounds.Height) / 22.0f;
            using (Pen pen = new Pen(color, Math.Max(1.4f, 1.6f * scale)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int cx = bounds.Left + bounds.Width / 2;
                int cy = bounds.Top + bounds.Height / 2;
                int arm = (int)Math.Round(5 * scale);
                if (action == 0)
                {
                    // close
                    graphics.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm);
                    graphics.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm);
                }
                else if (action == 1)
                {
                    // copy
                    Rectangle back = new Rectangle(cx - arm - 1, cy - arm, arm + 3, arm + 3);
                    Rectangle front = new Rectangle(cx - arm + 2, cy - arm + 3, arm + 3, arm + 3);
                    graphics.DrawRectangle(pen, back);
                    graphics.DrawRectangle(pen, front);
                }
                else if (action == 2)
                {
                    // link: two small circles joined by a line
                    int r = Math.Max(2, (int)Math.Round(3 * scale));
                    graphics.DrawEllipse(pen, cx - arm - r, cy - r, r * 2, r * 2);
                    graphics.DrawEllipse(pen, cx + arm - r, cy - r, r * 2, r * 2);
                    graphics.DrawLine(pen, cx - arm + r, cy, cx + arm - r, cy);
                }
            }
        }
    }
}
