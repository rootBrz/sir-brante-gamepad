using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Brante.Gamepad
{
    // Delivers clicks, wheel notches and key presses to the game.
    internal interface IInputSink
    {
        string Name { get; }
        bool MouseHeld { get; }
        void Tick(float now);
        bool MouseDown();
        void MouseUp();
        void Wheel(int notches);
        void Key(PadKey key, float now);
        void ReleaseAll();
    }

    // Synthesizes operating-system input. Tested under Proton, but it is global: it follows the real
    // cursor and keyboard focus.
    internal sealed class Win32Sink : IInputSink
    {
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

        private bool mouseHeld;
        private byte heldKey;
        private float keyReleaseAt;

        public string Name { get { return "Win32"; } }
        public bool MouseHeld { get { return mouseHeld; } }

        public void Tick(float now)
        {
            if (heldKey != 0 && now >= keyReleaseAt) ReleaseKey();
        }

        public bool MouseDown()
        {
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouseHeld = true;
            return true;
        }

        public void MouseUp()
        {
            if (!mouseHeld) return;
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            mouseHeld = false;
        }

        public void Wheel(int notches)
        {
            mouse_event(0x0800, 0, 0, unchecked((uint)(notches * 120)), UIntPtr.Zero);
        }

        public void Key(PadKey key, float now)
        {
            ReleaseKey();
            byte code = key == PadKey.Escape ? (byte)0x1b : key == PadKey.Space ? (byte)0x20 : key == PadKey.Left ? (byte)0x25 : (byte)0x27;
            keybd_event(code, 0, 0, UIntPtr.Zero);
            heldKey = code;
            keyReleaseAt = now + 0.08f;
        }

        public void ReleaseAll()
        {
            MouseUp();
            ReleaseKey();
        }

        private void ReleaseKey()
        {
            if (heldKey == 0) return;
            keybd_event(heldKey, 0, 2, UIntPtr.Zero);
            heldKey = 0;
        }
    }

    // Stays inside the process. Key presses are answered from Input.GetKey* through Harmony postfixes; clicks,
    // drags and wheel notches are dispatched through the EventSystem the way StandaloneInputModule would.
    // The pointer position is still the real cursor so hover, tooltips and the game's cursor keep working.
    internal sealed class DirectSink : IInputSink
    {
        private const float KeyHold = 0.08f;
        private static VirtualKeys keys = new VirtualKeys();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private PointerEventData press;
        private bool dragging;
        private Harmony harmony;

        public string Name { get { return "Direct"; } }
        public bool MouseHeld { get { return press != null; } }

        // Patches the Input class and verifies the patch really answers the game's calls.
        internal bool TryInstall(out string error)
        {
            error = null;
            try
            {
                harmony = new Harmony("local.brante.gamepad.input");
                Patch("GetKeyDown", new[] { typeof(KeyCode) }, nameof(KeyDown));
                Patch("GetKey", new[] { typeof(KeyCode) }, nameof(KeyHeld));
                Patch("GetKeyUp", new[] { typeof(KeyCode) }, nameof(KeyUp));
                PatchGetter("anyKey", nameof(AnyKey));
                PatchGetter("anyKeyDown", nameof(AnyKeyDown));
                keys.Press(PadKey.Escape, Time.frameCount, Time.unscaledTime, 1f);
                bool answered = Input.GetKeyDown(KeyCode.Escape) && Input.GetKey(KeyCode.Escape);
                keys = new VirtualKeys();
                if (!answered) throw new InvalidOperationException("Input.GetKeyDown ignored the patch");
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                keys = new VirtualKeys();
                if (harmony != null) harmony.UnpatchSelf();
                harmony = null;
                return false;
            }
        }

        private void Patch(string method, Type[] arguments, string postfix)
        {
            System.Reflection.MethodBase original = AccessTools.Method(typeof(Input), method, arguments);
            if (original == null) throw new MissingMethodException("Input." + method);
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(DirectSink), postfix));
        }

        private void PatchGetter(string property, string postfix)
        {
            System.Reflection.MethodBase original = AccessTools.PropertyGetter(typeof(Input), property);
            if (original == null) throw new MissingMethodException("Input." + property);
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(DirectSink), postfix));
        }

        internal void Uninstall()
        {
            if (harmony != null) harmony.UnpatchSelf();
            harmony = null;
        }

        private static bool Map(KeyCode code, out PadKey key)
        {
            switch (code)
            {
                case KeyCode.Escape: key = PadKey.Escape; return true;
                case KeyCode.Space: key = PadKey.Space; return true;
                case KeyCode.LeftArrow: key = PadKey.Left; return true;
                case KeyCode.RightArrow: key = PadKey.Right; return true;
                default: key = PadKey.Escape; return false;
            }
        }

        private static void KeyDown(KeyCode key, ref bool __result)
        {
            PadKey mapped;
            if (!__result && Map(key, out mapped)) __result = keys.Down(mapped, Time.frameCount);
        }
        private static void KeyHeld(KeyCode key, ref bool __result)
        {
            PadKey mapped;
            if (!__result && Map(key, out mapped)) __result = keys.Held(mapped, Time.frameCount);
        }
        private static void KeyUp(KeyCode key, ref bool __result)
        {
            PadKey mapped;
            if (!__result && Map(key, out mapped)) __result = keys.Up(mapped, Time.frameCount);
        }
        private static void AnyKey(ref bool __result) { if (!__result) __result = keys.AnyHeld(Time.frameCount); }
        private static void AnyKeyDown(ref bool __result) { if (!__result) __result = keys.AnyDown(Time.frameCount); }

        public void Key(PadKey key, float now)
        {
            keys.Press(key, Time.frameCount + 1, now, KeyHold);
        }

        public void Tick(float now)
        {
            keys.Tick(Time.frameCount, now);
            if (press == null) return;
            EventSystem system = EventSystem.current;
            if (system == null || press.pointerDrag == null) return;
            Vector2 position = Input.mousePosition;
            press.delta = position - press.position;
            press.position = position;
            if (!dragging)
            {
                float threshold = system.pixelDragThreshold;
                if ((position - press.pressPosition).sqrMagnitude < threshold * threshold) return;
                ExecuteEvents.Execute(press.pointerDrag, press, ExecuteEvents.beginDragHandler);
                dragging = true;
                press.dragging = true;
            }
            if (press.delta == Vector2.zero) return;
            if (press.pointerPress != press.pointerDrag && press.pointerPress != null)
            {
                ExecuteEvents.Execute(press.pointerPress, press, ExecuteEvents.pointerUpHandler);
                press.eligibleForClick = false;
                press.pointerPress = null;
                press.rawPointerPress = null;
            }
            if (Raycast(system, position)) press.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(press.pointerDrag, press, ExecuteEvents.dragHandler);
        }

        private bool Raycast(EventSystem system, Vector2 position)
        {
            PointerEventData probe = new PointerEventData(system) { position = position };
            hits.Clear();
            system.RaycastAll(probe, hits);
            return hits.Count > 0;
        }

        public bool MouseDown()
        {
            EventSystem system = EventSystem.current;
            if (system == null || press != null) return false;
            Vector2 position = Input.mousePosition;
            if (!Raycast(system, position)) return false;
            RaycastResult hit = hits[0];
            GameObject over = hit.gameObject;
            PointerEventData data = new PointerEventData(system)
            {
                button = PointerEventData.InputButton.Left,
                position = position,
                pressPosition = position,
                delta = Vector2.zero,
                useDragThreshold = true,
                eligibleForClick = true,
                clickCount = 1,
                clickTime = Time.unscaledTime,
                pointerCurrentRaycast = hit,
                pointerPressRaycast = hit
            };
            if (ExecuteEvents.GetEventHandler<ISelectHandler>(over) != system.currentSelectedGameObject)
                system.SetSelectedGameObject(null, data);
            GameObject pressed = ExecuteEvents.ExecuteHierarchy(over, data, ExecuteEvents.pointerDownHandler);
            if (pressed == null) pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
            data.pointerPress = pressed;
            data.rawPointerPress = over;
            data.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(over);
            if (data.pointerDrag != null) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.initializePotentialDrag);
            press = data;
            dragging = false;
            return true;
        }

        public void MouseUp()
        {
            if (press == null) return;
            PointerEventData data = press;
            press = null;
            EventSystem system = EventSystem.current;
            GameObject over = null;
            if (system != null)
            {
                data.position = Input.mousePosition;
                if (Raycast(system, data.position))
                {
                    over = hits[0].gameObject;
                    data.pointerCurrentRaycast = hits[0];
                }
            }
            if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            GameObject clickable = over != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(over) : null;
            if (data.pointerPress != null && data.pointerPress == clickable && data.eligibleForClick)
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
            else if (data.pointerDrag != null && dragging && over != null)
                ExecuteEvents.ExecuteHierarchy(over, data, ExecuteEvents.dropHandler);
            data.eligibleForClick = false;
            if (data.pointerDrag != null && dragging) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.endDragHandler);
            dragging = false;
        }

        public void Wheel(int notches)
        {
            EventSystem system = EventSystem.current;
            if (system == null || !Raycast(system, Input.mousePosition)) return;
            PointerEventData data = new PointerEventData(system)
            {
                position = Input.mousePosition,
                scrollDelta = new Vector2(0f, notches),
                pointerCurrentRaycast = hits[0]
            };
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.scrollHandler);
        }

        public void ReleaseAll()
        {
            MouseUp();
            keys.Clear(Time.frameCount);
        }
    }
}
