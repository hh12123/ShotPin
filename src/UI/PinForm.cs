using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShotPin.App;
using ShotPin.Core;

namespace ShotPin.UI
{
    /// <summary>
    /// 一张悬浮截图。无边框、置顶、自绘；可以挂在桌面装饰挂线上，也可以取下自由悬浮。
    /// </summary>
    internal sealed class PinForm : Form
    {
        private readonly PinManager _manager;
        private readonly Guid _id;
        private readonly Bitmap _image;
        private string _name;
        private float _zoom;
        private readonly int _border = 2;
        private bool _hung;

        private bool _dragging;
        private Point _dragStartCursor;
        private Point _dragStartLocation;
        private bool _hover;
        private int _hoverButton = -1;

        private readonly Timer _badgeTimer;
        private bool _badgeVisible;

        private readonly ContextMenuStrip _menu;

        public PinForm(
            PinManager manager,
            Guid id,
            string name,
            Bitmap image,
            Rectangle desiredBounds,
            float zoom,
            bool hung)
        {
            _manager = manager;
            _id = id;
            _name = name;
            _image = image;
            _zoom = GeometryUtil.ClampZoom(zoom);
            _hung = hung;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Theme.WindowBackground;
            Text = name;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Size size = GeometryUtil.WindowSizeForZoom(_image.Size, _zoom, _border);
            Bounds = new Rectangle(desiredBounds.Left, desiredBounds.Top, size.Width, size.Height);

            _badgeTimer = new Timer();
            _badgeTimer.Interval = 1200;
            _badgeTimer.Tick += OnBadgeTick;

            _menu = new ContextMenuStrip();
        }

        public Guid Id
        {
            get { return _id; }
        }

        public string PinName
        {
            get { return _name; }
        }

        public Bitmap Image
        {
            get { return _image; }
        }

        public float Zoom
        {
            get { return _zoom; }
        }

        public bool IsHung
        {
            get { return _hung; }
        }

        public Rectangle ScreenBounds
        {
            get { return Bounds; }
        }

        private int Scale(int value)
        {
            return Theme.Scale(this, value);
        }

        /// <summary>
        /// 由管理器调用：挂线高度变化或被挂上时，把窗口顶部对齐到挂线下方。
        /// </summary>
        internal void SyncHungPosition()
        {
            if (!_hung)
            {
                return;
            }
            int centerX = Left + Width / 2;
            int top = _manager.LineYAtScreen(centerX) + _manager.HangGap;
            if (Top != top)
            {
                Bounds = new Rectangle(Left, top, Width, Height);
            }
        }

        public void SetHung(bool hung)
        {
            if (_hung == hung)
            {
                if (hung)
                {
                    SyncHungPosition();
                }
                return;
            }
            _hung = hung;
            if (_hung)
            {
                SyncHungPosition();
            }
            Invalidate();
            _manager.NotifyHungChanged(this);
        }

        public void SetZoom(float zoom, bool keepCenter)
        {
            zoom = GeometryUtil.ClampZoom(zoom);
            if (Math.Abs(zoom - _zoom) < 0.0001f)
            {
                return;
            }
            Rectangle old = Bounds;
            Size size = GeometryUtil.WindowSizeForZoom(_image.Size, zoom, _border);
            int x;
            int y;
            if (_hung)
            {
                int centerX = old.Left + old.Width / 2;
                x = centerX - size.Width / 2;
                y = _manager.LineYAtScreen(centerX) + _manager.HangGap;
            }
            else
            {
                x = old.Left;
                y = old.Top;
                if (keepCenter)
                {
                    x -= (size.Width - old.Width) / 2;
                    y -= (size.Height - old.Height) / 2;
                }
            }
            _zoom = zoom;
            Bounds = new Rectangle(x, y, size.Width, size.Height);
            ShowZoomBadge();
            Invalidate();
            _manager.NotifyGeometryChanged(this);
        }

        public void ResetZoom()
        {
            SetZoom(1.0f, true);
        }

        public void FitToScreen()
        {
            Screen screen = Screen.FromControl(this);
            if (screen == null)
            {
                return;
            }
            float zoom = GeometryUtil.FitZoom(_image.Size, screen.WorkingArea);
            if (_hung)
            {
                int line = _manager.LineYAtScreen(Left + Width / 2) + _manager.HangGap;
                int available = screen.WorkingArea.Bottom - line - Scale(8);
                if (available > 40)
                {
                    zoom = Math.Min(zoom, available / (float)_image.Height);
                }
            }
            SetZoom(zoom, true);
            if (_hung)
            {
                SyncHungPosition();
            }
            else
            {
                Bounds = GeometryUtil.ClampToWorkArea(Bounds, screen.WorkingArea);
            }
            _manager.NotifyGeometryChanged(this);
        }

