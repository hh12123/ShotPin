using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShotPin.App;

namespace ShotPin.UI
{
    /// <summary>
    /// 挂线最左端的小绳结：唯一可交互的挂线部件，上下拖动可调整挂线高度。
    /// </summary>
    internal sealed class ClotheslineGripForm : Form
    {
        private readonly ClotheslineManager _manager;
        private bool _dragging;
        private bool _hover;

        public ClotheslineGripForm(ClotheslineManager manager)
        {
            _manager = manager;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.WindowBackground;
            DoubleBuffered = true;
            Size = new Size(36, 24);
            Cursor = Cursors.SizeNS;
            Text = "调整挂线高度";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void SetPosition(int left, int lineY)
        {
            Location = new Point(left, lineY - Height / 2);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(Theme.WindowBackground);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle box = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(graphics, box, 6, _hover || _dragging ? Theme.AccentSoft : Theme.PanelSoft);
            using (Pen border = new Pen(_hover || _dragging ? Theme.Accent : Theme.Border, 1.2f))
            {
                using (GraphicsPath path = Theme.RoundedRect(box, 6))
                {
                    graphics.DrawPath(border, path);
                }
            }

            int cx = Width / 2;
            int cy = Height / 2;
            using (Pen dot = new Pen(Color.FromArgb(235, _manager.LineColor), 1.6f))
            {
                dot.StartCap = LineCap.Round;
                dot.EndCap = LineCap.Round;
                graphics.DrawLine(dot, cx - 5, cy - 3, cx + 5, cy - 3);
                graphics.DrawLine(dot, cx - 5, cy, cx + 5, cy);
                graphics.DrawLine(dot, cx - 5, cy + 3, cx + 5, cy + 3);
            }
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
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                Capture = true;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                _manager.SetLineFromScreen(Cursor.Position.Y);
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
            Cursor = Cursors.SizeNS;
            _manager.CommitLinePosition();
            Invalidate();
        }
    }
}
