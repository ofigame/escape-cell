using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquashBot.Visual
{
    /// <summary>
    /// A stand of spectators along the floor's far right wall (beyond the east ramparts): stepped stone tiers running the
    /// whole length of the floor — more tiers on a floor of more stages — full of a cheering crowd in many colours, drawn
    /// instanced. They bob in their seats and jump up when foi fells a robot (more for vanG).
    /// </summary>
    public class FloorStand : MonoBehaviour
    {
        private class Batch { public Mesh mesh; public Material mat; public readonly List<Vector3> pos = new List<Vector3>(); public readonly List<float> phase = new List<float>(); public Matrix4x4[] m; public bool head; }

        private readonly List<Batch> batches = new List<Batch>();
        private readonly Matrix4x4[] chunk = new Matrix4x4[1023];
        private float excite;
        private static Mesh cube;

        /// <param name="x0">World x of the stand's first tier (just past the wall).</param>
        /// <param name="z0">World z where it starts (south) and <paramref name="z1"/> where it ends (north).</param>
        /// <param name="baseY">The first tier's height (over the wall's top, so the crowd shows above it).</param>
        public static FloorStand Build(Transform parent, float x0, float z0, float z1, float baseY, int tiers, Color stone, int seed)
            => Build(parent, x0, new List<Vector3> { new Vector3(z0, z1, baseY) }, tiers, stone, seed);

        /// <summary>A stand in sections along the floor (x: south end z, y: north end z, z: the first tier's height), each at the
        /// height of the floor beside it: on a floor of stages it climbs with them.</summary>
        public static FloorStand Build(Transform parent, float x0, List<Vector3> sections, int tiers, Color stone, int seed)
        {
            var go = new GameObject("FloorStand");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<FloorStand>();
            s.Make(x0, sections, tiers, stone, seed);
            return s;
        }

        /// <summary>The crowd cheers (0..1); it settles back by itself.</summary>
        public void Cheer(float strength) => excite = Mathf.Max(excite, strength);

        private static Mesh Cube()
        {
            if (cube != null) return cube;
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = g.GetComponent<MeshFilter>().sharedMesh;
            Destroy(g);
            return cube;
        }

        private void Make(float x0, List<Vector3> sections, int tiers, Color stone, int seed)
        {
            var rng = new System.Random(seed);
            const float step = 0.9f, rise = 0.5f;
            var light = MaterialFactory.CreateSurface(stone, Color.black, 0.15f, 0f);
            var dark = MaterialFactory.CreateSurface(stone * 0.72f + new Color(0f, 0f, 0f, 1f), Color.black, 0.1f, 0f);
            var flagCols = new[] { new Color(0.95f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 1f), new Color(1f, 0.78f, 0.25f), new Color(0.35f, 0.85f, 0.5f) };
            var flagMats = new Material[flagCols.Length];
            for (int i = 0; i < flagCols.Length; i++) flagMats[i] = MaterialFactory.Create(flagCols[i], Color.black);
            float ground = float.MaxValue;
            foreach (var sec in sections) ground = Mathf.Min(ground, sec.z);
            foreach (var sec in sections) MakeSection(x0, sec.x, sec.y, sec.z, ground, tiers, rng, step, rise, light, dark, flagMats);
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            // The crowd: many colours of shirts, four skins, one box body and one box head each.
            var shirts = new[] { new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.55f, 1f), new Color(1f, 0.8f, 0.3f), new Color(0.35f, 0.8f, 0.5f), new Color(0.7f, 0.45f, 0.95f), new Color(0.95f, 0.95f, 0.95f) };
            var skins = new[] { new Color(0.95f, 0.78f, 0.64f), new Color(0.85f, 0.64f, 0.48f), new Color(0.62f, 0.44f, 0.3f), new Color(0.4f, 0.28f, 0.2f) };
            Material Mat(Color c) { var m = MaterialFactory.Create(c, c * 0.06f); m.enableInstancing = true; return m; }
            var bodies = new Batch[shirts.Length];
            for (int i = 0; i < shirts.Length; i++) { bodies[i] = new Batch { mesh = Cube(), mat = Mat(shirts[i]) }; batches.Add(bodies[i]); }
            var heads = new Batch[skins.Length];
            for (int i = 0; i < skins.Length; i++) { heads[i] = new Batch { mesh = Cube(), mat = Mat(skins[i]), head = true }; batches.Add(heads[i]); }
            foreach (var sec in sections)
            {
                float z0 = sec.x, z1 = sec.y, baseY = sec.z;
                for (int i = 0; i < tiers; i++)
                {
                    float x = x0 + i * step + step * 0.45f, y = baseY + i * rise;
                    for (float z = z0 + 0.3f; z < z1 - 0.2f; z += 0.42f)
                    {
                        if (rng.NextDouble() < 0.12) continue; // an empty seat here and there
                        float ph = (float)rng.NextDouble() * 10f;
                        var b = bodies[rng.Next(bodies.Length)];
                        b.pos.Add(new Vector3(x, y + 0.18f, z)); b.phase.Add(ph);
                        var h = heads[rng.Next(heads.Length)];
                        h.pos.Add(new Vector3(x, y + 0.46f, z)); h.phase.Add(ph);
                    }
                }
            }
            foreach (var b in batches) b.m = new Matrix4x4[b.pos.Count];
            OwnedMaterials.OwnAllUnder(gameObject); // the tiers' and banners' materials, and the crowd's:
            foreach (var b in batches) OwnedMaterials.Own(gameObject, b.mat);
        }

        /// <summary>One section of the stand: its tiers, back wall with banners and the block under it.</summary>
        private void MakeSection(float x0, float z0, float z1, float baseY, float ground, int tiers, System.Random rng, float step, float rise, Material light, Material dark, Material[] flagMats)
        {
            float len = z1 - z0, zc = (z0 + z1) * 0.5f;
            // The tiers: each a seat slab and its riser, stepping up and away from the floor; a back wall with banners.
            for (int i = 0; i < tiers; i++)
            {
                float x = x0 + i * step, y = baseY + i * rise;
                Shapes.Rounded("Seat", transform, new Vector3(x + step * 0.5f, y - 0.05f, zc), new Vector3(step, 0.1f, len), 0.02f, light);
                Shapes.Rounded("Riser", transform, new Vector3(x + step * 0.5f, y - rise * 0.5f - 0.1f, zc), new Vector3(step, rise, len), 0.02f, dark);
            }
            float backX = x0 + tiers * step, topY = baseY + tiers * rise;
            Shapes.Rounded("Back", transform, new Vector3(backX + 0.2f, topY + 0.6f - (topY - baseY) * 0.5f, zc), new Vector3(0.4f, topY - baseY + 1.6f, len + 0.4f), 0.03f, dark);
            // Under it all down to the ground (below the lowest section too): a solid block, so it never floats.
            float bodyTop = baseY - 0.2f, bodyBottom = ground - 4.2f;
            Shapes.Rounded("Body", transform, new Vector3(x0 + tiers * step * 0.5f, (bodyTop + bodyBottom) * 0.5f, zc), new Vector3(tiers * step, bodyTop - bodyBottom, len), 0.02f, dark);
            for (float z = z0 + 1f; z < z1; z += 3.2f)
                Shapes.Rounded("Banner", transform, new Vector3(backX - 0.02f, topY + 0.7f, z), new Vector3(0.04f, 0.9f, 0.6f), 0.01f, flagMats[rng.Next(flagMats.Length)]);
        }

        private void LateUpdate()
        {
            float time = Time.time;
            excite = Mathf.MoveTowards(excite, 0f, Time.deltaTime * 0.3f);
            var face = Quaternion.Euler(0f, -90f, 0f); // facing the floor (west)
            var bodyScale = new Vector3(0.3f, 0.36f, 0.22f);
            var headScale = Vector3.one * 0.2f;
            foreach (var b in batches)
            {
                for (int i = 0; i < b.pos.Count; i++)
                {
                    float jump = Mathf.Max(0f, Mathf.Sin(time * (6f + excite * 4f) + b.phase[i])) * (0.02f + excite * 0.22f);
                    b.m[i] = Matrix4x4.TRS(b.pos[i] + Vector3.up * jump, face, b.head ? headScale : bodyScale);
                }
                for (int k = 0; k < b.m.Length; k += 1023)
                {
                    int n = Mathf.Min(1023, b.m.Length - k);
                    System.Array.Copy(b.m, k, chunk, 0, n);
                    Graphics.DrawMeshInstanced(b.mesh, 0, b.mat, chunk, n, null, ShadowCastingMode.Off, true);
                }
            }
        }
    }
}
