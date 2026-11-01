using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShotPin.App;

namespace ShotPin.UI
{
    /// <summary>
    /// 覆盖整个虚拟桌面的选区层：冻结画面、框选、拖动边角微调，确认后才截图。
    /// </summary>
    internal sealed class SelectionOverlayForm : Form
    {
        private enum DragMode
        {
            None,
            Creating,
            Moving,
            ResizeLeft,
            ResizeTop,
            ResizeRight,
            ResizeBottom,
            ResizeTopLeft,
            ResizeTopRight,
            ResizeBottomLeft,
            ResizeBottomRight
        }

        private static readonly DragMode[] HandleModes = new DragMode[]
        {
            DragMode.ResizeTopLeft,
            DragMode.ResizeTopRight,
            DragMode.ResizeBottomLeft,
            DragMode.ResizeBottomRight,
            DragMode.ResizeLeft,
            DragMode.ResizeTop,
            DragMode.ResizeRight,
            DragMode.ResizeBottom
        };

        private readonly Bitmap _frozen;
        private readonly Rectangle _virtualScreen;

        private Rectangle _selection;
        private Rectangle _selectionAtDragStart;
        private Point _anchor;
        private DragMode _mode;
        private DragMode _hoverHandle;
        private bool _hoverConfirm;
        private bool _pressedConfirm;
        private bool _hasSelection;

        public Rectangle SelectedScreenRect { get; private set; }
        public Bitmap ResultBitmap { get; private set; }

        public SelectionOverlayForm(Bitmap frozen, Rectangle virtualScreen)
        {
            _frozen = frozen;
            _virtualScreen = virtualScreen;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Bounds = virtualScreen;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            BackColor = Color.Black;
            Text = "ShotPin 截图";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        private int Scale(int value)
        {
            return Theme.Scale(this, value);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            BringToFront();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 全部由 OnPaint 绘制，避免双缓冲闪烁。
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            if (_frozen != null)
            {
                graphics.DrawImageUnscaled(_frozen, 0, 0);
            }
            else
            {
                graphics.Clear(Color.Black);
            }

            Rectangle selection = GetLocalSelection();
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                if (!_hasSelection || selection.Width <= 0 || selection.Height <= 0)
                {
                    graphics.FillRectangle(dim, ClientRectangle);
                }
                else
                {
                    FillIfNotEmpty(graphics, dim, 0, 0, ClientSize.Width, selection.Top);
                    FillIfNotEmpty(graphics, dim, 0, selection.Bottom, ClientSize.Width, ClientSize.Height - selection.Bottom);
                    FillIfNotEmpty(graphics, dim, 0, selection.Top, selection.Left, selection.Height);
                    FillIfNotEmpty(graphics, dim, selection.Right, selection.Top,
                        ClientSize.Width - selection.Right, selection.Height);
                }
            }

            if (_hasSelection && selection.Width > 0 && selection.Height > 0)
            {
                using (Pen border = new Pen(Theme.Accent, 1.5f))
                {
                    graphics.DrawRectangle(border, selection.Left, selection.Top,
                        Math.Max(0, selection.Width - 1), Math.Max(0, selection.Height - 1));
                }
                DrawSizeLabel(graphics, selection);
                if (_mode != DragMode.Creating)
                {
                    DrawHandles(graphics, selection);
                    DrawConfirmButton(graphics);
                }
            }

            DrawHint(graphics);
        }

        private static void FillIfNotEmpty(Graphics graphics, Brush brush, int x, int y, int width, int height)
        {
            if (width > 0 && height > 0)
            {
                graphics.FillRectangle(brush, x, y, width, height);
            }
        }

        private void DrawHandles(Graphics graphics, Rectangle selection)
        {
            int size = Math.Max(6, Scale(7));
            for (int i = 0; i < HandleModes.Length; i++)
            {
                DragMode mode = HandleModes[i];
                Point center = HandleCenter(selection, mode);
                Rectangle box = new Rectangle(center.X - size / 2, center.Y - size / 2, size, size);
                bool hover = (_hoverHandle == mode);
                using (SolidBrush fill = new SolidBrush(hover ? Theme.Accent : Color.White))
                using (Pen border = new Pen(Theme.Accent, 1.2f))
                {
                    graphics.FillRectangle(fill, box);
                    graphics.DrawRectangle(border, box);
                }
            }
        }

