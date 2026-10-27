using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The duel's bodies as solid things (Docs/DUELLO_CARPISMA_VE_TEMAS.md): the push box round each trunk (its radius
    /// measured from the body's mesh, times <see cref="trunkScale"/>), when one body is over the other's head (no clash),
    /// how far and how fast a blow throws the struck one, and the effects' limits. Looks only: the rules' places and the
    /// server never move by it. Resources/Combat/CollisionSettings (made by the editor; without it, these values).
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Combat/Collision Settings")]
    public class CollisionSettings : ScriptableObject
    {
        [Header("Push box (section 2)")]
        /// <summary>The push box's radius: the trunk's measured radius times this (the big head is not in it).</summary>
        public float trunkScale = 1f;
        /// <summary>A body whose trunk could not be measured: its radius as a share of its height.</summary>
        public float fallbackRadiusShare = 0.12f;
        /// <summary>One body this much of its height over the other (a leap over its head): no clash.</summary>
        public float overHeadShare = 0.6f;

        [Header("Thrown by a blow (section 4), in tiles and seconds")]
        public float shakeDistance = 0.05f, shakeSeconds = 0.1f;
        public float staggerDistance = 0.25f, staggerSeconds = 0.25f;
        public float knockbackDistance = 0.6f, knockbackSeconds = 0.4f;
        public float launchDistance = 1.2f;
        public float ultiFinalDistance = 1f, ultiFinalSeconds = 0.5f;
        /// <summary>The throw over its time: fast at first, slowing.</summary>
        public AnimationCurve throwCurve = new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));
        /// <summary>The striker following the thrown one (a share of its throw), as far as the push box lets it.</summary>
        public float followShare = 0.5f;

        [Header("Effects (section 5)")]
        /// <summary>The most hit effects on screen at once.</summary>
        public int maxHitEffects = 3;
        /// <summary>An ultimate's blows before its last: their effects this small.</summary>
        public float ultiMiddleScale = 0.6f;

        private static CollisionSettings current;

        public static CollisionSettings Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<CollisionSettings>("Combat/CollisionSettings");
                if (current == null) current = CreateInstance<CollisionSettings>();
                return current;
            }
        }
    }
}
