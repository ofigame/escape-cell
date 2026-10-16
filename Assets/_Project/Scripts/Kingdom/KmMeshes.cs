using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Kingdom
{
    /// <summary>
    /// Low-poly meshes for the kingdom, where things come by the thousand (trees) or need a shape the game's boxes and
    /// balls lack (cones for roofs and pines). Each is a few dozen triangles; trees carry two parts (trunk, crown) as
    /// two submeshes so one draw call colours each.
    /// </summary>
    public static class KmMeshes
    {
        private class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<int>[] sub = { new List<int>(), new List<int>() };

            /// <summary>A cone (or a cylinder when the top is as wide): flat-shaded sides, a bottom cap.</summary>
            public void Cone(int part, int sides, float r0, float r1, float y0, float y1, float twist = 0f)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = (i + twist) / sides * Mathf.PI * 2f, a1 = (i + 1 + twist) / sides * Mathf.PI * 2f;
                    var b0 = new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0);
                    var b1 = new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0);
                    var t0 = new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1);
                    var t1 = new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1);
                    var normal = Vector3.Cross(t0 - b0, b1 - b0).normalized;
                    Quad(part, b0, t0, t1, b1, normal);
                }
                // The bottom.
                var c = new Vector3(0f, y0, 0f);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = (i + twist) / sides * Mathf.PI * 2f, a1 = (i + 1 + twist) / sides * Mathf.PI * 2f;
                    Tri(part, c, new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0), new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0), Vector3.down);
                }
            }

            /// <summary>A ball made of a once-split icosahedron (80 faces), smooth-shaded.</summary>
            public void Ball(int part, Vector3 centre, Vector3 radius)
            {
                float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
                var p = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                for (int i = 0; i < p.Count; i++) p[i] = p[i].normalized;
                int[] f =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                for (int i = 0; i < f.Length; i += 3)
                {
                    Vector3 a = p[f[i]], b = p[f[i + 1]], c = p[f[i + 2]];
                    Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                    foreach (var (x, y, z) in new[] { (a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca) })
                    {
                        int s = v.Count;
                        v.Add(centre + Vector3.Scale(x, radius)); n.Add(x);
                        v.Add(centre + Vector3.Scale(y, radius)); n.Add(y);
                        v.Add(centre + Vector3.Scale(z, radius)); n.Add(z);
                        sub[part].Add(s); sub[part].Add(s + 1); sub[part].Add(s + 2);
                    }
                }
            }

            public void Quad(int part, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int s = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int i = 0; i < 4; i++) n.Add(normal);
                sub[part].AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            public void Tri(int part, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                int s = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                for (int i = 0; i < 3; i++) n.Add(normal);
                sub[part].AddRange(new[] { s, s + 1, s + 2 });
            }

            public Mesh Make(string name, int parts)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v);
                m.SetNormals(n);
                m.subMeshCount = parts;
                for (int i = 0; i < parts; i++) m.SetTriangles(sub[i], i);
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>A pine: a short trunk and two stacked cones (≈ 50 triangles). Submesh 0 trunk, 1 needles.</summary>
        public static Mesh Pine()
        {
            var b = new Builder();
            b.Cone(0, 5, 0.16f, 0.12f, 0f, 1.0f);
            b.Cone(1, 7, 1.15f, 0.05f, 0.8f, 2.6f);
            b.Cone(1, 7, 0.85f, 0.02f, 1.9f, 3.6f, 0.5f);
            return b.Make("KmPine", 2);
        }

        /// <summary>A leafy tree: a trunk and a round crown with a smaller one beside it. Submesh 0 trunk, 1 leaves.</summary>
        public static Mesh RoundTree()
        {
            var b = new Builder();
            b.Cone(0, 5, 0.2f, 0.14f, 0f, 1.6f);
            b.Ball(1, new Vector3(0f, 2.3f, 0f), new Vector3(1.25f, 1.1f, 1.25f));
            b.Ball(1, new Vector3(0.6f, 2.9f, 0.3f), new Vector3(0.7f, 0.65f, 0.7f));
            return b.Make("KmRoundTree", 2);
        }

        private static Mesh cone;

        /// <summary>An eight-sided cone 1 wide and 1 tall standing on its base (roofs, tower tops), scaled where used.</summary>
        public static Mesh Cone()
        {
            if (cone != null) return cone;
            var b = new Builder();
            b.Cone(0, 8, 0.5f, 0f, 0f, 1f, 0.5f);
            cone = b.Make("KmCone", 1);
            return cone;
        }
    }
}
