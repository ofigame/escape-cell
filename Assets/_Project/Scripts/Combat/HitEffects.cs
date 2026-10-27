using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The duel's hit effects kept honest (Docs/DUELLO_CARPISMA_VE_TEMAS.md, section 5.2): each blow has an id and shows
    /// its effect once (a blow foreseen and then confirmed is one blow), no more than the settings' few on screen at once,
    /// and where the hit box met the hurt area: on the struck part's surface toward the weapon, a little toward the camera
    /// so it is drawn in front of the body. No allocation per blow.
    /// </summary>
    public static class HitEffects
    {
        private static int lastId;
        private static readonly int[] shown = new int[32];
        private static int shownAt;
        private static readonly float[] busyUntil = new float[8];
        private const float EffectSeconds = 0.35f;

        /// <summary>The blow landing now (0: none named): its effect is shown once.</summary>
        public static int CurrentBlow;
        /// <summary>A blow's effect asked for again and refused (the lab's count: a delayed confirmation).</summary>
        public static int Repeats { get; private set; }
        /// <summary>Effects shown, all told (the lab's count).</summary>
        public static int Shown { get; private set; }

        /// <summary>A new blow's id.</summary>
        public static int NewBlow() => ++lastId;

        /// <summary>Whether blow <paramref name="id"/> may show its effect now (first time, and room on screen); 0: no id.</summary>
        public static bool Claim(int id)
        {
            if (id > 0)
            {
                for (int i = 0; i < shown.Length; i++) if (shown[i] == id) { Repeats++; return false; }
                shown[shownAt] = id; shownAt = (shownAt + 1) % shown.Length;
            }
            float now = Time.unscaledTime;
            int max = Mathf.Clamp(CollisionSettings.Current.maxHitEffects, 1, busyUntil.Length);
            int busy = 0, free = -1;
            for (int i = 0; i < max; i++) { if (busyUntil[i] > now) busy++; else if (free < 0) free = i; }
            if (free < 0) return false; // (already the most there may be on screen)
            busyUntil[free] = now + EffectSeconds;
            Shown++;
            return true;
        }

        public static void ResetCounts() { Repeats = 0; Shown = 0; }

        /// <summary>
        /// Where a blow lands: the weapon's tip (or the striker's chest, a body blow) against the struck part (its head, its
        /// chest or its legs): the point on that part's surface nearest the weapon, a little toward the camera.
        /// </summary>
        public static Vector3 ContactPoint(Vector3 weaponAt, Vector3 partAt, float partRadius)
        {
            var toWeapon = weaponAt - partAt;
            var p = partAt + (toWeapon.sqrMagnitude > 1e-6f ? toWeapon.normalized * partRadius : Vector3.zero);
            var cam = Camera.main;
            if (cam != null) p += (cam.transform.position - p).normalized * partRadius * 0.5f;
            return p;
        }
    }
}
