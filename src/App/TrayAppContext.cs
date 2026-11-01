using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using ShotPin.Core;
using ShotPin.UI;

namespace ShotPin.App
{
    internal sealed class TrayAppContext : ApplicationContext
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "ShotPin";

        private readonly SettingsStore _settingsStore;
        private readonly HotkeyManager _hotkeys;
        private readonly PinManager _pins;
        private readonly ConnectorManager _connectors;

        private NotifyIcon _tray;
        private Icon _trayIcon;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _regionItem;
        private ToolStripMenuItem _fullscreenItem;
        private ToolStripMenuItem _virtualItem;
        private ToolStripMenuItem _toggleItem;
        private ToolStripMenuItem _closeAllItem;
        private ToolStripMenuItem _autostartItem;
        private System.Windows.Forms.Timer _startupTimer;
        private List<string> _startupMessages = new List<string>();
        private bool _capturing;
        private bool _disposed;

        public static TrayAppContext Current { get; private set; }

        public TrayAppContext()
        {
            Current = this;
            _settingsStore = new SettingsStore();
            _settingsStore.Load();
            Log.Info("settings loaded from " + _settingsStore.FilePath);

            _hotkeys = new HotkeyManager();
            _pins = new PinManager(this);
            _connectors = new ConnectorManager(this, _pins, _hotkeys);
            _pins.Connectors = _connectors;

            BuildTray();

            List<string> failures = RegisterHotkeys();
            for (int i = 0; i < failures.Count; i++)
            {
                _startupMessages.Add(failures[i]);
            }
            if (_settingsStore.Settings.Warnings.Count > 0)
            {
                for (int i = 0; i < _settingsStore.Settings.Warnings.Count; i++)
                {
                    _startupMessages.Add(_settingsStore.Settings.Warnings[i]);
                    Log.Warn(_settingsStore.Settings.Warnings[i]);
                }
            }

            if (_startupMessages.Count > 0)
            {
                _startupTimer = new System.Windows.Forms.Timer();
                _startupTimer.Interval = 1200;
                _startupTimer.Tick += OnStartupTimerTick;
                _startupTimer.Start();
            }

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            Log.Info("tray ready");
        }

        public AppSettings Settings
        {
            get { return _settingsStore.Settings; }
        }

        public void Notify(string message)
        {
            Notify(message, ToolTipIcon.Info);
        }

        public void Notify(string message, ToolTipIcon icon)
        {
            try
            {
                if (_tray != null)
                {
                    _tray.ShowBalloonTip(3000, "ShotPin", message, icon);
                }
            }
            catch (Exception ex)
            {
                Log.Error("notify failed", ex);
            }
        }

