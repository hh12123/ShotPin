using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ShotPin.App;
using ShotPin.Core;

namespace ShotPin.UI
{
    /// <summary>
    /// 管理桌面常驻装饰挂线：线的高度、夹子、挂绳，以及图片的上挂/取下。
    /// </summary>
    internal sealed class ClotheslineManager : IDisposable
    {
        private readonly TrayAppContext _app;
        private readonly PinManager _pins;
        private readonly ClotheslineOverlayForm _overlay;
        private readonly ClotheslineGripForm _grip;
        private readonly Timer _repaintTimer;

        private bool _repaintPending;
        private bool _boardHidden = true;
        private bool _disposed;
        private int _lineY;
        private int _linePercent;
        private int _sag;
        private Color _color;
        private int _width;

        public event EventHandler LineMoved;

        public ClotheslineManager(TrayAppContext app, PinManager pins)
        {
            _app = app;
            _pins = pins;

            AppSettings settings = app.Settings;
            _linePercent = settings.LineYPercent;
            _color = settings.LineColor;
            _width = settings.LineWidth;
            _lineY = ComputeLineY(_linePercent);

            _overlay = new ClotheslineOverlayForm();
            _grip = new ClotheslineGripForm(this);
            _sag = Theme.Scale(_grip, 5);
            PositionGrip();

            _repaintTimer = new Timer();
            _repaintTimer.Interval = 15;
            _repaintTimer.Tick += OnRepaintTick;
        }

        public int LinePercent
        {
            get { return _linePercent; }
        }

        public Color LineColor
        {
            get { return _color; }
        }

        public int HangGap
        {
            get { return _grip == null ? 24 : Theme.Scale(_grip, 16); }
        }

        public int LineYAtScreen(int screenX)
        {
            Rectangle screen = SystemInformation.VirtualScreen;
            return ClotheslineLayout.LineYAt(screenX, screen.Left, screen.Right, _lineY, _sag);
        }

        public bool IsNearLine(PinForm pin)
        {
            if (pin == null)
            {
                return false;
            }
            int line = LineYAtScreen(pin.Left + pin.Width / 2) + HangGap;
            return ClotheslineLayout.NearLine(pin.Top, line, Theme.Scale(pin, 22));
        }

        public void HangPin(PinForm pin)
        {
            if (pin == null)
            {
                return;
            }
            pin.SetHung(true);
            RequestRepaint();
        }

        public void UnhangPin(PinForm pin)
        {
            if (pin == null)
            {
                return;
            }
            pin.SetHung(false);
            RequestRepaint();
        }

        public void NotifyHungChanged(PinForm pin)
        {
            UpdateVisibility();
        }

        public void OnPinClosed(Guid id)
        {
            UpdateVisibility();
        }

        public void Show()
        {
            _boardHidden = false;
            UpdateVisibility();
        }

        public void Hide()
        {
            _boardHidden = true;
            HideSurfaces();
        }

