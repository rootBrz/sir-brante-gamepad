using UnityEngine;

namespace Brante.Gamepad
{
    // Optional click feedback: a short rumble and/or a soft tick.
    internal sealed class Feedback
    {
        private readonly XInputReader reader;
        private readonly GameObject host;
        private AudioSource source;
        private AudioClip clip;
        private float stopAt;

        internal bool Rumble, Sound;
        internal float Volume = 0.25f;

        internal Feedback(XInputReader reader, GameObject host)
        {
            this.reader = reader;
            this.host = host;
        }

        internal void Click(float now)
        {
            if (Rumble)
            {
                reader.Vibrate(0, 24000);
                stopAt = now + 0.06f;
            }
            if (Sound)
            {
                if (source == null) Prepare();
                if (source != null) source.PlayOneShot(clip, Volume);
            }
        }

        internal void Tick(float now)
        {
            if (stopAt > 0f && now >= stopAt) Stop();
        }

        internal void Stop()
        {
            if (stopAt <= 0f) return;
            reader.Vibrate(0, 0);
            stopAt = 0f;
        }

        private void Prepare()
        {
            const int rate = 22050, length = 880;
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
                samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * i / rate) * Mathf.Exp(-6f * i / length);
            clip = AudioClip.Create("BranteGamepadClick", length, 1, rate, false);
            clip.SetData(samples, 0);
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
    }
}
