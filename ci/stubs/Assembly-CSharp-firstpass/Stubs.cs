using System;

namespace Steamworks
{
    public struct GameOverlayActivated_t
    {
        public byte m_bActive;
    }

    public abstract class Callback
    {
    }

    public sealed class Callback<T> : Callback, IDisposable where T : struct
    {
        public delegate void DispatchDelegate(T param);

        public static Callback<T> Create(DispatchDelegate func) { return null; }
        public void Dispose() { }
    }
}
