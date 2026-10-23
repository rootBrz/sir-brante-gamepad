using UnityEngine;
using UnityEngine.EventSystems;

namespace Brante.Gamepad
{
    // The mod's own settings panel, opened with the ModMenu button and driven by the controller.
    internal sealed class ControllerMenu
    {
        private readonly SettingsMenu model;
        private EventSystem system;
        private GUIStyle rowStyle, titleStyle;
        private int direction, style;
        private float nextRepeat;

        internal ControllerMenu(SettingsMenu model) { this.model = model; }

        internal bool IsOpen { get; private set; }

        internal void Open()
        {
            IsOpen = true;
            direction = 0;
            system = EventSystem.current;
            if (system != null) system.enabled = false;
        }

        internal void Close()
        {
            IsOpen = false;
            if (system != null) system.enabled = true;
            system = null;
            direction = 0;
        }

        internal void Update(Buttons buttons, Buttons pressed, Bindings bindings, float now)
        {
            int wanted = (buttons & Buttons.DPadUp) != 0 ? 1 : (buttons & Buttons.DPadDown) != 0 ? 2 :
                (buttons & Buttons.DPadLeft) != 0 ? 3 : (buttons & Buttons.DPadRight) != 0 ? 4 : 0;
            if (wanted != 0 && (wanted != direction || now >= nextRepeat))
            {
                if (wanted <= 2) model.Move(wanted == 1 ? -1 : 1);
                else if (wanted != direction || model.Selected.Kind == MenuKind.Number) model.Selected.Adjust(wanted == 3 ? -1 : 1);
                bool numeric = wanted > 2 && model.Selected.Kind == MenuKind.Number;
                nextRepeat = now + (wanted != direction ? 0.35f : numeric ? 0.06f : 0.16f);
            }
            direction = wanted;
            if ((pressed & (bindings.Back | bindings.ModMenu)) != 0) Close();
            else if ((pressed & bindings.Click) != 0) model.Selected.Activate();
        }

        internal void Draw(Bindings bindings)
        {
            if (!IsOpen) return;
            int size = Mathf.Max(16, Screen.height / 38);
            if (rowStyle == null || style != size)
            {
                rowStyle = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleLeft };
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(size * 1.3f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                style = size;
            }
            float width = Mathf.Min(Screen.width - 40, size * 36f), height = Mathf.Min(Screen.height - 40, size * 26f);
            Rect box = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            Color color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Box(box, "");
            GUI.Label(new Rect(box.x, box.y + size * 0.5f, width, size * 2f), "Controller settings", titleStyle);

            float rowHeight = size * 1.6f, top = box.y + size * 3f;
            int visible = Mathf.Max(1, (int)((height - size * 6.5f) / rowHeight));
            int first = model.FirstVisible(visible);
            bool restart = false;
            for (int row = 0; row < visible && first + row < model.Items.Count; row++)
            {
                MenuItem item = model.Items[first + row];
                bool selected = first + row == model.Index;
                Rect line = new Rect(box.x + size, top + row * rowHeight, width - size * 2f, rowHeight - 4f);
                if (selected)
                {
                    GUI.color = new Color(0.96f, 0.83f, 0.52f, 0.22f);
                    GUI.DrawTexture(line, Texture2D.whiteTexture);
                    GUI.color = color;
                }
                string value = selected && item.Kind != MenuKind.Toggle ? "<  " + item.Text + "  >" : item.Text;
                GUI.Label(new Rect(line.x + size * 0.5f, line.y, line.width * 0.6f, line.height), item.Label + (item.NeedsRestart ? " *" : ""), rowStyle);
                rowStyle.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(line.x + line.width * 0.5f, line.y, line.width * 0.5f - size * 0.5f, line.height), value, rowStyle);
                rowStyle.alignment = TextAnchor.MiddleLeft;
                restart |= item.NeedsRestart;
            }
            string help = HintText.MenuHelp(bindings) + (restart ? "\n* applies after restarting the game" : "");
            GUI.Label(new Rect(box.x + size, box.yMax - size * 3f, width - size * 2f, size * 2.6f), help, rowStyle);
        }
    }
}
