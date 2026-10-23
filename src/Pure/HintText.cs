using System.Text;

namespace Brante.Gamepad
{
    // What is under the pointer; decides which hints are worth showing.
    internal struct HintContext
    {
        internal bool OverTextField, OverScrollable, OverDraggable;
    }

    internal static class HintText
    {
        internal static string Build(Bindings bindings, HintContext context)
        {
            StringBuilder text = new StringBuilder();
            Add(text, "D-pad", "focus");
            Add(text, "L stick", "pointer");
            if (context.OverScrollable) Add(text, "R stick", "scroll");
            Add(text, ButtonNames.Describe(bindings.Click), context.OverTextField ? "type" : "select");
            if (context.OverDraggable) Add(text, "hold " + ButtonNames.Describe(bindings.Click) + " + L stick", "drag");
            Add(text, ButtonNames.Describe(bindings.Back), "back");
            Add(text, ButtonNames.Describe(bindings.PagePrevious) + "/" + ButtonNames.Describe(bindings.PageNext), "page");
            Add(text, ButtonNames.Describe(bindings.Skip), "skip");
            if (context.OverTextField) Add(text, ButtonNames.Describe(bindings.Keyboard), "keyboard");
            Add(text, ButtonNames.Describe(bindings.ModMenu), "settings");
            Add(text, ButtonNames.Describe(bindings.ToggleHints), "hide hints");
            return text.ToString();
        }

        internal static string KeyboardHelp(Bindings bindings)
        {
            return ButtonNames.Describe(bindings.Click) + ": type   " + ButtonNames.Describe(bindings.Skip) + ": delete   " +
                ButtonNames.Describe(bindings.Keyboard) + ": case   " + ButtonNames.Describe(bindings.ModMenu) + ": done   " +
                ButtonNames.Describe(bindings.Back) + ": cancel";
        }

        internal static string MenuHelp(Bindings bindings)
        {
            return "D-pad up/down: select   left/right: change   " + ButtonNames.Describe(bindings.Click) + ": toggle   " +
                ButtonNames.Describe(bindings.Back) + "/" + ButtonNames.Describe(bindings.ModMenu) + ": close";
        }

        private static void Add(StringBuilder text, string key, string action)
        {
            if (text.Length > 0) text.Append("   ");
            text.Append(key).Append(": ").Append(action);
        }
    }
}
