using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// foi thin or big by its parts (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 6). The body has no shapes of its own,
    /// so they are made here once per mesh: "thin" and "thick" for the trunk (chest, belly, shoulders) and for the arms,
    /// every point moved straight out from (or in to) the bone it rides — across the bone, never along it, so no pose,
    /// grip or animation changes; the fists grow every way round the wrist. The gloves on the arms grow with them. The
    /// push box and the hurt areas are measured on the body as made: they never grow with it (the collision rules).
    /// </summary>
    public static class BodyShape
    {
        private const string TorsoThin = "foi_torso_thin", TorsoThick = "foi_torso_thick", ArmsThin = "foi_arms_thin", ArmsThick = "foi_arms_thick";
        private const string FloorKey = "sb_body_floor";

        private static readonly Dictionary<Mesh, Mesh> shaped = new Dictionary<Mesh, Mesh>();
        /// <summary>The gloves' own across-to-along share as fitted (they are fitted again after every change of gear).</summary>
        private static readonly Dictionary<Transform, Vector3> gearRest = new Dictionary<Transform, Vector3>();

        private enum Group { None, Torso, Arm, Fist }

        /// <summary>
        /// The players who had the body before the bag came in keep at least today's thickness (they never shrink; their
        /// own screen only). Decided once: a player with any progress then is one of them.
        /// </summary>
        public static bool Floor
        {
            get
            {
                if (Prefs.HasKey(FloorKey)) return Prefs.GetInt(FloorKey, 0) == 1;
                bool old = RobotParts.Level(RobotPart.Body) + RobotParts.Level(RobotPart.Arms) + RobotParts.Level(RobotPart.Shell) > 0 || SaveData.UnlockedLevel > 1 || DuelLevel.Level > 1;
                Prefs.SetInt(FloorKey, old ? 1 : 0);
                Prefs.Save();
                return old;
            }
        }

        /// <summary>Shapes the body under <paramref name="skin"/> for these part levels (<paramref name="mine"/>: the
        /// player's own foi, with its floor).</summary>
        public static void Apply(Transform skin, int body, int arms, bool mine)
        {
            if (skin == null) return;
            var set = BodyShapeSettings.Current;
            float tb = set.Body(body), ta = set.Arms(arms);
            if (mine && Floor) { tb = Mathf.Max(tb, set.today); ta = Mathf.Max(ta, set.today); }
            float torso = set.Scale(tb, set.torsoThin, set.torsoThick);
            float arm = set.Scale(ta, set.armThin, set.armThick);
            float fist = set.Scale(ta, set.fistThin, set.fistThick);
            foreach (var sm in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = Shaped(sm);
                if (mesh == null) continue;
                if (sm.sharedMesh != mesh) sm.sharedMesh = mesh;
                Weight(sm, mesh, TorsoThin, torso < 1f ? (1f - torso) / (1f - set.torsoThin) : 0f);
                Weight(sm, mesh, TorsoThick, torso > 1f ? (torso - 1f) / (set.torsoThick - 1f) : 0f);
                Weight(sm, mesh, ArmsThin, arm < 1f ? (1f - arm) / (1f - set.armThin) : 0f);
                Weight(sm, mesh, ArmsThick, arm > 1f ? (arm - 1f) / (set.armThick - 1f) : 0f);
            }
            // the gloves: across with the arm, every way with the fist
            foreach (var t in skin.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Gear_")) continue;
                bool hand = t.name == "Gear_Hand";
                bool armGear = hand || t.name == "Gear_UpperArm" || t.name == "Gear_Forearm";
                if (!armGear) continue;
                if (!gearRest.TryGetValue(t, out var rest)) { rest = t.localScale; gearRest[t] = rest; }
                float k = hand ? fist : arm;
                t.localScale = hand ? rest * k : new Vector3(rest.x * k, rest.y, rest.z * k);
            }
            if (gearRest.Count > 64) { var dead = new List<Transform>(); foreach (var kv in gearRest) if (kv.Key == null) dead.Add(kv.Key); foreach (var d in dead) gearRest.Remove(d); }
        }

        private static void Weight(SkinnedMeshRenderer sm, Mesh mesh, string shape, float w)
        {
            int i = mesh.GetBlendShapeIndex(shape);
            if (i >= 0) sm.SetBlendShapeWeight(i, Mathf.Clamp01(w) * 100f);
        }

        private static Group GroupOf(string bone)
        {
            int colon = bone.LastIndexOf(':');
            if (colon >= 0) bone = bone.Substring(colon + 1);
            if (bone == "Spine" || bone == "Spine1" || bone == "Spine2" || bone == "LeftShoulder" || bone == "RightShoulder") return Group.Torso;
            if (bone == "LeftArm" || bone == "RightArm" || bone == "LeftForeArm" || bone == "RightForeArm") return Group.Arm;
            if (bone.StartsWith("LeftHand") || bone.StartsWith("RightHand")) return Group.Fist;
            return Group.None;
        }

        /// <summary>The mesh with its four shapes (made once per mesh; null: nothing of it rides the trunk or the arms).</summary>
        private static Mesh Shaped(SkinnedMeshRenderer sm)
        {
            var src = sm.sharedMesh;
            if (src == null) return null;
            if (src.GetBlendShapeIndex(TorsoThick) >= 0) return src; // (already shaped)
            if (shaped.TryGetValue(src, out var done)) return done;
            shaped[src] = null;
            var bones = sm.bones;
            if (!src.isReadable || bones == null || bones.Length == 0) return null;
            var verts = src.vertices;
            var weights = src.boneWeights;
            var binds = src.bindposes;
            if (weights.Length != verts.Length || binds.Length != bones.Length) return null;
            var set = BodyShapeSettings.Current;

            // each bone: its group, where it is in the mesh's own frame, where its child is (its axis), its side's wrist
            int n = bones.Length;
            var group = new Group[n];
            var at = new Vector3[n];
            var end = new Vector3[n];
            var wrist = new Vector3[n];
            bool any = false;
            for (int b = 0; b < n; b++)
            {
                if (bones[b] == null) continue;
                group[b] = GroupOf(bones[b].name);
                at[b] = binds[b].inverse.MultiplyPoint3x4(Vector3.zero);
                end[b] = at[b];
                // its axis: to its child (the spine's: the next of the spine or the neck, not a shoulder)
                int child = -1;
                for (int c = 0; c < n; c++)
                {
                    if (bones[c] == null || bones[c].parent != bones[b]) continue;
                    string cn = bones[c].name.Substring(bones[c].name.LastIndexOf(':') + 1);
                    if (child < 0 || cn.StartsWith("Spine") || cn == "Neck") child = c;
                }
                if (child >= 0) end[b] = binds[child].inverse.MultiplyPoint3x4(Vector3.zero);
                any |= group[b] != Group.None;
            }
            if (!any) return null;
            for (int b = 0; b < n; b++)
            {
                if (group[b] != Group.Fist) continue;
                wrist[b] = at[b];
                for (var p = bones[b]; p != null; p = p.parent)
                {
                    string nm = p.name.Substring(p.name.LastIndexOf(':') + 1);
                    if (nm == "LeftHand" || nm == "RightHand") { int i = System.Array.IndexOf(bones, p); if (i >= 0) wrist[b] = at[i]; break; }
                }
            }

            var torsoThin = new Vector3[verts.Length]; var torsoThick = new Vector3[verts.Length];
            var armThin = new Vector3[verts.Length]; var armThick = new Vector3[verts.Length];
            bool moved = false;
            for (int v = 0; v < verts.Length; v++)
            {
                var bw = weights[v];
                Add(bw.boneIndex0, bw.weight0); Add(bw.boneIndex1, bw.weight1); Add(bw.boneIndex2, bw.weight2); Add(bw.boneIndex3, bw.weight3);
                void Add(int b, float w)
                {
                    if (w <= 0f || b < 0 || b >= n || group[b] == Group.None) return;
                    var p = verts[v];
                    if (group[b] == Group.Fist)
                    {
                        var off = (p - wrist[b]) * w;
                        armThin[v] += off * (set.fistThin - 1f);
                        armThick[v] += off * (set.fistThick - 1f);
                    }
                    else
                    {
                        var out_ = (p - Closest(p, at[b], end[b])) * w; // (straight out from the bone, across it)
                        if (group[b] == Group.Torso) { torsoThin[v] += out_ * (set.torsoThin - 1f); torsoThick[v] += out_ * (set.torsoThick - 1f); }
                        else { armThin[v] += out_ * (set.armThin - 1f); armThick[v] += out_ * (set.armThick - 1f); }
                    }
                    moved = true;
                }
            }
            if (!moved) return null;
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + "_shaped";
            mesh.AddBlendShapeFrame(TorsoThin, 100f, torsoThin, null, null);
            mesh.AddBlendShapeFrame(TorsoThick, 100f, torsoThick, null, null);
            mesh.AddBlendShapeFrame(ArmsThin, 100f, armThin, null, null);
            mesh.AddBlendShapeFrame(ArmsThick, 100f, armThick, null, null);
            shaped[src] = mesh;
            return mesh;
        }

        private static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float len = ab.sqrMagnitude;
            if (len < 1e-10f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
        }
    }
}
