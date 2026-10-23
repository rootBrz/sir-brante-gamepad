namespace Brante.Gamepad
{
    internal enum PadKey { Escape, Space, Left, Right }

    // Key state the game reads through Input.GetKeyDown/GetKey/GetKeyUp. Script execution order inside a frame
    // is not controlled, so every change becomes visible from the NEXT frame: a press made while frame F runs is
    // reported Down in F+1 for all scripts alike. Frames and times are passed in so the logic is testable.
    internal sealed class VirtualKeys
    {
        private const int Count = 4;
        private readonly int[] downFrame = new int[Count];
        private readonly int[] upFrame = new int[Count];
        private readonly float[] releaseAt = new float[Count];
        private readonly bool[] held = new bool[Count];

        internal VirtualKeys()
        {
            for (int i = 0; i < Count; i++) { downFrame[i] = -1; upFrame[i] = -1; }
        }

        // visibleFrame is the first frame in which the press is reported.
        internal void Press(PadKey key, int visibleFrame, float now, float hold)
        {
            int i = (int)key;
            if (held[i]) upFrame[i] = visibleFrame; // retrigger: the previous press ends where the new one starts
            downFrame[i] = visibleFrame;
            releaseAt[i] = now + hold;
            held[i] = true;
        }

        internal void Tick(int frame, float now)
        {
            for (int i = 0; i < Count; i++)
            {
                if (!held[i] || now < releaseAt[i] || frame < downFrame[i]) continue;
                held[i] = false;
                upFrame[i] = frame + 1;
            }
        }

        internal void Clear(int frame)
        {
            for (int i = 0; i < Count; i++)
            {
                if (held[i]) upFrame[i] = frame + 1;
                held[i] = false;
                downFrame[i] = -1;
            }
        }

        internal bool Down(PadKey key, int frame) { return downFrame[(int)key] == frame; }
        internal bool Held(PadKey key, int frame) { return held[(int)key] && frame >= downFrame[(int)key]; }
        internal bool Up(PadKey key, int frame) { return upFrame[(int)key] == frame; }

        internal bool AnyHeld(int frame)
        {
            for (int i = 0; i < Count; i++) if (held[i] && frame >= downFrame[i]) return true;
            return false;
        }

        internal bool AnyDown(int frame)
        {
            for (int i = 0; i < Count; i++) if (downFrame[i] == frame) return true;
            return false;
        }
    }
}
