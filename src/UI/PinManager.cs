using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ShotPin.App;
using ShotPin.Core;

namespace ShotPin.UI
{
    /// <summary>
    /// 管理所有悬浮图：创建、挂到装饰线上、显隐、关闭，以及右键菜单动作。
    /// </summary>
    internal sealed class PinManager : IDisposable
    {
        private readonly TrayAppContext _app;
        private readonly List<PinForm> _pins = new List<PinForm>();
        private int _counter;
        private bool _boardVisible = true;
        private bool _disposed;

        public ClotheslineManager Clothesline { get; set; }

        public PinManager(TrayAppContext app)
        {
            _app = app;
        }

        public IList<PinForm> AllPins
        {
            get { return _pins.AsReadOnly(); }
        }

        public bool IsBoardVisible
        {
            get { return _boardVisible; }
        }

        public PinForm Find(Guid id)
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                if (_pins[i].Id == id)
                {
                    return _pins[i];
                }
            }
            return null;
        }

        public int LineYAtScreen(int screenX)
        {
            return Clothesline == null ? 0 : Clothesline.LineYAtScreen(screenX);
        }

        public int HangGap
        {
            get { return Clothesline == null ? 0 : Clothesline.HangGap; }
        }

        public bool IsNearLine(PinForm pin)
        {
            return Clothesline != null && Clothesline.IsNearLine(pin);
        }

        public PinForm CreatePin(Bitmap image, Rectangle screenRect)
        {
            if (image == null)
            {
                return null;
            }

            PinForm pin = null;
            try
            {
                Point center = new Point(
                    screenRect.Left + screenRect.Width / 2,
                    screenRect.Top + screenRect.Height / 2);
                Screen screen = Screen.FromPoint(center);
                Rectangle work = screen.WorkingArea;

                float zoom = GeometryUtil.FitZoom(image.Size, work);
                int gap = Clothesline == null ? 0 : Clothesline.HangGap;
                int lineY = Clothesline == null
                    ? work.Top
                    : Clothesline.LineYAtScreen(center.X);
                int availableBelowLine = work.Bottom - (lineY + gap) - 8;
                if (availableBelowLine > 40)
                {
                    zoom = Math.Min(zoom, availableBelowLine / (float)image.Height);
                }
                zoom = GeometryUtil.ClampZoom(zoom);

                Size windowSize = GeometryUtil.WindowSizeForZoom(image.Size, zoom, 2);
                int desiredLeft = screenRect.Left;
                int left;
                int top;
                if (Clothesline != null)
                {
                    left = ClotheslineLayout.PlaceOnLine(
                        desiredLeft,
                        windowSize.Width,
                        work.Left,
                        work.Right,
                        GetHungBounds(),
                        8);
                    top = Clothesline.LineYAtScreen(left + windowSize.Width / 2) + gap;

                    int room = work.Bottom - top - 8;
                    if (room > 40 && windowSize.Height > room)
                    {
                        zoom = GeometryUtil.ClampZoom(room / (float)image.Height);
                        windowSize = GeometryUtil.WindowSizeForZoom(image.Size, zoom, 2);
                        left = ClotheslineLayout.PlaceOnLine(
                            desiredLeft,
                            windowSize.Width,
                            work.Left,
                            work.Right,
                            GetHungBounds(),
                            8);
                        top = Clothesline.LineYAtScreen(left + windowSize.Width / 2) + gap;
                    }
                }
                else
                {
                    Rectangle desired = new Rectangle(desiredLeft, work.Top + 20, windowSize.Width, windowSize.Height);
                    List<Rectangle> existing = new List<Rectangle>();
                    for (int i = 0; i < _pins.Count; i++)
                    {
                        existing.Add(_pins[i].ScreenBounds);
                    }
                    Rectangle placed = GeometryUtil.CascadePlacement(desired, work, existing);
                    left = placed.Left;
                    top = placed.Top;
                }

                _counter++;
                string name = "截图 " + _counter;
                Rectangle bounds = new Rectangle(left, top, windowSize.Width, windowSize.Height);
                pin = new PinForm(this, Guid.NewGuid(), name, image, bounds, zoom, Clothesline != null);
                pin.FormClosed += OnPinClosed;
                _pins.Add(pin);
                image = null; // 所有权已转移给 PinForm
                Log.Info("pin created: " + name + " bounds=" + pin.Bounds + " zoom=" + zoom.ToString("0.000") + " hung=" + pin.IsHung);

                if (!_boardVisible)
                {
                    ShowAll();
                }
                else
                {
                    pin.Show();
                }
                pin.Activate();
                pin.Refresh();
                if (Clothesline != null)
                {
                    Clothesline.NotifyHungChanged(pin);
                }

                if (_app.Settings.AutoCopy)
                {
                    ClipboardUtil.TryCopyImage(pin.Image);
                }
                if (_app.Settings.AutoSave)
                {
                    SavePin(pin, false);
                }
                return pin;
            }
            catch (Exception ex)
            {
                Log.Error("create pin failed", ex);
                _app.Notify("创建悬浮图失败，详情见日志", ToolTipIcon.Error);
                if (pin != null)
                {
                    try
                    {
                        pin.Close();
                    }
                    catch
                    {
                    }
                }
                if (image != null)
                {
                    image.Dispose();
                }
                return null;
            }
        }

        private List<Rectangle> GetHungBounds()
        {
            List<Rectangle> result = new List<Rectangle>();
            for (int i = 0; i < _pins.Count; i++)
            {
                if (_pins[i].IsHung)
                {
                    result.Add(_pins[i].ScreenBounds);
                }
            }
            return result;
        }

        private void OnPinClosed(object sender, FormClosedEventArgs e)
        {
            PinForm pin = sender as PinForm;
            if (pin == null)
            {
                return;
            }
            _pins.Remove(pin);
            pin.FormClosed -= OnPinClosed;
            if (Clothesline != null)
            {
                Clothesline.OnPinClosed(pin.Id);
            }
            Log.Info("pin closed: " + pin.PinName);
        }

        public void NotifyGeometryChanged(PinForm pin)
        {
            if (Clothesline != null)
            {
                Clothesline.RequestRepaint();
            }
        }

        public void NotifyHungChanged(PinForm pin)
        {
            if (Clothesline != null)
            {
                Clothesline.NotifyHungChanged(pin);
            }
        }

        public bool HideForCapture()
        {
            if (!_boardVisible)
            {
                return false;
            }
            for (int i = 0; i < _pins.Count; i++)
            {
                _pins[i].Hide();
            }
            if (Clothesline != null)
            {
                Clothesline.Hide();
            }
            return true;
        }

        public void RestoreAfterCapture(bool wasVisible)
        {
            if (!wasVisible)
            {
                return;
            }
            ShowAll();
        }

        public void ShowAll()
        {
            _boardVisible = true;
            for (int i = 0; i < _pins.Count; i++)
            {
                if (!_pins[i].Visible)
                {
                    _pins[i].Show();
                }
            }
            if (Clothesline != null)
            {
                Clothesline.Show();
            }
        }

        public void HideAll()
        {
            _boardVisible = false;
            for (int i = 0; i < _pins.Count; i++)
            {
                _pins[i].Hide();
            }
            if (Clothesline != null)
            {
                Clothesline.Hide();
            }
        }

        public void ToggleBoard()
        {
            if (_boardVisible)
            {
                HideAll();
            }
            else
            {
                ShowAll();
            }
        }

        public void CloseAll()
        {
            PinForm[] copy = _pins.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                try
                {
                    copy[i].Close();
                }
                catch (Exception ex)
                {
                    Log.Error("close pin failed", ex);
                }
            }
            _pins.Clear();
            if (Clothesline != null)
            {
                Clothesline.RequestRepaint();
            }
        }

        public void HangPin(PinForm pin)
        {
            if (pin == null)
            {
                return;
            }
            if (Clothesline != null)
            {
                Clothesline.HangPin(pin);
            }
            else
            {
                pin.SetHung(true);
            }
        }

        public void UnhangPin(PinForm pin)
        {
            if (pin == null)
            {
                return;
            }
            if (Clothesline != null)
            {
                Clothesline.UnhangPin(pin);
            }
            else
            {
                pin.SetHung(false);
            }
        }

        public void CopyPin(PinForm pin)
        {
            if (pin == null)
            {
                return;
            }
            if (ClipboardUtil.TryCopyImage(pin.Image))
            {
                _app.Notify("已复制到剪贴板");
            }
            else
            {
                _app.Notify("复制到剪贴板失败（剪贴板可能被占用）", ToolTipIcon.Warning);
            }
        }

        public void SavePin(PinForm pin, bool saveAs)
        {
            if (pin == null)
            {
                return;
            }
            try
            {
                string path;
                if (saveAs)
                {
                    using (SaveFileDialog dialog = new SaveFileDialog())
                    {
                        dialog.Title = "保存截图";
                        dialog.Filter = "PNG 图片|*.png";
                        dialog.DefaultExt = "png";
                        dialog.AddExtension = true;
                        dialog.FileName = "ShotPin_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                        dialog.RestoreDirectory = true;
                        if (dialog.ShowDialog(pin) != DialogResult.OK)
                        {
                            return;
                        }
                        path = dialog.FileName;
                    }
                }
                else
                {
                    path = CaptureService.UniquePngPath(_app.Settings.SaveDir);
                }

                CaptureService.SavePng(pin.Image, path);
                Log.Info("image saved: " + path);
                _app.Notify("已保存：" + path);
            }
            catch (Exception ex)
            {
                Log.Error("save image failed", ex);
                _app.Notify("保存失败：" + ex.Message, ToolTipIcon.Error);
            }
        }

        public void PopulateMenu(ContextMenuStrip menu, PinForm pin)
        {
            menu.Items.Clear();

            ToolStripMenuItem copy = (ToolStripMenuItem)menu.Items.Add("复制图片", null, delegate { CopyPin(pin); });
            copy.ShortcutKeyDisplayString = "Ctrl+C";

            ToolStripMenuItem saveAs = (ToolStripMenuItem)menu.Items.Add("另存为 PNG...", null, delegate { SavePin(pin, true); });
            saveAs.ShortcutKeyDisplayString = "Ctrl+S";

            menu.Items.Add(new ToolStripSeparator());

            string hangText = pin.IsHung ? "从挂线取下" : "挂到挂线";
            menu.Items.Add(new ToolStripMenuItem(hangText, null, delegate
            {
                if (pin.IsHung)
                {
                    UnhangPin(pin);
                }
                else
                {
                    HangPin(pin);
                }
            }));

            menu.Items.Add(new ToolStripMenuItem("适应屏幕", null, delegate { pin.FitToScreen(); }));
            menu.Items.Add(new ToolStripMenuItem("100%", null, delegate { pin.ResetZoom(); }));

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem close = (ToolStripMenuItem)menu.Items.Add("关闭", null, delegate { pin.Close(); });
            close.ShortcutKeyDisplayString = "Esc";
            menu.Items.Add(new ToolStripMenuItem("关闭全部", null, delegate { CloseAll(); }));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            CloseAll();
        }
    }
}