        public void MoveBy(int dx, int dy)
        {
            if (_hung)
            {
                if (dx == 0)
                {
                    return;
                }
                Location = new Point(Left + dx, Top);
                SyncHungPosition();
            }
            else
            {
                Location = new Point(Left + dx, Top + dy);
            }
            _manager.NotifyGeometryChanged(this);
        }

        private void ShowZoomBadge()
        {
            _badgeVisible = true;
            _badgeTimer.Stop();
            _badgeTimer.Start();
        }

        private void OnBadgeTick(object sender, EventArgs e)
        {
            _badgeTimer.Stop();
            _badgeVisible = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(Theme.WindowBackground);

            int imageWidth = Math.Max(1, Width - _border * 2);
            int imageHeight = Math.Max(1, Height - _border * 2);
            Rectangle imageRect = new Rectangle(_border, _border, imageWidth, imageHeight);

            if (_zoom >= 1.0f)
            {
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
            }
            else
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            }
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.DrawImage(_image, imageRect);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color borderColor = ContainsFocus ? Theme.Accent : Theme.Border;
            using (Pen borderPen = new Pen(borderColor, 2.0f))
            {
                int inset = 1;
                graphics.DrawRectangle(borderPen, inset, inset,
                    Math.Max(0, Width - inset * 2 - 1), Math.Max(0, Height - inset * 2 - 1));
            }

            if (_hover && !_dragging)
            {
                DrawToolbar(graphics);
            }

            if (_badgeVisible)
            {
                DrawZoomBadge(graphics);
            }
        }

        private void DrawToolbar(Graphics graphics)
        {
            List<PinHit> hits = BuildToolbar();
            for (int i = 0; i < hits.Count; i++)
            {
                Color background = (i == _hoverButton) ? Theme.AccentSoft : Theme.Panel;
                Theme.FillRounded(graphics, hits[i].Bounds, Scale(4), background);
                Color iconColor = (hits[i].Action == 2 && _hung) ? Theme.Accent : Theme.Text;
                Theme.DrawButtonIcon(graphics, hits[i].Bounds, hits[i].Action, iconColor);
            }
        }

        private void DrawZoomBadge(Graphics graphics)
        {
            string text = ((int)Math.Round(_zoom * 100.0f)).ToString() + "%";
            SizeF size = graphics.MeasureString(text, Theme.SmallFont);
            int paddingX = Scale(7);
            int paddingY = Scale(3);
            int width = (int)Math.Ceiling(size.Width) + paddingX * 2;
            int height = (int)Math.Ceiling(size.Height) + paddingY * 2;
            int x = Scale(6);
            int y = Height - height - Scale(6);
            if (y < Scale(6))
            {
                y = Scale(6);
            }
            Rectangle box = new Rectangle(x, y, width, height);
            Theme.FillRounded(graphics, box, Scale(4), Theme.Panel);
            using (SolidBrush brush = new SolidBrush(Theme.Text))
            {
                graphics.DrawString(text, Theme.SmallFont, brush, box.Left + paddingX, box.Top + paddingY);
            }
        }

        private List<PinHit> BuildToolbar()
        {
            List<PinHit> hits = new List<PinHit>();
            int button = Scale(22);
            int gap = Scale(2);
            int margin = Scale(6);
            int total = button * 3 + gap * 2;
            if (Width < total + margin * 2 || Height < button + margin * 2)
            {
                return hits;
            }
            int x = Width - margin - total;
            int y = margin;
            hits.Add(new PinHit(new Rectangle(x, y, button, button), 1));
            hits.Add(new PinHit(new Rectangle(x + button + gap, y, button, button), 2));
            hits.Add(new PinHit(new Rectangle(x + (button + gap) * 2, y, button, button), 0));
            return hits;
        }

