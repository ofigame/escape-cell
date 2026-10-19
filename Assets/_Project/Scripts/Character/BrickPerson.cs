using BrickMuseum.Build;
using BrickMuseum.Data;
using UnityEngine;

namespace BrickMuseum.Character
{
    /// <summary>
    /// A person built from our own bricks with human proportions (not a toy figure): shoe and leg columns, a 3×2 brick
    /// torso, round-brick arms with round hands, a neck, a 2×2 head with a face sticker and a domed hairdo. It is built at
    /// brick scale (~8.4 tall) and shrunk to ~2.4 studs so it matches the cars and houses. Arms swing from the shoulders,
    /// legs from the hips, opposite to each other.
    /// </summary>
    public sealed class BrickPerson
    {
        public const float BuiltHeight = 8.4f, Height = 2.0f, Scale = Height / BuiltHeight; // ~1.8 m (1 stud ≈ 0.9 m)
        private const float P = Piece.Plate;
        private const float Hip = 3.6f, Shoulder = 5.8f;

        public readonly Transform root;
        private readonly Transform body, legL, legR, armL, armR, head;
        private float phase, swing, bob;

        /// <param name="height">2.0 for a grown-up, less for a child.</param>
        /// <param name="merge">Townspeople: each limb becomes one mesh (fewer objects for a crowd).</param>
        public BrickPerson(Transform parent, Outfit o, float height = Height, bool merge = false)
        {
            root = new GameObject("BrickPerson").transform;
            root.SetParent(parent, false);
            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localScale = Vector3.one * (height / BuiltHeight);

            legL = Pivot(body, "LegL", new Vector3(-0.75f, Hip, 0));
            legR = Pivot(body, "LegR", new Vector3(0.75f, Hip, 0));
            foreach (var leg in new[] { legL, legR })
            {
                Part(leg, "brick", o.shoes, 0f - Hip, 1, 2, 2, new Vector3(0, 0, 0.35f)); // shoe, a bit longer to the front
                Part(leg, "brick", o.pants, 2 * P - Hip, 1, 1, 3);
                Part(leg, "brick", o.pants, 5 * P - Hip, 1, 1, 3);
                Part(leg, "brick", o.pants, 8 * P - Hip, 1, 1, 1);
            }
            // torso: a belt plate and shirt bricks; a stripe or a tie on top of the plain shirt
            Part(body, "brick", o.pants, Hip, 3, 2, 1);
            Part(body, "brick", o.shirt, Hip + P, 3, 2, 3);
            if (o.pattern == "stripe")
            {
                Part(body, "brick", o.trim != "" ? o.trim : o.shirt == "white" ? "red" : "white", Hip + 4 * P, 3, 2, 1);
                Part(body, "brick", o.shirt, Hip + 5 * P, 3, 2, 1);
            }
            else Part(body, "brick", o.shirt, Hip + 4 * P, 3, 2, 2);
            if (o.pattern == "tie") Part(body, "tile", o.trim != "" ? o.trim : o.shirt == "red" ? "dblue" : "red", Hip + P, 1, 1, 4, new Vector3(0, 0, 0.62f));
            armL = Pivot(body, "ArmL", new Vector3(-2f, Shoulder, 0));
            armR = Pivot(body, "ArmR", new Vector3(2f, Shoulder, 0));
            foreach (var arm in new[] { armL, armR })
            {
                Part(arm, "round", o.shirt, -3 * P, 1, 1, 3);
                Part(arm, "round", o.shirt, -6 * P, 1, 1, 3);
                Part(arm, "round", o.skin, -8 * P, 1, 1, 2); // hand
            }
            head = Pivot(body, "Head", new Vector3(0, Hip + 6 * P, 0));
            Part(head, "round", o.skin, 0f, 1, 1, 1);                    // neck
            Part(head, "brick", o.skin, P, 2, 2, 3);
            BuildHair(o);
            BuildHat(o);
            var face = PieceFactory.Create(Piece.Make("sticker", "white", 0, 0, 0, 2, 1, 0, "S", "face"), PieceLook.Solid, head);
            face.transform.localPosition = new Vector3(0, P, 1.5f);
            if (merge) foreach (var t in new[] { body, legL, legR, armL, armR, head }) Merge(t, new[] { body, legL, legR, armL, armR, head });
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>Joins the plain pieces hanging under one pivot into a single mesh (one sub-mesh per colour).</summary>
        private static void Merge(Transform pivot, Transform[] pivots)
        {
            var groups = new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>();
            var inv = pivot.worldToLocalMatrix;
            foreach (var mf in pivot.GetComponentsInChildren<MeshFilter>())
            {
                var t = mf.transform;
                if (t.childCount > 0 || t.GetComponentInParent<TMPro.TMP_Text>() != null) continue;   // stickers keep their text
                var owner = t.parent;
                while (owner != null && System.Array.IndexOf(pivots, owner) < 0) owner = owner.parent;
                if (owner != pivot) continue;
                var mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
                if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new System.Collections.Generic.List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = inv * t.localToWorldMatrix });
                Object.Destroy(t.gameObject);
                t.gameObject.SetActive(false);
            }
            if (groups.Count == 0) return;
            var subs = new System.Collections.Generic.List<CombineInstance>();
            var mats = new System.Collections.Generic.List<Material>();
            foreach (var kv in groups)
            {
                var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                subs.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
                mats.Add(kv.Key);
            }
            var whole = new Mesh { name = "person", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            whole.CombineMeshes(subs.ToArray(), false, true);
            foreach (var c in subs) Object.Destroy(c.mesh);
            var go = new GameObject("Merged");
            go.transform.SetParent(pivot, false);
            go.AddComponent<MeshFilter>().sharedMesh = whole;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
        }

