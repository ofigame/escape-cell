using UnityEngine;

namespace SquashBot.Audio
{
    /// <summary>
    /// Synthesizes the game's sound effects and music loop at startup, so no audio files are needed.
    /// Everything is soft, rounded "toy synth" sounds to match the pastel look.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 44100;

        private enum Wave { Sine, Triangle, Square, Noise }

        // ---------- Effects ----------

        public static AudioClip Hop() => Build("hop", 0.09f, (b) =>
            Tone(b, 0f, 0.09f, 440f, 760f, Wave.Triangle, 0.45f, 0.004f));

        public static AudioClip Bump() => Build("bump", 0.08f, (b) =>
            Tone(b, 0f, 0.08f, 190f, 120f, Wave.Triangle, 0.5f, 0.003f));

        public static AudioClip Coin() => Build("coin", 0.3f, (b) =>
        {
            Tone(b, 0f, 0.07f, 988f, 988f, Wave.Sine, 0.35f, 0.002f, harmonic: 0.3f);
            Tone(b, 0.06f, 0.24f, 1319f, 1319f, Wave.Sine, 0.4f, 0.002f, harmonic: 0.3f);
        });

        public static AudioClip Warning() => Build("warning", 0.12f, (b) =>
            Tone(b, 0f, 0.1f, 740f, 700f, Wave.Triangle, 0.18f, 0.005f));

        public static AudioClip Impact() => Build("impact", 0.35f, (b) =>
        {
            Tone(b, 0f, 0.3f, 110f, 42f, Wave.Sine, 0.9f, 0.002f);
            Tone(b, 0f, 0.12f, 0f, 0f, Wave.Noise, 0.35f, 0.001f, lowpass: 0.12f);
        });

        public static AudioClip Squash() => Build("squash", 0.6f, (b) =>
        {
            Tone(b, 0f, 0.5f, 420f, 70f, Wave.Square, 0.25f, 0.003f, lowpass: 0.25f);
            Tone(b, 0f, 0.25f, 0f, 0f, Wave.Noise, 0.35f, 0.001f, lowpass: 0.08f);
        });

        public static AudioClip Fall() => Build("fall", 0.7f, (b) =>
            Tone(b, 0f, 0.7f, 600f, 90f, Wave.Triangle, 0.4f, 0.005f));

        public static AudioClip Shield() => Build("shield", 0.55f, (b) =>
        {
            float[] notes = { 1047f, 1319f, 1568f, 2093f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.06f, 0.3f, notes[i], notes[i], Wave.Sine, 0.28f, 0.002f, harmonic: 0.25f);
        });

        public static AudioClip Blocked() => Build("blocked", 0.4f, (b) =>
        {
            Tone(b, 0f, 0.35f, 1180f, 1150f, Wave.Sine, 0.3f, 0.001f);
            Tone(b, 0f, 0.3f, 1770f, 1720f, Wave.Sine, 0.2f, 0.001f);
            Tone(b, 0f, 0.08f, 0f, 0f, Wave.Noise, 0.25f, 0.001f, lowpass: 0.3f);
        });

        public static AudioClip CloseCall() => Build("closecall", 0.35f, (b) =>
            Tone(b, 0f, 0.35f, 0f, 0f, Wave.Noise, 0.25f, 0.12f, lowpass: 0.05f, lowpassEnd: 0.4f));

        public static AudioClip Win() => Build("win", 0.9f, (b) =>
        {
            float[] notes = { 523f, 659f, 784f, 1047f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.1f, i == notes.Length - 1 ? 0.5f : 0.16f, notes[i], notes[i], Wave.Triangle, 0.35f, 0.004f, harmonic: 0.2f);
        });

        public static AudioClip Lose() => Build("lose", 0.8f, (b) =>
        {
            float[] notes = { 392f, 330f, 262f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.16f, i == notes.Length - 1 ? 0.45f : 0.18f, notes[i], notes[i] * 0.98f, Wave.Triangle, 0.35f, 0.004f);
        });