        private bool HasHungPins()
        {
            IList<PinForm> pins = _pins.AllPins;
            for (int i = 0; i < pins.Count; i++)
            {
                if (pins[i].IsHung)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateVisibility()
        {
            if (_disposed)
            {
                return;
            }
            if (!_boardHidden && HasHungPins())
            {
                RepaintNow();
                if (!_overlay.Visible)
                {
                    _overlay.Show();
                }
                if (!_grip.Visible)
                {
                    _grip.Show();
                }
                PositionGrip();
                _grip.BringToFront();
            }
            else
            {
                HideSurfaces();
            }
        }

        private void HideSurfaces()
        {
            if (_overlay != null && _overlay.Visible)
            {
                _overlay.Hide();
            }
            if (_grip != null && _grip.Visible)
            {
                _grip.Hide();
            }
        }

        public void RequestRepaint()
        {
            if (_disposed || _boardHidden)
            {
                return;
            }
            if (!HasHungPins())
            {
                HideSurfaces();
                return;
            }
            _repaintPending = true;
            if (!_repaintTimer.Enabled)
            {
                _repaintTimer.Start();
            }
        }

        private void OnRepaintTick(object sender, EventArgs e)
        {
            _repaintTimer.Stop();
            if (_repaintPending)
            {
                RepaintNow();
            }
        }

        public void RepaintNow()
        {
            _repaintPending = false;
            if (_boardHidden || !HasHungPins())
            {
                HideSurfaces();
                return;
            }

            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            if (_overlay.Bounds != virtualScreen)
            {
                _overlay.Relayout(virtualScreen);
            }

            List<PinForm> pins = new List<PinForm>(_pins.AllPins);
            List<Rectangle> excludes = new List<Rectangle>();
            List<HangerInfo> hangers = new List<HangerInfo>();
            for (int i = 0; i < pins.Count; i++)
            {
                PinForm pin = pins[i];
                excludes.Add(pin.ScreenBounds);
                if (pin.IsHung)
                {
                    int centerX = pin.Left + pin.Width / 2;
                    hangers.Add(new HangerInfo(centerX, LineYAtScreen(centerX), pin.Top));
                }
            }

            _overlay.UpdateScene(excludes, hangers, _lineY, _sag, _color, _width);
        }

        public void SetLineFromScreen(int screenY)
        {
            Rectangle screen = SystemInformation.VirtualScreen;
            int minY = screen.Top + Theme.Scale(_grip, 40);
            int maxY = screen.Top + (int)(screen.Height * 0.6);
            int lineY = ClotheslineLayout.ClampLineY(screenY, minY, maxY);
            if (lineY == _lineY)
            {
                return;
            }

            _lineY = lineY;
            int percent = (int)Math.Round((_lineY - screen.Top) * 100.0 / Math.Max(1, screen.Height));
            if (percent < 5)
            {
                percent = 5;
            }
            if (percent > 60)
            {
                percent = 60;
            }
            _linePercent = percent;

            MoveHungPins();
            PositionGrip();
            RequestRepaint();
        }

        public void CommitLinePosition()
        {
            if (LineMoved != null)
            {
                LineMoved(this, EventArgs.Empty);
            }
            RequestRepaint();
        }

        public void ApplySettings(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }
            _color = settings.LineColor;
            _width = settings.LineWidth;
            _linePercent = settings.LineYPercent;
            _lineY = ComputeLineY(_linePercent);
            MoveHungPins();
            PositionGrip();
            RequestRepaint();
        }

        public void Relayout()
        {
            _lineY = ComputeLineY(_linePercent);
            MoveHungPins();
            PositionGrip();
            if (_overlay != null)
            {
                _overlay.Relayout(SystemInformation.VirtualScreen);
            }
            RequestRepaint();
        }

        private void MoveHungPins()
        {
            IList<PinForm> pins = _pins.AllPins;
            for (int i = 0; i < pins.Count; i++)
            {
                if (pins[i].IsHung)
                {
                    pins[i].SyncHungPosition();
                }
            }
        }

        private int ComputeLineY(int percent)
        {
            Rectangle screen = SystemInformation.VirtualScreen;
            if (percent < 5)
            {
                percent = 5;
            }
            if (percent > 60)
            {
                percent = 60;
            }
            int y = screen.Top + (int)Math.Round(screen.Height * percent / 100.0);
            return ClotheslineLayout.ClampLineY(y, screen.Top + 40, screen.Top + (int)(screen.Height * 0.6));
        }

        private void PositionGrip()
        {
            if (_grip == null)
            {
                return;
            }
            Rectangle screen = SystemInformation.VirtualScreen;
            int x = screen.Left + 22;
            int lineY = LineYAtScreen(x + _grip.Width / 2);
            _grip.SetPosition(x, lineY);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _repaintTimer.Stop();
            _repaintTimer.Dispose();
            try
            {
                _grip.Hide();
                _grip.Dispose();
            }
            catch
            {
            }
            try
            {
                _overlay.Hide();
                _overlay.Dispose();
            }
            catch
            {
            }
        }
    }
}
