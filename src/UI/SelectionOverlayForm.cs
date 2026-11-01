using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShotPin.App;

namespace ShotPin.UI
{
    /// <summary>
    /// 覆盖整个虚拟桌面的选区层：显示冻结画面、压暗未选区域、拖拽确认。
    /// </summary>
    internal sealed class SelectionOverlayForm : Form
    {
        private readonly Bitmap _frozen;
        private readonly Rectangle _virtualScreen;
        private Point _start;
        private Point _current;
        private bool _dragging;
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
                if (selection.Width <= 0 || selection.Height <= 0)
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

            if (selection.Width > 0 && selection.Height > 0)
            {
                using (Pen border = new Pen(Theme.Accent, 1.5f))
                {
                    graphics.DrawRectangle(border, selection.Left, selection.Top,
                        Math.Max(0, selection.Width - 1), Math.Max(0, selection.Height - 1));
                }
                DrawSizeLabel(graphics, selection);
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

        private void DrawSizeLabel(Graphics graphics, Rectangle selection)
        {
            string text = selection.Width + " × " + selection.Height;
            SizeF textSize = graphics.MeasureString(text, Theme.SmallFont);
            int paddingX = Theme.Scale(this, 8);
            int paddingY = Theme.Scale(this, 3);
            int width = (int)Math.Ceiling(textSize.Width) + paddingX * 2;
            int height = (int)Math.Ceiling(textSize.Height) + paddingY * 2;
            int x = selection.Right - width;
            int y = selection.Bottom + Theme.Scale(this, 6);
            if (x < Theme.Scale(this, 4))
            {
                x = Theme.Scale(this, 4);
            }
            if (x + width > ClientSize.Width - Theme.Scale(this, 4))
            {
                x = ClientSize.Width - width - Theme.Scale(this, 4);
            }
            if (y + height > ClientSize.Height - Theme.Scale(this, 4))
            {
                y = selection.Top - height - Theme.Scale(this, 6);
            }
            Rectangle box = new Rectangle(x, y, width, height);
            Theme.FillRounded(graphics, box, Theme.Scale(this, 4), Theme.Panel);
            using (SolidBrush textBrush = new SolidBrush(Theme.Text))
            {
                graphics.DrawString(text, Theme.SmallFont, textBrush, box.Left + paddingX, box.Top + paddingY);
            }
        }

        private void DrawHint(Graphics graphics)
        {
            string hint = "拖动鼠标选择区域 · Esc 取消 · Enter 确认";
            SizeF size = graphics.MeasureString(hint, Theme.UiFont);
            int paddingX = Theme.Scale(this, 14);
            int paddingY = Theme.Scale(this, 7);
            int width = (int)Math.Ceiling(size.Width) + paddingX * 2;
            int height = (int)Math.Ceiling(size.Height) + paddingY * 2;
            int x = (ClientSize.Width - width) / 2;
            int y = Theme.Scale(this, 22);
            Rectangle box = new Rectangle(x, y, width, height);
            Theme.FillRounded(graphics, box, Theme.Scale(this, 7), Theme.Panel);
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
            int left = Math.Min(_start.X, _current.X);
            int top = Math.Min(_start.Y, _current.Y);
            int right = Math.Max(_start.X, _current.X);
            int bottom = Math.Max(_start.Y, _current.Y);
            Rectangle rect = Rectangle.FromLTRB(left, top, right, bottom);
            rect.Intersect(ClientRectangle);
            return rect;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _start = e.Location;
                _current = e.Location;
                _dragging = true;
                _hasSelection = true;
                Capture = true;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                _current = e.Location;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_dragging)
            {
                return;
            }
            _dragging = false;
            Capture = false;
            _current = e.Location;
            ConfirmSelection();
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
