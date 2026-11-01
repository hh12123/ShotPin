using System;
using System.Collections.Generic;
using System.Drawing;

namespace ShotPin.Core
{
    /// <summary>
    /// 桌面装饰挂线的纯几何逻辑：轻微下垂的绳线、沿线摆放、吸附判断。
    /// </summary>
    public static class ClotheslineLayout
    {
        /// <summary>
        /// 挂线在 x 处的屏幕 Y。线两端为 baseY，中间按二次曲线下垂 sag 像素。
        /// </summary>
        public static int LineYAt(int x, int left, int right, int baseY, int sag)
        {
            if (right <= left || sag == 0)
            {
                return baseY;
            }
            double t = (x - left) / (double)(right - left);
            if (t < 0.0)
            {
                t = 0.0;
            }
            else if (t > 1.0)
            {
                t = 1.0;
            }
            return baseY + (int)Math.Round(sag * 4.0 * t * (1.0 - t));
        }

        public static int ClampLineY(int desiredY, int topLimit, int bottomLimit)
        {
            if (bottomLimit < topLimit)
            {
                return topLimit;
            }
            if (desiredY < topLimit)
            {
                return topLimit;
            }
            if (desiredY > bottomLimit)
            {
                return bottomLimit;
            }
            return desiredY;
        }

        /// <summary>
        /// 在挂线上给新图找一个尽量靠近 desiredLeft、且不与已挂图片重叠的位置。
        /// 找不到空位时返回夹紧后的期望位置（允许重叠）。
        /// </summary>
        public static int PlaceOnLine(
            int desiredLeft,
            int width,
            int leftBound,
            int rightBound,
            IList<Rectangle> existing,
            int gap)
        {
            if (width < 1)
            {
                width = 1;
            }
            int maxLeft = rightBound - width;
            if (maxLeft < leftBound)
            {
                maxLeft = leftBound;
            }

            int start = desiredLeft;
            if (start < leftBound)
            {
                start = leftBound;
            }
            else if (start > maxLeft)
            {
                start = maxLeft;
            }

            if (existing == null || existing.Count == 0)
            {
                return start;
            }

            List<int> candidates = new List<int>();
            candidates.Add(start);
            candidates.Add(leftBound);
            candidates.Add(maxLeft);
            for (int i = 0; i < existing.Count; i++)
            {
                candidates.Add(existing[i].Right + gap);
                candidates.Add(existing[i].Left - gap - width);
            }

            int best = start;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                int candidate = candidates[i];
                if (candidate < leftBound)
                {
                    candidate = leftBound;
                }
                if (candidate > maxLeft)
                {
                    candidate = maxLeft;
                }
                if (Overlaps(candidate, width, existing, gap))
                {
                    continue;
                }
                int distance = Math.Abs(candidate - start);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        private static bool Overlaps(int left, int width, IList<Rectangle> existing, int gap)
        {
            int right = left + width;
            for (int i = 0; i < existing.Count; i++)
            {
                Rectangle item = existing[i];
                if (right + gap > item.Left && left < item.Right + gap)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool NearLine(int pinTop, int lineY, int threshold)
        {
            return Math.Abs(pinTop - lineY) <= threshold;
        }
    }
}
