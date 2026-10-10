using System;

namespace SquashBot.Audio
{
    /// <summary>
    /// Writes the game's music from code, so every note is our own (no samples, no licences). A theme is eight bars in
    /// its own key, scale, tempo and chord loop, rendered as three layers of exactly the same length that play in sync:
    /// <list type="number">
    /// <item>calm: a warm pad, electric-piano chords and a round, low bass (always on);</item>
    /// <item>groove: a soft kick, claps, a shaker and a gently moving bass line (fades in as the level gets tense);</item>
    /// <item>intense: a singing lead melody, a bell arpeggio and soft crashes (danger, a stomping monster, the end).</item>
    /// </list>
    /// The sounds are kept warm (sines, filtered tones, no raw saws in the bass), the melody is written as a
    /// question-and-answer phrase that lands on the chords, and a small reverb gives everything some room. It is
    /// plain math on float arrays, so it runs on a worker thread; tails wrap around, so the loop is seamless.
    /// </summary>
    public static partial class MusicSynth
    {
        public const int Rate = 22050;
        public const int Layers = 3;
        private const int Bars = 8;
        private const double TwoPi = Math.PI * 2;

        private sealed class Style
        {
            public int root;          // MIDI note of the key
            public int[] scale;       // semitones of the seven scale steps
            public int[] chords;      // scale degree of each bar's chord (four, played twice)
            public float bpm;
            public bool calm;         // half-time drums, softer and sparser
            public double leadTone;   // 0 = pure and soft, 1 = brighter
            public int seed;
        }

