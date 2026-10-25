using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// A struck body's springy answer, in the body's own frame (it faces the striker): a twist (yaw, + to its left), a
    /// side bend (roll, + drops its left shoulder), a fold (pitch, + forward), the head's nod (+ forward), the shoulders'
    /// drop, the hips' rise or sink (of its height). Each blow kicks them by its direction, height and strength, in one of
    /// several variants (never the same twice running), and each rings back to rest like a spring: ten blows in a row
    /// never look alike, and the body's place never moves.
    /// </summary>
    public class HitSpring
    {
        private const int Channels = 7;
        public const int Yaw = 0, Roll = 1, Pitch = 2, Head = 3, ShoulderL = 4, ShoulderR = 5, Hips = 6;

        private readonly float[] x = new float[Channels];
        private readonly float[] v = new float[Channels];
        private readonly float[] k = new float[Channels]; // (a blow's kick: kept, no new array a blow)
        private float omega = 30f, damping = 0.42f;
        private int lastVariant = -1;

        /// <summary>A channel's value now (degrees; the hips' in fractions of the body's height).</summary>
        public float this[int channel] => x[channel];

        /// <summary>Something to show (a blow still ringing).</summary>
        public bool Active
        {
            get
            {
                for (int i = 0; i < Channels; i++) if (Mathf.Abs(x[i]) > 0.05f || Mathf.Abs(v[i]) > 0.5f) return true;
                return false;
            }
        }

        /// <summary>
        /// A blow: <paramref name="degrees"/> is how far the body is thrown (its strength's, from the reaction profile),
        /// <paramref name="recovery"/> how long it takes to settle (seconds). Returns the variant used.
        /// </summary>
        public int Kick(HitDirection dir, HitHeight height, float degrees, float recovery)
        {
            int variant = Random.Range(0, 3);
            if (variant == lastVariant) variant = (variant + 1 + Random.Range(0, 2)) % 3;
            lastVariant = variant;
            float amp = variant == 0 ? 0.85f : variant == 1 ? 1f : 1.18f;
            float side = variant == 1 ? -1f : 1f; // (the other shoulder, the other way round, now and then)
            float d = degrees * amp;
            omega = 2f * Mathf.PI / Mathf.Max(0.08f, recovery * 0.9f);
            System.Array.Clear(k, 0, Channels);
            switch (dir)
            {
                case HitDirection.Horizontal_RightToLeft:
                    k[Yaw] = d; k[Roll] = d * 0.6f; k[ShoulderL] = d * 0.9f; k[Head] = d * 0.25f * side;
                    break;
                case HitDirection.Horizontal_LeftToRight:
                    k[Yaw] = -d; k[Roll] = -d * 0.6f; k[ShoulderR] = d * 0.9f; k[Head] = d * 0.25f * side;
                    break;
                case HitDirection.Overhead:
                    k[Head] = d * 1.2f; k[Pitch] = d * 0.6f; k[Hips] = -0.035f; k[Roll] = d * 0.15f * side;
                    break;
                case HitDirection.Upward:
                    k[Head] = -d * 1.25f; k[Pitch] = -d * 0.8f; k[Hips] = 0.015f; k[Yaw] = d * 0.15f * side;
                    break;
                case HitDirection.Thrust:
                    k[Pitch] = d; k[Head] = d * 0.5f; k[ShoulderL] = d * 0.3f; k[ShoulderR] = d * 0.3f; k[Yaw] = d * 0.2f * side;
                    break;
                default: // a whirl: thrown half round
                    k[Yaw] = Mathf.Min(60f, d * 2.6f) * side; k[Roll] = d * 0.3f;
                    break;
            }
            if (height == HitHeight.High) k[Head] += d * 0.35f;
            else if (height == HitHeight.Low) k[Pitch] += d * 0.3f;
            // the kick as a velocity: the spring swings out to about the target and rings back
            for (int i = 0; i < Channels; i++) v[i] += k[i] * omega * 1.25f;
            return variant;
        }

        /// <summary>The spring a frame on (picture time).</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;
            float c = 2f * damping * omega, kk = omega * omega;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8); // (small steps: steady at any frame rate)
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
                for (int i = 0; i < Channels; i++)
                {
                    v[i] += (-kk * x[i] - c * v[i]) * h;
                    x[i] += v[i] * h;
                }
        }

        /// <summary>At rest at once.</summary>
        public void Clear()
        {
            for (int i = 0; i < Channels; i++) { x[i] = 0f; v[i] = 0f; }
        }
    }
}
