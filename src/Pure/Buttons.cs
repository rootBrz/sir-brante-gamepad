using System;
using System.Collections.Generic;
using System.Text;

namespace Brante.Gamepad
{
    // XInput wButtons bit values; triggers are synthesized above the 16-bit range.
    [Flags]
    internal enum Buttons : uint
    {
        None = 0,
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftStick = 0x0040,
        RightStick = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
        LeftTrigger = 0x10000,
        RightTrigger = 0x20000,
        DPad = DPadUp | DPadDown | DPadLeft | DPadRight
    }

    internal static class ButtonNames
    {
        private static readonly KeyValuePair<string, Buttons>[] Names =
        {
            new KeyValuePair<string, Buttons>("A", Buttons.A),
            new KeyValuePair<string, Buttons>("B", Buttons.B),
            new KeyValuePair<string, Buttons>("X", Buttons.X),
            new KeyValuePair<string, Buttons>("Y", Buttons.Y),
            new KeyValuePair<string, Buttons>("LB", Buttons.LeftShoulder),
            new KeyValuePair<string, Buttons>("RB", Buttons.RightShoulder),
            new KeyValuePair<string, Buttons>("LT", Buttons.LeftTrigger),
            new KeyValuePair<string, Buttons>("RT", Buttons.RightTrigger),
            new KeyValuePair<string, Buttons>("Start", Buttons.Start),
            new KeyValuePair<string, Buttons>("Back", Buttons.Back),
            new KeyValuePair<string, Buttons>("LS", Buttons.LeftStick),
            new KeyValuePair<string, Buttons>("RS", Buttons.RightStick),
            new KeyValuePair<string, Buttons>("DPadUp", Buttons.DPadUp),
            new KeyValuePair<string, Buttons>("DPadDown", Buttons.DPadDown),
            new KeyValuePair<string, Buttons>("DPadLeft", Buttons.DPadLeft),
            new KeyValuePair<string, Buttons>("DPadRight", Buttons.DPadRight)
        };

        // Accepts names separated by commas, plus signs or spaces, e.g. "B, Start". "None" or "" gives no buttons.
        internal static bool TryParse(string text, out Buttons mask)
        {
            mask = Buttons.None;
            if (text == null) return false;
            string[] parts = text.Split(new[] { ',', '+', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (string.Equals(part, "None", StringComparison.OrdinalIgnoreCase)) continue;
                string name = part;
                if (string.Equals(name, "View", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Share", StringComparison.OrdinalIgnoreCase)) name = "Back";
                bool found = false;
                foreach (KeyValuePair<string, Buttons> entry in Names)
                {
                    if (!string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)) continue;
                    mask |= entry.Value;
                    found = true;
                    break;
                }
                if (!found) { mask = Buttons.None; return false; }
            }
            return true;
        }

        internal static string Describe(Buttons mask)
        {
            StringBuilder text = new StringBuilder();
            foreach (KeyValuePair<string, Buttons> entry in Names)
            {
                if ((mask & entry.Value) != entry.Value) continue;
                if (text.Length > 0) text.Append('/');
                text.Append(entry.Key == "Back" ? "Share/View" : entry.Key);
            }
            return text.ToString();
        }
    }
}
