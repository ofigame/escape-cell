using System;

namespace SquashBot.Audio
{
    /// <summary>
    /// Writes the game's music from code, so every note is our own (no samples, no licences). A theme is eight bars in
    /// its own key, scale, tempo and chord loop, rendered as three layers of exactly the same length that play in sync:
    /// <list type="number">
    /// <item>calm: a soft pad, a sub bass and a gentle arpeggio (always on);</item>
    /// <item>groove: kick, snare, hi-hats and a moving bass line (fades in as the level gets tense);</item>
    /// <item>intense: a lead melody, fast hats, crashes and tom fills (danger, a stomping monster, the last seconds).</item>
    /// </list>
    /// Everything is plain math on float arrays, so it runs on a worker thread; notes that ring past the end wrap
    /// around to the start, which makes the loop seamless.
    /// </summary>
    public static class MusicSynth
    {
        public const int Rate = 22050;
        public const int Layers = 3;
        private const int Bars = 8;

        private enum Wave { Sine, Triangle, Saw, Square }

        private sealed class Style
        {
            public int root;          // MIDI note of the key
            public int[] scale;       // semitones of the seven scale steps
            public int[] chords;      // scale degree of each bar's chord (four, played twice)
            public float bpm;
            public bool calm;         // half-time drums, softer arpeggio
            public Wave lead;
            public int seed;
        }

