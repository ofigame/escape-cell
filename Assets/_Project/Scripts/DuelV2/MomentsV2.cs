using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The v2 fight's time (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 6.2): hit stops (a few frames nearly frozen) and slow
    /// motions, timed in real seconds, laid over the prototype's own speed (its slow motion, its pause). The rules tick
    /// with the scaled time, so a hit stop stops them too: the moment is the same for the eye and the rules.
    /// </summary>
    public static class MomentsV2
    {
        private static float stopUntil, slowUntil, slowScale = 1f;

        public static void HitStop(float ms)
        {
            if (ms <= 0f) return;
            stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + ms * 0.001f);
        }

        public static void Slow(float seconds, float scale = 0.3f)
        {
            if (seconds <= 0f) return;
            if (Time.unscaledTime < slowUntil && scale > slowScale) return; // (a deeper slow motion already playing wins)
            slowUntil = Time.unscaledTime + seconds;
            slowScale = scale;
        }

        public static void Clear() { stopUntil = slowUntil = 0f; slowScale = 1f; }

        /// <summary>This frame's time scale (the tool's times the moment's).</summary>
        public static float Scale(float tool)
        {
            float k = 1f;
            if (Time.unscaledTime < slowUntil) k = slowScale;
            if (Time.unscaledTime < stopUntil) k = 0.02f;
            return tool * k;
        }
    }
}
