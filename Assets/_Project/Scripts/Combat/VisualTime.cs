using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// A fighter's own picture clock, on its visual root: the hit freeze (its body and weapon all but still for a few
    /// milliseconds) and the big moments' slow motion run on it. The game's clock (Time.timeScale, the match timer, the
    /// server) never changes; only how fast this body moves on screen.
    /// </summary>
    public class VisualTime : MonoBehaviour
    {
        private static float globalScale = 1f, globalUntil;
        private float freezeUntil, slowUntil, slowScale = 1f;

        /// <summary>Every fighter's pictures slowed together for a while (a big moment), real seconds.</summary>
        public static void SlowAll(float scale, float seconds)
        {
            globalScale = Mathf.Clamp(scale, 0.05f, 1f);
            globalUntil = Time.unscaledTime + Mathf.Max(0f, seconds);
        }

        /// <summary>The big moments' slow motion now (1: none).</summary>
        public static float GlobalScale => Time.unscaledTime < globalUntil ? globalScale : 1f;

        /// <summary>Frozen by a blow right now.</summary>
        public bool Frozen => Time.unscaledTime < freezeUntil;

        /// <summary>This body's picture speed now (the freeze times the slow motion).</summary>
        public float Scale => (Frozen ? CombatLibrary.Settings.hitstopSpeed : 1f) * (Time.time < slowUntil ? slowScale : 1f) * GlobalScale;

        /// <summary>This body's pictures slowed for a while (frozen by ice: 0.7), game seconds.</summary>
        public void SlowFor(float scale, float seconds)
        {
            slowScale = Mathf.Clamp(scale, 0.1f, 1f);
            slowUntil = Mathf.Max(slowUntil, Time.time + Mathf.Max(0f, seconds));
        }

        /// <summary>This frame's time for this body's pictures.</summary>
        public float DeltaTime => Time.deltaTime * Scale;

        /// <summary>Holds this body all but still for <paramref name="seconds"/> (real time; a longer freeze wins).</summary>
        public void Freeze(float seconds) => freezeUntil = Mathf.Max(freezeUntil, Time.unscaledTime + Mathf.Max(0f, seconds));

        /// <summary>The clock on a visual root (made the first time it is asked for).</summary>
        public static VisualTime On(Transform visualRoot)
        {
            if (visualRoot == null) return null;
            var v = visualRoot.GetComponent<VisualTime>();
            return v != null ? v : visualRoot.gameObject.AddComponent<VisualTime>();
        }

        /// <summary>The picture time for something under a visual root (plain frame time with the slow motion if none).</summary>
        public static float DeltaFor(Component c)
        {
            var v = c != null ? c.GetComponentInParent<VisualTime>() : null;
            return v != null ? v.DeltaTime : Time.deltaTime * GlobalScale;
        }
    }
}
