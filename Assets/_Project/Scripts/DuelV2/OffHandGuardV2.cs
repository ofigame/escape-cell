using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The free hand in a fighter's guard (a one-handed weapon, no shield carried): its fist up in front of the belly a
    /// little out to the side, the elbow bent, where it is seen, not hung straight down the side (from the duel's side
    /// camera a hanging arm lies on the trunk and looks gone). Laid over the body's pose after it each frame; let go
    /// (smoothly) while a blow's clip, a fall or a two-handed grip has the arm.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public class OffHandGuardV2 : MonoBehaviour
    {
        /// <summary>The guard wanted now (the view sets it from the rules' state: standing, walking, guarding).</summary>
        public bool Wanted { get; set; }

        private SkinnedHero hero;
        private ShieldHold shield;
        private Transform arm, fore, hand, root;
        private float weight;

        public static OffHandGuardV2 On(SkinnedHero hero, Transform root)
        {
            if (hero == null) return null;
            var g = hero.GetComponent<OffHandGuardV2>();
            if (g == null) g = hero.gameObject.AddComponent<OffHandGuardV2>();
            g.hero = hero; g.root = root;
            return g;
        }

        private static Transform Bone(Transform under, string end)
        {
            foreach (var t in under.GetComponentsInChildren<Transform>(true)) if (t.name == end || t.name.EndsWith(":" + end)) return t;
            return null;
        }

        private void LateUpdate()
        {
            if (hero == null) return;
            if (arm == null) { arm = Bone(transform, "LeftArm"); fore = Bone(transform, "LeftForeArm"); hand = Bone(transform, "LeftHand"); }
            if (shield == null) shield = GetComponent<ShieldHold>();
            if (arm == null || fore == null || hand == null) return;
            bool free = Wanted && !hero.TwoHandedNow && (shield == null || !shield.Carried) && !hero.Flailing && !hero.InAir;
            weight = Mathf.MoveTowards(weight, free ? 1f : 0f, Time.deltaTime / 0.12f);
            if (weight <= 0.001f) return;
            var body = root != null ? root : transform;
            Vector3 ahead = body.forward, up = Vector3.up, left = -body.right;
            float reach = Vector3.Distance(arm.position, fore.position) + Vector3.Distance(fore.position, hand.position);
            var guard = arm.position + ahead * reach * 0.55f + left * reach * 0.12f - up * reach * 0.45f; // (the fist before the belly, a little out)
            var now = hand.position;
            Reach(arm, fore, hand, Vector3.Lerp(now, guard, weight * weight * (3f - 2f * weight)));
        }

        /// <summary>Two-joint reach: the elbow bends (to the side it already bends to) so the hand comes to the target.</summary>
        private static void Reach(Transform upper, Transform lower, Transform end, Vector3 target)
        {
            var origin = upper.position;
            float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, end.position);
            var to = target - origin;
            if (to.sqrMagnitude < 1e-8f || a < 1e-5f || b < 1e-5f) return;
            var dir = to.normalized;
            float c = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 1e-4f, a + b - 1e-4f);
            float x = (a * a - b * b + c * c) / (2f * c), h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            var bend = lower.position - origin; bend -= dir * Vector3.Dot(bend, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.Cross(dir, Vector3.up);
            var elbow = origin + dir * x + bend.normalized * h;
            upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, origin + dir * c - lower.position) * lower.rotation;
        }
    }
}
