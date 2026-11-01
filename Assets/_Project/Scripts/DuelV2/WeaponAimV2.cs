using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// A blow's weapon onto the other (the v2 duel): the body's clip swings the arm, but the weapon rides in the fist at
    /// its resting angle, so a spear's thrust pointed at the sky and a mace came down short on the floor. Laid over the
    /// pose after everything else each frame: the weapon turned at the wrist (about the fist) so its length points at
    /// the other's chest, as much as <see cref="Weight"/> says (the view sets it by the blow's phase).
    /// </summary>
    [DefaultExecutionOrder(650)]
    public class WeaponAimV2 : MonoBehaviour
    {
        /// <summary>The weapon (its own transform: its up is its length, its origin in the fist).</summary>
        public Transform Weapon;
        /// <summary>Where it points (the other's chest).</summary>
        public Vector3 Target;
        /// <summary>0 its own angle .. 1 straight at the target.</summary>
        public float Weight;

        public static WeaponAimV2 On(GameObject host) => host.GetComponent<WeaponAimV2>() ?? host.AddComponent<WeaponAimV2>();

        private void LateUpdate()
        {
            if (Weapon == null || Weight <= 0.001f || Weapon.parent == null) return;
            var along = Weapon.up;
            var to = Target - Weapon.position;
            if (to.sqrMagnitude < 1e-4f) return;
            var turn = Quaternion.FromToRotation(along, to.normalized);
            turn.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) < 0.1f || axis.sqrMagnitude < 1e-6f) return;
            Weapon.parent.RotateAround(Weapon.position, axis, angle * Mathf.Clamp01(Weight));
        }
    }
}
