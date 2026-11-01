using System;
using System.Collections.Generic;
using System.Drawing;

namespace ShotPin.Core
{
    /// <summary>
    /// 悬浮图缩放、摆放和屏幕可见性计算的纯几何逻辑。
    /// </summary>
    public static class GeometryUtil
    {
        public const float MinZoom = 0.1f;
        public const float MaxZoom = 8.0f;

        public static float ClampZoom(float zoom)
        {
            if (float.IsNaN(zoom) || zoom <= 0f)
            {
                return 1.0f;
            }
            if (zoom < MinZoom)
            {
                return MinZoom;
            }
            if (zoom > MaxZoom)
            {
                return MaxZoom;
            }
            return zoom;
        }

        /// <summary>
        /// 让图片初始缩放不超过工作区的 85%。
        /// </summary>
        public static float FitZoom(Size imageSize, Rectangle workArea)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return 1.0f;
            }
            float zoom = 1.0f;
            float maxWidth = workArea.Width * 0.85f;
            float maxHeight = workArea.Height * 0.85f;
            if (imageSize.Width > maxWidth)
            {
                zoom = Math.Min(zoom, maxWidth / imageSize.Width);
            }
            if (imageSize.Height > maxHeight)
            {
                zoom = Math.Min(zoom, maxHeight / imageSize.Height);
            }
            return ClampZoom(zoom);
        }

        public static Size WindowSizeForZoom(Size imageSize, float zoom, int padding)
        {
            if (zoom < MinZoom)
            {
                zoom = MinZoom;
            }
            int width = (int)Math.Round(imageSize.Width * zoom) + padding * 2;
            int height = (int)Math.Round(imageSize.Height * zoom) + padding * 2;
            if (width < 1)
            {
                width = 1;
            }
            if (height < 1)
            {
                height = 1;
            }
            return new Size(width, height);
        }

        /// <summary>
        /// 以窗口中心为锚点计算缩放后的矩形。
        /// </summary>
        public static Rectangle ZoomedBounds(Rectangle current, Size imageSize, float newZoom, int padding)
        {
            Size size = WindowSizeForZoom(imageSize, newZoom, padding);
            int centerX = current.Left + current.Width / 2;
            int centerY = current.Top + current.Height / 2;
            return new Rectangle(centerX - size.Width / 2, centerY - size.Height / 2, size.Width, size.Height);
        }

        /// <summary>
        /// 把窗口夹到工作区内；窗口比工作区大时保留左上角。
        /// </summary>
        public static Rectangle ClampToWorkArea(Rectangle bounds, Rectangle workArea)
        {
            int x = bounds.X;
            int y = bounds.Y;
            int maxX = workArea.Right - bounds.Width;
            int maxY = workArea.Bottom - bounds.Height;
            if (maxX < workArea.Left)
            {
                maxX = workArea.Left;
            }
            if (maxY < workArea.Top)
            {
                maxY = workArea.Top;
            }
            if (x < workArea.Left)
            {
                x = workArea.Left;
            }
            if (y < workArea.Top)
            {
                y = workArea.Top;
            }
            if (x > maxX)
            {
                x = maxX;
            }
            if (y > maxY)
            {
                y = maxY;
            }
            return new Rectangle(x, y, bounds.Width, bounds.Height);
        }

        /// <summary>
        /// 新图尽量放在期望位置；与已有图重叠时按 24px 级联，最多尝试 64 次。
        /// </summary>
        public static Rectangle CascadePlacement(Rectangle desired, Rectangle workArea, IList<Rectangle> existing)
        {
            Rectangle fallback = ClampToWorkArea(desired, workArea);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                Rectangle candidate = desired;
                int offset = attempt * 24;
                candidate.Offset(offset, offset);
                candidate = ClampToWorkArea(candidate, workArea);

                bool hidden = false;
                if (existing != null)
                {
                    for (int i = 0; i < existing.Count; i++)
                    {
                        // 允许 24px 级联带来的部分重叠，只避免新窗口被完全盖住。
                        Rectangle hit = Rectangle.Intersect(existing[i], candidate);
                        long hitArea = (long)hit.Width * (long)hit.Height;
                        long candidateArea = (long)candidate.Width * (long)candidate.Height;
                        if (candidateArea > 0 && hitArea >= candidateArea * 9 / 10)
                        {
                            hidden = true;
                            break;
                        }
                    }
                }
                if (!hidden)
                {
                    return candidate;
                }
            }
            return fallback;
        }

        /// <summary>
        /// 保证窗口至少有一部分留在可见区域内，避免被拖到屏幕外找不回来。
        /// </summary>
        public static Rectangle KeepReachable(Rectangle bounds, Rectangle screen, int minVisible)
        {
            int minX = screen.Left - bounds.Width + minVisible;
            int maxX = screen.Right - minVisible;
            int minY = screen.Top;
            int maxY = screen.Bottom - minVisible;
            int x = bounds.X;
            int y = bounds.Y;
            if (x < minX)
            {
                x = minX;
            }
            if (x > maxX)
            {
                x = maxX;
            }
            if (y < minY)
            {
                y = minY;
            }
            if (y > maxY)
            {
                y = maxY;
            }
            return new Rectangle(x, y, bounds.Width, bounds.Height);
        }
    }
}
