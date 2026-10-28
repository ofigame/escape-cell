using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The rank gear's bracers, pauldrons and kneepads (Docs/RUTBE_KUSAMI_TAC_SAC_BIRLESIK.md, section 2.2): stand-in plates
    /// of the tier's metal and trim until their models come, on the forearm over the wrist, the shoulder and the knee,
    /// each side. Named Gear_* so the battle wear reaches them too; they follow their bones.
    /// </summary>
    public static class RankGearParts
    {
        private const string Prefix = "Gear_Rank";
        private static readonly System.Collections.Generic.Dictionary<int, Material> metals = new System.Collections.Generic.Dictionary<int, Material>(), trims = new System.Collections.Generic.Dictionary<int, Material>();

        /// <summary>Tier <paramref name="tier"/>'s plates on body <paramref name="skin"/> (0: none).</summary>
        public static void Put(Transform skin, int tier)
        {
            if (skin == null) return;
            foreach (var t in skin.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith(Prefix)) Object.DestroyImmediate(t.gameObject);
            if (tier <= 0) return;
            var row = RankGearData.Current.Of(tier);
            if (!metals.TryGetValue(tier, out var metal) || metal == null) metals[tier] = metal = MaterialFactory.CreateSurface(row.metal, Color.black, 0.6f, row.metallic);
            if (!trims.TryGetValue(tier, out var trim) || trim == null) trims[tier] = trim = MaterialFactory.CreateSurface(row.trim, tier >= 6 ? RankGearData.Current.marshalGlow * 0.6f : Color.black, 0.7f, 0.5f);
            var facing = skin.forward;
            foreach (var side in new[] { "Left", "Right" })
            {
                var fore = Bone(skin, side + "ForeArm"); var hand = Bone(skin, side + "Hand");
                var arm = Bone(skin, side + "Arm");
                var leg = Bone(skin, side + "Leg"); var up = Bone(skin, side + "UpLeg"); var foot = Bone(skin, side + "Foot");
                if (fore != null && hand != null) Bracer(fore, fore.position, hand.position, metal, trim);
                if (arm != null && fore != null) Pauldron(arm, arm.position, fore.position, skin.up, metal, trim, side == "Left" ? -skin.right : skin.right);
                if (leg != null && up != null && foot != null) Kneepad(leg, leg.position, up.position, foot.position, facing, metal, trim);
            }
        }

        // a band round the forearm, over the wrist (clear of the grip)
        private static void Bracer(Transform bone, Vector3 from, Vector3 to, Material metal, Material trim)
        {
            float len = Vector3.Distance(from, to);
            var root = Frame(Prefix + "Bracer", Vector3.Lerp(from, to, 0.62f), to - from, bone);
            float w = len * 0.36f;
            Part(PrimitiveType.Cylinder, "Band", root, Vector3.zero, new Vector3(w, len * 0.17f, w), metal);
            Part(PrimitiveType.Cylinder, "Rim", root, Vector3.up * len * 0.17f, new Vector3(w * 1.06f, len * 0.025f, w * 1.06f), trim);
            Part(PrimitiveType.Cylinder, "Rim", root, -Vector3.up * len * 0.17f, new Vector3(w * 1.06f, len * 0.025f, w * 1.06f), trim);
        }

        // a rounded plate over the shoulder, a little out to the side
        private static void Pauldron(Transform bone, Vector3 from, Vector3 to, Vector3 up, Material metal, Material trim, Vector3 outward)
        {
            float len = Vector3.Distance(from, to);
            var root = new GameObject(Prefix + "Pauldron").transform;
            root.position = from + outward * len * 0.08f + up * len * 0.1f;
            root.rotation = Quaternion.LookRotation(outward, up);
            root.SetParent(bone, true);
            Part(PrimitiveType.Sphere, "Plate", root, Vector3.zero, new Vector3(len * 0.62f, len * 0.36f, len * 0.5f), metal);
            Part(PrimitiveType.Sphere, "Stud", root, new Vector3(0f, len * 0.12f, len * 0.22f), Vector3.one * len * 0.09f, trim);
        }

        // a plate on the front of the knee
        private static void Kneepad(Transform bone, Vector3 knee, Vector3 hip, Vector3 foot, Vector3 facing, Material metal, Material trim)
        {
            float len = Vector3.Distance(knee, foot);
            var along = (hip - foot).normalized;
            var fwd = Vector3.ProjectOnPlane(facing, along).normalized;
            var root = new GameObject(Prefix + "Knee").transform;
            root.position = knee + fwd * len * 0.11f;
            root.rotation = Quaternion.LookRotation(fwd, along);
            root.SetParent(bone, true);
            Part(PrimitiveType.Sphere, "Plate", root, Vector3.zero, new Vector3(len * 0.3f, len * 0.32f, len * 0.14f), metal);
            Part(PrimitiveType.Sphere, "Stud", root, new Vector3(0f, 0f, len * 0.06f), Vector3.one * len * 0.08f, trim);
        }

        private static Transform Frame(string name, Vector3 at, Vector3 along, Transform bone)
        {
            var root = new GameObject(name).transform;
            root.position = at;
            root.rotation = Quaternion.FromToRotation(Vector3.up, along.normalized);
            root.SetParent(bone, true);
            return root;
        }

        private static void Part(PrimitiveType type, string name, Transform parent, Vector3 at, Vector3 size, Material paint)
        {
            var go = Shapes.Primitive(type, name, parent, at, size, paint);
            var col = go.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
            var r = go.GetComponent<Renderer>(); if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Transform Bone(Transform root, string bone)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == bone || t.name.EndsWith(":" + bone)) return t;
            return null;
        }
    }
}
