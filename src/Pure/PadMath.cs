using System;

namespace Brante.Gamepad
{
    internal static class PadMath
    {
        internal const byte TriggerThreshold = 30;

        internal static float Axis(short value, float deadzone)
        {
            float axis = value < 0 ? value / 32768f : value / 32767f;
            float magnitude = Math.Abs(axis);
            return magnitude <= deadzone ? 0f : Math.Sign(axis) * (magnitude - deadzone) / (1f - deadzone);
        }

        // Response curve for pointer speed: exponent 1 is linear, higher values give finer control near the center.
        internal static float Curve(float axis, float exponent)
        {
            if (axis == 0f || exponent == 1f) return axis;
            return Math.Sign(axis) * (float)Math.Pow(Math.Abs(axis), exponent);
        }
    }
}