        private static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        private static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };
        private static readonly int[] Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };
        private static readonly int[] Harmonic = { 0, 2, 3, 5, 7, 8, 11 };

        private static Style StyleOf(MusicTheme theme)
        {
            switch (theme)
            {
                case MusicTheme.Menu: return new Style { root = 60, scale = Major, chords = new[] { 0, 5, 3, 4 }, bpm = 96f, calm = true, leadTone = 0.1, seed = 11 };
                case MusicTheme.World0: return new Style { root = 62, scale = Major, chords = new[] { 0, 4, 5, 3 }, bpm = 112f, leadTone = 0.35, seed = 21 };
                case MusicTheme.World1: return new Style { root = 64, scale = Dorian, chords = new[] { 0, 3, 6, 3 }, bpm = 110f, leadTone = 0.3, seed = 32 };
                case MusicTheme.World2: return new Style { root = 57, scale = Minor, chords = new[] { 0, 5, 2, 6 }, bpm = 116f, leadTone = 0.4, seed = 43 };
                case MusicTheme.World3: return new Style { root = 65, scale = Lydian, chords = new[] { 0, 1, 5, 4 }, bpm = 108f, leadTone = 0.25, seed = 54 };
                case MusicTheme.World4: return new Style { root = 55, scale = Mixolydian, chords = new[] { 0, 6, 3, 0 }, bpm = 120f, leadTone = 0.45, seed = 65 };
                case MusicTheme.World5: return new Style { root = 59, scale = Minor, chords = new[] { 0, 3, 4, 0 }, bpm = 118f, leadTone = 0.35, seed = 76 };
                case MusicTheme.Tunnel: return new Style { root = 62, scale = Minor, chords = new[] { 0, 5, 2, 6 }, bpm = 132f, leadTone = 0.55, seed = 87 };
                case MusicTheme.Monster: return new Style { root = 57, scale = Harmonic, chords = new[] { 0, 5, 3, 4 }, bpm = 120f, leadTone = 0.5, seed = 98 };
                default: return new Style { root = 52, scale = Harmonic, chords = new[] { 0, 5, 1, 4 }, bpm = 128f, leadTone = 0.6, seed = 109 };
            }
        }

        /// <summary>The three layers of a theme (calm, groove, intense), each the same number of samples. Thread-safe.</summary>
        public static float[][] Render(MusicTheme theme)
        {
            var st = StyleOf(theme);
            int beat = (int)Math.Round(60.0 / st.bpm * Rate);
            int bar = beat * 4;
            int total = bar * Bars;
            int eighth = beat / 2;
            var calm = new float[total];
            var groove = new float[total];
            var intense = new float[total];
            uint rng = (uint)(st.seed * 2654435761u) | 1u;

            int Note(int degree, int octave)
            {
                int d = ((degree % 7) + 7) % 7;
                int o = (int)Math.Floor(degree / 7.0);
                return st.root + st.scale[d] + 12 * (o + octave);
            }

            // Bass notes stay between E2 and E3: low enough to be round, high enough for a phone speaker to carry.
            int BassNote(int degree)
            {
                int m = Note(degree, -2);
                while (m < 40) m += 12;
                while (m > 52) m -= 12;
                return m;
            }

            // ---- Calm: pad, electric piano, round bass ----
            for (int b = 0; b < Bars; b++)
            {
                int deg = st.chords[b % 4];
                int start = b * bar;
                for (int k = 0; k < 3; k++)
                {
                    double f = Freq(Note(deg + k * 2, -1));
                    Pad(calm, start, bar, f, 0.035);
                }
                // Piano chords: on the beat for calm themes, a pushed rhythm otherwise.
                int[] hits = st.calm ? new[] { 0, 4 } : new[] { 0, 3, 6 };
                foreach (int h in hits)
                    for (int k = 0; k < 3; k++)
                        EPiano(calm, start + h * eighth, Freq(Note(deg + k * 2, 0)), 0.05);
                // Bass: a warm sine an octave low, with a little second harmonic so phone speakers can hear it.
                double bass = Freq(BassNote(deg));
                Bass(calm, start, beat * 2 - 200, bass, 0.22);
                Bass(calm, start + beat * 2, beat * 2 - 200, bass, 0.18);
            }

            // ---- Groove: soft drums and a moving bass ----
            int[] walk = { 0, 0, 7, 0, 5, 0, 7, 12 };
            for (int b = 0; b < Bars; b++)
            {
                int deg = st.chords[b % 4];
                int start = b * bar;
                for (int q = 0; q < 4; q++)
                {
                    int at = start + q * beat;
                    if (!st.calm || q == 0 || q == 2) Kick(groove, at, 0.42);
                    if (st.calm ? q == 2 : q % 2 == 1) Clap(groove, at, 0.16, ref rng);
                    Shaker(groove, at + eighth, 0.05, ref rng);
                    Shaker(groove, at, 0.025, ref rng);
                }
                double root = Freq(BassNote(deg));
                for (int e = 0; e < 8; e++)
                {
                    if (st.calm && e % 2 == 1) continue;
                    Bass(groove, start + e * eighth, (int)(eighth * 0.8), root * Math.Pow(2, walk[e] / 12.0), 0.11);
                }
            }

            // ---- Intense: lead melody, bell arpeggio, crashes ----
            var melody = Melody(st, ref rng);
            foreach (var n in melody)
            {
                double f = Freq(Note(n.degree, 1));
                Lead(intense, n.bar * bar + n.step * eighth, n.length * eighth - 300, f, 0.075, st.leadTone);
            }
            for (int b = 0; b < Bars; b++)
            {
                int deg = st.chords[b % 4];
                int start = b * bar;
                int[] arp = { 0, 2, 4, 7, 4, 2, 4, 7 };
                for (int s = 0; s < 8; s++) Bell(intense, start + s * eighth, Freq(Note(deg + arp[s], 1)), 0.025);
                if (b == 0 || b == 4) Crash(intense, start, 0.06, ref rng);
                if (b == 3 || b == 7)
                    for (int t = 0; t < 3; t++) Tom(intense, start + beat * 3 + t * beat / 3, 150 - t * 22, 0.16);
            }

            // A little room: the same small reverb on each layer, more on the soft parts.
            Reverb(calm, 0.25);
            Reverb(groove, 0.1);
            Reverb(intense, 0.3);

            // One gain for all three layers, so the full mix peaks just under clipping.
            float peak = 0f;
            for (int i = 0; i < total; i++)
            {
                float m = Math.Abs(calm[i] + groove[i] + intense[i]);
                if (m > peak) peak = m;
            }
            float gain = peak > 0f ? 0.85f / peak : 1f;
            var layers = new[] { calm, groove, intense };
            foreach (var l in layers)
                for (int i = 0; i < total; i++) l[i] *= gain;
            return layers;
        }

        // ---------- The melody ----------

        private struct MelodyNote
        {
            public int bar, step, length, degree;
        }

        /// <summary>
        /// An eight-bar tune as two four-bar phrases: a two-bar idea, its answer, the idea again over the next chords,
        /// and an ending that comes home to the key note. Strong beats sit on chord notes; in between it steps along
        /// the scale towards the next one.
        /// </summary>
        private static MelodyNote[] Melody(Style st, ref uint rng)
        {
            // A few hand-made rhythms (eighth-note steps and lengths per bar); the theme picks two.
            int[][][] rhythms =
            {
                new[] { new[] { 0, 2 }, new[] { 2, 1 }, new[] { 3, 1 }, new[] { 4, 4 } },
                new[] { new[] { 0, 1 }, new[] { 1, 1 }, new[] { 2, 2 }, new[] { 4, 2 }, new[] { 6, 2 } },
                new[] { new[] { 0, 3 }, new[] { 3, 1 }, new[] { 4, 2 }, new[] { 6, 2 } },
                new[] { new[] { 0, 2 }, new[] { 2, 2 }, new[] { 4, 1 }, new[] { 5, 1 }, new[] { 6, 2 } },
                new[] { new[] { 1, 1 }, new[] { 2, 2 }, new[] { 4, 4 } },
            };
            var a = rhythms[Next(ref rng) % (uint)rhythms.Length];
            var bRhythm = rhythms[Next(ref rng) % (uint)rhythms.Length];
            var ending = new[] { new[] { 0, 2 }, new[] { 2, 2 }, new[] { 4, 4 } };
            var notes = new System.Collections.Generic.List<MelodyNote>();
            int prev = 4; // start around the fifth
            for (int bar = 0; bar < Bars; bar++)
            {
                int chord = st.chords[bar % 4];
                var rhythm = bar == Bars - 1 || bar == 3 ? ending : bar % 2 == 0 ? a : bRhythm;
                for (int i = 0; i < rhythm.Length; i++)
                {
                    int step = rhythm[i][0], len = rhythm[i][1];
                    bool strong = step % 4 == 0;
                    int target;
                    if (bar == Bars - 1 && i == rhythm.Length - 1) target = 7;                 // home: the key note up high
                    else if (bar == 3 && i == rhythm.Length - 1) target = chord + 4;            // half-way: rest on a fifth
                    else if (strong)
                    {
                        // The chord note closest to where the tune is.
                        int best = chord, bestD = 99;
                        foreach (int c in new[] { chord, chord + 2, chord + 4, chord + 7, chord - 3 })
                        {
                            int d = Math.Abs(c - prev);
                            if (d < bestD) { bestD = d; best = c; }
                        }
                        target = best;
                    }
                    else
                    {
                        // A step up or down the scale, now and then a little leap.
                        uint r = Next(ref rng) % 6;
                        target = prev + (r < 2 ? 1 : r < 4 ? -1 : r == 4 ? 2 : -2);
                    }
                    target = Math.Max(0, Math.Min(11, target));
                    notes.Add(new MelodyNote { bar = bar, step = step, length = len, degree = target });
                    prev = target;
                }
            }
            return notes.ToArray();
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

        /// <summary>A soft electric piano: a sine with a quickly fading bell-like overtone and a gentle decay.</summary>
        private static void EPiano(float[] b, int start, double f, double vol)
        {
            int count = (int)(1.4 * Rate);
            double p1 = 0, p2 = 0;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                p1 += f / Rate;
                p2 += f * 2.0 / Rate;
                double env = (1 - Math.Exp(-t * 400)) * Math.Exp(-t * 2.2);
                double v = Math.Sin(TwoPi * p1) + 0.35 * Math.Sin(TwoPi * p2) * Math.Exp(-t * 8) + 0.1 * Math.Sin(TwoPi * p1 * 3) * Math.Exp(-t * 14);
                Add(b, start + i, v * env * vol * (1 + 0.04 * Math.Sin(t * TwoPi * 5)));
            }
        }

        /// <summary>A bell/pluck for arpeggios: sine plus a soft upper partial, short decay.</summary>
        private static void Bell(float[] b, int start, double f, double vol)
        {
            int count = (int)(0.6 * Rate);
            double p = 0;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                p += f / Rate;
                double env = (1 - Math.Exp(-t * 600)) * Math.Exp(-t * 6);
                Add(b, start + i, (Math.Sin(TwoPi * p) + 0.25 * Math.Sin(TwoPi * p * 4) * Math.Exp(-t * 20)) * env * vol);
            }
        }

        /// <summary>A warm pad: three slightly detuned triangles through a gentle low-pass, slow swell.</summary>
        private static void Pad(float[] b, int start, int length, double f, double vol)
        {
            int attack = Rate / 2, release = Rate * 3 / 4;
            int count = length + release;
            double p1 = 0, p2 = 0, p3 = 0, lp = 0;
            for (int i = 0; i < count; i++)
            {
                p1 += f / Rate;
                p2 += f * 1.004 / Rate;
                p3 += f * 0.996 / Rate;
                double v = (Tri(p1) + Tri(p2) + Tri(p3)) / 3;
                lp += (v - lp) * 0.08;
                double env = i < attack ? i / (double)attack : i < length ? 1 : 1 - (i - length) / (double)release;
                Add(b, start + i, lp * env * vol);
            }
        }

        /// <summary>A round bass: sine with a touch of its octave, short attack and release (never buzzy).</summary>
        private static void Bass(float[] b, int start, int length, double f, double vol)
        {
            int attack = (int)(0.008 * Rate), release = (int)(0.06 * Rate);
            int count = length + release;
            double p = 0;
            for (int i = 0; i < count; i++)
            {
                p += f / Rate;
                double env = i < attack ? i / (double)attack : i < length ? 1 - 0.25 * (i - attack) / (double)Math.Max(1, length - attack) : 0.75 * (1 - (i - length) / (double)release);
                Add(b, start + i, (Math.Sin(TwoPi * p) + 0.18 * Math.Sin(TwoPi * p * 2)) * env * vol);
            }
        }

        /// <summary>The lead: a soft, singing tone (sine with a little odd-harmonic colour), vibrato after the attack.</summary>
        private static void Lead(float[] b, int start, int length, double f, double vol, double tone)
        {
            int attack = (int)(0.02 * Rate), release = (int)(0.18 * Rate);
            int count = Math.Max(1, length) + release;
            double p = 0, lp = 0;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                double vib = t > 0.15 ? 1 + 0.005 * Math.Sin(TwoPi * 5.2 * t) : 1;
                p += f * vib / Rate;
                double v = Math.Sin(TwoPi * p) + tone * 0.3 * Math.Sin(TwoPi * p * 3) + tone * 0.12 * Math.Sin(TwoPi * p * 5);
                lp += (v - lp) * 0.5;
                double env = i < attack ? i / (double)attack : i < length ? 0.85 + 0.15 * Math.Exp(-(i - attack) / (Rate * 0.2)) : 0.85 * (1 - (i - length) / (double)release);
                Add(b, start + i, lp * env * vol);
            }
        }

        private static double Tri(double p)
        {
            double x = p - Math.Floor(p);
            return 1 - 4 * Math.Abs(x - 0.5);
        }

        /// <summary>A soft, round kick: a short pitch drop, no click.</summary>
        private static void Kick(float[] b, int start, double vol)
        {
            double p = 0;
            int count = (int)(0.28 * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                p += (48 + 70 * Math.Exp(-t * 35)) / Rate;
                double env = (1 - Math.Exp(-t * 900)) * Math.Exp(-t * 11);
                Add(b, start + i, Math.Sin(TwoPi * p) * env * vol);
            }
        }

        /// <summary>A clap: three quick bursts of filtered noise and a short tail.</summary>
        private static void Clap(float[] b, int start, double vol, ref uint rng)
        {
            double lp = 0, prev = 0;
            int count = (int)(0.22 * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                double n = Noise(ref rng);
                lp += (n - lp) * 0.35;
                double band = lp - prev;
                prev = lp;
                double env = t < 0.03 ? (Math.Sin(t * Math.PI * 100) > 0 ? 1 : 0.3) : Math.Exp(-(t - 0.03) * 22);
                Add(b, start + i, band * 2.2 * env * vol);
            }
        }

        /// <summary>A shaker tick: a whisper of high noise.</summary>
        private static void Shaker(float[] b, int start, double vol, ref uint rng)
        {
            double prev = 0;
            int count = (int)(0.05 * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                double n = Noise(ref rng);
                double hp = n - prev;
                prev = n;
                double env = (1 - Math.Exp(-t * 300)) * Math.Exp(-t * 70);
                Add(b, start + i, hp * env * vol);
            }
        }

        private static void Crash(float[] b, int start, double vol, ref uint rng)
        {
            double prev = 0;
            int count = (int)(1.6 * Rate);
            for (int i = 0; i < count; i++)
            {
                double n = Noise(ref rng);
                double hp = n - prev;
                prev = n;
                Add(b, start + i, hp * Math.Exp(-i / (double)Rate * 2.4) * vol);
            }
        }

        private static void Tom(float[] b, int start, double pitch, double vol)
        {
            double p = 0;
            int count = (int)(0.2 * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                p += pitch * (1 + 0.5 * Math.Exp(-t * 25)) / Rate;
                Add(b, start + i, Math.Sin(TwoPi * p) * (1 - Math.Exp(-t * 800)) * Math.Exp(-t * 12) * vol);
            }
        }

        /// <summary>
        /// A small room (four damped comb filters into two all-passes), mixed in by <paramref name="wet"/>. The buffer is
        /// run through twice so the reverb tail of the end carries over the loop point.
        /// </summary>
        private static void Reverb(float[] b, double wet)
        {
            int[] combLen = { 557, 593, 641, 677 };
            int[] apLen = { 113, 277 };
            var combs = new double[combLen.Length][];
            var combIdx = new int[combLen.Length];
            var combLp = new double[combLen.Length];
            for (int c = 0; c < combs.Length; c++) combs[c] = new double[combLen[c]];
            var aps = new double[apLen.Length][];
            var apIdx = new int[apLen.Length];
            for (int a = 0; a < aps.Length; a++) aps[a] = new double[apLen[a]];
            var output = new float[b.Length];
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < b.Length; i++)
                {
                    double x = b[i] * 0.3;
                    double sum = 0;
                    for (int c = 0; c < combs.Length; c++)
                    {
                        double y = combs[c][combIdx[c]];
                        combLp[c] = y * 0.6 + combLp[c] * 0.4; // damping: highs fade faster
                        combs[c][combIdx[c]] = x + combLp[c] * 0.8;
                        combIdx[c] = (combIdx[c] + 1) % combs[c].Length;
                        sum += y;
                    }
                    for (int a = 0; a < aps.Length; a++)
                    {
                        double buf = aps[a][apIdx[a]];
                        double y = -sum * 0.5 + buf;
                        aps[a][apIdx[a]] = sum + buf * 0.5;
                        apIdx[a] = (apIdx[a] + 1) % aps[a].Length;
                        sum = y;
                    }
                    if (pass == 1) output[i] = (float)(sum * wet);
                }
            for (int i = 0; i < b.Length; i++) b[i] += output[i];
        }
    }
}