        private static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        private static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };
        private static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };
        private static readonly int[] Harmonic = { 0, 2, 3, 5, 7, 8, 11 };

        private static Style StyleOf(MusicTheme theme)
        {
            switch (theme)
            {
                case MusicTheme.Menu: return new Style { root = 57, scale = Minor, chords = new[] { 0, 5, 2, 6 }, bpm = 92f, calm = true, lead = Wave.Triangle, seed = 11 };
                case MusicTheme.World0: return new Style { root = 60, scale = Major, chords = new[] { 0, 4, 5, 3 }, bpm = 112f, lead = Wave.Square, seed = 21 };
                case MusicTheme.World1: return new Style { root = 62, scale = Dorian, chords = new[] { 0, 3, 0, 6 }, bpm = 118f, lead = Wave.Triangle, seed = 32 };
                case MusicTheme.World2: return new Style { root = 64, scale = Minor, chords = new[] { 0, 5, 6, 0 }, bpm = 124f, lead = Wave.Saw, seed = 43 };
                case MusicTheme.World3: return new Style { root = 65, scale = Lydian, chords = new[] { 0, 1, 4, 0 }, bpm = 116f, lead = Wave.Square, seed = 54 };
                case MusicTheme.World4: return new Style { root = 55, scale = Minor, chords = new[] { 0, 3, 6, 2 }, bpm = 128f, lead = Wave.Saw, seed = 65 };
                case MusicTheme.World5: return new Style { root = 59, scale = Phrygian, chords = new[] { 0, 1, 0, 6 }, bpm = 132f, lead = Wave.Square, seed = 76 };
                case MusicTheme.Tunnel: return new Style { root = 62, scale = Minor, chords = new[] { 0, 5, 2, 6 }, bpm = 140f, lead = Wave.Saw, seed = 87 };
                case MusicTheme.Monster: return new Style { root = 57, scale = Harmonic, chords = new[] { 0, 5, 3, 4 }, bpm = 126f, lead = Wave.Square, seed = 98 };
                default: return new Style { root = 52, scale = Phrygian, chords = new[] { 0, 1, 5, 1 }, bpm = 138f, lead = Wave.Saw, seed = 109 };
            }
        }

        /// <summary>The three layers of a theme (calm, groove, intense), each the same number of samples. Thread-safe.</summary>
        public static float[][] Render(MusicTheme theme)
        {
            var st = StyleOf(theme);
            int beat = (int)Math.Round(60.0 / st.bpm * Rate);
            int bar = beat * 4;
            int total = bar * Bars;
            var layers = new float[Layers][];
            for (int i = 0; i < Layers; i++) layers[i] = new float[total];
            uint rng = (uint)(st.seed * 2654435761u) | 1u;

            int Note(int degree, int octave)
            {
                int d = ((degree % 7) + 7) % 7;
                int o = (int)Math.Floor(degree / 7.0);
                return st.root + st.scale[d] + 12 * (o + octave);
            }

            // ---- Calm: pad, sub bass, arpeggio ----
            var calm = layers[0];
            for (int b = 0; b < Bars; b++)
            {
                int deg = st.chords[b % 4];
                int start = b * bar;
                for (int k = 0; k < 3; k++)
                {
                    double f = Freq(Note(deg + k * 2, -1));
                    Pad(calm, start, bar + beat / 2, f, 0.055);
                    Pad(calm, start, bar + beat / 2, f * 1.005, 0.045);
                }
                double sub = Freq(Note(deg, -2));
                for (int h = 0; h < 2; h++) Tone(calm, start + h * beat * 2, beat * 2, sub, Wave.Sine, 0.2, 0.01, 0.2, 1.0);
                // Arpeggio up and down the chord; eighths, or gentle quarters for calm themes.
                int steps = st.calm ? 4 : 8;
                int len = bar / steps;
                int[] up = { 0, 2, 4, 7, 4, 2, 0, 2 };
                for (int s = 0; s < steps; s++)
                    Tone(calm, start + s * len, (int)(len * 0.9), Freq(Note(deg + up[s % up.Length], 0)), Wave.Triangle, st.calm ? 0.07 : 0.05, 0.004, 0.15, 0.5);
            }

            // ---- Groove: drums and bass line ----
            var groove = layers[1];
            int[] bassPattern = { 0, 0, 7, 0, 4, 0, 7, 5 };
            if ((Next(ref rng) & 1) == 0) bassPattern = new[] { 0, 7, 0, 7, 0, 5, 4, 2 };
            for (int b = 0; b < Bars; b++)
            {
                int deg = st.chords[b % 4];
                int start = b * bar;
                for (int q = 0; q < 4; q++)
                {
                    int at = start + q * beat;
                    bool kick = st.calm ? q == 0 || q == 2 : true;
                    if (kick) Kick(groove, at, 0.5);
                    bool snare = st.calm ? q == 2 : q == 1 || q == 3;
                    if (snare) Snare(groove, at, 0.32, ref rng);
                    Hat(groove, at, 0.08, false, ref rng);
                    Hat(groove, at + beat / 2, 0.13, false, ref rng);
                }
                if (!st.calm && b % 2 == 1) Kick(groove, start + beat * 3 + beat / 2, 0.35); // a push into the next bar
                for (int e = 0; e < 8; e++)
                {
                    int semis = bassPattern[e];
                    double f = Freq(Note(deg, -2)) * Math.Pow(2, semis / 12.0) * 2;
                    Tone(groove, start + e * beat / 2, (int)(beat * 0.45), f, Wave.Saw, 0.13, 0.003, 0.05, 0.12);
                }
            }

            // ---- Intense: lead melody, fast hats, crashes, fills, sparkle ----
            var intense = layers[2];
            // A two-bar motif of eighth notes (some held), from chord tones and passing notes; it follows the chords.
            var motif = new int[16];
            var hold = new bool[16];
            int cur = 4;
            for (int i = 0; i < 16; i++)
            {
                uint r = Next(ref rng);
                int move = (int)(r % 5) - 2;
                cur = Math.Max(0, Math.Min(9, cur + move));
                motif[i] = cur;
                hold[i] = i > 0 && (r >> 8) % 4 == 0;
            }
            for (int b = 0; b < Bars; b++)
            {
                int deg = st.chords[b % 4];
                int start = b * bar;
                int half = (b % 2) * 8;
                for (int e = 0; e < 8; e++)
                {
                    int i = half + e;
                    if (hold[i]) continue;
                    int len = 1;
                    while (e + len < 8 && hold[half + e + len]) len++;
                    // The second half of the loop answers a step higher.
                    int d = deg + motif[i] + (b >= 4 && e >= 4 ? 1 : 0);
                    Tone(intense, start + e * beat / 2, (int)(beat / 2 * len * 0.92), Freq(Note(d, 1)), st.lead, 0.12, 0.006, 0.08, 0.35, vibrato: true);
                }
                for (int s = 0; s < 16; s++) Hat(intense, start + s * beat / 4, s % 4 == 0 ? 0.0 : 0.06, s % 8 == 6, ref rng);
                if (b == 0 || b == 4) Crash(intense, start, 0.16, ref rng);
                if (b == 3 || b == 7)
                    for (int t = 0; t < 4; t++) Tom(intense, start + beat * 3 + t * beat / 4, 200 - t * 30, 0.22);
                // A quiet high sparkle on sixteenths.
                for (int s = 0; s < 16; s++)
                    Tone(intense, start + s * beat / 4, beat / 5, Freq(Note(deg + (s % 3) * 2, 2)), Wave.Sine, 0.025, 0.002, 0.04, 1.0);
            }

            // One gain for all three layers, so the full mix peaks just under clipping.
            float peak = 0f;
            for (int i = 0; i < total; i++)
            {
                float m = Math.Abs(layers[0][i] + layers[1][i] + layers[2][i]);
                if (m > peak) peak = m;
            }
            float gain = peak > 0f ? 0.88f / peak : 1f;
            foreach (var l in layers)
                for (int i = 0; i < total; i++) l[i] *= gain;
            return layers;
        }

        // ---------- Instruments (all write with wrap-around, so tails loop) ----------

        private static double Freq(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        private static uint Next(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }

        private static float Noise(ref uint s) => (Next(ref s) & 0xFFFF) / 32767.5f - 1f;

        private static void Add(float[] b, int i, double v) => b[((i % b.Length) + b.Length) % b.Length] += (float)v;

        private static double Osc(Wave w, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Triangle: return 1.0 - 4.0 * Math.Abs(p - 0.5);
                case Wave.Saw: return 2.0 * p - 1.0;
                case Wave.Square: return p < 0.5 ? 0.7 : -0.7;
                default: return Math.Sin(p * 2.0 * Math.PI);
            }
        }

        /// <param name="cutoff">One-pole low-pass amount (1 = open, small = dark).</param>
        private static void Tone(float[] b, int start, int length, double freq, Wave wave, double volume, double attack, double release,
            double cutoff, bool vibrato = false)
        {
            double phase = 0, lp = 0;
            int a = Math.Max(1, (int)(attack * Rate));
            int r = Math.Max(1, (int)(release * Rate));
            int count = length + r;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                double f = vibrato && t > 0.12 ? freq * (1.0 + 0.006 * Math.Sin(t * 2 * Math.PI * 5.5)) : freq;
                phase += f / Rate;
                double env = i < a ? i / (double)a : i < length ? 1.0 : 1.0 - (i - length) / (double)r;
                if (i >= a && i < length) env = 0.75 + 0.25 * Math.Exp(-(i - a) / (Rate * 0.25)); // a little pluck
                double v = Osc(wave, phase);
                lp += (v - lp) * cutoff;
                Add(b, start + i, lp * env * volume);
            }
        }

        private static void Pad(float[] b, int start, int length, double freq, double volume)
        {
            double phase = 0, lp = 0;
            int a = Rate / 3;
            int r = Rate / 2;
            int count = length + r;
            for (int i = 0; i < count; i++)
            {
                phase += freq / Rate;
                double env = i < a ? i / (double)a : i < length ? 1.0 : 1.0 - (i - length) / (double)r;
                lp += (Osc(Wave.Saw, phase) - lp) * 0.06;
                Add(b, start + i, lp * env * volume);
            }
        }

        private static void Kick(float[] b, int start, double volume)
        {
            double phase = 0;
            int count = (int)(0.32 * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                double f = 45 + 95 * Math.Exp(-t * 28);
                phase += f / Rate;
                double env = Math.Exp(-t * 9);
                Add(b, start + i, Math.Sin(phase * 2 * Math.PI) * env * volume);
            }
        }

        private static void Snare(float[] b, int start, double volume, ref uint rng)
        {
            double phase = 0, prev = 0;
            int count = (int)(0.2 * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                phase += 185.0 / Rate;
                double n = Noise(ref rng);
                double hp = n - prev;
                prev = n;
                double v = hp * 0.7 * Math.Exp(-t * 18) + Math.Sin(phase * 2 * Math.PI) * 0.5 * Math.Exp(-t * 30);
                Add(b, start + i, v * volume);
            }
        }

        private static void Hat(float[] b, int start, double volume, bool open, ref uint rng)
        {
            if (volume <= 0) return;
            double prev = 0;
            double decay = open ? 14 : 60;
            int count = (int)((open ? 0.22 : 0.06) * Rate);
            for (int i = 0; i < count; i++)
            {
                double n = Noise(ref rng);
                double hp = n - prev;
                prev = n;
                Add(b, start + i, hp * Math.Exp(-i / (double)Rate * decay) * volume);
            }
        }

        private static void Crash(float[] b, int start, double volume, ref uint rng)
        {
            double prev = 0;
            int count = (int)(1.4 * Rate);
            for (int i = 0; i < count; i++)
            {
                double n = Noise(ref rng);
                double hp = n - prev;
                prev = n;
                Add(b, start + i, hp * Math.Exp(-i / (double)Rate * 2.6) * volume);
            }
        }

        private static void Tom(float[] b, int start, double pitch, double volume)
        {
            double phase = 0;
            int count = (int)(0.18 * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                phase += pitch * (1 + 0.6 * Math.Exp(-t * 25)) / Rate;
                Add(b, start + i, Math.Sin(phase * 2 * Math.PI) * Math.Exp(-t * 14) * volume);
            }
        }
    }
}
