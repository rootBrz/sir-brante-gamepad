using System;
using System.Collections.Generic;
using System.Globalization;

namespace Brante.Gamepad
{
    internal enum MenuKind { Toggle, Number, Choice }

    // One adjustable setting. Values are read and written through delegates so the menu stays independent of
    // the configuration system.
    internal sealed class MenuItem
    {
        internal readonly string Label;
        internal readonly MenuKind Kind;
        internal readonly bool NeedsRestart;
        private readonly float min, max, step;
        private readonly string[] choices;
        private readonly Func<float> get;
        private readonly Action<float> set;

        private MenuItem(string label, MenuKind kind, float min, float max, float step, string[] choices,
            Func<float> get, Action<float> set, bool needsRestart)
        {
            Label = label; Kind = kind; this.min = min; this.max = max; this.step = step; this.choices = choices;
            this.get = get; this.set = set; NeedsRestart = needsRestart;
        }

        internal static MenuItem Toggle(string label, Func<bool> get, Action<bool> set, bool needsRestart = false)
        {
            return new MenuItem(label, MenuKind.Toggle, 0, 1, 1, null, () => get() ? 1f : 0f, v => set(v > 0.5f), needsRestart);
        }

        internal static MenuItem Number(string label, float min, float max, float step, Func<float> get, Action<float> set)
        {
            return new MenuItem(label, MenuKind.Number, min, max, step, null, get, set, false);
        }

        internal static MenuItem Choice(string label, string[] choices, Func<int> get, Action<int> set, bool needsRestart = false)
        {
            return new MenuItem(label, MenuKind.Choice, 0, choices.Length - 1, 1, choices, () => get(), v => set((int)v), needsRestart);
        }

        internal string Text
        {
            get
            {
                float value = get();
                switch (Kind)
                {
                    case MenuKind.Toggle: return value > 0.5f ? "On" : "Off";
                    case MenuKind.Choice: return choices[Math.Max(0, Math.Min(choices.Length - 1, (int)Math.Round(value)))];
                    default: return value.ToString("F" + Decimals(step), CultureInfo.InvariantCulture);
                }
            }
        }

        // Left (-1) and right (+1). Toggles flip, choices cycle, numbers move one step and stop at the ends.
        internal void Adjust(int direction)
        {
            float value = get();
            switch (Kind)
            {
                case MenuKind.Toggle:
                    set(value > 0.5f ? 0f : 1f);
                    break;
                case MenuKind.Choice:
                    int count = choices.Length;
                    set(((int)Math.Round(value) + direction + count) % count);
                    break;
                default:
                    float next = min + (float)Math.Round((value + direction * step - min) / step) * step;
                    set(Math.Max(min, Math.Min(max, next)));
                    break;
            }
        }

        // The confirm button: flips toggles and cycles choices; numbers ignore it.
        internal void Activate()
        {
            if (Kind != MenuKind.Number) Adjust(1);
        }

        private static int Decimals(float step)
        {
            int places = 0;
            while (places < 3 && Math.Abs(step * (float)Math.Pow(10, places) - Math.Round(step * (float)Math.Pow(10, places))) > 1e-4f) places++;
            return places;
        }
    }

    internal sealed class SettingsMenu
    {
        internal readonly List<MenuItem> Items;
        internal int Index;

        internal SettingsMenu(List<MenuItem> items) { Items = items; }

        internal MenuItem Selected { get { return Items[Index]; } }

        // Wraps at the ends.
        internal void Move(int direction)
        {
            if (Items.Count == 0) return;
            Index = (Index + direction + Items.Count) % Items.Count;
        }

        // First row to draw so the selected row stays visible among visibleRows rows.
        internal int FirstVisible(int visibleRows)
        {
            if (Items.Count <= visibleRows) return 0;
            return Math.Max(0, Math.Min(Items.Count - visibleRows, Index - visibleRows / 2));
        }
    }
}