        public static AudioClip Click() => Build("click", 0.04f, (b) =>
            Tone(b, 0f, 0.035f, 1600f, 1400f, Wave.Sine, 0.25f, 0.001f));

        // ---------- The duel's weapons (each family its own swing and its own blow) ----------

        /// <summary>A sword's swing: a thin, bright hiss rising ("shiing"), a faint ring of the blade.</summary>
        public static AudioClip SwishSword() => Build("swish_sword", 0.14f, (b) =>
        {
            Tone(b, 0f, 0.13f, 0f, 0f, Wave.Noise, 0.55f, 0.05f, lowpass: 0.35f, lowpassEnd: 0.85f);
            Tone(b, 0.02f, 0.11f, 2800f, 3400f, Wave.Sine, 0.07f, 0.04f);
        });

        /// <summary>A spear's thrust: a short, middle "voosh".</summary>
        public static AudioClip SwishSpear() => Build("swish_spear", 0.12f, (b) =>
            Tone(b, 0f, 0.11f, 0f, 0f, Wave.Noise, 0.6f, 0.035f, lowpass: 0.18f, lowpassEnd: 0.42f));

        /// <summary>An axe's swing: a deep swell of air ("vuuum") with the weight in it.</summary>
        public static AudioClip SwishAxe() => Build("swish_axe", 0.26f, (b) =>
        {
            Tone(b, 0f, 0.24f, 0f, 0f, Wave.Noise, 0.85f, 0.1f, lowpass: 0.05f, lowpassEnd: 0.13f);
            Tone(b, 0f, 0.22f, 140f, 90f, Wave.Triangle, 0.16f, 0.1f);
        });

        /// <summary>A heavy weapon's swing: a very low rush, a mechanical breath under it ("hhh").</summary>
        public static AudioClip SwishHeavy() => Build("swish_heavy", 0.36f, (b) =>
        {
            Tone(b, 0f, 0.34f, 0f, 0f, Wave.Noise, 1f, 0.16f, lowpass: 0.022f, lowpassEnd: 0.06f);
            Tone(b, 0f, 0.32f, 72f, 50f, Wave.Sine, 0.35f, 0.14f);
            Tone(b, 0f, 0.3f, 55f, 47f, Wave.Square, 0.12f, 0.12f, lowpass: 0.05f);
        });

        /// <summary>A sword's blow landing: a sharp metal "tink" (bright, out-of-tune partials) on a click.</summary>
        public static AudioClip HitSword() => Build("hit_sword", 0.22f, (b) =>
        {
            Tone(b, 0f, 0.2f, 2450f, 2440f, Wave.Sine, 0.32f, 0.001f);
            Tone(b, 0f, 0.16f, 3720f, 3700f, Wave.Sine, 0.22f, 0.001f);
            Tone(b, 0f, 0.1f, 5150f, 5100f, Wave.Sine, 0.12f, 0.001f);
            Tone(b, 0f, 0.02f, 0f, 0f, Wave.Noise, 0.3f, 0.0005f, lowpass: 0.9f);
        });

        /// <summary>A spear's blow landing: a piercing "tshak", a short ring.</summary>
        public static AudioClip HitSpear() => Build("hit_spear", 0.17f, (b) =>
        {
            Tone(b, 0f, 0.03f, 0f, 0f, Wave.Noise, 0.5f, 0.0005f, lowpass: 0.5f);
            Tone(b, 0f, 0.12f, 1250f, 1150f, Wave.Sine, 0.3f, 0.001f);
            Tone(b, 0f, 0.07f, 300f, 200f, Wave.Triangle, 0.25f, 0.001f);
        });

        /// <summary>An axe's blow landing: a solid "tak" (wood and metal), a low thud.</summary>
        public static AudioClip HitAxe() => Build("hit_axe", 0.26f, (b) =>
        {
            Tone(b, 0f, 0.2f, 190f, 115f, Wave.Sine, 0.75f, 0.001f);
            Tone(b, 0f, 0.06f, 0f, 0f, Wave.Noise, 0.45f, 0.0008f, lowpass: 0.15f);
            Tone(b, 0f, 0.1f, 900f, 860f, Wave.Sine, 0.12f, 0.001f);
        });