        private void BuildTray()
        {
            _menu = new ContextMenuStrip();
            _menu.Opening += OnMenuOpening;

            _regionItem = new ToolStripMenuItem("区域截图", null, delegate { BeginCaptureRegion(); });
            _fullscreenItem = new ToolStripMenuItem("当前屏幕截图", null, delegate { BeginCaptureMonitor(); });
            _virtualItem = new ToolStripMenuItem("全部屏幕截图", null, delegate { BeginCaptureVirtual(); });

            _toggleItem = new ToolStripMenuItem("隐藏所有图片", null, delegate { ToggleBoard(); });
            _closeAllItem = new ToolStripMenuItem("关闭所有图片", null, delegate { CloseAllPins(); });

            ToolStripMenuItem settingsItem = new ToolStripMenuItem("打开设置文件", null, delegate { OpenSettings(); });
            ToolStripMenuItem reloadItem = new ToolStripMenuItem("重新加载设置", null, delegate { ReloadSettings(); });
            _autostartItem = new ToolStripMenuItem("开机自启", null, delegate { ToggleAutoStart(); });

            _menu.Items.Add(_regionItem);
            _menu.Items.Add(_fullscreenItem);
            _menu.Items.Add(_virtualItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_toggleItem);
            _menu.Items.Add(_closeAllItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(settingsItem);
            _menu.Items.Add(reloadItem);
            _menu.Items.Add(_autostartItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("关于", null, delegate { ShowAbout(); }));
            _menu.Items.Add(new ToolStripMenuItem("退出", null, delegate { ExitThread(); }));

            _trayIcon = LoadTrayIcon();
            _tray = new NotifyIcon();
            _tray.Icon = _trayIcon;
            _tray.Text = "ShotPin 截图悬浮工具";
            _tray.ContextMenuStrip = _menu;
            _tray.MouseClick += OnTrayMouseClick;
            _tray.Visible = true;
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    return icon;
                }
            }
            catch
            {
            }
            return SystemIcons.Application;
        }

        private List<string> RegisterHotkeys()
        {
            List<string> failures = new List<string>();
            AppSettings settings = Settings;

            RegisterOne(failures, "区域截图", settings.RegionHotkey, BeginCaptureRegion);
            RegisterOne(failures, "当前屏幕截图", settings.FullscreenHotkey, BeginCaptureMonitor);
            RegisterOne(failures, "显示/隐藏所有图片", settings.ToggleBoardHotkey, ToggleBoard);
            RegisterOne(failures, "关闭所有图片", settings.CloseAllHotkey, CloseAllPins);
            return failures;
        }

        private void RegisterOne(List<string> failures, string name, HotkeySpec spec, Action action)
        {
            string error;
            if (!_hotkeys.TryRegister(name, spec, action, out error))
            {
                failures.Add(name + " 热键 " + (spec == null ? "" : spec.Text) + " 注册失败：" + error +
                             "，可能被其它程序占用。");
            }
        }

        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_regionItem != null)
            {
                _regionItem.ShortcutKeyDisplayString = Settings.RegionHotkey.Text;
            }
            if (_fullscreenItem != null)
            {
                _fullscreenItem.ShortcutKeyDisplayString = Settings.FullscreenHotkey.Text;
            }
            if (_toggleItem != null)
            {
                _toggleItem.Text = _pins.IsBoardVisible ? "隐藏所有图片" : "显示所有图片";
                _toggleItem.ShortcutKeyDisplayString = Settings.ToggleBoardHotkey.Text;
            }
            if (_closeAllItem != null)
            {
                _closeAllItem.Enabled = _pins.AllPins.Count > 0;
                _closeAllItem.ShortcutKeyDisplayString = Settings.CloseAllHotkey.Text;
            }
            if (_autostartItem != null)
            {
                _autostartItem.Checked = IsAutoStartEnabled();
            }
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                BeginCaptureRegion();
            }
        }

        private void OnStartupTimerTick(object sender, EventArgs e)
        {
            if (_startupTimer != null)
            {
                _startupTimer.Stop();
                _startupTimer.Dispose();
                _startupTimer = null;
            }
            if (_startupMessages.Count > 0)
            {
                string first = _startupMessages[0];
                string suffix = _startupMessages.Count > 1
                    ? "（另有 " + (_startupMessages.Count - 1) + " 条提示，详见日志）"
                    : string.Empty;
                Notify(first + suffix, ToolTipIcon.Warning);
            }
        }

        private void BeginCaptureRegion()
        {
            BeginCapture(false, false);
        }

        private void BeginCaptureMonitor()
        {
            BeginCapture(true, false);
        }

        private void BeginCaptureVirtual()
        {
            BeginCapture(true, true);
        }

        private void BeginCapture(bool fullscreen, bool allScreens)
        {
            if (_capturing || _disposed)
            {
                return;
            }
            _capturing = true;
            bool boardWasVisible = false;
            Bitmap result = null;
            Rectangle resultRect = Rectangle.Empty;

            try
            {
                boardWasVisible = _pins.HideForCapture();
                Native.DwmFlush();
                Thread.Sleep(80);

                Rectangle virtualScreen = CaptureService.VirtualScreen;
                Bitmap frozen = CaptureService.CaptureVirtualScreen();

                if (fullscreen)
                {
                    if (allScreens)
                    {
                        result = frozen;
                        resultRect = virtualScreen;
                        frozen = null;
                    }
                    else
                    {
                        Screen screen = Screen.FromPoint(Cursor.Position);
                        Rectangle bounds = screen.Bounds;
                        Rectangle local = new Rectangle(
                            bounds.Left - virtualScreen.Left,
                            bounds.Top - virtualScreen.Top,
                            bounds.Width,
                            bounds.Height);
                        result = CaptureService.Crop(frozen, local);
                        frozen.Dispose();
                        frozen = null;
                        resultRect = bounds;
                    }
                }
                else
                {
                    using (SelectionOverlayForm overlay = new SelectionOverlayForm(frozen, virtualScreen))
                    {
                        frozen = null; // overlay 接管并释放冻结画面
                        if (overlay.ShowDialog() == DialogResult.OK)
                        {
                            result = overlay.ResultBitmap;
                            resultRect = overlay.SelectedScreenRect;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("capture failed", ex);
                Notify("截图失败：" + ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                _pins.RestoreAfterCapture(boardWasVisible);
                _capturing = false;
            }

            if (result == null)
            {
                return;
            }

            try
            {
                _pins.CreatePin(result, resultRect);
                result = null; // 所有权转给 PinForm
            }
            catch (Exception ex)
            {
                Log.Error("create pin after capture failed", ex);
                Notify("创建悬浮图失败：" + ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                if (result != null)
                {
                    result.Dispose();
                }
            }
        }

        private void ToggleBoard()
        {
            _pins.ToggleBoard();
            Log.Info("board visible=" + _pins.IsBoardVisible);
        }

        private void CloseAllPins()
        {
            _pins.CloseAll();
            Log.Info("all pins closed");
        }

        private void OpenSettings()
        {
            try
            {
                if (!System.IO.File.Exists(_settingsStore.FilePath))
                {
                    _settingsStore.Load();
                }
                Process.Start("notepad.exe", "\"" + _settingsStore.FilePath + "\"");
            }
            catch (Exception ex)
            {
                Log.Error("open settings failed", ex);
                Notify("无法打开设置文件：" + ex.Message, ToolTipIcon.Error);
            }
        }

        private void ReloadSettings()
        {
            _settingsStore.Load();
            AppSettings settings = Settings;

            _hotkeys.UnregisterAll();
            List<string> failures = RegisterHotkeys();
            _connectors.UpdateStyle(settings.LinkColor, settings.LinkWidth);

            List<string> messages = new List<string>();
            for (int i = 0; i < settings.Warnings.Count; i++)
            {
                messages.Add(settings.Warnings[i]);
                Log.Warn(settings.Warnings[i]);
            }
            for (int i = 0; i < failures.Count; i++)
            {
                messages.Add(failures[i]);
                Log.Warn(failures[i]);
            }

            if (messages.Count == 0)
            {
                Notify("设置已重新加载");
            }
            else
            {
                Notify(messages[0] + (messages.Count > 1 ? "（另有 " + (messages.Count - 1) + " 条提示，详见日志）" : string.Empty),
                    ToolTipIcon.Warning);
            }
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    object value = key.GetValue(RunValueName);
                    return value != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void ToggleAutoStart()
        {
            try
            {
                bool enabled = !IsAutoStartEnabled();
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException("无法打开注册表启动项");
                    }
                    if (enabled)
                    {
                        key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(RunValueName, false);
                    }
                }
                if (_autostartItem != null)
                {
                    _autostartItem.Checked = enabled;
                }
                Notify(enabled ? "已开启开机自启" : "已关闭开机自启");
            }
            catch (Exception ex)
            {
                Log.Error("toggle autostart failed", ex);
                Notify("设置开机自启失败：" + ex.Message, ToolTipIcon.Error);
            }
        }

        private void ShowAbout()
        {
            string text = "ShotPin " + Application.ProductVersion + " — 截图悬浮工具\n\n" +
                          "区域截图：" + Settings.RegionHotkey.Text + "\n" +
                          "当前屏幕截图：" + Settings.FullscreenHotkey.Text + "\n" +
                          "显示/隐藏所有图片：" + Settings.ToggleBoardHotkey.Text + "\n" +
                          "关闭所有图片：" + Settings.CloseAllHotkey.Text + "\n\n" +
                          "配置文件：" + _settingsStore.FilePath + "\n" +
                          "日志目录：" + Log.LogDir;
            MessageBox.Show(text, "ShotPin", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            try
            {
                Rectangle screen = SystemInformation.VirtualScreen;
                IList<PinForm> pins = _pins.AllPins;
                for (int i = 0; i < pins.Count; i++)
                {
                    pins[i].Bounds = GeometryUtil.KeepReachable(pins[i].Bounds, screen, 40);
                }
                _connectors.Relayout();
            }
            catch (Exception ex)
            {
                Log.Error("display settings change failed", ex);
            }
        }

        protected override void ExitThreadCore()
        {
            Cleanup();
            base.ExitThreadCore();
        }

        private void Cleanup()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Log.Info("cleaning up");

            try
            {
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            }
            catch
            {
            }

            if (_startupTimer != null)
            {
                _startupTimer.Stop();
                _startupTimer.Dispose();
                _startupTimer = null;
            }

            try
            {
                _connectors.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("dispose connectors failed", ex);
            }
            try
            {
                _pins.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("dispose pins failed", ex);
            }
            try
            {
                _hotkeys.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("dispose hotkeys failed", ex);
            }

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            if (_trayIcon != null)
            {
                try
                {
                    _trayIcon.Dispose();
                }
                catch
                {
                }
                _trayIcon = null;
            }
            if (_menu != null)
            {
                _menu.Dispose();
                _menu = null;
            }
            Current = null;
        }
    }
}
