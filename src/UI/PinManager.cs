using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ShotPin.App;
using ShotPin.Core;

namespace ShotPin.UI
{
    /// <summary>
    /// 管理所有悬浮图：创建、级联摆放、显隐、关闭，以及右键菜单动作。
    /// </summary>
    internal sealed class PinManager : IDisposable
    {
        private readonly TrayAppContext _app;
        private readonly List<PinForm> _pins = new List<PinForm>();
        private int _counter;
        private bool _boardVisible = true;
        private bool _disposed;

        public ConnectorManager Connectors { get; set; }

        public bool IsLinkMode
        {
            get { return Connectors != null && Connectors.IsLinkMode; }
        }

        public Guid LinkSourceId
        {
            get { return Connectors == null ? Guid.Empty : Connectors.LinkSourceId; }
        }

        public void BeginLink(Guid sourceId)
        {
            if (Connectors != null)
            {
                Connectors.BeginLink(sourceId);
            }
        }

        public void CompleteLink(Guid targetId)
        {
            if (Connectors != null)
            {
                Connectors.CompleteLink(targetId);
            }
        }

        public void CancelLinkMode()
        {
            if (Connectors != null)
            {
                Connectors.CancelLinkMode();
            }
        }

        public bool CancelLinkModeIfActive()
        {
            return Connectors != null && Connectors.CancelLinkModeIfActive();
        }

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

        public PinForm CreatePin(Bitmap image, Rectangle screenRect)
        {
            if (image == null)
            {
                return null;
            }

            PinForm pin = null;
            try
            {
                Point center = new Point(screenRect.Left + screenRect.Width / 2, screenRect.Top + screenRect.Height / 2);
                Screen screen = Screen.FromPoint(center);
                Rectangle work = screen.WorkingArea;
                float zoom = GeometryUtil.FitZoom(image.Size, work);
                Size windowSize = GeometryUtil.WindowSizeForZoom(image.Size, zoom, 2);

                Rectangle desired = new Rectangle(screenRect.Left, screenRect.Top, windowSize.Width, windowSize.Height);
                List<Rectangle> existing = new List<Rectangle>();
                for (int i = 0; i < _pins.Count; i++)
                {
                    existing.Add(_pins[i].ScreenBounds);
                }
                Rectangle placed = GeometryUtil.CascadePlacement(desired, work, existing);

                _counter++;
                string name = "截图 " + _counter;
                pin = new PinForm(this, Guid.NewGuid(), name, image, placed, zoom);
                pin.FormClosed += OnPinClosed;
                _pins.Add(pin);
                image = null; // 所有权已转移给 PinForm
                Log.Info("pin created: " + name + " bounds=" + pin.Bounds + " zoom=" + zoom.ToString("0.000"));

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
                if (Connectors != null)
                {
                    Connectors.RequestRepaint();
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

        private void OnPinClosed(object sender, FormClosedEventArgs e)
        {
            PinForm pin = sender as PinForm;
            if (pin == null)
            {
                return;
            }
            _pins.Remove(pin);
            pin.FormClosed -= OnPinClosed;
            if (Connectors != null)
            {
                Connectors.OnImageClosed(pin.Id);
            }
            Log.Info("pin closed: " + pin.PinName);
        }

        public void NotifyGeometryChanged(PinForm pin)
        {
            if (Connectors != null)
            {
                Connectors.RequestRepaint();
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
            if (Connectors != null)
            {
                Connectors.Hide();
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
            if (Connectors != null)
            {
                Connectors.Show();
            }
        }

        public void HideAll()
        {
            _boardVisible = false;
            for (int i = 0; i < _pins.Count; i++)
            {
                _pins[i].Hide();
            }
            if (Connectors != null)
            {
                Connectors.Hide();
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
            if (Connectors != null)
            {
                Connectors.ClearAll();
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
            menu.Items.Add(new ToolStripMenuItem("适应屏幕", null, delegate { pin.FitToScreen(); }));
            menu.Items.Add(new ToolStripMenuItem("100%", null, delegate { pin.ResetZoom(); }));

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem connect = new ToolStripMenuItem("连线到");
            int others = 0;
            for (int i = 0; i < _pins.Count; i++)
            {
                PinForm other = _pins[i];
                if (other.Id == pin.Id)
                {
                    continue;
                }
                others++;
                Guid targetId = other.Id;
                ToolStripMenuItem targetItem = new ToolStripMenuItem(other.PinName);
                targetItem.Click += delegate
                {
                    if (Connectors != null)
                    {
                        Connectors.Connect(pin.Id, targetId);
                    }
                };
                connect.DropDownItems.Add(targetItem);
            }
            connect.Enabled = others > 0;
            menu.Items.Add(connect);

            bool hasLinks = Connectors != null && Connectors.HasLinks(pin.Id);
            ToolStripItem unlink = menu.Items.Add("断开所有连线", null, delegate
            {
                if (Connectors != null)
                {
                    Connectors.RemoveAllFor(pin.Id);
                }
            });
            unlink.Enabled = hasLinks;

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
