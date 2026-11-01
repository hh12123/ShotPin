using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ShotPin.App;
using ShotPin.Core;

namespace ShotPin.UI
{
    /// <summary>
    /// 维护图片之间的连线、连线模式的进入/取消，以及透明层重绘节流。
    /// </summary>
    internal sealed class ConnectorManager : IDisposable
    {
        private readonly TrayAppContext _app;
        private readonly PinManager _pins;
        private readonly HotkeyManager _hotkeys;
        private readonly LinkGraph _links = new LinkGraph();

        private readonly Timer _repaintTimer;
        private readonly Timer _linkTimer;
        private ConnectorOverlayForm _overlay;
        private bool _repaintPending;
        private bool _boardHidden;
        private int _escHotkeyId = -1;
        private Color _color = Theme.Accent;
        private int _width = 2;
        private bool _disposed;

        public ConnectorManager(TrayAppContext app, PinManager pins, HotkeyManager hotkeys)
        {
            _app = app;
            _pins = pins;
            _hotkeys = hotkeys;
            if (app != null && app.Settings != null)
            {
                _color = app.Settings.LinkColor;
                _width = app.Settings.LinkWidth;
            }

            _repaintTimer = new Timer();
            _repaintTimer.Interval = 15;
            _repaintTimer.Tick += OnRepaintTick;

            _linkTimer = new Timer();
            _linkTimer.Interval = 10000;
            _linkTimer.Tick += OnLinkTimeout;
        }

        public bool IsLinkMode { get; private set; }
        public Guid LinkSourceId { get; private set; }

        public bool HasLinks(Guid id)
        {
            return _links.HasLinks(id);
        }

        public void Connect(Guid a, Guid b)
        {
            if (_links.Add(a, b))
            {
                Log.Info("link added");
                RequestRepaint();
            }
        }

        public void RemoveAllFor(Guid id)
        {
            _links.RemoveAll(id);
            RequestRepaint();
        }

        public void BeginLink(Guid sourceId)
        {
            if (IsLinkMode && LinkSourceId == sourceId)
            {
                CancelLinkMode();
                return;
            }
            StopLinkMode();
            IsLinkMode = true;
            LinkSourceId = sourceId;
            _linkTimer.Stop();
            _linkTimer.Start();
            RegisterEscapeHotkey();
            InvalidatePins();
            Log.Info("link mode started");
        }

        public void CompleteLink(Guid targetId)
        {
            if (!IsLinkMode)
            {
                return;
            }
            Guid source = LinkSourceId;
            if (targetId == source)
            {
                CancelLinkMode();
                return;
            }
            bool added = _links.Add(source, targetId);
            CancelLinkMode();
            if (added)
            {
                Log.Info("link added by click");
                RequestRepaint();
            }
        }

        public bool CancelLinkModeIfActive()
        {
            if (!IsLinkMode)
            {
                return false;
            }
            CancelLinkMode();
            return true;
        }

        public void CancelLinkMode()
        {
            if (!IsLinkMode)
            {
                return;
            }
            StopLinkMode();
            InvalidatePins();
            Log.Info("link mode cancelled");
        }

        private void StopLinkMode()
        {
            IsLinkMode = false;
            LinkSourceId = Guid.Empty;
            if (_linkTimer != null)
            {
                _linkTimer.Stop();
            }
            UnregisterEscapeHotkey();
        }

        private void RegisterEscapeHotkey()
        {
            if (_hotkeys == null || _escHotkeyId >= 0)
            {
                return;
            }
            _escHotkeyId = _hotkeys.RegisterTransient("取消连线", 0, 0x1B, delegate { CancelLinkMode(); });
        }

        private void UnregisterEscapeHotkey()
        {
            if (_escHotkeyId >= 0 && _hotkeys != null)
            {
                _hotkeys.Unregister(_escHotkeyId);
            }
            _escHotkeyId = -1;
        }

        private void OnLinkTimeout(object sender, EventArgs e)
        {
            _linkTimer.Stop();
            CancelLinkMode();
        }

        public void OnImageClosed(Guid id)
        {
            _links.RemoveAll(id);
            if (IsLinkMode && LinkSourceId == id)
            {
                CancelLinkMode();
            }
            RequestRepaint();
        }

        public void ClearAll()
        {
            _links.Clear();
            CancelLinkMode();
            HideOverlay();
        }

        public void RequestRepaint()
        {
            if (_disposed)
            {
                return;
            }
            if (_links.Count == 0 || _boardHidden)
            {
                HideOverlay();
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
            if (_links.Count == 0 || _boardHidden)
            {
                HideOverlay();
                return;
            }

            List<PinForm> pins = new List<PinForm>(_pins.AllPins);
            List<Rectangle> excludes = new List<Rectangle>();
            for (int i = 0; i < pins.Count; i++)
            {
                excludes.Add(pins[i].ScreenBounds);
            }

            List<ConnectorLine> lines = new List<ConnectorLine>();
            IList<LinkPair> links = _links.All;
            for (int i = 0; i < links.Count; i++)
            {
                PinForm a = _pins.Find(links[i].A);
                PinForm b = _pins.Find(links[i].B);
                if (a == null || b == null)
                {
                    continue;
                }
                Point pa;
                Point pb;
                GeometryUtil.GetEdgePoints(a.ScreenBounds, b.ScreenBounds, out pa, out pb);
                lines.Add(new ConnectorLine(pa, pb));
            }

            if (lines.Count == 0)
            {
                HideOverlay();
                return;
            }

            EnsureOverlay();
            if (_overlay == null)
            {
                return;
            }
            _overlay.UpdateStyle(_color, _width);
            _overlay.UpdateScene(excludes, lines);
        }

        private void EnsureOverlay()
        {
            if (_overlay == null || _overlay.IsDisposed)
            {
                _overlay = new ConnectorOverlayForm();
                _overlay.UpdateStyle(_color, _width);
            }
        }

        private void HideOverlay()
        {
            if (_overlay != null)
            {
                _overlay.Hide();
            }
        }

        public void Show()
        {
            _boardHidden = false;
            if (_links.Count > 0)
            {
                RepaintNow();
            }
        }

        public void Hide()
        {
            _boardHidden = true;
            if (_overlay != null)
            {
                _overlay.Hide();
            }
        }

        public void UpdateStyle(Color color, int width)
        {
            _color = color;
            _width = Math.Max(1, Math.Min(8, width));
            if (_overlay != null)
            {
                _overlay.UpdateStyle(_color, _width);
            }
            RequestRepaint();
        }

        public void Relayout()
        {
            if (_overlay != null)
            {
                _overlay.Relayout(SystemInformation.VirtualScreen);
            }
            RequestRepaint();
        }

        private void InvalidatePins()
        {
            IList<PinForm> pins = _pins.AllPins;
            for (int i = 0; i < pins.Count; i++)
            {
                pins[i].Invalidate();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            StopLinkMode();
            _repaintTimer.Stop();
            _repaintTimer.Dispose();
            _linkTimer.Stop();
            _linkTimer.Dispose();
            if (_overlay != null)
            {
                _overlay.Hide();
                _overlay.Dispose();
                _overlay = null;
            }
        }
    }
}
