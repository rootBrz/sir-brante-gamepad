using UnityEngine;

namespace Brante.Gamepad
{
    // Hint bar, first-run toast and the focus ring, drawn with IMGUI over the game.
    internal sealed class Overlay
    {
        private GUIStyle hintStyle, toastStyle;
        private int hintSize, toastSize;

        internal void DrawHints(string text, float scale)
        {
            int size = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 55f * scale));
            if (hintStyle == null || hintSize != size)
            {
                hintStyle = new GUIStyle(GUI.skin.box) { fontSize = size, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                hintSize = size;
            }
            float height = size * 2.4f;
            GUI.Box(new Rect(0, Screen.height - height, Screen.width, height), text, hintStyle);
        }

        internal void DrawToast(string text, float scale)
        {
            int size = Mathf.Max(16, Mathf.RoundToInt(Screen.height / 36f * scale));
            if (toastStyle == null || toastSize != size)
            {
                toastStyle = new GUIStyle(GUI.skin.box) { fontSize = size, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                toastSize = size;
            }
            float width = Mathf.Min(Screen.width - 40, size * 30f), height = size * 3f;
            GUI.Box(new Rect((Screen.width - width) / 2, Screen.height * 0.12f, width, height), text, toastStyle);
        }

        // Soft rounded highlight with a faint glow and a slow pulse; box is in Unity screen space (y up).
        internal void DrawFocusRing(Box box)
        {
            float unit = Screen.height / 1080f;
            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 3f);
            float pad = 5f * unit;
            Rect rect = new Rect(box.XMin - pad, Screen.height - box.YMax - pad, box.Width + 2 * pad, box.Height + 2 * pad);
            float radius = Mathf.Min(10f * unit, Mathf.Min(rect.width, rect.height) * 0.5f);
            Color color = GUI.color;
            Texture2D white = Texture2D.whiteTexture;
            Color gold = new Color(0.96f, 0.83f, 0.52f);
            // Wash inside, glow outside, crisp line between.
            Fill(rect, white, new Color(gold.r, gold.g, gold.b, 0.07f * pulse), 0f, radius);
            for (int layer = 3; layer >= 1; layer--)
            {
                float grow = layer * 3f * unit;
                Rect glow = new Rect(rect.x - grow, rect.y - grow, rect.width + 2 * grow, rect.height + 2 * grow);
                Fill(glow, white, new Color(gold.r, gold.g, gold.b, 0.10f * pulse / layer), 3f * unit, radius + grow);
            }
            Fill(rect, white, new Color(gold.r, gold.g, gold.b, 0.85f * pulse), Mathf.Max(1.5f, 2f * unit), radius);
            GUI.color = color;
        }

        // border 0 fills the rounded rectangle; otherwise only an outline of that width is drawn.
        private static void Fill(Rect rect, Texture2D texture, Color color, float border, float radius)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true, 0f, color,
                border > 0f ? new Vector4(border, border, border, border) : Vector4.zero,
                new Vector4(radius, radius, radius, radius));
        }
    }
}
