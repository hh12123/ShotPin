using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShotPin.App;

namespace ShotPin.UI
{
    internal sealed class ConnectorLine
    {
        public readonly Point A;
        public readonly Point B;

        public ConnectorLine(Point a, Point b)
        {
            A = a;
            B = b;
        }
    }

    /// <summary>
    /// 覆盖整个虚拟桌面、鼠标穿透、逐像素透明的连线层。
    /// 用 GDI+ 画到 32bppPArgb 位图，再复制到 DIB 交给 UpdateLayeredWindow。
    /// </summary>
    internal sealed class ConnectorOverlayForm : Form
    {
        private Bitmap _canvas;
        private Size _canvasSize;
        private IntPtr _memoryDc = IntPtr.Zero;
        private IntPtr _dib = IntPtr.Zero;
        private IntPtr _oldObject = IntPtr.Zero;
        private IntPtr _bits = IntPtr.Zero;

        private List<ConnectorLine> _lines = new List<ConnectorLine>();
        private List<Rectangle> _excludes = new List<Rectangle>();
        private Color _color = Theme.Accent;
        private int _lineWidth = 2;
        private bool _layerFailed;

        public ConnectorOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            Bounds = SystemInformation.VirtualScreen;
            Text = "ShotPin 连线层";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
            IntPtr handle = Handle; // 提前创建句柄，便于注册分层属性
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
            // 分层窗口内容完全由 UpdateLayeredWindow 提供。
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

        public void UpdateStyle(Color color, int width)
        {
            _color = color;
            _lineWidth = Math.Max(1, Math.Min(8, width));
            if (_lines.Count > 0)
            {
                Render();
            }
        }

        public void UpdateScene(IList<Rectangle> excludes, IList<ConnectorLine> lines)
        {
            _excludes = new List<Rectangle>();
            if (excludes != null)
            {
                for (int i = 0; i < excludes.Count; i++)
                {
                    _excludes.Add(excludes[i]);
                }
            }
            _lines = new List<ConnectorLine>();
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    _lines.Add(lines[i]);
                }
            }

            EnsureResources();
            Render();

            if (_lines.Count > 0)
            {
                if (!Visible)
                {
                    Show();
                }
            }
            else
            {
                if (Visible)
                {
                    Hide();
                }
            }
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

                int width = Math.Max(1, _lineWidth);
                int haloWidth = Math.Max(width + 2, 4);
                using (Pen halo = new Pen(Color.FromArgb(105, 255, 255, 255), haloWidth))
                using (Pen core = new Pen(Color.FromArgb(235, _color), width))
                using (SolidBrush dot = new SolidBrush(Color.FromArgb(240, _color)))
                {
                    halo.StartCap = LineCap.Round;
                    halo.EndCap = LineCap.Round;
                    core.StartCap = LineCap.Round;
                    core.EndCap = LineCap.Round;

                    for (int i = 0; i < _lines.Count; i++)
                    {
                        Point a = LocalPoint(_lines[i].A);
                        Point b = LocalPoint(_lines[i].B);
                        graphics.DrawLine(halo, a, b);
                        graphics.DrawLine(core, a, b);
                    }

                    int radius = Math.Max(2, width + 1);
                    for (int i = 0; i < _lines.Count; i++)
                    {
                        Point a = LocalPoint(_lines[i].A);
                        Point b = LocalPoint(_lines[i].B);
                        graphics.FillEllipse(dot, a.X - radius, a.Y - radius, radius * 2, radius * 2);
                        graphics.FillEllipse(dot, b.X - radius, b.Y - radius, radius * 2, radius * 2);
                    }
                }
                graphics.ResetClip();
            }

            ApplyLayer();
        }

        private Rectangle LocalRect(Rectangle screenRect)
        {
            return new Rectangle(
                screenRect.Left - Bounds.Left,
                screenRect.Top - Bounds.Top,
                screenRect.Width,
                screenRect.Height);
        }

        private Point LocalPoint(Point screenPoint)
        {
            return new Point(screenPoint.X - Bounds.Left, screenPoint.Y - Bounds.Top);
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
                Log.Error("connector layer copy failed", ex);
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
                    Log.Warn("UpdateLayeredWindow failed: " + Marshal.GetLastWin32Error());
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