        private void DrawSizeLabel(Graphics graphics, Rectangle selection)
        {
            string text = selection.Width + " × " + selection.Height;
            SizeF textSize = graphics.MeasureString(text, Theme.SmallFont);
            int paddingX = Scale(8);
            int paddingY = Scale(3);
            int width = (int)Math.Ceiling(textSize.Width) + paddingX * 2;
            int height = (int)Math.Ceiling(textSize.Height) + paddingY * 2;
            int x = selection.Left;
            int y = selection.Top - height - Scale(6);
            if (y < Scale(4))
            {
                y = selection.Top + Scale(6);
            }
            if (x + width > ClientSize.Width - Scale(4))
            {
                x = ClientSize.Width - width - Scale(4);
            }
            if (x < Scale(4))
            {
                x = Scale(4);
            }
            Rectangle box = new Rectangle(x, y, width, height);
            Theme.FillRounded(graphics, box, Scale(4), Theme.Panel);
            using (SolidBrush textBrush = new SolidBrush(Theme.Text))
            {
                graphics.DrawString(text, Theme.SmallFont, textBrush, box.Left + paddingX, box.Top + paddingY);
            }
        }

        private void DrawConfirmButton(Graphics graphics)
        {
            Rectangle box = GetConfirmButtonRect();
            if (box == Rectangle.Empty)
            {
                return;
            }
            Color fill = (_hoverConfirm || _pressedConfirm) ? Color.FromArgb(255, 96, 156, 255) : Theme.Accent;
            Theme.FillRounded(graphics, box, Scale(5), fill);
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush text = new SolidBrush(Color.White))
                {
                    graphics.DrawString("截图", Theme.UiFont, text, box, format);
                }
            }
        }

        private Rectangle GetConfirmButtonRect()
        {
            if (!_hasSelection)
            {
                return Rectangle.Empty;
            }
            Rectangle selection = GetLocalSelection();
            if (selection.Width < 3 || selection.Height < 3)
            {
                return Rectangle.Empty;
            }

            int width = Scale(74);
            int height = Scale(30);
            int gap = Scale(8);
            int x = selection.Right - width;
            int y = selection.Bottom + gap;
            if (y + height > ClientSize.Height - Scale(4))
            {
                y = selection.Bottom - height - gap;
                if (y < Scale(4))
                {
                    y = selection.Top - height - gap;
                }
            }
            x = Math.Max(Scale(4), Math.Min(x, ClientSize.Width - width - Scale(4)));
            y = Math.Max(Scale(4), Math.Min(y, ClientSize.Height - height - Scale(4)));
            return new Rectangle(x, y, width, height);
        }

        private void DrawHint(Graphics graphics)
        {
            string hint = _hasSelection
                ? "拖动边/角调整 · 拖动选区移动 · Enter/双击 确认 · Esc 取消"
                : "拖动鼠标选择区域 · Esc 取消";
            SizeF size = graphics.MeasureString(hint, Theme.UiFont);
            int paddingX = Scale(14);
            int paddingY = Scale(7);
            int width = (int)Math.Ceiling(size.Width) + paddingX * 2;
            int height = (int)Math.Ceiling(size.Height) + paddingY * 2;
            int x = (ClientSize.Width - width) / 2;
            int y = Scale(22);
            if (x < Scale(4))
            {
                x = Scale(4);
            }
            Rectangle box = new Rectangle(x, y, width, height);
            Theme.FillRounded(graphics, box, Scale(7), Theme.Panel);
            using (SolidBrush textBrush = new SolidBrush(Theme.Text))
            {
                graphics.DrawString(hint, Theme.UiFont, textBrush, box.Left + paddingX, box.Top + paddingY);
            }
        }

        private Rectangle GetLocalSelection()
        {
            if (!_hasSelection)
            {
                return Rectangle.Empty;
            }
            Rectangle selection = Rectangle.Intersect(_selection, ClientRectangle);
            return selection;
        }

        private Point HandleCenter(Rectangle selection, DragMode mode)
        {
            int left = selection.Left;
            int top = selection.Top;
            int right = selection.Right - 1;
            int bottom = selection.Bottom - 1;
            int centerX = selection.Left + selection.Width / 2;
            int centerY = selection.Top + selection.Height / 2;
            switch (mode)
            {
                case DragMode.ResizeLeft: return new Point(left, centerY);
                case DragMode.ResizeTop: return new Point(centerX, top);
                case DragMode.ResizeRight: return new Point(right, centerY);
                case DragMode.ResizeBottom: return new Point(centerX, bottom);
                case DragMode.ResizeTopLeft: return new Point(left, top);
                case DragMode.ResizeTopRight: return new Point(right, top);
                case DragMode.ResizeBottomLeft: return new Point(left, bottom);
                case DragMode.ResizeBottomRight: return new Point(right, bottom);
                default: return new Point(centerX, centerY);
            }
        }

        private DragMode HitHandle(Point point)
        {
            Rectangle selection = GetLocalSelection();
            if (selection.Width < 3 || selection.Height < 3)
            {
                return DragMode.None;
            }
            int radius = Scale(11);
            for (int i = 0; i < HandleModes.Length; i++)
            {
                Point center = HandleCenter(selection, HandleModes[i]);
                if (Math.Abs(point.X - center.X) <= radius && Math.Abs(point.Y - center.Y) <= radius)
                {
                    return HandleModes[i];
                }
            }
            return DragMode.None;
        }

        private static Cursor CursorFor(DragMode mode)
        {
            switch (mode)
            {
                case DragMode.ResizeLeft:
                case DragMode.ResizeRight:
                    return Cursors.SizeWE;
                case DragMode.ResizeTop:
                case DragMode.ResizeBottom:
                    return Cursors.SizeNS;
                case DragMode.ResizeTopLeft:
                case DragMode.ResizeBottomRight:
                    return Cursors.SizeNWSE;
                case DragMode.ResizeTopRight:
                case DragMode.ResizeBottomLeft:
                    return Cursors.SizeNESW;
                case DragMode.Moving:
                    return Cursors.SizeAll;
                default:
                    return Cursors.Cross;
            }
        }

        private void UpdateHover(Point point)
        {
            Rectangle confirm = GetConfirmButtonRect();
            bool hoverConfirm = confirm != Rectangle.Empty && confirm.Contains(point);
            DragMode handle = DragMode.None;
            if (!hoverConfirm)
            {
                handle = HitHandle(point);
            }

            Cursor cursor;
            if (hoverConfirm)
            {
                cursor = Cursors.Hand;
            }
            else if (handle != DragMode.None)
            {
                cursor = CursorFor(handle);
            }
            else if (_hasSelection && GetLocalSelection().Contains(point))
            {
                cursor = Cursors.SizeAll;
            }
            else
            {
                cursor = Cursors.Cross;
            }

            bool changed = (hoverConfirm != _hoverConfirm) || (handle != _hoverHandle);
            _hoverConfirm = hoverConfirm;
            _hoverHandle = handle;
            Cursor = cursor;
            if (changed)
            {
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                CancelSelection();
                return;
            }
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _anchor = e.Location;
            if (_hasSelection)
            {
                Rectangle confirm = GetConfirmButtonRect();
                if (confirm != Rectangle.Empty && confirm.Contains(e.Location))
                {
                    _mode = DragMode.None;
                    _pressedConfirm = true;
                    Capture = true;
                    Invalidate();
                    return;
                }

                DragMode handle = HitHandle(e.Location);
                if (handle != DragMode.None)
                {
                    _mode = handle;
                    _selectionAtDragStart = GetLocalSelection();
                    Capture = true;
                    Invalidate();
                    return;
                }

                if (GetLocalSelection().Contains(e.Location))
                {
                    _mode = DragMode.Moving;
                    _selectionAtDragStart = GetLocalSelection();
                    Capture = true;
                    Invalidate();
                    return;
                }
            }

            _mode = DragMode.Creating;
            _selection = Rectangle.Empty;
            _hasSelection = true;
            _hoverHandle = DragMode.None;
            _hoverConfirm = false;
            _pressedConfirm = false;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_pressedConfirm)
            {
                return;
            }
            if (_mode == DragMode.None)
            {
                UpdateHover(e.Location);
                return;
            }

            Point current = e.Location;
            if (_mode == DragMode.Creating)
            {
                _selection = NormalizeSelection(_anchor, current);
            }
            else if (_mode == DragMode.Moving)
            {
                int dx = current.X - _anchor.X;
                int dy = current.Y - _anchor.Y;
                int x = _selectionAtDragStart.Left + dx;
                int y = _selectionAtDragStart.Top + dy;
                int maxX = Math.Max(0, ClientSize.Width - _selectionAtDragStart.Width);
                int maxY = Math.Max(0, ClientSize.Height - _selectionAtDragStart.Height);
                x = Math.Max(0, Math.Min(maxX, x));
                y = Math.Max(0, Math.Min(maxY, y));
                _selection = new Rectangle(x, y, _selectionAtDragStart.Width, _selectionAtDragStart.Height);
            }
            else
            {
                _selection = ResizeSelection(current);
            }

            Invalidate();
        }

        private Rectangle NormalizeSelection(Point a, Point b)
        {
            int left = Math.Min(a.X, b.X);
            int top = Math.Min(a.Y, b.Y);
            int right = Math.Max(a.X, b.X);
            int bottom = Math.Max(a.Y, b.Y);
            Rectangle rect = Rectangle.FromLTRB(left, top, right, bottom);
            rect.Intersect(ClientRectangle);
            return rect;
        }

        private Rectangle ResizeSelection(Point current)
        {
            int left = _selectionAtDragStart.Left;
            int top = _selectionAtDragStart.Top;
            int right = _selectionAtDragStart.Right;
            int bottom = _selectionAtDragStart.Bottom;
            int dx = current.X - _anchor.X;
            int dy = current.Y - _anchor.Y;
            int min = Math.Max(3, Scale(4));

            bool moveLeft = _mode == DragMode.ResizeLeft ||
                            _mode == DragMode.ResizeTopLeft ||
                            _mode == DragMode.ResizeBottomLeft;
            bool moveRight = _mode == DragMode.ResizeRight ||
                             _mode == DragMode.ResizeTopRight ||
                             _mode == DragMode.ResizeBottomRight;
            bool moveTop = _mode == DragMode.ResizeTop ||
                           _mode == DragMode.ResizeTopLeft ||
                           _mode == DragMode.ResizeTopRight;
            bool moveBottom = _mode == DragMode.ResizeBottom ||
                              _mode == DragMode.ResizeBottomLeft ||
                              _mode == DragMode.ResizeBottomRight;

            if (moveLeft)
            {
                left = Math.Max(0, Math.Min(_selectionAtDragStart.Right - min, _selectionAtDragStart.Left + dx));
            }
            if (moveRight)
            {
                right = Math.Min(ClientSize.Width, Math.Max(_selectionAtDragStart.Left + min, _selectionAtDragStart.Right + dx));
            }
            if (moveTop)
            {
                top = Math.Max(0, Math.Min(_selectionAtDragStart.Bottom - min, _selectionAtDragStart.Top + dy));
            }
            if (moveBottom)
            {
                bottom = Math.Min(ClientSize.Height, Math.Max(_selectionAtDragStart.Top + min, _selectionAtDragStart.Bottom + dy));
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressedConfirm)
            {
                _pressedConfirm = false;
                Capture = false;
                if (e.Button == MouseButtons.Left && GetConfirmButtonRect().Contains(e.Location))
                {
                    ConfirmSelection();
                    return;
                }
                UpdateHover(e.Location);
                Invalidate();
                return;
            }
            if (e.Button != MouseButtons.Left || _mode == DragMode.None)
            {
                return;
            }

            _mode = DragMode.None;
            Capture = false;

            Rectangle selection = GetLocalSelection();
            if (selection.Width < 3 || selection.Height < 3)
            {
                _selection = Rectangle.Empty;
                _hasSelection = false;
                _hoverHandle = DragMode.None;
                _hoverConfirm = false;
            }
            else
            {
                _selection = selection;
            }

            UpdateHover(e.Location);
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && _hasSelection && GetLocalSelection().Contains(e.Location))
            {
                ConfirmSelection();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                CancelSelection();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                ConfirmSelection();
                return true;
            }
            if (_hasSelection &&
                (keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down))
            {
                int step = (keyData & Keys.Shift) == Keys.Shift ? 10 : 1;
                int dx = 0;
                int dy = 0;
                if (keyData == Keys.Left)
                {
                    dx = -step;
                }
                else if (keyData == Keys.Right)
                {
                    dx = step;
                }
                else if (keyData == Keys.Up)
                {
                    dy = -step;
                }
                else
                {
                    dy = step;
                }
                Rectangle selection = GetLocalSelection();
                int x = Math.Max(0, Math.Min(ClientSize.Width - selection.Width, selection.Left + dx));
                int y = Math.Max(0, Math.Min(ClientSize.Height - selection.Height, selection.Top + dy));
                _selection = new Rectangle(x, y, selection.Width, selection.Height);
                Invalidate();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ConfirmSelection()
        {
            Rectangle local = GetLocalSelection();
            if (local.Width < 3 || local.Height < 3)
            {
                CancelSelection();
                return;
            }
            Bitmap result = CaptureService.Crop(_frozen, local);
            if (result == null)
            {
                CancelSelection();
                return;
            }

            SelectedScreenRect = new Rectangle(
                _virtualScreen.Left + local.Left,
                _virtualScreen.Top + local.Top,
                local.Width,
                local.Height);
            ResultBitmap = result;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CancelSelection()
        {
            ResultBitmap = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_frozen != null)
                {
                    _frozen.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
