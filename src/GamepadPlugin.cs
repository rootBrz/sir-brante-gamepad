using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace Brante.Gamepad
{
    [BepInPlugin("local.brante.gamepad", "Sir Brante Gamepad", BuildInfo.Version)]
    public sealed class GamepadPlugin : BaseUnityPlugin
    {
        private const float ProbeInterval = 0.1f;

        private ConfigEntry<float> deadzone, cursorSpeed, scrollSpeed, pointerCurve, precisionFactor, boostFactor, hintScale, clickVolume;
        private ConfigEntry<bool> showHints, diagnostics, wrapNavigation, focusRing, rumble, clickSound, welcomed, skipIntro, skipLogos, skipLoadingWait;
        private ConfigEntry<string> backend;
        private Bindings bindings;
        private IInputSink sink;
        private DirectSink directSink;
        private readonly XInputReader reader = new XInputReader();
        private readonly UiScanner scanner = new UiScanner();
        private readonly ControllerKeyboard keyboard = new ControllerKeyboard();
        private ControllerMenu menu;
        private readonly Overlay overlay = new Overlay();
        private Feedback feedback;
        private Callback<GameOverlayActivated_t> overlayCallback;
        private Harmony cursorPatch;
        private static bool forceAnyKey;
        private static float introSeenAt = -1f;

        private bool steamOverlay, primed, connected, active;
        private Buttons previous;
        private int direction;
        private float nextRepeat, nextScroll, nextProbe, toastUntil;
        private Probe probe;
        private string hintText = "";
        private HintContext hintContext;
        private bool hintsDirty = true;
        private bool splashLogged, loadingPatched;
        private static int loadingWaitsRemoved;

        private void Awake()
        {
            Config.SaveOnConfigSet = false; // the settings menu changes values at key-repeat rate; save when it closes
            cursorPatch = new Harmony("local.brante.gamepad.cursor");
            cursorPatch.Patch(AccessTools.Method(typeof(Cursor), nameof(Cursor.SetCursor),
                new[] { typeof(Texture2D), typeof(Vector2), typeof(CursorMode) }),
                prefix: new HarmonyMethod(typeof(GamepadPlugin), nameof(UseGameCursor)));

            skipLogos = Config.Bind("Startup", "SkipLogos", true, "Skip the startup logo screens (Unity splash and studio logos). Patches UnityPlayer.dll in memory only; applies at launch.");
            skipLoadingWait = Config.Bind("Startup", "SkipLoadingWait", true, "Remove the fixed 1.5 s of waiting on the game's start-up loading screen.");
            if (skipLoadingWait.Value)
            {
                PatchLoadingWait();
                if (!loadingPatched) AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => PatchLoadingWait();
            }
            if (skipLogos.Value)
            {
                string reason = SplashPatch.Apply();
                if (reason == null) Logger.LogInfo("Startup logo skip enabled.");
                else Logger.LogWarning("Startup logo skip unavailable: " + reason + ".");
            }
            skipIntro = Config.Bind("Startup", "SkipIntro", false, "Skip the story intro scene (title logo with voice-over) that plays when a new game starts.");
            if (skipIntro.Value) PatchIntroSkip();

            deadzone = Config.Bind("Controller", "Deadzone", 0.22f,
                new ConfigDescription("Stick deadzone; raise to prevent drift.", new AcceptableValueRange<float>(0.05f, 0.9f)));
            cursorSpeed = Config.Bind("Controller", "CursorSpeed", 900f,
                new ConfigDescription("Pointer speed in screen pixels per second.", new AcceptableValueRange<float>(100f, 3000f)));
            scrollSpeed = Config.Bind("Controller", "ScrollSpeed", 8f,
                new ConfigDescription("Right-stick scroll notches per second.", new AcceptableValueRange<float>(1f, 30f)));
            pointerCurve = Config.Bind("Controller", "PointerCurve", 1f,
                new ConfigDescription("Stick response exponent. 1 is linear; higher gives finer control near the center.", new AcceptableValueRange<float>(1f, 3f)));
            precisionFactor = Config.Bind("Controller", "PrecisionFactor", 0.3f,
                new ConfigDescription("Pointer and scroll speed multiplier while the Precision button is held.", new AcceptableValueRange<float>(0.05f, 1f)));
            boostFactor = Config.Bind("Controller", "BoostFactor", 2f,
                new ConfigDescription("Pointer and scroll speed multiplier while the Boost button is held.", new AcceptableValueRange<float>(1f, 5f)));
            backend = Config.Bind("Input", "Backend", "Win32",
                new ConfigDescription("Win32 sends operating-system input (the default, tested under Proton). Direct delivers clicks, scrolling and keys inside the game process; falls back to Win32 if it cannot be installed.",
                    new AcceptableValueList<string>("Win32", "Direct")));
            wrapNavigation = Config.Bind("Navigation", "Wrap", false, "At the last control in a direction, jump to the opposite edge.");
            showHints = Config.Bind("Display", "ShowHints", true, "Show controller controls; toggle with the ToggleHints button.");
            focusRing = Config.Bind("Display", "FocusRing", true, "Softly highlight the control under the pointer while using the controller.");
            hintScale = Config.Bind("Display", "HintScale", 1f,
                new ConfigDescription("Size multiplier for the hint bar and notices.", new AcceptableValueRange<float>(0.5f, 3f)));
            welcomed = Config.Bind("Display", "Welcomed", false, "Set automatically after the first-run notice.");
            rumble = Config.Bind("Feedback", "Rumble", false, "Short vibration on click.");
            clickSound = Config.Bind("Feedback", "ClickSound", false, "Soft tick sound on click.");
            clickVolume = Config.Bind("Feedback", "ClickVolume", 0.25f,
                new ConfigDescription("Volume of the click sound.", new AcceptableValueRange<float>(0f, 1f)));
            diagnostics = Config.Bind("Debug", "LogActions", false, "Log controller actions and focus targets for troubleshooting.");
            bindings = Bindings.Parse(action => Config.Bind("Buttons", action, DefaultButton(action), ButtonHelp(action)).Value,
                (action, value) => Logger.LogWarning("Unknown button name in [Buttons] " + action + " = \"" + value + "\"; using the default."));

            feedback = new Feedback(reader, gameObject) { Rumble = rumble.Value, Sound = clickSound.Value, Volume = clickVolume.Value };
            sink = CreateSink();
            menu = new ControllerMenu(BuildMenu());
            overlayCallback = Callback<GameOverlayActivated_t>.Create(value => steamOverlay = value.m_bActive != 0);
            Logger.LogInfo("Gamepad ready (" + sink.Name + " input): D-pad focus, left stick pointer, right stick scroll, " +
                ButtonNames.Describe(bindings.Click) + " click, " + ButtonNames.Describe(bindings.Back) + " back, " + ButtonNames.Describe(bindings.ModMenu) + " settings.");
        }

        private SettingsMenu BuildMenu()
        {
            return new SettingsMenu(new System.Collections.Generic.List<MenuItem>
            {
                MenuItem.Toggle("Button hints", () => showHints.Value, v => showHints.Value = v),
                MenuItem.Toggle("Focus highlight", () => focusRing.Value, v => focusRing.Value = v),
                MenuItem.Number("Hint size", 0.5f, 3f, 0.1f, () => hintScale.Value, v => hintScale.Value = v),
                MenuItem.Number("Pointer speed", 100f, 3000f, 50f, () => cursorSpeed.Value, v => cursorSpeed.Value = v),
                MenuItem.Number("Pointer curve", 1f, 3f, 0.1f, () => pointerCurve.Value, v => pointerCurve.Value = v),
                MenuItem.Number("Precision speed (hold)", 0.05f, 1f, 0.05f, () => precisionFactor.Value, v => precisionFactor.Value = v),
                MenuItem.Number("Boost speed (hold)", 1f, 5f, 0.25f, () => boostFactor.Value, v => boostFactor.Value = v),
                MenuItem.Number("Scroll speed", 1f, 30f, 1f, () => scrollSpeed.Value, v => scrollSpeed.Value = v),
                MenuItem.Number("Stick deadzone", 0.05f, 0.9f, 0.01f, () => deadzone.Value, v => deadzone.Value = v),
                MenuItem.Toggle("Wrap navigation", () => wrapNavigation.Value, v => wrapNavigation.Value = v),
                MenuItem.Toggle("Rumble on click", () => rumble.Value, v => rumble.Value = v),
                MenuItem.Toggle("Click sound", () => clickSound.Value, v => clickSound.Value = v),
                MenuItem.Number("Click volume", 0f, 1f, 0.05f, () => clickVolume.Value, v => clickVolume.Value = v),
                MenuItem.Toggle("Skip startup logos", () => skipLogos.Value, v => skipLogos.Value = v, true),
                MenuItem.Toggle("Skip loading wait", () => skipLoadingWait.Value, v => skipLoadingWait.Value = v, true),
                MenuItem.Toggle("Skip story intro", () => skipIntro.Value, v => skipIntro.Value = v, true),
                MenuItem.Choice("Input backend", new[] { "Win32", "Direct" }, () => backend.Value == "Direct" ? 1 : 0,
                    v => backend.Value = v == 1 ? "Direct" : "Win32", true)
            });
        }

        private static string DefaultButton(string action)
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
                default: return Bindings.DefaultBoost;
            }
        }

        private static string ButtonHelp(string action)
        {
            string text;
            switch (action)
            {
                case "Click": text = "Click; hold to drag. Also types a key on the controller keyboard."; break;
                case "Back": text = "Sends Escape; cancels the controller keyboard."; break;
                case "ModMenu": text = "Opens the mod settings panel; accepts text on the controller keyboard."; break;
                case "Skip": text = "Sends Space (skips cutscenes); deletes on the controller keyboard."; break;
                case "Keyboard": text = "Opens the controller keyboard over a text field; changes case inside it."; break;
                case "PagePrevious": text = "Previous dialogue page."; break;
                case "PageNext": text = "Next dialogue page."; break;
                case "ToggleHints": text = "Show or hide the control hints."; break;
                case "Precision": text = "Hold to slow the pointer and scrolling."; break;
                default: text = "Hold to speed up the pointer and scrolling."; break;
            }
            return text + " Names: A B X Y LB RB LT RT Start Back LS RS, comma-separated for several, None to disable.";
        }

        private IInputSink CreateSink()
        {
            if (backend.Value == "Direct")
            {
                DirectSink direct = new DirectSink();
                string error;
                if (direct.TryInstall(out error))
                {
                    directSink = direct;
                    return direct;
                }
                Logger.LogWarning("Direct input could not be installed (" + error + "); using Win32 input.");
            }
            return new Win32Sink();
        }

        // The intro (class CutsceneIntro) ends on Input.anyKey. While its Update runs, anyKey reports true,
        // so the game's own skip code runs on the first frame and stops the voice-over.
        private void PatchIntroSkip()
        {
            try
            {
                Type intro = AccessTools.TypeByName("CutsceneIntro");
                System.Reflection.MethodBase update = intro == null ? null : AccessTools.Method(intro, "Update");
                System.Reflection.MethodBase anyKey = AccessTools.PropertyGetter(typeof(Input), "anyKey");
                if (update == null || anyKey == null) throw new MissingMethodException("CutsceneIntro.Update or Input.anyKey");
                cursorPatch.Patch(update, prefix: new HarmonyMethod(typeof(GamepadPlugin), nameof(IntroBegin)),
                    postfix: new HarmonyMethod(typeof(GamepadPlugin), nameof(IntroEnd)),
                    finalizer: new HarmonyMethod(typeof(GamepadPlugin), nameof(IntroFinalizer)));
                cursorPatch.Patch(anyKey, postfix: new HarmonyMethod(typeof(GamepadPlugin), nameof(IntroAnyKey)));
                Logger.LogInfo("Intro skip enabled.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Intro skip unavailable: " + exception.Message);
            }
        }

        // Waits half a second after the intro's first Update so the scene's flow machine exists, as it would by the
        // time a player could press a key.
        private static void IntroBegin()
        {
            if (introSeenAt < 0f) introSeenAt = Time.unscaledTime;
            forceAnyKey = Time.unscaledTime - introSeenAt >= 0.5f;
        }
        private static void IntroEnd() { if (forceAnyKey) introSeenAt = -1f; forceAnyKey = false; }
        private static void IntroFinalizer() { if (forceAnyKey) introSeenAt = -1f; forceAnyKey = false; }
        private static void IntroAnyKey(ref bool __result) { if (forceAnyKey) __result = true; }

        // Unity renders the game's chosen texture even when gamescope hides hardware cursors after gamepad input.
        private static void UseGameCursor(ref CursorMode cursorMode)
        {
            cursorMode = CursorMode.ForceSoftware;
        }

        private void Trace(string message) { if (diagnostics.Value) Logger.LogInfo(message); }
        private void TraceKeyboard(string message) { Trace(message); hintsDirty = true; }

        // Logs when Unity's startup logos end, so the saving from SkipLogos can be read from the log.
        private void WatchSplash()
        {
            if (splashLogged || !UnityEngine.Rendering.SplashScreen.isFinished) return;
            splashLogged = true;
            Logger.LogInfo("Startup logos finished at " + Time.realtimeSinceStartup.ToString("F2") + " s.");
        }

        // GamePreparationScene waits a fixed 1 s and 0.5 s (WaitForSeconds) around loading the boot scene. A transpiler
        // on the coroutine sets those waits to zero; the scene still loads for as long as loading really takes.
        private void PatchLoadingWait()
        {
            if (loadingPatched) return;
            Type scene = AccessTools.TypeByName("_Scripts.AMVCC.Views.Scenes.GamePreparationScript");
            if (scene == null) return; // game assembly not loaded yet; retried when it is
            loadingPatched = true;
            try
            {
                Type iterator = null;
                foreach (Type nested in scene.GetNestedTypes(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                    if (nested.Name.Contains("LoadBootScene")) iterator = nested;
                System.Reflection.MethodBase moveNext = iterator == null ? null : AccessTools.Method(iterator, "MoveNext");
                if (moveNext == null) throw new MissingMethodException("GamePreparationScript.LoadBootScene iterator");
                loadingWaitsRemoved = 0;
                cursorPatch.Patch(moveNext, transpiler: new HarmonyMethod(typeof(GamepadPlugin), nameof(RemoveWaits)));
                if (loadingWaitsRemoved == 0) throw new InvalidOperationException("no WaitForSeconds found");
                Logger.LogInfo("Loading wait removed (" + loadingWaitsRemoved + " waits).");
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Loading wait removal unavailable: " + exception.Message);
            }
        }

        private static IEnumerable<CodeInstruction> RemoveWaits(IEnumerable<CodeInstruction> code)
        {
            System.Reflection.ConstructorInfo wait = AccessTools.Constructor(typeof(WaitForSeconds), new[] { typeof(float) });
            List<CodeInstruction> list = new List<CodeInstruction>(code);
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].opcode != System.Reflection.Emit.OpCodes.Newobj || !object.Equals(list[i].operand, wait)) continue;
                if (list[i - 1].opcode != System.Reflection.Emit.OpCodes.Ldc_R4) continue;
                list[i - 1].operand = 0f;
                loadingWaitsRemoved++;
            }
            return list;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            WatchSplash();
            sink.Tick(now);
            feedback.Rumble = rumble.Value;
            feedback.Sound = clickSound.Value;
            feedback.Volume = clickVolume.Value;
            feedback.Tick(now);

            PadState pad;
            bool found = reader.Read(deadzone.Value, out pad);
            if (found != connected)
            {
                connected = found;
                if (found) { active = true; hintsDirty = true; } // show the hints without waiting for the first input
                Logger.LogInfo(found ? "XInput controller connected." : "XInput controller disconnected.");
            }
            IntPtr window;
            bool ownWindow = OsCursor.GameIsForeground(out window);
            if (diagnostics.Value && pad.buttons != previous)
                Logger.LogInfo("Pad " + reader.Index + " buttons=" + ((uint)pad.buttons).ToString("X5") +
                    " focused=" + Application.isFocused + " ownWindow=" + ownWindow +
                    " overlay=" + steamOverlay + " keyboard=" + keyboard.IsOpen);
            if (!found || !Application.isFocused || !ownWindow || steamOverlay)
            {
                sink.ReleaseAll();
                feedback.Stop();
                previous = pad.buttons;
                primed = found;
                active = false;
                return;
            }
            if (!primed) { previous = pad.buttons; primed = true; return; }

            Buttons pressed = pad.buttons & ~previous;
            if ((pressed & bindings.ToggleHints) != 0) { showHints.Value = !showHints.Value; Config.Save(); }
            if (keyboard.IsOpen)
            {
                keyboard.Update(pad.buttons, pressed, bindings, now, TraceKeyboard);
                previous = pad.buttons;
                return;
            }
            if (menu.IsOpen)
            {
                menu.Update(pad.buttons, pressed, bindings, now);
                if (!menu.IsOpen) Config.Save();
                previous = pad.buttons;
                return;
            }
            if ((pressed & bindings.ModMenu) != 0)
            {
                sink.ReleaseAll();
                menu.Open();
                previous = pad.buttons;
                return;
            }

            float x = PadMath.Curve(PadMath.Axis(pad.lx, deadzone.Value), pointerCurve.Value);
            float y = PadMath.Curve(PadMath.Axis(pad.ly, deadzone.Value), pointerCurve.Value);
            float scroll = PadMath.Axis(pad.ry, deadzone.Value);
            if (pressed != 0 || x != 0 || y != 0 || scroll != 0)
            {
                if (!active) hintsDirty = true;
                active = true;
                if (!welcomed.Value)
                {
                    welcomed.Value = true;
                    Config.Save();
                    toastUntil = now + 6f;
                }
            }
            if (Input.GetMouseButtonDown(0) && !sink.MouseHeld && pressed == 0) active = false;

            float speed = (pad.buttons & bindings.Precision) != 0 ? precisionFactor.Value :
                (pad.buttons & bindings.Boost) != 0 ? boostFactor.Value : 1f;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (x != 0 || y != 0)
                    OsCursor.MoveTo(window, (Vector2)Input.mousePosition + new Vector2(x, y) * cursorSpeed.Value * speed * Time.unscaledDeltaTime);
                int wanted = (pad.buttons & Buttons.DPadUp) != 0 ? 1 : (pad.buttons & Buttons.DPadDown) != 0 ? 2 :
                    (pad.buttons & Buttons.DPadLeft) != 0 ? 3 : (pad.buttons & Buttons.DPadRight) != 0 ? 4 : 0;
                if (wanted != 0 && (wanted != direction || now >= nextRepeat))
                {
                    Navigate(window, wanted == 3 ? -1 : wanted == 4 ? 1 : 0, wanted == 1 ? 1 : wanted == 2 ? -1 : 0);
                    nextRepeat = now + (wanted != direction ? 0.35f : 0.14f);
                }
                direction = wanted;
                if (scroll != 0 && now >= nextScroll)
                {
                    sink.Wheel(scroll > 0 ? 1 : -1);
                    nextScroll = now + 1f / (scrollSpeed.Value * Mathf.Abs(scroll) * (speed > 1f ? speed : 1f));
                    Trace("Scroll " + (scroll > 0 ? "up" : "down"));
                }
                if ((pressed & bindings.Click) != 0 && !OpenKeyboardAtPointer())
                {
                    if (sink.MouseDown()) feedback.Click(now);
                    Trace("Click down at " + Input.mousePosition);
                }
                if ((pad.buttons & bindings.Click) == 0 && sink.MouseHeld) { sink.MouseUp(); Trace("Click up"); }
            }
            else if ((pressed & bindings.Click) != 0) sink.Key(PadKey.Space, now);
            if ((pressed & bindings.Back) != 0) sink.Key(PadKey.Escape, now);
            if ((pressed & bindings.Skip) != 0) sink.Key(PadKey.Space, now);
            if ((pressed & bindings.PagePrevious) != 0) sink.Key(PadKey.Left, now);
            if ((pressed & bindings.PageNext) != 0) sink.Key(PadKey.Right, now);
            if ((pressed & bindings.Keyboard) != 0) OpenKeyboardAtPointer();
            previous = pad.buttons;

            if (active && now >= nextProbe) RefreshProbe(now);
        }

        private void Navigate(IntPtr window, int x, int y)
        {
            Vector2 destination;
            string name;
            if (!scanner.Move(Input.mousePosition, x, y, wrapNavigation.Value, out destination, out name)) return;
            OsCursor.MoveTo(window, destination);
            nextProbe = 0f;
            Trace("Focus " + name + " at " + destination);
        }

        private bool OpenKeyboardAtPointer()
        {
            Probe field = scanner.Inspect(Input.mousePosition);
            if (!field.HasField) return false;
            keyboard.Open(field, Trace);
            sink.ReleaseAll();
            return true;
        }

        private void RefreshProbe(float now)
        {
            nextProbe = now + ProbeInterval;
            probe = scanner.Inspect(Input.mousePosition);
            if (probe.context.OverTextField != hintContext.OverTextField || probe.context.OverScrollable != hintContext.OverScrollable ||
                probe.context.OverDraggable != hintContext.OverDraggable) hintsDirty = true;
            hintContext = probe.context;
        }

        private void OnGUI()
        {
            if (!Application.isFocused || steamOverlay) return;
            if (keyboard.IsOpen) { keyboard.Draw(bindings); return; }
            if (menu.IsOpen) { menu.Draw(bindings); return; }
            if (!active || !connected) return;
            float scale = hintScale.Value;
            if (focusRing.Value && probe.target != null && probe.box.Area < Screen.width * (float)Screen.height * 0.25f)
                overlay.DrawFocusRing(probe.box);
            if (showHints.Value)
            {
                if (hintsDirty) { hintText = HintText.Build(bindings, hintContext); hintsDirty = false; }
                overlay.DrawHints(hintText, scale);
            }
            if (Time.unscaledTime < toastUntil)
                overlay.DrawToast("Controller ready. " + ButtonNames.Describe(bindings.ToggleHints) + " shows or hides the control hints.", scale);
        }

        private void OnDisable()
        {
            sink.ReleaseAll();
            feedback.Stop();
            if (keyboard.IsOpen) keyboard.Close(false, null);
            if (menu.IsOpen) { menu.Close(); Config.Save(); }
        }

        private void OnDestroy()
        {
            sink.ReleaseAll();
            feedback.Stop();
            if (directSink != null) directSink.Uninstall();
            if (cursorPatch != null) cursorPatch.UnpatchSelf();
            if (overlayCallback != null) overlayCallback.Dispose();
        }
    }
}