        private PinHit HitToolbar(Point point)
        {
            if (!_hover)
            {
                return null;
            }
            List<PinHit> hits = BuildToolbar();
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Bounds.Contains(point))
                {
                    return hits[i];
                }
            }
            return null;
        }

        private int HitButtonIndex(Point point)
        {
            List<PinHit> hits = BuildToolbar();
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Bounds.Contains(point))
                {
                    return i;
                }
            }
            return -1;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _hoverButton = -1;
            if (!_dragging)
            {
                Cursor = Cursors.Default;
            }
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            PinHit hit = HitToolbar(e.Location);
            if (hit != null)
            {
                ExecuteAction(hit.Action);
                return;
            }

            _dragging = true;
            _dragStartCursor = Cursor.Position;
            _dragStartLocation = Location;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Point current = Cursor.Position;
                int dx = current.X - _dragStartCursor.X;
                int dy = current.Y - _dragStartCursor.Y;

                if (_hung)
                {
                    if (dy > Scale(26))
                    {
                        _hung = false;
                        _dragStartCursor = current;
                        _dragStartLocation = Location;
                        dx = 0;
                        dy = 0;
                        _manager.NotifyHungChanged(this);
                    }
                    else
                    {
                        int newLeft = _dragStartLocation.X + dx;
                        int top = _manager.LineYAtScreen(newLeft + Width / 2) + _manager.HangGap;
                        Location = new Point(newLeft, top);
                        _manager.NotifyGeometryChanged(this);
                        return;
                    }
                }

                Location = new Point(
                    _dragStartLocation.X + dx,
                    _dragStartLocation.Y + dy);
                _manager.NotifyGeometryChanged(this);
                return;
            }

            int hoverButton = HitButtonIndex(e.Location);
            if (hoverButton != _hoverButton)
            {
                _hoverButton = hoverButton;
                Invalidate();
            }
            Cursor = (hoverButton >= 0) ? Cursors.Hand : Cursors.SizeAll;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                _manager.PopulateMenu(_menu, this);
                _menu.Show(this, e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left || !_dragging)
            {
                return;
            }
            _dragging = false;
            Capture = false;
            Cursor = Cursors.SizeAll;

            bool hungChanged = false;
            if (!_hung && _manager.IsNearLine(this))
            {
                _hung = true;
                hungChanged = true;
            }
            if (_hung)
            {
                SyncHungPosition();
                Rectangle screen = SystemInformation.VirtualScreen;
                int minLeft = screen.Left - Width + Scale(48);
                int maxLeft = screen.Right - Scale(48);
                int left = Left;
                if (left < minLeft)
                {
                    left = minLeft;
                }
                if (left > maxLeft)
                {
                    left = maxLeft;
                }
                if (left != Left)
                {
                    Location = new Point(left, Top);
                }
                SyncHungPosition();
            }
            else
            {
                Bounds = GeometryUtil.KeepReachable(Bounds, SystemInformation.VirtualScreen, Scale(40));
            }

            if (hungChanged)
            {
                _manager.NotifyHungChanged(this);
            }
            _manager.NotifyGeometryChanged(this);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_dragging)
            {
                return;
            }
            float factor = e.Delta > 0 ? 1.1f : 1.0f / 1.1f;
            SetZoom(_zoom * factor, true);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                ResetZoom();
            }
        }

        private void ExecuteAction(int action)
        {
            if (action == 0)
            {
                Close();
            }
            else if (action == 1)
            {
                _manager.CopyPin(this);
            }
            else if (action == 2)
            {
                if (_hung)
                {
                    _manager.UnhangPin(this);
                }
                else
                {
                    _manager.HangPin(this);
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            if (keyData == (Keys.Control | Keys.C))
            {
                _manager.CopyPin(this);
                return true;
            }
            if (keyData == (Keys.Control | Keys.S))
            {
                _manager.SavePin(this, false);
                return true;
            }
            if (keyData == (Keys.Control | Keys.D0) || keyData == (Keys.Control | Keys.NumPad0))
            {
                ResetZoom();
                return true;
            }
            if (keyData == Keys.Delete)
            {
                Close();
                return true;
            }
            if (keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down)
            {
                int step = (Control.ModifierKeys & Keys.Shift) != 0 ? 10 : 1;
                if (keyData == Keys.Left)
                {
                    MoveBy(-step, 0);
                }
                else if (keyData == Keys.Right)
                {
                    MoveBy(step, 0);
                }
                else if (keyData == Keys.Up)
                {
                    MoveBy(0, -step);
                }
                else
                {
                    MoveBy(0, step);
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _badgeTimer.Stop();
                _badgeTimer.Dispose();
                _menu.Dispose();
                _image.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class PinHit
        {
            public readonly Rectangle Bounds;
            public readonly int Action;

            public PinHit(Rectangle bounds, int action)
            {
                Bounds = bounds;
                Action = action;
            }
        }
    }
}
