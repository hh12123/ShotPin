using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ShotPin.App
{
    internal static class CaptureService
    {
        public static Rectangle VirtualScreen
        {
            get { return SystemInformation.VirtualScreen; }
        }

        public static Bitmap CaptureVirtualScreen()
        {
            return CaptureBounds(SystemInformation.VirtualScreen);
        }

        public static Bitmap CaptureBounds(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                throw new ArgumentException("截图区域为空");
            }
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        public static Bitmap Crop(Bitmap source, Rectangle localRect)
        {
            if (source == null)
            {
                return null;
            }
            Rectangle bounds = new Rectangle(0, 0, source.Width, source.Height);
            Rectangle clipped = Rectangle.Intersect(bounds, localRect);
            if (clipped.Width <= 0 || clipped.Height <= 0)
            {
                return null;
            }
            return source.Clone(clipped, PixelFormat.Format32bppArgb);
        }

        public static void SavePng(Bitmap bitmap, string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            bitmap.Save(path, ImageFormat.Png);
        }

        public static string UniquePngPath(string folder)
        {
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(folder, "ShotPin_" + stamp + ".png");
            int counter = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(folder, "ShotPin_" + stamp + "_" + counter + ".png");
                counter++;
            }
            return path;
        }
    }

    internal static class ClipboardUtil
    {
        public static bool TryCopyImage(Bitmap source)
        {
            if (source == null)
            {
                return false;
            }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    // 交给剪贴板一份独立副本，悬浮图关闭时不影响剪贴板。
                    Bitmap copy = new Bitmap(source);
                    Clipboard.SetImage(copy);
                    return true;
                }
                catch (Exception ex)
                {
                    if (attempt == 2)
                    {
                        Log.Error("clipboard copy failed", ex);
                    }
                    Thread.Sleep(60);
                }
            }
            return false;
        }
    }
}
