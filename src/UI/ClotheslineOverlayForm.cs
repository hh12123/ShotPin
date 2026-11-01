using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShotPin.App;
using ShotPin.Core;

namespace ShotPin.UI
{
    internal sealed class HangerInfo
    {
        public readonly int X;
        public readonly int LineY;
        public readonly int PinTop;

        public HangerInfo(int x, int lineY, int pinTop)
        {
            X = x;
            LineY = lineY;
            PinTop = pinTop;
        }
    }

    /// <summary>
    /// 覆盖整个虚拟桌面的装饰挂线层：鼠标穿透、逐像素透明，画绳线、夹子和挂绳。
    /// </summary>
    internal sealed class ClotheslineOverlayForm : Form
    {
        private Bitmap _canvas;
        private Size _canvasSize;
        private IntPtr _memoryDc = IntPtr.Zero;
        private IntPtr _dib = IntPtr.Zero;
        private IntPtr _oldObject = IntPtr.Zero;
        private IntPtr _bits = IntPtr.Zero;

        private List<HangerInfo> _hangers = new List<HangerInfo>();
        private List<Rectangle> _excludes = new List<Rectangle>();
        private int _lineY;
        private int _sag = 8;
        private Color _color = Color.FromArgb(201, 185, 154);
        private int _lineWidth = 2;
        private bool _layerFailed;

        public ClotheslineOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            Bounds = SystemInformation.VirtualScreen;
            Text = "ShotPin 挂线层";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
            IntPtr handle = Handle;
            GC.KeepAlive(handle);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT |
                              Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
        }

        public void Relayout(Rectangle virtualScreen)
        {
            if (Bounds != virtualScreen)
            {
                Bounds = virtualScreen;
                DestroyResources();
                EnsureResources();
                Render();
            }
        }

        public void UpdateScene(
            IList<Rectangle> excludes,
            IList<HangerInfo> hangers,
            int lineY,
            int sag,
            Color color,
            int lineWidth)
        {
            _excludes = new List<Rectangle>();
            if (excludes != null)
            {
                for (int i = 0; i < excludes.Count; i++)
                {
                    _excludes.Add(excludes[i]);
                }
            }
            _hangers = new List<HangerInfo>();
            if (hangers != null)
            {
                for (int i = 0; i < hangers.Count; i++)
                {
                    _hangers.Add(hangers[i]);
                }
            }
            _lineY = lineY;
            _sag = Math.Max(0, sag);
            _color = color;
            _lineWidth = Math.Max(1, Math.Min(8, lineWidth));

            EnsureResources();
            Render();
        }

        private void EnsureResources()
        {
            Size size = SystemInformation.VirtualScreen.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                size = new Size(1, 1);
            }
            if (_canvas != null && _canvasSize == size && _memoryDc != IntPtr.Zero)
            {
                return;
            }

