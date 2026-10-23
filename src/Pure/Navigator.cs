using System;
using System.Collections.Generic;

namespace Brante.Gamepad
{
    // Axis-aligned rectangle in Unity screen space (y grows upward). A point is a zero-size box.
    internal struct Box
    {
        internal float XMin, YMin, XMax, YMax;
        internal Box(float xMin, float yMin, float xMax, float yMax) { XMin = xMin; YMin = yMin; XMax = xMax; YMax = yMax; }
        internal float CenterX { get { return (XMin + XMax) * 0.5f; } }
        internal float CenterY { get { return (YMin + YMax) * 0.5f; } }
        internal float Width { get { return XMax - XMin; } }
        internal float Height { get { return YMax - YMin; } }
        internal float Area { get { return Math.Max(0f, Width) * Math.Max(0f, Height); } }
        internal static Box Point(float x, float y) { return new Box(x, y, x, y); }
        internal bool Contains(float x, float y) { return x >= XMin && x <= XMax && y >= YMin && y <= YMax; }
        internal Box Union(Box other)
        {
            return new Box(Math.Min(XMin, other.XMin), Math.Min(YMin, other.YMin), Math.Max(XMax, other.XMax), Math.Max(YMax, other.YMax));
        }
        internal Box Intersect(Box other)
        {
            return new Box(Math.Max(XMin, other.XMin), Math.Max(YMin, other.YMin), Math.Min(XMax, other.XMax), Math.Min(YMax, other.YMax));
        }
    }

    internal static class Navigator
    {
        private const float MinimumTravel = 8f;
        private const float LateralWeight = 3f;
        private const float CenterOffsetWeight = 0.25f;

        // dx/dy select the direction: (1,0) right, (-1,0) left, (0,1) up, (0,-1) down.
        // Lower is better; infinity rejects targets that are not ahead of the origin.
        internal static float Score(Box from, Box to, int dx, int dy)
        {
            float forward = (to.CenterX - from.CenterX) * dx + (to.CenterY - from.CenterY) * dy;
            if (forward <= MinimumTravel) return float.PositiveInfinity;
            float lateralGap, lateralOffset;
            if (dx != 0)
            {
                lateralGap = Gap(from.YMin, from.YMax, to.YMin, to.YMax);
                lateralOffset = Math.Abs(to.CenterY - from.CenterY);
            }
            else
            {
                lateralGap = Gap(from.XMin, from.XMax, to.XMin, to.XMax);
                lateralOffset = Math.Abs(to.CenterX - from.CenterX);
            }
            return forward + lateralGap * LateralWeight + lateralOffset * CenterOffsetWeight;
        }

        private static float Gap(float aMin, float aMax, float bMin, float bMax)
        {
            return Math.Max(0f, Math.Max(aMin - bMax, bMin - aMax));
        }

        // Fills order with candidate indices, best first. With wrap, an empty result falls back to
        // the candidates on the far side of the screen, nearest to the origin's row or column first.
        internal static void Rank(Box from, IList<Box> candidates, int dx, int dy, bool wrap, List<int> order)
        {
            order.Clear();
            List<KeyValuePair<float, int>> scored = new List<KeyValuePair<float, int>>();
            for (int i = 0; i < candidates.Count; i++)
            {
                float score = Score(from, candidates[i], dx, dy);
                if (!float.IsPositiveInfinity(score)) scored.Add(new KeyValuePair<float, int>(score, i));
            }
            if (scored.Count == 0 && wrap)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    float behind = (candidates[i].CenterX - from.CenterX) * -dx + (candidates[i].CenterY - from.CenterY) * -dy;
                    if (behind <= MinimumTravel) continue;
                    float lateral = dx != 0 ? Math.Abs(candidates[i].CenterY - from.CenterY) : Math.Abs(candidates[i].CenterX - from.CenterX);
                    // Farthest along the reverse direction first (it is the opposite edge), then closest to the same row/column.
                    scored.Add(new KeyValuePair<float, int>(lateral * LateralWeight - behind, i));
                }
            }
            scored.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
            foreach (KeyValuePair<float, int> entry in scored) order.Add(entry.Value);
        }
    }
}
