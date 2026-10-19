using System.Collections.Generic;
using UnityEngine;

namespace BrickMuseum.Build
{
    /// <summary>Small procedural mesh builder: boxes, cylinders/cones, extruded outlines, half spheres, quads.</summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> tris = new List<int>();

        /// <summary>Transform applied to everything added afterwards.</summary>
        public Matrix4x4 Xf = Matrix4x4.identity;

        private int V(Vector3 p, Vector3 n, Vector2 uv)
        {
            verts.Add(Xf.MultiplyPoint3x4(p));
            normals.Add(Xf.MultiplyVector(n).normalized);
            uvs.Add(uv);
            return verts.Count - 1;
        }

        /// <summary>Adds a triangle facing the way its vertex normals point (Unity: clockwise = front).</summary>
        private void T(int a, int b, int c)
        {
            var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, normals[a] + normals[b] + normals[c]) < 0) { tris.Add(a); tris.Add(c); tris.Add(b); }
            else { tris.Add(a); tris.Add(b); tris.Add(c); }
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int i = V(a, n, new Vector2(0, 0)); V(b, n, new Vector2(1, 0)); V(c, n, new Vector2(1, 1)); V(d, n, new Vector2(0, 1));
            T(i, i + 2, i + 1);
            T(i, i + 3, i + 2);
        }

        public void Box(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f, c = center;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z), p010 = c + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z), p011 = c + new Vector3(-h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, Vector3.back);
            Quad(p101, p111, p011, p001, Vector3.forward);
            Quad(p001, p011, p010, p000, Vector3.left);
            Quad(p100, p110, p111, p101, Vector3.right);
            Quad(p010, p011, p111, p110, Vector3.up);
            Quad(p001, p000, p100, p101, Vector3.down);
        }

        /// <summary>Frustum along +y from base (bottom centre); rTop 0 makes a cone.</summary>
        public void Cylinder(Vector3 bottom, float rBottom, float rTop, float height, int segs, bool capTop = true, bool capBottom = true)
        {
            float slope = (rBottom - rTop) / Mathf.Max(0.0001f, height);
            int start = verts.Count;
            for (int i = 0; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var n = (dir + Vector3.up * slope).normalized;
                V(bottom + dir * rBottom, n, new Vector2(i / (float)segs, 0));
                V(bottom + dir * rTop + Vector3.up * height, n, new Vector2(i / (float)segs, 1));
            }
            for (int i = 0; i < segs; i++)
            {
                int a = start + i * 2;
                T(a, a + 1, a + 3);
                T(a, a + 3, a + 2);
            }
            if (capTop && rTop > 0.001f) Disc(bottom + Vector3.up * height, rTop, segs, true);
            if (capBottom) Disc(bottom, rBottom, segs, false);
        }

        private void Disc(Vector3 c, float r, int segs, bool up)
        {
            var n = up ? Vector3.up : Vector3.down;
            int centre = V(c, n, new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                V(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r, n, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < segs; i++)
            {
                if (up) { T(centre, centre + 2 + i, centre + 1 + i); }
                else { T(centre, centre + 1 + i, centre + 2 + i); }
            }
        }

        /// <summary>Upper half sphere scaled to height, bottom centre at c.</summary>
        public void Dome(Vector3 c, float r, float height, int segs)
        {
            int rings = segs / 2, start = verts.Count;
            float sy = height / r;
            for (int j = 0; j <= rings; j++)
            {
                float phi = j * Mathf.PI * 0.5f / rings;
                for (int i = 0; i <= segs; i++)
                {
                    float a = i * Mathf.PI * 2f / segs;
                    var unit = new Vector3(Mathf.Cos(a) * Mathf.Cos(phi), Mathf.Sin(phi), Mathf.Sin(a) * Mathf.Cos(phi));
                    var n = new Vector3(unit.x, unit.y / sy, unit.z).normalized;
                    V(c + new Vector3(unit.x * r, unit.y * height, unit.z * r), n, new Vector2(i / (float)segs, j / (float)rings));
                }
            }
            int row = segs + 1;
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < segs; i++)
                {
                    int a = start + j * row + i;
                    T(a, a + row, a + row + 1);
                    T(a, a + row + 1, a + 1);
                }
            Disc(c, r, segs, false);
        }

        /// <summary>A flat outline in the x/y plane (counter-clockwise), extruded from z0 to z1.</summary>
        public void Extrude(List<Vector2> outline, float z0, float z1)
        {
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                var e = (b - a).normalized;
                var nrm = new Vector3(e.y, -e.x, 0); // outward for a counter-clockwise outline
                Quad(new Vector3(a.x, a.y, z1), new Vector3(b.x, b.y, z1), new Vector3(b.x, b.y, z0), new Vector3(a.x, a.y, z0), nrm);
            }
            var idx = Triangulate(outline);
            int f = verts.Count;
            foreach (var p in outline) V(new Vector3(p.x, p.y, z1), Vector3.forward, p);
            int bk = verts.Count;
            foreach (var p in outline) V(new Vector3(p.x, p.y, z0), Vector3.back, p);
            for (int i = 0; i < idx.Count; i += 3)
            {
                T(f + idx[i], f + idx[i + 2], f + idx[i + 1]);
                T(bk + idx[i], bk + idx[i + 1], bk + idx[i + 2]);
            }
        }

        /// <summary>A flat triangle turned to face the given direction (scenery: mountain ridges).</summary>
        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 towards)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, towards) < 0) n = -n;
            int i = V(a, n, new Vector2(a.x, a.z) * 0.01f); V(b, n, new Vector2(b.x, b.z) * 0.01f); V(c, n, new Vector2(c.x, c.z) * 0.01f);
            T(i, i + 1, i + 2);
        }

        public int VertexCount => verts.Count;

        /// <summary>A quad facing +z, centred at c; the texture reads correctly for someone looking at it from +z.</summary>
        public void Plane(Vector3 c, float w, float h)
        {
            Quad(c + new Vector3(w / 2, -h / 2, 0), c + new Vector3(-w / 2, -h / 2, 0), c + new Vector3(-w / 2, h / 2, 0), c + new Vector3(w / 2, h / 2, 0), Vector3.forward);
        }

        /// <summary>Ear clipping for a simple counter-clockwise polygon.</summary>
        private static List<int> Triangulate(List<Vector2> pts)
        {
            var result = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < pts.Count; i++) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[ia], b = pts[ib], c = pts[ic];
                    if (Cross(b - a, c - b) <= 1e-7f) continue; // reflex or flat
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (InTri(pts[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) idx.RemoveAt(0); // degenerate leftovers
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 > 0 && d2 > 0 && d3 > 0;
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetNormals(normals); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