            DestroyResources();
            _canvasSize = size;
            _canvas = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(_canvas))
            {
                graphics.Clear(Color.Transparent);
            }

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                _memoryDc = Native.CreateCompatibleDC(screenDc);
                Native.BITMAPINFO info = new Native.BITMAPINFO();
                info.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
                info.bmiHeader.biWidth = size.Width;
                info.bmiHeader.biHeight = -size.Height;
                info.bmiHeader.biPlanes = 1;
                info.bmiHeader.biBitCount = 32;
                info.bmiHeader.biCompression = Native.BI_RGB;
                _dib = Native.CreateDIBSection(screenDc, ref info, 0, out _bits, IntPtr.Zero, 0);
                if (_dib != IntPtr.Zero)
                {
                    _oldObject = Native.SelectObject(_memoryDc, _dib);
                }
            }
            finally
            {
                if (screenDc != IntPtr.Zero)
                {
                    Native.ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        private void DestroyResources()
        {
            if (_memoryDc != IntPtr.Zero && _oldObject != IntPtr.Zero)
            {
                Native.SelectObject(_memoryDc, _oldObject);
                _oldObject = IntPtr.Zero;
            }
            if (_dib != IntPtr.Zero)
            {
                Native.DeleteObject(_dib);
                _dib = IntPtr.Zero;
            }
            if (_memoryDc != IntPtr.Zero)
            {
                Native.DeleteDC(_memoryDc);
                _memoryDc = IntPtr.Zero;
            }
            if (_canvas != null)
            {
                _canvas.Dispose();
                _canvas = null;
            }
            _bits = IntPtr.Zero;
        }

        private void Render()
        {
            if (_canvas == null)
            {
                return;
            }

            using (Graphics graphics = Graphics.FromImage(_canvas))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                for (int i = 0; i < _excludes.Count; i++)
                {
                    Rectangle local = LocalRect(_excludes[i]);
                    if (local.Width > 0 && local.Height > 0)
                    {
                        graphics.ExcludeClip(local);
                    }
                }

                DrawRope(graphics);
                DrawHangers(graphics);
                graphics.ResetClip();
            }

            ApplyLayer();
        }

        private void DrawRope(Graphics graphics)
        {
            int width = Width;
            int height = Height;
            if (width <= 1 || height <= 1)
            {
                return;
            }

            int step = 8;
            List<PointF> points = new List<PointF>();
            for (int x = 0; x <= width; x += step)
            {
                int screenX = Bounds.Left + x;
                int lineScreenY = ClotheslineLayout.LineYAt(
                    screenX, Bounds.Left, Bounds.Right, _lineY, _sag);
                points.Add(new PointF(x, lineScreenY - Bounds.Top));
            }
            PointF last = new PointF(width - 1,
                ClotheslineLayout.LineYAt(Bounds.Right - 1, Bounds.Left, Bounds.Right, _lineY, _sag) - Bounds.Top);
            if (points.Count == 0 || points[points.Count - 1].X < last.X)
            {
                points.Add(last);
            }

            if (points.Count < 2)
            {
                return;
            }

            using (Pen shadow = new Pen(Color.FromArgb(85, 0, 0, 0), _lineWidth + 2))
            using (Pen core = new Pen(Color.FromArgb(215, _color), _lineWidth))
            using (Pen shine = new Pen(Color.FromArgb(85, 255, 255, 255), 1))
            {
                shadow.LineJoin = LineJoin.Round;
                shadow.StartCap = LineCap.Round;
                shadow.EndCap = LineCap.Round;
                core.LineJoin = LineJoin.Round;
                core.StartCap = LineCap.Round;
                core.EndCap = LineCap.Round;
                shine.StartCap = LineCap.Round;
                shine.EndCap = LineCap.Round;

                graphics.DrawLines(shadow, points.ToArray());
                graphics.DrawLines(core, points.ToArray());
                graphics.DrawLines(shine, points.ToArray());
            }

            DrawKnot(graphics, points[0]);
            DrawKnot(graphics, points[points.Count - 1]);
        }

        private void DrawKnot(Graphics graphics, PointF point)
        {
            int radius = Math.Max(3, _lineWidth + 2);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(235, _color)))
            {
                graphics.FillEllipse(shadow, point.X - radius - 1, point.Y - radius + 1,
                    radius * 2 + 2, radius * 2 + 2);
                graphics.FillEllipse(fill, point.X - radius, point.Y - radius,
                    radius * 2, radius * 2);
            }
        }

        private void DrawHangers(Graphics graphics)
        {
            if (_hangers.Count == 0)
            {
                return;
            }

            using (Pen stringPen = new Pen(Color.FromArgb(160, _color), 1.4f))
            using (Pen stringShadow = new Pen(Color.FromArgb(85, 0, 0, 0), 3.0f))
            using (Pen clipShadow = new Pen(Color.FromArgb(120, 0, 0, 0), 4.0f))
            using (Pen clipPen = new Pen(Color.FromArgb(235, _color), 2.2f))
            using (SolidBrush ring = new SolidBrush(Color.FromArgb(200, _color)))
            {
                stringPen.StartCap = LineCap.Round;
                stringPen.EndCap = LineCap.Round;
                stringShadow.StartCap = LineCap.Round;
                stringShadow.EndCap = LineCap.Round;
                clipPen.StartCap = LineCap.Round;
                clipPen.EndCap = LineCap.Round;

                for (int i = 0; i < _hangers.Count; i++)
                {
                    HangerInfo hanger = _hangers[i];
                    int x = hanger.X - Bounds.Left;
                    int lineY = hanger.LineY - Bounds.Top;
                    int pinTop = hanger.PinTop - Bounds.Top;
                    if (x < 0 || x >= Width || pinTop <= lineY)
                    {
                        continue;
                    }

                    graphics.DrawLine(stringShadow, x, lineY + 8, x, pinTop);
                    graphics.DrawLine(stringPen, x, lineY + 8, x, pinTop);

                    // 深色底衬让夹子在浅色桌面上也看得清。
                    graphics.DrawLine(clipShadow, x - 3, lineY - 2, x - 3, lineY + 10);
                    graphics.DrawLine(clipShadow, x + 3, lineY - 2, x + 3, lineY + 10);
                    graphics.DrawLine(clipPen, x - 3, lineY - 2, x - 3, lineY + 10);
                    graphics.DrawLine(clipPen, x + 3, lineY - 2, x + 3, lineY + 10);
                    graphics.DrawLine(clipPen, x - 3, lineY + 1, x + 3, lineY + 1);

                    int radius = 2;
                    graphics.FillEllipse(ring, x - radius, pinTop - radius, radius * 2, radius * 2);
                }
            }
        }

        private Rectangle LocalRect(Rectangle screenRect)
        {
            return new Rectangle(
                screenRect.Left - Bounds.Left,
                screenRect.Top - Bounds.Top,
                screenRect.Width,
                screenRect.Height);
        }

        private void ApplyLayer()
        {
            if (_canvas == null || _memoryDc == IntPtr.Zero || _dib == IntPtr.Zero || _bits == IntPtr.Zero)
            {
                return;
            }

            BitmapData data = null;
            try
            {
                data = _canvas.LockBits(new Rectangle(0, 0, _canvas.Width, _canvas.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                int rowBytes = _canvas.Width * 4;
                byte[] row = new byte[rowBytes];
                for (int y = 0; y < _canvas.Height; y++)
                {
                    IntPtr source = new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride);
                    Marshal.Copy(source, row, 0, rowBytes);
                    IntPtr destination = new IntPtr(_bits.ToInt64() + (long)y * rowBytes);
                    Marshal.Copy(row, 0, destination, rowBytes);
                }
            }
            catch (Exception ex)
            {
                Log.Error("clothesline layer copy failed", ex);
                return;
            }
            finally
            {
                if (data != null)
                {
                    _canvas.UnlockBits(data);
                }
            }

            Native.POINT destinationPoint = new Native.POINT(Bounds.Left, Bounds.Top);
            Native.SIZE size = new Native.SIZE(_canvasSize.Width, _canvasSize.Height);
            Native.POINT sourcePoint = new Native.POINT(0, 0);
            Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
            blend.BlendOp = Native.AC_SRC_OVER;
            blend.BlendFlags = 0;
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = Native.AC_SRC_ALPHA;

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                bool ok = Native.UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref destinationPoint,
                    ref size,
                    _memoryDc,
                    ref sourcePoint,
                    0,
                    ref blend,
                    Native.ULW_ALPHA);
                if (!ok && !_layerFailed)
                {
                    _layerFailed = true;
                    Log.Warn("clothesline UpdateLayeredWindow failed: " + Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                if (screenDc != IntPtr.Zero)
                {
                    Native.ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DestroyResources();
            }
            base.Dispose(disposing);
        }
    }
}