        /// <summary>A heavy weapon's blow landing: a deep "GOOM", the floor shaking with it.</summary>
        public static AudioClip HitHeavy() => Build("hit_heavy", 0.46f, (b) =>
        {
            Tone(b, 0f, 0.42f, 95f, 38f, Wave.Sine, 1f, 0.001f);
            Tone(b, 0f, 0.15f, 0f, 0f, Wave.Noise, 0.6f, 0.001f, lowpass: 0.06f);
            Tone(b, 0f, 0.3f, 60f, 34f, Wave.Triangle, 0.4f, 0.002f);
        });

        /// <summary>Two weapons meeting: a big ringing "klang" (bell-like partials), a bright spark of noise.</summary>
        public static AudioClip Clang() => Build("clang", 0.72f, (b) =>
        {
            float[] partials = { 523f, 1340f, 2210f, 3105f, 4380f };
            float[] volumes = { 0.4f, 0.32f, 0.24f, 0.16f, 0.1f };
            for (int i = 0; i < partials.Length; i++) Tone(b, 0f, 0.7f - i * 0.08f, partials[i], partials[i] * 0.995f, Wave.Sine, volumes[i], 0.001f);
            Tone(b, 0f, 0.03f, 0f, 0f, Wave.Noise, 0.35f, 0.0005f, lowpass: 0.75f);
        });

        // ---------- Building blocks ----------

        private delegate void Writer(float[] buffer);

        private static AudioClip Build(string name, float seconds, Writer write)
        {
            var b = new float[Mathf.CeilToInt(seconds * Rate)];
            write(b);
            Normalize(b, 0.9f, onlyIfLouder: true);
            var clip = AudioClip.Create(name, b.Length, 1, Rate, false);
            clip.SetData(b, 0);
            return clip;
        }

        private static void Tone(float[] b, float start, float duration, float f0, float f1, Wave wave, float volume,
            float attack, float harmonic = 0f, float lowpass = 1f, float lowpassEnd = -1f)
        {
            int s0 = Mathf.FloorToInt(start * Rate);
            int count = Mathf.FloorToInt(duration * Rate);
            float phase = 0f;
            float lp = 0f;
            uint seed = 0x9E3779B9u ^ (uint)s0;
            if (lowpassEnd < 0f) lowpassEnd = lowpass;

            for (int i = 0; i < count && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float u = i / (float)count;
                float f = Mathf.Lerp(f0, f1, u);
                phase += f / Rate;

                float v;
                switch (wave)
                {
                    case Wave.Triangle: v = 1f - 4f * Mathf.Abs(Mathf.Repeat(phase + 0.25f, 1f) - 0.5f); break;
                    case Wave.Square: v = Mathf.Repeat(phase, 1f) < 0.5f ? 0.6f : -0.6f; break;
                    case Wave.Noise:
                        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                        v = (seed / (float)uint.MaxValue) * 2f - 1f;
                        break;
                    default: v = Mathf.Sin(phase * 2f * Mathf.PI); break;
                }
                if (harmonic > 0f) v += Mathf.Sin(phase * 4f * Mathf.PI) * harmonic;

                // One-pole low-pass for softer, rounder tones.
                lp += (v - lp) * Mathf.Lerp(lowpass, lowpassEnd, u);
                v = lp;

                // Envelope: quick attack, exponential-ish decay.
                float env = t < attack ? t / attack : Mathf.Pow(1f - u, 2.2f);
                b[s0 + i] += v * env * volume;
            }
        }

        private static void Normalize(float[] b, float peak, bool onlyIfLouder = false)
        {
            float max = 0f;
            foreach (float v in b) max = Mathf.Max(max, Mathf.Abs(v));
            if (max < 1e-5f || (onlyIfLouder && max <= peak)) return;
            float k = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }
    }
}
