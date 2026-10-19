using System.Collections.Generic;
using UnityEngine;

namespace BrickMuseum.City
{
    /// <summary>
    /// The lie of the land. The city is laid out on a straight "plan" (blocks, roads, slots, all in <see cref="CityLayout"/>)
    /// and then shaped: the plan is gently bent (<see cref="Warp"/>: north-south roads wave east and west, east-west roads
    /// north and south, so no street is straight and no block is a square) and lifted onto hills (<see cref="Height"/>:
    /// two hills, a rise toward the forest, flat by the lake and at the city's edges). Meshes are built on the plan and
    /// bent with <see cref="Bend"/>; points go through <see cref="Point"/>; the walker's world position comes back with
    /// <see cref="ToPlan"/>. All in the city root's local space (x, z relative to the city centre).
    /// </summary>
    public static class CityShape
    {
        // ---------- the bend ----------

        private static float Dx(float z) => 10f * Mathf.Sin(z / 57f + 0.7f) + 4f * Mathf.Sin(z / 23f + 2.1f);
        private static float Dz(float x) => 9f * Mathf.Sin(x / 63f + 1.3f) + 3.5f * Mathf.Sin(x / 29f + 0.4f);

        /// <summary>The lake shore stays a straight quay: east-west waves fade out toward it.</summary>
        private static float ShoreFade(float z) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((CityLayout.MaxZ - z) / 50f));

        public static Vector2 Warp(Vector2 p) => new Vector2(p.x + Dx(p.y), p.y + Dz(p.x) * ShoreFade(p.y));

        /// <summary>The plan point under a bent point (a few fixed-point steps; the bend is gentle).</summary>
        public static Vector2 ToPlan(Vector2 w)
        {
            var p = w;
            for (int i = 0; i < 5; i++) p = new Vector2(w.x - Dx(p.y), w.y - Dz(p.x) * ShoreFade(p.y));
            return p;
        }

        /// <summary>How much the plan's x axis is turned at a point, as a yaw in degrees (Unity: positive turns +x toward -z).</summary>
        public static float Angle(Vector2 p)
        {
            var a = Warp(p);
            var e = Warp(p + new Vector2(1f, 0f)) - a;
            return Mathf.Atan2(-e.y, e.x) * Mathf.Rad2Deg;
        }

        // ---------- the hills ----------

        private static float Hill(float x, float z, float cx, float cz, float h, float sigma)
        {
            float d2 = (x - cx) * (x - cx) + (z - cz) * (z - cz);
            return h * Mathf.Exp(-d2 / (2f * sigma * sigma));
        }

        /// <summary>Ground height above the city's base at a plan point (0 by the lake and along the edges).</summary>
        public static float Height(Vector2 p)
        {
            float x = p.x, z = p.y;
            float h = Hill(x, z, 100f, -100f, 15f, 52f) + Hill(x, z, -118f, -36f, 11f, 44f) + Hill(x, z, 128f, 50f, 5f, 32f)
                      + 6f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((-40f - z) / 120f));                   // rising toward the forest
            float edge = Mathf.Min(Mathf.Min(x - CityLayout.MinX, CityLayout.MaxX - x), Mathf.Min(z - CityLayout.MinZ, CityLayout.MaxZ - z));
            return h * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 40f));
        }

        /// <summary>Slope of the ground at a plan point: the up vector of a surface lying on it.</summary>
        public static Vector3 Normal(Vector2 p)
        {
            const float e = 1f;
            float hx = Height(p + new Vector2(e, 0)) - Height(p - new Vector2(e, 0));
            float hz = Height(p + new Vector2(0, e)) - Height(p - new Vector2(0, e));
            return new Vector3(-hx / (2f * e), 1f, -hz / (2f * e)).normalized;
        }

        /// <summary>A plan point at height y above the ground, bent and lifted: city-root local position.</summary>
        public static Vector3 Point(Vector2 p, float y)
        {
            var w = Warp(p);
            return new Vector3(w.x, y + Height(p), w.y);
        }

        // ---------- meshes ----------

        /// <summary>
        /// A mesh made on the plan, cut into small triangles (no edge longer than <paramref name="maxEdge"/> across the
        /// ground) so it can follow the bend and the hills, then bent and lifted. Flat shaded.
        /// </summary>
        public static Mesh Bend(Mesh m, float maxEdge = 5f)
        {
            var src = m.vertices;
            var tris = m.triangles;
            var outV = new List<Vector3>(tris.Length * 2);
            var stack = new Stack<(Vector3, Vector3, Vector3)>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                stack.Push((src[tris[i]], src[tris[i + 1]], src[tris[i + 2]]));
                while (stack.Count > 0)
                {
                    var (a, b, c) = stack.Pop();
                    float ab = Flat(a - b), bc = Flat(b - c), ca = Flat(c - a);
                    float longest = Mathf.Max(ab, Mathf.Max(bc, ca));
                    if (longest <= maxEdge * maxEdge) { outV.Add(a); outV.Add(b); outV.Add(c); continue; }
                    // split the longest edge in two, keeping the winding
                    if (longest == ab) { var mid = (a + b) / 2f; stack.Push((a, mid, c)); stack.Push((mid, b, c)); }
                    else if (longest == bc) { var mid = (b + c) / 2f; stack.Push((a, b, mid)); stack.Push((a, mid, c)); }
                    else { var mid = (c + a) / 2f; stack.Push((a, b, mid)); stack.Push((mid, b, c)); }
                }
            }
            var verts = new Vector3[outV.Count];
            var idx = new int[outV.Count];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = outV[i];
                verts[i] = Point(new Vector2(v.x, v.z), v.y);
                idx[i] = i;
            }
            var mesh = new Mesh { name = m.name + " bent", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.triangles = idx;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Flat(Vector3 d) => d.x * d.x + d.z * d.z;
    }
}