        private void BuildHair(Outfit o)
        {
            if (o.hairStyle == "bald") return;
            if (o.hat == "none") Part(head, "dome", o.hair, 4 * P, 2, 2, 2);
            Part(head, "brick", o.hair, P, 2, 1, 3, new Vector3(0, 0, -1.3f));                // down the back of the head
            if (o.hairStyle == "long") Part(head, "brick", o.hair, -2 * P, 2, 1, 3, new Vector3(0, 0, -1.5f));
            if (o.hairStyle == "bangs") Part(head, "tile", o.hair, 3 * P, 2, 1, 1, new Vector3(0, 0, 0.75f));
        }

        private void BuildHat(Outfit o)
        {
            switch (o.hat)
            {
                case "cap":
                    string cap = o.hatColor != "" ? o.hatColor : "red";
                    Part(head, "round", cap, 4 * P, 2, 2, 1);
                    Part(head, "dome", cap, 5 * P, 2, 2, 1);
                    Part(head, "tile", cap, 4 * P, 2, 1, 1, new Vector3(0, 0, 1.3f));         // the visor
                    break;
                case "helmet":
                    string helmet = o.hatColor != "" ? o.hatColor : "yellow";
                    Part(head, "round", helmet, 4 * P, 3, 3, 1);                                // the brim
                    Part(head, "dome", helmet, 5 * P, 2, 2, 3);
                    break;
                case "top":
                    Part(head, "round", "black", 4 * P, 3, 3, 1);
                    Part(head, "round", "black", 5 * P, 2, 2, 5);
                    break;
                case "crown":
                    Part(head, "round", "gold", 4 * P, 2, 2, 1);
                    foreach (var (x, z) in new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (-0.5f, 0.5f), (0.5f, 0.5f) }) Part(head, "cone", "gold", 5 * P, 1, 1, 2, new Vector3(x, 0, z));
                    break;
            }
        }

        private static Transform Pivot(Transform parent, string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        /// <summary>One piece under a pivot; y = its bottom relative to the pivot.</summary>
        private static void Part(Transform parent, string type, string color, float y, int w, int d, int h, Vector3 offset = default)
        {
            var go = PieceFactory.Create(Piece.Make(type, color, 0, 0, 0, w, d, h), PieceLook.Solid, parent);
            go.transform.localPosition = new Vector3(offset.x, y, offset.z);
        }

        /// <summary>
        /// Moves the limbs. speed = how fast the person is moving (studs/s), airborne = jumping or falling.
        /// Walking swings arms and legs opposite to each other; faster = bigger, quicker steps; standing breathes.
        /// </summary>
        /// <summary>Holds the right arm up and forward (a fishing rod in hand), after <see cref="Animate"/>.</summary>
        public void HoldRightArm(float pitch) => armR.localRotation = Quaternion.Euler(-pitch, 0, 6f);

        /// <summary>Sitting: thighs forward, hands on the knees (after <see cref="Animate"/>).</summary>
        public void SitPose()
        {
            legL.localRotation = Quaternion.Euler(-88f, 0, 0);
            legR.localRotation = Quaternion.Euler(-88f, 0, 0);
            armL.localRotation = Quaternion.Euler(-35f, 0, -4f);
            armR.localRotation = Quaternion.Euler(-35f, 0, 4f);
        }

        /// <summary>The hips' height above the feet (where a seat meets them).</summary>
        public const float HipHeight = Hip * Scale;

        public void Animate(float speed, bool airborne, float dt)
        {
            float target = airborne ? 0.45f : Mathf.Clamp01(speed / 7f);
            swing = Mathf.MoveTowards(swing, target, dt * 4f);
            if (!airborne) phase += dt * Mathf.Lerp(6f, 13f, Mathf.Clamp01(speed / 7f)) * (speed > 0.1f ? 1f : 0f);
            float s = Mathf.Sin(phase);
            float legAngle = airborne ? 28f : s * Mathf.Lerp(0f, 42f, swing);
            float armAngle = airborne ? -60f : s * Mathf.Lerp(0f, 38f, swing);
            legL.localRotation = Quaternion.Euler(legAngle, 0, 0);
            legR.localRotation = Quaternion.Euler(airborne ? -12f : -legAngle, 0, 0);
            armL.localRotation = Quaternion.Euler(-armAngle, 0, airborne ? -18f : -4f);
            armR.localRotation = Quaternion.Euler(armAngle, 0, airborne ? 18f : 4f);
            // standing: a slow breath; moving: a little bounce on each step
            bob += dt;
            float lift = speed > 0.1f && !airborne ? Mathf.Abs(Mathf.Cos(phase)) * 0.25f * swing : Mathf.Sin(bob * 2f) * 0.04f;
            body.localPosition = new Vector3(0, lift * Scale, 0);
            head.localRotation = Quaternion.Euler(speed > 0.1f ? 4f * swing : Mathf.Sin(bob * 0.7f) * 3f, 0, 0);
        }
    }
}
