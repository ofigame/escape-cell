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

        // ---------- Music ----------

        /// <summary>A calm 16-second loop: soft pad chords (Am F C G) with a gentle arpeggio on top.</summary>
        public static AudioClip MusicLoop()
        {
            const float bpm = 120f;
            float beat = 60f / bpm;
            float bar = beat * 4f;
            float length = bar * 8f;
            var b = new float[Mathf.CeilToInt(length * Rate)];

            float[][] chords =
            {
                new[] { 220f, 261.6f, 329.6f },  // Am
                new[] { 174.6f, 220f, 261.6f },  // F
                new[] { 261.6f, 329.6f, 392f },  // C
                new[] { 196f, 246.9f, 293.7f },  // G
            };

            for (int barIndex = 0; barIndex < 8; barIndex++)
            {
                var chord = chords[barIndex % 4];
                float start = barIndex * bar;

                // Pad: two slightly detuned voices per note, slow attack, overlapping release.
                foreach (float f in chord)
                {
                    Pad(b, start, bar + 0.4f, f, 0.05f);
                    Pad(b, start, bar + 0.4f, f * 1.004f, 0.04f);
                }

                // Arpeggio: eighth notes cycling up through the chord an octave higher.
                for (int step = 0; step < 8; step++)
                {
                    float f = chord[step % 3] * 2f * (step >= 6 ? 2f : 1f);
                    Tone(b, start + step * beat * 0.5f, beat * 0.45f, f, f, Wave.Sine, 0.07f, 0.004f, harmonic: 0.15f);
                }

                // Soft bass on the root.
                Tone(b, start, beat * 1.8f, chord[0] * 0.5f, chord[0] * 0.5f, Wave.Sine, 0.16f, 0.01f);
                Tone(b, start + beat * 2f, beat * 1.8f, chord[0] * 0.5f, chord[0] * 0.5f, Wave.Sine, 0.12f, 0.01f);
            }

            Normalize(b, 0.55f);
            var clip = AudioClip.Create("music", b.Length, 1, Rate, false);
            clip.SetData(b, 0);
            return clip;
        }

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

        private static void Pad(float[] b, float start, float duration, float f, float volume)
        {
            int s0 = Mathf.FloorToInt(start * Rate);
            int count = Mathf.FloorToInt(duration * Rate);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < count; i++)
            {
                int idx = (s0 + i) % b.Length; // tails wrap around for a seamless loop
                float u = i / (float)count;
                phase += f / Rate;
                float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
                lp += (saw - lp) * 0.035f;
                float env = Mathf.Clamp01(u / 0.25f) * Mathf.Clamp01((1f - u) / 0.3f);
                b[idx] += lp * env * volume;
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
