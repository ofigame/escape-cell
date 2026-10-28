using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// How a body answers a blow, by its strength (indexed by <see cref="HitStrength"/>): how far its spine and head are
    /// thrown (degrees, springing back), how far its picture is pushed (tiles) and how long it takes to settle.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Combat/Hit Reaction Profile", fileName = "HitReactions")]
    public class HitReactionProfile : ScriptableObject
    {
        public float[] springDegrees = { 9f, 14f, 22f, 26f, 30f };
        public float[] knockbackTiles = { 0.08f, 0.2f, 0.5f, 0f, 0f };
        public float[] recoveryMs = { 120f, 250f, 450f, 900f, 900f };

        public float Degrees(HitStrength s) => Pick(springDegrees, s);
        public float Recovery(HitStrength s) => Pick(recoveryMs, s) / 1000f;

        private static float Pick(float[] a, HitStrength s) => a == null || a.Length == 0 ? 0f : a[Mathf.Clamp((int)s, 0, a.Length - 1)];
    }
}
