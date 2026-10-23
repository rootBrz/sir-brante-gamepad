using System;

namespace Brante.Gamepad
{
    // Which physical buttons trigger each action. Navigation (D-pad) and the sticks are fixed.
    // ModMenu opens the mod's settings panel and accepts text on the controller keyboard.
    internal sealed class Bindings
    {
        internal const string DefaultClick = "A";
        internal const string DefaultBack = "B";
        internal const string DefaultModMenu = "Start";
        internal const string DefaultSkip = "X";
        internal const string DefaultKeyboard = "Y";
        internal const string DefaultPagePrevious = "LB";
        internal const string DefaultPageNext = "RB";
        internal const string DefaultToggleHints = "Back";
        internal const string DefaultPrecision = "LT";
        internal const string DefaultBoost = "RT";

        internal Buttons Click, Back, ModMenu, Skip, Keyboard, PagePrevious, PageNext, ToggleHints, Precision, Boost;

        // Invalid names fall back to the default for that action and are reported through onInvalid.
        internal static Bindings Parse(Func<string, string> read, Action<string, string> onInvalid)
        {
            Bindings bindings = new Bindings();
            bindings.Click = One(read, "Click", DefaultClick, onInvalid);
            bindings.Back = One(read, "Back", DefaultBack, onInvalid);
            bindings.ModMenu = One(read, "ModMenu", DefaultModMenu, onInvalid);
            bindings.Skip = One(read, "Skip", DefaultSkip, onInvalid);
            bindings.Keyboard = One(read, "Keyboard", DefaultKeyboard, onInvalid);
            bindings.PagePrevious = One(read, "PagePrevious", DefaultPagePrevious, onInvalid);
            bindings.PageNext = One(read, "PageNext", DefaultPageNext, onInvalid);
            bindings.ToggleHints = One(read, "ToggleHints", DefaultToggleHints, onInvalid);
            bindings.Precision = One(read, "Precision", DefaultPrecision, onInvalid);
            bindings.Boost = One(read, "Boost", DefaultBoost, onInvalid);
            return bindings;
        }

        private static Buttons One(Func<string, string> read, string action, string fallback, Action<string, string> onInvalid)
        {
            string text = read(action);
            Buttons mask;
            if (ButtonNames.TryParse(text, out mask)) return mask;
            if (onInvalid != null) onInvalid(action, text);
            ButtonNames.TryParse(fallback, out mask);
            return mask;
        }
    }
}
