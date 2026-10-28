using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Gear on foi's limbs: each piece is drawn in a limb's own frame (y runs down the limb from its joint, 1 = the limb's
    /// length; z faces the way foi faces), so it sits on a body's bone (<see cref="SkinnedHero.Gear"/>). The workshop's
    /// arm and leg gear is gone; what is left is the frame and a plain pad, for the rank gear to build on.
    /// </summary>
    public static class GearModels
    {
        private static readonly System.Collections.Generic.Dictionary<string, Material> made = new System.Collections.Generic.Dictionary<string, Material>();

        /// <summary>A material made once for its colours and finish (outfits change often: no new one each time).</summary>
        private static Material Surface(Color c, Color e, float smooth, float metal)
        {
            string key = c + "|" + e + "|" + smooth + "|" + metal;
            if (!made.TryGetValue(key, out var m) || m == null) made[key] = m = MaterialFactory.CreateSurface(c, e, smooth, metal);
            return m;
        }

        /// <summary>The limbs a piece is made for.</summary>
        public enum Limb { UpperArm, Forearm, Hand, Shin, Foot }

        /// <summary>Which limbs an arm or leg piece covers.</summary>
        public static Limb[] LimbsOf(string id) => id.StartsWith("arms.") ? new[] { Limb.Forearm } : new[] { Limb.Shin };

        /// <summary>Builds the part of <paramref name="id"/> that goes on <paramref name="limb"/> under <paramref name="frame"/>.</summary>
        public static void Build(Transform frame, string id, Limb limb, Color color)
        {
            var main = Surface(color, Color.black, 0.4f, 0.15f);
            Shapes.Rounded("Pad", frame, new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 0.5f, 0.8f), 0.2f, main);
        }

        /// <summary>A limb frame between two joints: y down the limb from <paramref name="from"/> (1 = its length), z the way <paramref name="facing"/> points.</summary>
        public static Transform Frame(string name, Vector3 from, Vector3 to, Vector3 facing, float thickness)
        {
            var t = new GameObject(name).transform;
            var down = to - from;
            float len = down.magnitude;
            var fwd = Vector3.ProjectOnPlane(facing, down.normalized);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(down, Vector3.right);
            t.SetPositionAndRotation(from, Quaternion.LookRotation(fwd.normalized, down / Mathf.Max(len, 1e-5f)));
            t.localScale = new Vector3(len * thickness, len, len * thickness);
            return t;
        }
    }
}
