using System;

namespace SquashBot.Audio
{
    /// <summary>
    /// The fight layer: a hard-rock loop (eight bars at 152 bpm in E minor) laid over the world's music while a fight
    /// is on — palm-muted power chords on a driven guitar, a bass doubling the roots, kick and snare with double-kick
    /// fills at the end of each phrase, eighth-note hats and a crash on every phrase. Mixed to sit under the effects.
    /// </summary>
    public static partial class MusicSynth
    {
        public static float[] RenderRock()
        {
            const double bpm = 152;
            int beat = (int)Math.Round(60.0 / bpm * Rate);
            int bar = beat * 4;
            int total = bar * Bars;
            int eighth = beat / 2, sixteenth = beat / 4;
            var b = new float[total];
            uint rng = 0x5EEDu;
            // E5 E5 C5 D5 | E5 G5 A5 B5 (MIDI roots of the power chords).
            int[] roots = { 40, 40, 36, 38, 40, 43, 45, 47 };

            for (int bi = 0; bi < Bars; bi++)
            {
                int start = bi * bar;
                int root = roots[bi];
                for (int e = 0; e < 8; e++)
                {
                    int at = start + e * eighth;
                    // The first beat of a bar rings open; the rest are tight, chugging palm mutes.
                    bool open = e == 0 || (e == 6 && bi % 2 == 1);
                    PowerChord(b, at, open ? eighth * 2 : (int)(eighth * 0.8), root, open ? 0.32 : 0.24, open ? 0.0 : 1.0);
                    RockBass(b, at, (int)(eighth * 0.9), Freq(root - 12), 0.3);
                }
                // Drums: kick on 1, the "and" of 2 and 3; snare on 2 and 4; hats on the eighths.
                foreach (int k in new[] { 0, 3, 4 }) Kick(b, start + k * eighth, 0.75);
                foreach (int s in new[] { 2, 6 }) Snare(b, start + s * eighth, 0.5, ref rng);
                for (int e = 0; e < 8; e++) Shaker(b, start + e * eighth, e % 2 == 0 ? 0.22 : 0.14, ref rng);
                if (bi % 4 == 0) Crash(b, start, 0.18, ref rng);
                // A double-kick fill closes each four-bar phrase, with toms.
                if (bi % 4 == 3)
                {
                    for (int s = 8; s < 16; s++) Kick(b, start + s * sixteenth, 0.55);
                    for (int s = 12; s < 16; s++) Tom(b, start + s * sixteenth, 160 - (s - 12) * 25, 0.35);
                }
            }

            // Glue: a soft clip over the mix, then normalise.
            float peak = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = (float)Math.Tanh(b[i] * 1.3);
                peak = Math.Max(peak, Math.Abs(b[i]));
            }
            if (peak > 0f) for (int i = 0; i < b.Length; i++) b[i] *= 0.9f / peak;
            return b;
        }

        /// <summary>A driven power chord (root, fifth, octave): detuned saws through hard clipping and a low-pass.</summary>
        private static void PowerChord(float[] b, int start, int length, int root, double vol, double mute)
        {
            double[] f = { Freq(root), Freq(root + 7), Freq(root + 12) };
            double[] ph = new double[6];
            double lp = 0, lp2 = 0;
            double cutoff = mute > 0.5 ? 0.12 : 0.28;
            int tail = (int)(0.03 * Rate);
            for (int i = 0; i < length + tail; i++)
            {
                double t = i / (double)Rate;
                double raw = 0;
                for (int n = 0; n < 3; n++)
                    for (int d = 0; d < 2; d++)
                    {
                        int k = n * 2 + d;
                        ph[k] += f[n] * (d == 0 ? 0.997 : 1.003) / Rate;
                        raw += (ph[k] % 1.0) * 2 - 1;
                    }
                double driven = Math.Tanh(raw * 2.8);
                lp += (driven - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                double env = (1 - Math.Exp(-t * 600)) * (i < length ? Math.Exp(-t * (mute > 0.5 ? 9 : 1.6)) : Math.Exp(-(i - length) / (double)tail * 5));
                Add(b, start + i, lp2 * env * vol);
            }
        }

        private static void RockBass(float[] b, int start, int length, double f, double vol)
        {
            double p = 0, lp = 0;
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)Rate;
                p += f / Rate;
                double sq = (p % 1.0) < 0.5 ? 1 : -1;
                lp += (sq - lp) * 0.08;
                double env = (1 - Math.Exp(-t * 500)) * Math.Exp(-t * 4);
                Add(b, start + i, lp * env * vol);
            }
        }

        /// <summary>A rock snare: a tuned body and a burst of bright noise.</summary>
        private static void Snare(float[] b, int start, double vol, ref uint rng)
        {
            double p = 0, prev = 0;
            int count = (int)(0.2 * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                p += 190 / (double)Rate;
                double n = Noise(ref rng);
                double hp = n - prev * 0.6;
                prev = n;
                double body = Math.Sin(TwoPi * p) * Math.Exp(-t * 30);
                double env = (1 - Math.Exp(-t * 900)) * Math.Exp(-t * 18);
                Add(b, start + i, (body * 0.6 + hp * 0.8) * env * vol);
            }
        }
    }
}
