namespace Brante.Gamepad
{
    // Tracks whether the player is using the controller or a keyboard/mouse. The plugin itself moves the cursor,
    // clicks and presses keys, so input seen shortly after one of its own actions is not counted as the player's.
    internal sealed class InputMode
    {
        internal const float Grace = 0.3f;
        private float ownUntil;

        internal bool KeyboardMouse { get; private set; }

        // Call whenever the plugin moves the pointer, clicks or presses a key.
        internal void Own(float now) { ownUntil = now + Grace; }

        // Returns true when this observation switches to keyboard/mouse.
        internal bool Observe(float now, bool mouseMoved, bool mouseClicked, bool keyPressed)
        {
            if (KeyboardMouse || now < ownUntil) return false;
            if (!mouseMoved && !mouseClicked && !keyPressed) return false;
            KeyboardMouse = true;
            return true;
        }

        // Controller input switches back; returns true if the mode changed.
        internal bool ControllerUsed()
        {
            if (!KeyboardMouse) return false;
            KeyboardMouse = false;
            return true;
        }
    }
}
