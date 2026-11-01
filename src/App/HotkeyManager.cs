using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ShotPin.Core;

namespace ShotPin.App
{
    internal sealed class HotkeyManager : IDisposable
    {
        private readonly HotkeyWindow _window;
        private readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        private int _nextId = 1;
        private bool _disposed;

        public HotkeyManager()
        {
            _window = new HotkeyWindow();
            _window.HotkeyPressed += OnHotkeyPressed;
            _window.EnsureHandle();
        }

        public bool TryRegister(string name, HotkeySpec spec, Action action, out string error)
        {
            error = string.Empty;
            if (_disposed || spec == null)
            {
                error = "热键为空";
                return false;
            }

            int id = _nextId++;
            if (!Native.RegisterHotKey(_window.Handle, id, spec.Modifiers | Native.MOD_NOREPEAT, spec.VirtualKey))
            {
                int code = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                error = new Win32Exception(code).Message + " (code " + code + ")";
                Log.Warn("hotkey register failed: " + name + " " + spec.Text + " -> " + error);
                return false;
            }

            _actions[id] = action;
            _names[id] = name;
            Log.Info("hotkey registered: " + name + " " + spec.Text);
            return true;
        }

        public int RegisterTransient(string name, uint modifiers, uint virtualKey, Action action)
        {
            if (_disposed)
            {
                return -1;
            }
            int id = _nextId++;
            if (!Native.RegisterHotKey(_window.Handle, id, modifiers | Native.MOD_NOREPEAT, virtualKey))
            {
                Log.Warn("transient hotkey failed: " + name);
                return -1;
            }
            _actions[id] = action;
            _names[id] = name;
            return id;
        }

        public void Unregister(int id)
        {
            if (id < 0 || _disposed)
            {
                return;
            }
            if (_actions.ContainsKey(id))
            {
                Native.UnregisterHotKey(_window.Handle, id);
                _actions.Remove(id);
                _names.Remove(id);
            }
        }

        public void UnregisterAll()
        {
            List<int> ids = new List<int>(_actions.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                Native.UnregisterHotKey(_window.Handle, ids[i]);
            }
            _actions.Clear();
            _names.Clear();
        }

        private void OnHotkeyPressed(int id)
        {
            Action action;
            if (!_actions.TryGetValue(id, out action) || action == null)
            {
                return;
            }
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error("hotkey action failed: " + nameofKey(id), ex);
            }
        }

        private string nameofKey(int id)
        {
            string name;
            return _names.TryGetValue(id, out name) ? name : ("id=" + id);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            {
                UnregisterAll();
            }
            catch
            {
            }
            _window.HotkeyPressed -= OnHotkeyPressed;
            try
            {
                _window.Dispose();
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// 只用于接收 WM_HOTKEY 的隐藏窗口。
    /// </summary>
    internal sealed class HotkeyWindow : Form
    {
        public event Action<int> HotkeyPressed;

        public HotkeyWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(-32000, -32000, 1, 1);
            Text = "ShotPin.HotkeyWindow";
        }

        public void EnsureHandle()
        {
            IntPtr handle = Handle;
            GC.KeepAlive(handle);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                Action<int> handler = HotkeyPressed;
                if (handler != null)
                {
                    handler(m.WParam.ToInt32());
                }
            }
            base.WndProc(ref m);
        }
    }
}
