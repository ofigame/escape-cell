using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquashBot.Visual
{
    /// <summary>
    /// What crowns the floors' walls from end to end, cheap enough for phones however long the walls: fires (little
    /// tongues of flame drawn all at once, instanced, only near the camera), ice swords (crystal blades standing up out
    /// of the wall tops, one merged mesh) or laser beams (thin light shafts rising to the sky, one merged mesh, pulsing).
    /// </summary>
    public class WallFxRenderer : MonoBehaviour
    {
        public enum Kind { Flames, Ice, Lasers }

        private Kind kind;
        private Color colour;
        private readonly List<Vector4> spots = new List<Vector4>(); // x, y, z, scale
        private Material hot, warm, beam;
        private static Mesh tongue;
        private readonly Matrix4x4[] hotBatch = new Matrix4x4[1023], warmBatch = new Matrix4x4[1023];

        /// <summary>A crown for the walls under <paramref name="root"/>: add its spots (<see cref="Add"/>), then <see cref="Finish"/>.</summary>
        public static WallFxRenderer Attach(Transform root, Kind kind, Color colour)
        {
            var r = new GameObject("WallFx " + kind).AddComponent<WallFxRenderer>();
            r.transform.SetParent(root, false);
            r.kind = kind;
            r.colour = colour;
            return r;
        }

        /// <summary>A spot on a wall top (world position) and how big the crown stands there.</summary>
        public void Add(Vector3 at, float scale) => spots.Add(new Vector4(at.x, at.y, at.z, scale));

        public void Finish()
        {
            if (tongue == null) tongue = Octahedron();
            switch (kind)
            {
                case Kind.Flames:
                    hot = Instanced(MaterialFactory.Create(new Color(1f, 0.85f, 0.4f), new Color(3f, 1.8f, 0.4f)));
                    warm = Instanced(MaterialFactory.Create(new Color(1f, 0.4f, 0.12f), new Color(2.6f, 0.6f, 0.08f)));
                    break;
                case Kind.Ice:
                    // Two or three blades of ice standing up at every spot, leaning a little each way.
                    Merge("IceSwords", MaterialFactory.CreateTransparent(new Color(0.75f, 0.92f, 1f, 0.8f), new Color(0.4f, 0.8f, 1.3f)), (list, s, i) =>
                    {
                        int n = 2 + i % 2;
                        for (int k = 0; k < n; k++)
                        {
                            float lean = (k - (n - 1) * 0.5f) * 14f;
                            var pos = new Vector3(s.x, s.y, s.z) + new Vector3((k - (n - 1) * 0.5f) * 0.22f, 0f, 0f);
                            float h = (0.55f + 0.25f * ((i + k) % 3)) * s.w;
                            list.Add(Matrix4x4.TRS(pos + Vector3.up * h * 0.5f, Quaternion.Euler(0f, i * 37f, lean), new Vector3(0.12f, h, 0.05f) * 1f));
                            list.Add(Matrix4x4.TRS(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, i * 37f, 0f), new Vector3(0.26f, 0.06f, 0.08f))); // the guard
                        }
                    });
                    break;
                case Kind.Lasers:
                    // Beams of light rising high into the sky from every other spot.
                    beam = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.45f), colour * 2.4f);
                    Merge("Lasers", beam, (list, s, i) =>
                    {
                        if (i % 2 != 0) return;
                        float h = 14f + (i % 5) * 3f;
                        list.Add(Matrix4x4.TRS(new Vector3(s.x, s.y + h * 0.5f, s.z), Quaternion.identity, new Vector3(0.07f, h, 0.07f)));
                        list.Add(Matrix4x4.TRS(new Vector3(s.x, s.y + 0.08f, s.z), Quaternion.identity, new Vector3(0.24f, 0.16f, 0.24f))); // the emitter
                    });
                    break;
            }
        }

        private static Material Instanced(Material m)
        {
            var copy = new Material(m) { enableInstancing = true };
            return copy;
        }

        /// <summary>Builds every spot's pieces (octahedra, placed by <paramref name="place"/>) into one mesh.</summary>
        private void Merge(string name, Material mat, System.Action<List<Matrix4x4>, Vector4, int> place)
        {
            var mats = new List<Matrix4x4>();
            for (int i = 0; i < spots.Count; i++) place(mats, spots[i], i);
            if (mats.Count == 0) return;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            var src = tongue.vertices;
            var srcN = tongue.normals;
            var srcT = tongue.triangles;
            foreach (var m in mats)
            {
                int start = verts.Count;
                for (int v = 0; v < src.Length; v++) { verts.Add(m.MultiplyPoint3x4(src[v])); normals.Add(m.MultiplyVector(srcN[v]).normalized); }
                foreach (int t in srcT) tris.Add(start + t);
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            // Placed in world space: undo any parent transform.
            if (transform.parent != null) { go.transform.SetParent(null, true); go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.SetParent(transform, true); }
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void Update()
        {
            if (kind == Kind.Lasers && beam != null)
            {
                float k = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
                MaterialFactory.SetColors(beam, new Color(colour.r, colour.g, colour.b, 0.3f + 0.2f * k), colour * (1.6f + 1.2f * k));
            }
            if (kind != Kind.Flames || hot == null || spots.Count == 0) return;
            var cam = Camera.main;
            if (cam == null || !cam.enabled) return;
            var eye = cam.transform.position;
            float reach = Mathf.Max(30f, cam.farClipPlane * 0.4f);
            float reach2 = reach * reach;
            int nh = 0, nw = 0;
            float time = Time.time;
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                float dx = s.x - eye.x, dz = s.z - eye.z;
                if (dx * dx + dz * dz > reach2) continue; // only the fires near the camera are drawn
                var at = new Vector3(s.x, s.y, s.z);
                for (int k = 0; k < 3; k++)
                {
                    float seed = (i * 0.618f + k * 0.37f) % 1f;
                    float t = Mathf.Repeat(time / 0.7f + seed, 1f);
                    float rise = t * 0.5f * s.w;
                    float size = (Mathf.Sin(t * Mathf.PI) * 1.1f + 0.1f) * 0.16f * s.w;
                    var off = new Vector3((k - 1) * 0.16f * s.w, rise, Mathf.Sin(time * 9f + seed * 20f) * 0.03f);
                    var m = Matrix4x4.TRS(at + off, Quaternion.Euler(0f, 45f, 0f), new Vector3(size, size * 1.6f, size));
                    if (k == 1) { hotBatch[nh++] = m; if (nh == hotBatch.Length) { Graphics.DrawMeshInstanced(tongue, 0, hot, hotBatch, nh, null, ShadowCastingMode.Off, false); nh = 0; } }
                    else { warmBatch[nw++] = m; if (nw == warmBatch.Length) { Graphics.DrawMeshInstanced(tongue, 0, warm, warmBatch, nw, null, ShadowCastingMode.Off, false); nw = 0; } }
                }
            }
            if (nh > 0) Graphics.DrawMeshInstanced(tongue, 0, hot, hotBatch, nh, null, ShadowCastingMode.Off, false);
            if (nw > 0) Graphics.DrawMeshInstanced(tongue, 0, warm, warmBatch, nw, null, ShadowCastingMode.Off, false);
        }

        /// <summary>A unit octahedron (a flame tongue, an ice blade, a beam: six points, eight faces).</summary>
        private static Mesh Octahedron()
        {
            var v = new[]
            {
                new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, -0.5f), new Vector3(0f, -0.5f, 0f),
            };
            int[] faces = { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
            // Flat faces: each triangle its own corners (crisp facets).
            var verts = new Vector3[faces.Length];
            var tris = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { verts[i] = v[faces[i]]; tris[i] = i; }
            var mesh = new Mesh { name = "Octahedron", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
