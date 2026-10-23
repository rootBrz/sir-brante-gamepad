using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Brante.Gamepad
{
    internal struct PadState
    {
        internal Buttons buttons;
        internal short lx, ly, rx, ry;
    }

    // Polls XInput. Tries xinput1_3 first (known to work under Proton), then 1_4 and 9_1_0.
    internal sealed class XInputReader
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RawPad
        {
            public uint packet;
            public ushort buttons;
            public byte leftTrigger, rightTrigger;
            public short lx, ly, rx, ry;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Vibration { public ushort left, right; }

        [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")] private static extern uint GetState13(uint index, out RawPad state);
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] private static extern uint GetState14(uint index, out RawPad state);
        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] private static extern uint GetState910(uint index, out RawPad state);
        [DllImport("xinput1_3.dll", EntryPoint = "XInputSetState")] private static extern uint SetState13(uint index, ref Vibration vibration);
        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")] private static extern uint SetState14(uint index, ref Vibration vibration);
        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")] private static extern uint SetState910(uint index, ref Vibration vibration);

        private const uint NotConnected = 1167;
        private int api;
        private int controller = -1;

        internal bool Available { get { return api >= 0 && api <= 2; } }
        internal int Index { get { return controller; } }
        internal int Count { get; private set; }

        private uint GetState(uint index, out RawPad pad)
        {
            while (Available)
            {
                try
                {
                    switch (api)
                    {
                        case 0: return GetState13(index, out pad);
                        case 1: return GetState14(index, out pad);
                        default: return GetState910(index, out pad);
                    }
                }
                catch (DllNotFoundException) { api++; }
                catch (EntryPointNotFoundException) { api++; }
            }
            pad = default(RawPad);
            return NotConnected;
        }

        internal void Vibrate(ushort left, ushort right)
        {
            if (!Available || controller < 0) return;
            Vibration vibration = new Vibration { left = left, right = right };
            try
            {
                switch (api)
                {
                    case 0: SetState13((uint)controller, ref vibration); break;
                    case 1: SetState14((uint)controller, ref vibration); break;
                    default: SetState910((uint)controller, ref vibration); break;
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        // Prefers a controller that is being used; otherwise keeps the current one, then the first connected.
        internal bool Read(float deadzone, out PadState state)
        {
            state = default(PadState);
            Count = 0;
            int fallback = -1;
            PadState fallbackState = default(PadState);
            for (uint i = 0; i < 4; i++)
            {
                RawPad raw;
                if (GetState(i, out raw) != 0) continue;
                Count++;
                PadState candidate = Convert(raw);
                if (fallback < 0 || i == controller) { fallback = (int)i; fallbackState = candidate; }
                if (candidate.buttons != 0 || Math.Abs((int)candidate.lx) > deadzone * 32768 ||
                    Math.Abs((int)candidate.ly) > deadzone * 32768 || Math.Abs((int)candidate.ry) > deadzone * 32768)
                {
                    state = candidate;
                    controller = (int)i;
                    return true;
                }
            }
            controller = fallback;
            state = fallbackState;
            return fallback >= 0;
        }

        private static PadState Convert(RawPad raw)
        {
            Buttons buttons = (Buttons)raw.buttons;
            if (raw.leftTrigger > PadMath.TriggerThreshold) buttons |= Buttons.LeftTrigger;
            if (raw.rightTrigger > PadMath.TriggerThreshold) buttons |= Buttons.RightTrigger;
            return new PadState { buttons = buttons, lx = raw.lx, ly = raw.ly, rx = raw.rx, ry = raw.ry };
        }
    }

    // Win32 window and cursor calls shared by the pointer code.
    internal static class OsCursor
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Point { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WindowRect { public int left, top, right, bottom; }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out WindowRect rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);

        internal static bool GameIsForeground(out IntPtr window)
        {
            window = GetForegroundWindow();
            uint process;
            GetWindowThreadProcessId(window, out process);
            return process == GetCurrentProcessId();
        }

        // position is in Unity screen space (origin bottom-left).
        internal static void MoveTo(IntPtr window, Vector2 position)
        {
            WindowRect rect;
            if (!GetClientRect(window, out rect) || Screen.width <= 0 || Screen.height <= 0) return;
            Point point = new Point
            {
                x = Mathf.RoundToInt(Mathf.Clamp(position.x / Screen.width, 0, 1) * (rect.right - 1)),
                y = Mathf.RoundToInt(Mathf.Clamp(1f - position.y / Screen.height, 0, 1) * (rect.bottom - 1))
            };
            if (ClientToScreen(window, ref point)) SetCursorPos(point.x, point.y);
        }
    }
}
