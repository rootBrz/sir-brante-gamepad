using System;
using System.Collections.Generic;
using Brante.Gamepad;

internal static class Program
{
    private static int failures;
    private static int count;

    private static void Check(bool condition, string name)
    {
        count++;
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FAIL: " + name);
    }

    private static void Near(float expected, float actual, string name)
    {
        Check(Math.Abs(expected - actual) <= 0.0001f, name + " (expected " + expected + ", got " + actual + ")");
    }

    private static List<int> Rank(Box from, Box[] candidates, int dx, int dy, bool wrap = false)
    {
        List<int> order = new List<int>();
        Navigator.Rank(from, candidates, dx, dy, wrap, order);
        return order;
    }

    private static int Main()
    {
        PadMathChecks();
        ButtonChecks();
        NavigatorChecks();
        KeyboardChecks();
        VirtualKeyChecks();
        HintChecks();
        MenuChecks();
        InputModeChecks();
        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " of " + count + " checks failed.");
            return 1;
        }
        Console.WriteLine(count + " checks passed.");
        return 0;
    }

    private static void PadMathChecks()
    {
        Near(0, PadMath.Axis(0, 0.22f), "axis rest");
        Near(0, PadMath.Axis(7000, 0.22f), "axis inside deadzone");
        Near(0, PadMath.Axis(-7000, 0.22f), "axis inside deadzone negative");
        Near(1, PadMath.Axis(short.MaxValue, 0.22f), "axis positive endpoint");
        Near(-1, PadMath.Axis(short.MinValue, 0.22f), "axis negative endpoint");
        Near(-PadMath.Axis(16000, 0.22f), PadMath.Axis(-16000, 0.22f), "axis symmetric");
        Near(0.5f, PadMath.Curve(0.5f, 1f), "curve linear");
        Near(0.25f, PadMath.Curve(0.5f, 2f), "curve quadratic");
        Near(-0.25f, PadMath.Curve(-0.5f, 2f), "curve keeps sign");
        Near(0, PadMath.Curve(0, 2f), "curve zero");
        Near(1, PadMath.Curve(1, 3f), "curve endpoint");
    }

    private static void ButtonChecks()
    {
        Buttons mask;
        Check(ButtonNames.TryParse("A", out mask) && mask == Buttons.A, "parse single");
        Check(ButtonNames.TryParse("b, start", out mask) && mask == (Buttons.B | Buttons.Start), "parse list, case-insensitive");
        Check(ButtonNames.TryParse("LB+RB", out mask) && mask == (Buttons.LeftShoulder | Buttons.RightShoulder), "parse plus");
        Check(ButtonNames.TryParse("View", out mask) && mask == Buttons.Back, "parse View alias");
        Check(ButtonNames.TryParse("LT", out mask) && mask == Buttons.LeftTrigger, "parse trigger");
        Check(ButtonNames.TryParse("None", out mask) && mask == Buttons.None, "parse None");
        Check(ButtonNames.TryParse("", out mask) && mask == Buttons.None, "parse empty");
        Check(!ButtonNames.TryParse("Q", out mask) && mask == Buttons.None, "reject unknown name");
        Check(!ButtonNames.TryParse("A, Q", out mask) && mask == Buttons.None, "reject partly unknown list");
        Check(!ButtonNames.TryParse(null, out mask), "reject null");
        Check(ButtonNames.Describe(Buttons.Back) == "Share/View", "describe back");
        Check(ButtonNames.Describe(Buttons.B | Buttons.Start) == "B/Start", "describe list");

        List<string> invalid = new List<string>();
        Bindings defaults = Bindings.Parse(action => null == action ? "" : DefaultFor(action), (a, t) => invalid.Add(a));
        Check(invalid.Count == 0, "defaults all valid");
        Check(defaults.Click == Buttons.A && defaults.Back == Buttons.B && defaults.ModMenu == Buttons.Start, "default click/back/mod menu");
        Check(defaults.PagePrevious == Buttons.LeftShoulder && defaults.PageNext == Buttons.RightShoulder, "default pages");
        Check(defaults.Precision == Buttons.LeftTrigger && defaults.Boost == Buttons.RightTrigger, "default modifiers");

        invalid.Clear();
        Bindings custom = Bindings.Parse(action => action == "Click" ? "X" : action == "Skip" ? "bogus" : DefaultFor(action),
            (a, t) => invalid.Add(a));
        Check(custom.Click == Buttons.X, "custom click");
        Check(custom.Skip == Buttons.X && invalid.Count == 1 && invalid[0] == "Skip", "invalid binding falls back and is reported");
    }

    private static string DefaultFor(string action)
    {
        switch (action)
        {
            case "Click": return Bindings.DefaultClick;
            case "Back": return Bindings.DefaultBack;
            case "ModMenu": return Bindings.DefaultModMenu;
            case "Skip": return Bindings.DefaultSkip;
            case "Keyboard": return Bindings.DefaultKeyboard;
            case "PagePrevious": return Bindings.DefaultPagePrevious;
            case "PageNext": return Bindings.DefaultPageNext;
            case "ToggleHints": return Bindings.DefaultToggleHints;
            case "Precision": return Bindings.DefaultPrecision;
            case "Boost": return Bindings.DefaultBoost;
            default: return "";
        }
    }

    private static void NavigatorChecks()
    {
        Box origin = new Box(100, 100, 200, 140);
        Box right = new Box(300, 100, 400, 140);
        Box diagonal = new Box(250, 220, 350, 260);
        Box behind = new Box(-100, 100, 0, 140);

        Check(float.IsPositiveInfinity(Navigator.Score(origin, behind, 1, 0)), "rejects target behind");
        Check(float.IsPositiveInfinity(Navigator.Score(origin, origin, 1, 0)), "rejects self");
        Check(Navigator.Score(origin, right, 1, 0) < Navigator.Score(origin, diagonal, 1, 0), "prefers same row over diagonal");

        List<int> order = Rank(origin, new[] { diagonal, behind, right }, 1, 0);
        Check(order.Count == 2 && order[0] == 2 && order[1] == 0, "rank order drops rejected and sorts best first");

        Box above = new Box(100, 200, 200, 240);
        Box below = new Box(100, 0, 200, 40);
        order = Rank(origin, new[] { below, above }, 0, 1, false);
        Check(order.Count == 1 && order[0] == 1, "up chooses target above");
        order = Rank(origin, new[] { below, above }, 0, -1, false);
        Check(order.Count == 1 && order[0] == 0, "down chooses target below");

        // A wide neighbour overlapping the origin's row beats a narrow one that only matches by center offset.
        Box wideAbove = new Box(0, 200, 400, 240);
        Box farAbove = new Box(500, 200, 540, 240);
        order = Rank(origin, new[] { farAbove, wideAbove }, 0, 1, false);
        Check(order[0] == 1, "overlapping column wins");

        // Origin that is only a point (pointer over empty space).
        order = Rank(Box.Point(0, 0), new[] { new Box(50, -10, 90, 10), new Box(50, 100, 90, 120) }, 1, 0, false);
        Check(order[0] == 0, "point origin prefers same row");

        Box wrapLeft = new Box(0, 100, 100, 140);
        Box wrapMid = new Box(200, 100, 300, 140);
        Box wrapRight = new Box(400, 100, 500, 140);
        order = Rank(wrapRight, new[] { wrapLeft, wrapMid }, 1, 0, false);
        Check(order.Count == 0, "no wrap by default");
        order = Rank(wrapRight, new[] { wrapMid, wrapLeft }, 1, 0, true);
        Check(order.Count == 2 && order[0] == 1, "wrap goes to the opposite edge");
        order = Rank(wrapLeft, new[] { wrapMid, wrapRight }, 1, 0, true);
        Check(order.Count == 2 && order[0] == 0, "wrap not used when something is ahead");

        Box a = new Box(0, 0, 10, 10), b = new Box(5, 5, 20, 30);
        Box union = a.Union(b);
        Check(union.XMin == 0 && union.YMin == 0 && union.XMax == 20 && union.YMax == 30, "box union");
        Box overlap = a.Intersect(b);
        Check(overlap.XMin == 5 && overlap.YMin == 5 && overlap.XMax == 10 && overlap.YMax == 10, "box intersect");
        Check(a.Contains(5, 5) && !a.Contains(11, 5), "box contains");
    }

    private static void KeyboardChecks()
    {
        KeyboardLayout latin = KeyboardLayout.ForLanguage("en");
        Check(latin.Characters.Length == 30 && latin.Columns == 8, "latin has 26 letters and 4 marks");
        Check(latin.Label(latin.Characters.IndexOf(' '), false) == "Space", "space label");
        Check(latin.Label(0, true) == "a" && latin.Label(0, false) == "A", "case labels");
        Check(latin.Label(latin.ActionIndex(KeyboardAction.Cancel), false) == "Cancel", "action label");

        KeyboardLayout russian = KeyboardLayout.ForLanguage("ru");
        Check(russian.Characters.Contains("Я") && russian.Characters.Contains("Ё"), "russian layout has Cyrillic");
        Check(russian.Character(0, true) == 'а', "cyrillic lower case");
        Check(KeyboardLayout.ForLanguage("de-DE").Characters.Contains("Ü"), "region suffix is ignored");
        Check(KeyboardLayout.ForLanguage("ko").Characters == latin.Characters, "unsupported scripts fall back to latin");
        Check(KeyboardLayout.ForLanguage(null).Characters == latin.Characters, "null language falls back to latin");

        // 30 keys at 8 columns: rows 0-2 full, row 3 holds 6 keys (24..29).
        Check(latin.Rows == 4, "four rows");
        Check(latin.Move(0, 0, 1) == 8 && latin.Move(8, 0, -1) == 0, "down and up inside the grid");
        Check(latin.Move(0, 0, -1) == 0 && latin.Move(0, -1, 0) == 0, "stays at top-left edge");
        Check(latin.Move(29, 1, 0) == 29, "stays at last key");
        Check(latin.Move(22, 0, 1) == 29, "short last row lands on its final key");
        Check(latin.Move(20, 0, 1) == 28, "column is kept going down");

        int first = latin.ActionIndex(KeyboardAction.Delete);
        int last = latin.ActionIndex(KeyboardAction.Cancel);
        Check(latin.Move(24, 0, 1) == first, "left of last row reaches Delete");
        Check(latin.Move(29, 0, 1) == latin.ActionIndex(KeyboardAction.Done), "key under Done's column reaches Done");
        Check(latin.Move(first, 0, 1) == first, "no row below actions");
        Check(latin.Move(last, 0, -1) == 29, "Cancel goes up to the last key");
        Check(latin.Move(first, 0, -1) == 25, "Delete goes up to the key above its center");
        Check(latin.Move(first, 1, 0) == first + 1 && latin.Move(last, 1, 0) == last, "actions move sideways in their own row");
        Check(latin.Move(first, -1, 0) == first, "actions stop at Delete");

        for (int i = 0; i < latin.KeyCount; i++)
        {
            for (int d = 0; d < 4; d++)
            {
                int to = latin.Move(i, d == 2 ? -1 : d == 3 ? 1 : 0, d == 0 ? -1 : d == 1 ? 1 : 0);
                Check(to >= 0 && to < latin.KeyCount, "move stays in range from " + i);
            }
        }

        KeyboardText text = new KeyboardText("Ab", 3);
        Check(text.Append('c') && text.Value == "Abc", "append");
        Check(!text.Append('d') && text.Value == "Abc", "limit blocks append");
        Check(text.Delete() && text.Value == "Ab", "delete");
        KeyboardText empty = new KeyboardText(null, 0);
        Check(!empty.Delete() && empty.Value == "", "delete on empty");
        Check(empty.Append('x') && empty.Append('y') && empty.Value == "xy", "no limit when zero");
    }

    private static void VirtualKeyChecks()
    {
        VirtualKeys keys = new VirtualKeys();
        Check(!keys.Down(PadKey.Escape, 5) && !keys.Held(PadKey.Escape, 5) && !keys.AnyHeld(5), "idle");
        // Pressed while frame 9 runs; visible to every script from frame 10 on.
        keys.Press(PadKey.Escape, 10, 1.00f, 0.08f);
        Check(!keys.Down(PadKey.Escape, 9) && keys.Down(PadKey.Escape, 10) && !keys.Down(PadKey.Escape, 11), "down lasts one frame");
        Check(!keys.Held(PadKey.Escape, 9) && keys.Held(PadKey.Escape, 10) && keys.AnyHeld(10) && keys.AnyDown(10), "held from the visible frame");
        keys.Tick(10, 1.05f);
        Check(keys.Held(PadKey.Escape, 11), "still held before hold time");
        keys.Tick(9, 1.09f);
        Check(keys.Held(PadKey.Escape, 10), "not released before its visible frame is reached");
        keys.Tick(10, 1.09f);
        Check(!keys.Held(PadKey.Escape, 11) && keys.Up(PadKey.Escape, 11) && !keys.Up(PadKey.Escape, 12), "up is visible for one frame");
        Check(!keys.Held(PadKey.Space, 12) && !keys.Down(PadKey.Space, 10), "other keys unaffected");

        keys.Press(PadKey.Space, 20, 2.0f, 0.08f);
        keys.Press(PadKey.Space, 21, 2.01f, 0.08f);
        Check(keys.Down(PadKey.Space, 21) && keys.Up(PadKey.Space, 21), "retrigger ends the previous press");
        keys.Clear(21);
        Check(!keys.Held(PadKey.Space, 22) && keys.Up(PadKey.Space, 22) && !keys.Down(PadKey.Space, 21), "clear releases");
    }

    private static void MenuChecks()
    {
        bool toggle = false;
        float number = 0.5f;
        int choice = 0;
        MenuItem flag = MenuItem.Toggle("Flag", () => toggle, v => toggle = v);
        MenuItem amount = MenuItem.Number("Amount", 0.1f, 1f, 0.05f, () => number, v => number = v);
        MenuItem whole = MenuItem.Number("Whole", 100f, 300f, 50f, () => 100f, v => { });
        MenuItem pick = MenuItem.Choice("Pick", new[] { "Win32", "Direct" }, () => choice, v => choice = v, true);

        Check(flag.Text == "Off", "toggle text off");
        flag.Activate();
        Check(toggle && flag.Text == "On", "activate flips toggle");
        flag.Adjust(-1);
        Check(!toggle, "adjust flips toggle either way");
        Check(!flag.NeedsRestart && pick.NeedsRestart, "restart flag");

        Check(amount.Text == "0.50", "number text uses step precision");
        amount.Adjust(1);
        Near(0.55f, number, "number steps up without drift");
        for (int i = 0; i < 40; i++) amount.Adjust(1);
        Near(1f, number, "number stops at max");
        for (int i = 0; i < 40; i++) amount.Adjust(-1);
        Near(0.1f, number, "number stops at min");
        amount.Activate();
        Near(0.1f, number, "activate leaves numbers alone");
        Check(whole.Text == "100", "whole-number text has no decimals");

        Check(pick.Text == "Win32", "choice text");
        pick.Activate();
        Check(choice == 1 && pick.Text == "Direct", "activate cycles choice");
        pick.Adjust(1);
        Check(choice == 0, "choice wraps forward");
        pick.Adjust(-1);
        Check(choice == 1, "choice wraps backward");

        SettingsMenu menu = new SettingsMenu(new List<MenuItem> { flag, amount, whole, pick });
        Check(menu.Selected == flag, "starts on first item");
        menu.Move(-1);
        Check(menu.Selected == pick, "up wraps to last");
        menu.Move(1);
        Check(menu.Selected == flag, "down wraps to first");
        Check(menu.FirstVisible(10) == 0, "short list shows everything");
        List<MenuItem> many = new List<MenuItem>();
        for (int i = 0; i < 20; i++) many.Add(flag);
        SettingsMenu long_ = new SettingsMenu(many);
        Check(long_.FirstVisible(6) == 0, "long list starts at top");
        long_.Index = 10;
        Check(long_.FirstVisible(6) == 7, "selection stays centered");
        long_.Index = 19;
        Check(long_.FirstVisible(6) == 14, "last rows stay visible");
        Check(HintText.MenuHelp(Bindings.Parse(DefaultFor, null)).Contains("B/Start: close"), "menu help text");
    }

    private static void InputModeChecks()
    {
        InputMode mode = new InputMode();
        Check(!mode.KeyboardMouse, "starts in controller mode");
        Check(!mode.Observe(1f, false, false, false), "no input changes nothing");
        mode.Own(1f);
        Check(!mode.Observe(1.1f, true, true, true) && !mode.KeyboardMouse, "input right after the plugin's own action is ignored");
        Check(mode.Observe(1f + InputMode.Grace + 0.01f, true, false, false) && mode.KeyboardMouse, "mouse movement after the grace period switches mode");
        Check(!mode.Observe(5f, true, true, true), "already in keyboard/mouse mode");
        Check(mode.ControllerUsed() && !mode.KeyboardMouse, "controller input switches back");
        Check(!mode.ControllerUsed(), "no change when already in controller mode");
        Check(mode.Observe(6f, false, true, false), "a click switches mode");
        mode.ControllerUsed();
        Check(mode.Observe(7f, false, false, true), "a key press switches mode");
    }

    private static void HintChecks()
    {
        Bindings bindings = Bindings.Parse(DefaultFor, null);
        string plain = HintText.Build(bindings, new HintContext());
        Check(plain.Contains("D-pad: focus") && plain.Contains("A: select") && plain.Contains("B: back"), "base hints");
        Check(plain.Contains("LB/RB: page") && plain.Contains("Share/View: hide hints") && plain.Contains("Start: settings"), "page, settings and toggle hints");
        Check(!plain.Contains("keyboard") && !plain.Contains("scroll") && !plain.Contains("drag"), "context hints hidden by default");

        string text = HintText.Build(bindings, new HintContext { OverTextField = true });
        Check(text.Contains("Y: keyboard") && text.Contains("A: type"), "text field hints");
        Check(HintText.Build(bindings, new HintContext { OverScrollable = true }).Contains("R stick: scroll"), "scroll hint");
        Check(HintText.Build(bindings, new HintContext { OverDraggable = true }).Contains("hold A + L stick: drag"), "drag hint");

        Bindings remapped = Bindings.Parse(action => action == "Click" ? "X" : action == "Skip" ? "A" : DefaultFor(action), null);
        Check(HintText.Build(remapped, new HintContext()).Contains("X: select"), "hints follow remapped buttons");
        Check(HintText.KeyboardHelp(bindings) == "A: type   X: delete   Y: case   Start: done   B: cancel", "keyboard help text");
    }
}
