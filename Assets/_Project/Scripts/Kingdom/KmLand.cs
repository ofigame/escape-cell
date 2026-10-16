using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquashBot.Kingdom
{
    /// <summary>
    /// The kingdom's land: gentle green hills over 7 × 7 regions, the kingdom's own regions bright, the ones still to
    /// be won dimmed. Woods grow in patches (two kinds of tree, drawn by the GPU in batches per region, only the regions
    /// the camera sees). Far from the game's own scene.
    /// </summary>
    public class KmLand : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(-20000f, 0f, 20000f);
        private const int Verts = 25, Tex = 96;

        /// <summary>A tree that stands (map units), its kind and how big; the economy stage chops them.</summary>
        public struct Tree { public Vector2 at; public float size, turn; public byte kind; }

        private class Patch
        {
            public Vector2Int region;
            public bool open;
            public readonly List<Tree> trees = new List<Tree>();
            public Matrix4x4[][] matrices = new Matrix4x4[2][];
            public Bounds bounds;
        }

        private readonly List<Patch> patches = new List<Patch>();
        private readonly List<Material> mats = new List<Material>();
        private readonly List<Object> owned = new List<Object>();
        private Mesh[] treeMesh;
        private Material trunk, pine, leaf, trunkDim, pineDim, leafDim;
        private Camera cam;
        private readonly Plane[] planes = new Plane[6];

        public static KmLand Create(Transform parent, Camera cam)
        {
            var go = new GameObject("KmLand");
            go.transform.SetParent(parent, false);
            go.transform.position = Origin;
            var land = go.AddComponent<KmLand>();
            land.cam = cam;
            land.Build();
            return land;
        }

        private Material M(Color c, bool instanced = false)
        {
            var m = MaterialFactory.Create(c, Color.black);
            m.enableInstancing = instanced;
            mats.Add(m);
            return m;
        }

        // ---------- The land's shape and colour ----------

        /// <summary>The ground's height (map units): rolling hills, levelled round the palace.</summary>
        public static float Height(float x, float z)
        {
            float h = Mathf.PerlinNoise(x * 0.012f + 31f, z * 0.012f + 7f) * 7f + Mathf.PerlinNoise(x * 0.04f + 3f, z * 0.04f + 50f) * 1.4f;
            var mid = KmRegions.Centre(KmRegions.Start);
            float d = Vector2.Distance(new Vector2(x, z), mid);
            float flat = Mathf.PerlinNoise(mid.x * 0.012f + 31f, mid.y * 0.012f + 7f) * 7f + Mathf.PerlinNoise(mid.x * 0.04f + 3f, mid.y * 0.04f + 50f) * 1.4f;
            return Mathf.Lerp(flat, h, Mathf.SmoothStep(0f, 1f, (d - 18f) / 14f)) - 2f;
        }

        public static Vector3 World(float x, float z) => Origin + new Vector3(x, Height(x, z), z);
        public static Vector3 World(Vector2 p) => World(p.x, p.y);

        private static float Hash(float x, float z)
        {
            float h = Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Woods grow where this is high.</summary>
        private static float Woods(float x, float z) => Mathf.PerlinNoise(x * 0.025f + 90f, z * 0.025f + 12f);

        private static Color Ground(float x, float z, bool open)
        {
            float big = Mathf.PerlinNoise(x * 0.015f + 7f, z * 0.015f + 3f);
            var c = Color.Lerp(new Color(0.34f, 0.56f, 0.22f), new Color(0.52f, 0.62f, 0.27f), Mathf.SmoothStep(0.3f, 0.75f, big));
            c *= 1f + (Mathf.PerlinNoise(x * 0.1f + 13f, z * 0.1f + 4f) - 0.5f) * 0.16f;
            if (Mathf.PerlinNoise(x * 0.24f + 1f, z * 0.24f + 9f) > 0.7f) c = Color.Lerp(c, new Color(0.25f, 0.46f, 0.19f), 0.5f);
            if (Woods(x, z) > 0.58f) c = Color.Lerp(c, new Color(0.26f, 0.36f, 0.16f), 0.45f); // the woods' darker floor
            float grain = (Hash(Mathf.Floor(x * 2f), Mathf.Floor(z * 2f)) - 0.5f) * 0.07f;
            c = new Color(c.r + grain, c.g + grain, c.b + grain * 0.6f);
            if (!open)
            {
                // Still to be won: greyed and darkened (the kingdom's own land stands out).
                float grey = c.grayscale;
                c = Color.Lerp(c, new Color(grey, grey, grey), 0.55f) * 0.55f;
            }
            return c;
        }

        // ---------- Building ----------

        private void Build()
        {
            trunk = M(new Color(0.42f, 0.28f, 0.17f), true);
            pine = M(new Color(0.16f, 0.36f, 0.2f), true);
            leaf = M(new Color(0.27f, 0.52f, 0.22f), true);
            trunkDim = M(new Color(0.2f, 0.17f, 0.14f), true);
            pineDim = M(new Color(0.12f, 0.17f, 0.13f), true);
            leafDim = M(new Color(0.17f, 0.22f, 0.15f), true);
            treeMesh = new[] { KmMeshes.Pine(), KmMeshes.RoundTree() };
            owned.Add(treeMesh[0]);
            owned.Add(treeMesh[1]);
            for (int x = 0; x < KmRegions.N; x++)
                for (int y = 0; y < KmRegions.N; y++)
                    BuildRegion(new Vector2Int(x, y));
        }

        private void BuildRegion(Vector2Int r)
        {
            bool open = KmRegions.IsOpen(r);
            float s = KmRegions.Size, x0 = r.x * s, z0 = r.y * s;
            float step = s / (Verts - 1);
            var verts = new Vector3[Verts * Verts];
            var uv = new Vector2[Verts * Verts];
            for (int z = 0; z < Verts; z++)
                for (int x = 0; x < Verts; x++)
                {
                    float wx = x0 + x * step, wz = z0 + z * step;
                    verts[z * Verts + x] = new Vector3(wx, Height(wx, wz), wz);
                    uv[z * Verts + x] = new Vector2(x / (float)(Verts - 1), z / (float)(Verts - 1));
                }
            var tris = new int[(Verts - 1) * (Verts - 1) * 6];
            int t = 0;
            for (int z = 0; z < Verts - 1; z++)
                for (int x = 0; x < Verts - 1; x++)
                {
                    int i = z * Verts + x;
                    tris[t++] = i; tris[t++] = i + Verts; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + Verts; tris[t++] = i + Verts + 1;
                }
            var mesh = new Mesh { name = "KmGround", vertices = verts, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            owned.Add(mesh);
            var colours = new Color32[Tex * Tex];
            for (int z = 0; z < Tex; z++)
                for (int x = 0; x < Tex; x++)
                    colours[z * Tex + x] = Ground(x0 + (x + 0.5f) / Tex * s, z0 + (z + 0.5f) / Tex * s, open);
            var tex = new Texture2D(Tex, Tex, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(colours);
            tex.Apply(true, true);
            owned.Add(tex);
            var mat = MaterialFactory.Create(Color.white, Color.black);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mats.Add(mat);
            var go = new GameObject("Ground " + r.x + "," + r.y);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            // The woods: a tree every few units where the land says so (kept off the palace's ground).
            var p = new Patch { region = r, open = open };
            var rng = new System.Random(r.x * 131 + r.y * 17 + 3);
            var mid = KmRegions.Centre(KmRegions.Start);
            for (float z = z0 + 1.5f; z < z0 + s; z += 2.6f)
                for (float x = x0 + 1.5f; x < x0 + s; x += 2.6f)
                {
                    float jx = x + (float)rng.NextDouble() * 2f - 1f, jz = z + (float)rng.NextDouble() * 2f - 1f;
                    float w = Woods(jx, jz);
                    bool lone = rng.NextDouble() < 0.012;
                    if (w < 0.58f && !lone) continue;
                    if (Vector2.Distance(new Vector2(jx, jz), mid) < 22f) continue;
                    p.trees.Add(new Tree { at = new Vector2(jx, jz), size = 0.8f + (float)rng.NextDouble() * 0.5f, turn = (float)rng.NextDouble() * 360f, kind = (byte)(w > 0.66f || rng.NextDouble() < 0.4 ? 0 : 1) });
                }
            p.bounds = new Bounds(Origin + new Vector3(x0 + s * 0.5f, 4f, z0 + s * 0.5f), new Vector3(s + 6f, 20f, s + 6f));
            Refresh(p);
            patches.Add(p);
        }

        /// <summary>Lays a region's trees out for drawing again (after one is felled).</summary>
        private static void Refresh(Patch p)
        {
            var lists = new[] { new List<Matrix4x4>(), new List<Matrix4x4>() };
            foreach (var t in p.trees)
                lists[t.kind].Add(Matrix4x4.TRS(World(t.at) - Vector3.up * 0.1f, Quaternion.Euler(0f, t.turn, 0f), Vector3.one * t.size));
            p.matrices[0] = lists[0].ToArray();
            p.matrices[1] = lists[1].ToArray();
        }

        // ---------- Drawing ----------

        private void LateUpdate()
        {
            if (cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            foreach (var p in patches)
            {
                if (!GeometryUtility.TestPlanesAABB(planes, p.bounds)) continue;
                for (int k = 0; k < 2; k++)
                {
                    var m = p.matrices[k];
                    if (m == null || m.Length == 0) continue;
                    var mesh = treeMesh[k];
                    var crown = k == 0 ? (p.open ? pine : pineDim) : (p.open ? leaf : leafDim);
                    var wood = p.open ? trunk : trunkDim;
                    for (int start = 0; start < m.Length; start += 1023)
                    {
                        int count = Mathf.Min(1023, m.Length - start);
                        var chunk = m;
                        if (start > 0 || count < m.Length) { chunk = new Matrix4x4[count]; System.Array.Copy(m, start, chunk, 0, count); }
                        Graphics.DrawMeshInstanced(mesh, 0, wood, chunk, count, null, ShadowCastingMode.Off, true, 0, cam);
                        Graphics.DrawMeshInstanced(mesh, 1, crown, chunk, count, null, ShadowCastingMode.Off, true, 0, cam);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var m in mats) if (m != null) Destroy(m);
            foreach (var o in owned) if (o != null) Destroy(o);
        }
    }
}
