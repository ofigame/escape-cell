using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The prototype's fight sounds made by recipe, nearer real ones than the toy synth: no pure tones. A blow is a body's
    /// thud (a falling low pitch, softly overdriven), a slap of filtered noise and the weapon's own short, damped
    /// resonance (a blade's dull metal, an axe's wood); a swing is air (noise through a moving band); a shield a hollow
    /// "thonk"; a clash inharmonic metal modes whose highs die first; everything through a small room's echo and with
    /// the highs rolled off (no thin, tinny ring). Each sound has variants (other noise, a little other pitch).
    /// </summary>
    public static class DuelSoundSynthV2
    {
        public const int Rate = 44100;
        public const int Variants = 4;
        private const float Tail = 0.35f;

        private enum Pass { Low, Band, High }

        public static AudioClip Make(DuelSoundV2 kind, int v)
        {
            var b = Samples(kind, v);
            var clip = AudioClip.Create(kind + "_" + v, b.Length, 1, Rate, false);
            clip.SetData(b, 0);
            return clip;
        }

        public static float[] Samples(DuelSoundV2 kind, int v)
        {
            var r = new System.Random(7919 + (int)kind * 104729 + v * 1299709);
            float k = 1f + 0.045f * (v - 1.5f); // (a variant a little higher or lower)
            float[] b;
            float wet = 0.14f, gain = 0.9f, roll = 7000f;
            switch (kind)
            {
                case DuelSoundV2.SwingSword:
                    b = New(0.24f);
                    Noise(b, r, 0f, 0.22f, Pass.Band, 700f * k, 2300f * k, 1.4f, 1f, 0f, -1f);
                    Noise(b, r, 0.03f, 0.17f, Pass.Band, 2600f * k, 3400f * k, 3f, 0.25f, 0f, -1f); // (the edge's thin whistle)
                    gain = 0.5f; wet = 0.08f; roll = 9000f;
                    break;
                case DuelSoundV2.SwingSpear:
                    b = New(0.18f);
                    Noise(b, r, 0f, 0.16f, Pass.Band, 500f * k, 1500f * k, 1.1f, 1f, 0f, -1f);
                    gain = 0.45f; wet = 0.08f;
                    break;
                case DuelSoundV2.SwingAxe:
                    b = New(0.32f);
                    Noise(b, r, 0f, 0.3f, Pass.Band, 260f * k, 800f * k, 1f, 1f, 0f, -1f, flutter: 11f);
                    Noise(b, r, 0f, 0.3f, Pass.Low, 180f, 260f, 0.7f, 0.5f, 0f, -1f);
                    gain = 0.55f; wet = 0.1f;
                    break;
                case DuelSoundV2.SwingHeavy:
                    b = New(0.42f);
                    Noise(b, r, 0f, 0.4f, Pass.Band, 150f * k, 480f * k, 0.9f, 1f, 0f, -1f, flutter: 7f);
                    Noise(b, r, 0f, 0.4f, Pass.Low, 110f, 170f, 0.7f, 0.8f, 0f, -1f, flutter: 7f);
                    gain = 0.6f; wet = 0.1f;
                    break;

                case DuelSoundV2.HitSword:
                    b = New(0.25f);
                    Thump(b, 0f, 150f * k, 60f, 0.09f, 0.4f, 2.2f);
                    Noise(b, r, 0f, 0.07f, Pass.Band, 600f, 420f, 0.8f, 1.2f, 0.0008f, 0.025f); // (the body)
                    Noise(b, r, 0f, 0.03f, Pass.Band, 1900f * k, 1500f, 0.8f, 2f, 0.0004f, 0.008f); // (the slap)
                    Noise(b, r, 0.002f, 0.06f, Pass.Band, 3200f * k, 2400f, 1.6f, 1.1f, 0.001f, 0.018f); // (the blade's cut, "shk")
                    Modes(b, r, 0f, new[] { 820f * k, 1460f * k, 2310f * k }, new[] { 0.035f, 0.024f, 0.015f }, new[] { 0.16f, 0.1f, 0.05f });
                    break;
                case DuelSoundV2.HitSpear:
                    b = New(0.22f);
                    Thump(b, 0f, 190f * k, 75f, 0.07f, 0.35f, 2f);
                    Noise(b, r, 0f, 0.02f, Pass.High, 2200f, 2200f, 0.7f, 0.9f, 0.0003f, 0.006f); // (the point going in)
                    Noise(b, r, 0f, 0.05f, Pass.Band, 1300f * k, 900f, 0.9f, 1.6f, 0.0005f, 0.012f);
                    Noise(b, r, 0f, 0.06f, Pass.Band, 550f, 400f, 0.8f, 0.8f, 0.0008f, 0.02f);
                    Modes(b, r, 0f, new[] { 610f * k, 1170f * k }, new[] { 0.035f, 0.022f }, new[] { 0.14f, 0.07f });
                    break;
                case DuelSoundV2.HitAxe:
                    b = New(0.3f);
                    Thump(b, 0f, 115f * k, 45f, 0.11f, 0.3f, 2.6f);
                    Noise(b, r, 0f, 0.1f, Pass.Band, 520f, 330f, 0.8f, 1.2f, 0.0008f, 0.035f);
                    Noise(b, r, 0f, 0.04f, Pass.Band, 1250f * k, 900f, 1.1f, 2f, 0.0004f, 0.012f); // (the chop's crack)
                    Noise(b, r, 0f, 0.03f, Pass.Band, 2800f * k, 2200f, 1f, 0.6f, 0.0004f, 0.007f);
                    Modes(b, r, 0f, new[] { 310f * k, 740f * k, 1290f * k }, new[] { 0.04f, 0.028f, 0.016f }, new[] { 0.22f, 0.14f, 0.07f }); // (wood)
                    break;
                case DuelSoundV2.HitHeavy:
                    b = New(0.45f);
                    Thump(b, 0f, 85f * k, 34f, 0.2f, 0.3f, 3f);
                    Noise(b, r, 0f, 0.2f, Pass.Band, 380f, 220f, 0.8f, 1.2f, 0.001f, 0.06f);
                    Noise(b, r, 0f, 0.05f, Pass.Band, 900f * k, 600f, 0.7f, 1.8f, 0.0005f, 0.018f); // (the crunch)
                    Noise(b, r, 0f, 0.03f, Pass.Band, 2200f * k, 1800f, 0.9f, 0.6f, 0.0004f, 0.008f);
                    Ticks(b, r, 0.03f, 0.14f, 5, 1600f, 0.35f); // (bits flying)
                    wet = 0.18f;
                    break;
                case DuelSoundV2.Smash:
                    b = New(0.5f);
                    Thump(b, 0f, 70f * k, 30f, 0.24f, 0.3f, 3.5f);
                    Noise(b, r, 0f, 0.3f, Pass.Band, 700f, 200f, 0.7f, 1.2f, 0.001f, 0.08f);
                    Noise(b, r, 0f, 0.04f, Pass.Band, 1400f, 900f, 0.6f, 1.4f, 0.0004f, 0.012f);
                    wet = 0.22f; gain = 0.8f;
                    break;

                case DuelSoundV2.Block:
                    b = New(0.3f);
                    Thump(b, 0f, 170f * k, 95f, 0.07f, 0.35f, 1.8f);
                    Modes(b, r, 0f, new[] { 235f * k, 540f * k, 905f * k, 1420f * k, 2080f * k }, new[] { 0.08f, 0.06f, 0.042f, 0.03f, 0.02f }, new[] { 0.3f, 0.26f, 0.18f, 0.12f, 0.06f });
                    Noise(b, r, 0f, 0.03f, Pass.Band, 1500f, 1200f, 0.7f, 1.6f, 0.0004f, 0.008f);
                    Noise(b, r, 0f, 0.08f, Pass.Band, 650f, 450f, 0.8f, 1f, 0.0006f, 0.025f); // (the hollow of the shield)
                    wet = 0.18f; roll = 5000f;
                    break;
                case DuelSoundV2.Parry:
                    b = New(0.6f);
                    Thump(b, 0f, 140f * k, 70f, 0.08f, 0.6f, 2f);
                    Metal(b, r, 0f, 340f * k, 0.3f, 0.8f);
                    Noise(b, r, 0f, 0.012f, Pass.Band, 2400f, 2000f, 0.6f, 1.2f, 0.0003f, 0.004f);
                    Noise(b, r, 0.005f, 0.16f, Pass.Band, 3000f, 1800f, 1.5f, 0.35f, 0.002f, 0.06f); // (the blades sliding)
                    wet = 0.26f; roll = 6000f; gain = 0.85f;
                    break;
                case DuelSoundV2.Clash:
                    b = New(0.65f);
                    Metal(b, r, 0f, 400f * k, 0.22f, 1f);
                    Noise(b, r, 0f, 0.012f, Pass.Band, 2600f, 2200f, 0.6f, 1.6f, 0.0003f, 0.004f); // (the crack)
                    Noise(b, r, 0.01f, 0.13f, Pass.Band, 3400f, 2000f, 1.6f, 0.4f, 0.003f, 0.05f);
                    Thump(b, 0f, 120f, 70f, 0.06f, 0.4f, 1.5f);
                    wet = 0.26f; roll = 6000f; gain = 0.8f;
                    break;

                case DuelSoundV2.BodyFall:
                case DuelSoundV2.KO:
                    bool ko = kind == DuelSoundV2.KO;
                    b = New(ko ? 0.6f : 0.45f);
                    Thump(b, 0f, 95f * k, 38f, ko ? 0.16f : 0.12f, 0.32f, 2.8f);
                    Noise(b, r, 0f, 0.22f, Pass.Band, 420f, 220f, 0.8f, 1.2f, 0.001f, 0.06f);
                    Noise(b, r, 0f, 0.04f, Pass.Band, 1100f, 800f, 0.8f, 0.9f, 0.0005f, 0.012f); // (the slap on the floor)
                    Noise(b, r, 0f, 0.12f, Pass.Band, 2400f, 1800f, 0.5f, 0.35f, 0.015f, 0.05f); // (cloth, dust)
                    Thump(b, 0.15f, 80f * k, 40f, 0.08f, 0.15f, 2f); // (a small second bounce)
                    Noise(b, r, 0.15f, 0.08f, Pass.Band, 450f, 260f, 0.8f, 0.5f, 0.001f, 0.025f);
                    if (ko) Thump(b, 0f, 60f, 28f, 0.25f, 0.2f, 2.5f);
                    wet = 0.2f;
                    break;
                case DuelSoundV2.Jump:
                    b = New(0.12f);
                    Noise(b, r, 0f, 0.08f, Pass.Band, 900f * k, 700f, 0.8f, 0.7f, 0.002f, 0.025f); // (a foot's scuff)
                    Thump(b, 0f, 120f, 70f, 0.05f, 0.5f, 1.5f);
                    gain = 0.5f; wet = 0.06f;
                    break;

                case DuelSoundV2.Snow:
                    b = New(0.3f);
                    Thump(b, 0f, 110f * k, 55f, 0.08f, 0.3f, 1.5f);
                    Noise(b, r, 0f, 0.18f, Pass.Band, 800f, 350f, 0.7f, 1.2f, 0.001f, 0.05f); // ("pumf")
                    Ticks(b, r, 0.01f, 0.12f, 6, 2500f, 0.25f); // (the snow's crunch)
                    break;
                case DuelSoundV2.Fire:
                    b = New(0.75f);
                    Thump(b, 0f, 75f * k, 30f, 0.24f, 0.35f, 3f);
                    Noise(b, r, 0f, 0.6f, Pass.Low, 3200f, 180f, 0.7f, 1.3f, 0.002f, 0.14f); // (the burst)
                    Ticks(b, r, 0.05f, 0.55f, 18, 3000f, 0.3f); // (crackling)
                    wet = 0.2f;
                    break;
                case DuelSoundV2.Hurt:
                    // an armoured body taking the blow: a deep "umph", a dull plate's clank, a short scrape
                    b = New(0.3f);
                    Thump(b, 0f, 110f * k, 50f, 0.1f, 0.6f, 2.8f);
                    Noise(b, r, 0f, 0.08f, Pass.Band, 480f * k, 300f, 0.8f, 1.2f, 0.001f, 0.03f);
                    Modes(b, r, 0.004f, new[] { 330f * k, 610f * k, 980f * k }, new[] { 0.05f, 0.035f, 0.02f }, new[] { 0.3f, 0.2f, 0.1f });
                    Noise(b, r, 0.01f, 0.05f, Pass.Band, 1800f * k, 1300f, 1f, 0.5f, 0.002f, 0.015f);
                    break;
                default: // Lightning
                    b = New(1f);
                    Noise(b, r, 0f, 0.03f, Pass.High, 1500f, 1500f, 0.7f, 1f, 0.0002f, 0.008f); // (the crack)
                    Ticks(b, r, 0f, 0.08f, 8, 4000f, 0.35f);
                    Noise(b, r, 0.02f, 0.9f, Pass.Low, 260f, 110f, 0.7f, 1f, 0.05f, 0.25f); // (the rumble)
                    Thump(b, 0.01f, 60f, 30f, 0.25f, 0.2f, 2.5f);
                    wet = 0.25f; roll = 6000f;
                    break;
            }
            LowPass1(b, roll);
            Reverb(b, wet);
            Normalize(b, 0.92f * gain);
            FadeOut(b, 0.02f);
            return b;
        }

        // ---------- building blocks ----------

        private static float[] New(float seconds) => new float[Mathf.CeilToInt((seconds + Tail) * Rate)];

        private static float Rnd(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>Noise through a filter whose frequency moves (f0 → f1). decay &lt; 0: a swell (a swing's rise and fall), else a hit's attack and exponential fall.</summary>
        private static void Noise(float[] b, System.Random r, float start, float dur, Pass pass, float f0, float f1, float q, float vol, float attack, float decay, float flutter = 0f)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            var f = new Biquad();
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float u = i / (float)n, t = i / (float)Rate;
                if ((i & 15) == 0)
                {
                    float fr = f0 * Mathf.Pow(f1 / f0, decay < 0f ? Mathf.Sin(u * Mathf.PI * 0.5f) : u);
                    f.Set(pass, fr, q);
                }
                float y = f.Run(Rnd(r));
                float env = decay < 0f ? Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.75f)), 1.6f) : Env(t, attack, decay);
                if (flutter > 0f) env *= 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * flutter * t);
                b[s0 + i] += y * env * vol;
            }
        }

        /// <summary>A body's thud: a low pitch falling fast, softly overdriven (round, not a pure tone).</summary>
        private static void Thump(float[] b, float start, float f0, float f1, float dur, float vol, float drive)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * 2.2f * Rate);
            float phase = 0f, norm = (float)System.Math.Tanh(drive);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float fr = f0 * Mathf.Pow(f1 / f0, Mathf.Min(1f, t / dur));
                phase += fr / Rate;
                float s = (float)System.Math.Tanh(drive * Mathf.Sin(2f * Mathf.PI * phase)) / norm;
                b[s0 + i] += s * Env(t, 0.0015f, dur * 0.4f) * vol;
            }
        }

        /// <summary>Damped resonances (a solid thing struck): each its own frequency, fall time and loudness, a little detuned.</summary>
        private static void Modes(float[] b, System.Random r, float start, float[] freqs, float[] decays, float[] amps, float vol = 1f)
        {
            int s0 = (int)(start * Rate);
            for (int m = 0; m < freqs.Length; m++)
            {
                float fr = freqs[m] * (1f + 0.01f * Rnd(r)), ph = (float)r.NextDouble();
                int n = (int)(decays[m] * 6f * Rate);
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[s0 + i] += Mathf.Sin(2f * Mathf.PI * (fr * t + ph)) * Env(t, 0.0006f, decays[m]) * amps[m] * vol;
                }
            }
        }

        /// <summary>Two blades meeting: a metal bar's inharmonic modes (the high ones dying first), a slow beat between near pairs.</summary>
        private static void Metal(float[] b, System.Random r, float start, float f0, float ring, float vol)
        {
            float[] ratio = { 1f, 1.52f, 2.32f, 2.81f, 4.13f, 5.41f, 6.97f, 8.6f };
            var freqs = new float[ratio.Length * 2]; var decays = new float[freqs.Length]; var amps = new float[freqs.Length];
            for (int i = 0; i < ratio.Length; i++)
            {
                for (int p = 0; p < 2; p++)
                {
                    int j = i * 2 + p;
                    freqs[j] = f0 * ratio[i] * (p == 0 ? 1f : 1.004f + 0.003f * (float)r.NextDouble());
                    decays[j] = ring / Mathf.Pow(ratio[i], 0.85f);
                    amps[j] = 0.5f / Mathf.Pow(ratio[i], 0.8f);
                }
            }
            Modes(b, r, start, freqs, decays, amps, vol);
        }

        /// <summary>Little clicks scattered in time (bits flying, crackling).</summary>
        private static void Ticks(float[] b, System.Random r, float from, float to, int count, float freq, float vol)
        {
            for (int i = 0; i < count; i++)
            {
                float at = Mathf.Lerp(from, to, (float)r.NextDouble());
                Noise(b, r, at, 0.012f, Pass.Band, freq * (0.7f + 0.6f * (float)r.NextDouble()), freq, 1.2f, vol * (0.4f + 0.6f * (float)r.NextDouble()), 0.0003f, 0.003f);
            }
        }

        private static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        /// <summary>The highs rolled off (a one-pole low-pass, twice).</summary>
        private static void LowPass1(float[] b, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            for (int pass = 0; pass < 2; pass++)
            {
                float y = 0f;
                for (int i = 0; i < b.Length; i++) { y += (b[i] - y) * a; b[i] = y; }
            }
        }

        /// <summary>A small room's echo (Schroeder: four damped combs, two all-passes), mixed under the dry sound.</summary>
        private static void Reverb(float[] b, float wet)
        {
            if (wet <= 0f) return;
            int[] combs = { 1116, 1188, 1277, 1356 };
            var outp = new float[b.Length];
            foreach (int d in combs)
            {
                var line = new float[d]; int idx = 0; float damp = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float y = line[idx];
                    damp += (y - damp) * 0.45f;
                    line[idx] = b[i] + damp * 0.78f;
                    idx = (idx + 1) % d;
                    outp[i] += y * 0.25f;
                }
            }
            foreach (int d in new[] { 556, 225 })
            {
                var line = new float[d]; int idx = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float bufout = line[idx];
                    float x = outp[i];
                    line[idx] = x + bufout * 0.5f;
                    outp[i] = bufout - x * 0.5f;
                    idx = (idx + 1) % d;
                }
            }
            for (int i = 0; i < b.Length; i++) b[i] += outp[i] * wet;
        }

        private static void Normalize(float[] b, float peak)
        {
            float max = 0f;
            foreach (float v in b) max = Mathf.Max(max, Mathf.Abs(v));
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        private static void FadeOut(float[] b, float seconds)
        {
            int n = Mathf.Min(b.Length, (int)(seconds * Rate));
            for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= i / (float)n;
        }

        /// <summary>A two-pole filter (RBJ's cookbook): low, band or high pass.</summary>
        private struct Biquad
        {
            private float b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public void Set(Pass pass, float freq, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(freq, 20f, Rate * 0.45f) / Rate;
                float cos = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2f * q), a0 = 1f + alpha;
                switch (pass)
                {
                    case Pass.Low: b0 = (1f - cos) / 2f; b1 = 1f - cos; b2 = b0; break;
                    case Pass.High: b0 = (1f + cos) / 2f; b1 = -(1f + cos); b2 = b0; break;
                    default: b0 = alpha; b1 = 0f; b2 = -alpha; break;
                }
                b0 /= a0; b1 /= a0; b2 /= a0; a1 = -2f * cos / a0; a2 = (1f - alpha) / a0;
            }

            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }
    }
}
