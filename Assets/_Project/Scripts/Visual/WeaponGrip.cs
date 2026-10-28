using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A built weapon's grip points (Grip_Main at its origin, where the main hand's fist holds it; Grip_Off up the length
    /// for the second hand of a two-handed one), from <see cref="Combat.WeaponGripData"/>.
    /// </summary>
    public class WeaponGrip : MonoBehaviour
    {
        /// <summary>Held in both hands.</summary>
        public bool TwoHanded { get; set; }
        /// <summary>The second hand's place in the weapon's own frame (its length runs up +y from the main hand).</summary>
        public Vector3 OffLocal { get; set; }
        /// <summary>How far the fingers close (1: a full fist).</summary>
        public float Curl { get; set; } = 1f;

        /// <summary>The second hand's place in the world now.</summary>
        public Vector3 Off => transform.TransformPoint(OffLocal);
    }
}
