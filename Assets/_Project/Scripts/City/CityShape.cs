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

        // long, low waves: the streets curve, but a block is skewed by only a few degrees (it still reads as a square from above)
        private static float Dx(float z) => 14f * Mathf.Sin(z / 78f + 0.7f) + 3f * Mathf.Sin(z / 33f + 2.1f);
        private static float Dz(float x) => 12f * Mathf.Sin(x / 84f + 1.3f) + 3f * Mathf.Sin(x / 36f + 0.4f);

        /// <summary>The lake shore stays a straight quay: east-west waves fade out toward it.</summary>
        private static float ShoreFade(float z) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((CityLayout.MaxZ - z) / 50f));

/// <summary>How much of Gökkent's stronger, wandering bend applies (0 in the town, 1 in Gökkent).</summary>
        private static float MegaBlend(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - (CityLayout.MegaMinX - 120f)) / 120f));

        /// <summary>The bend at a plan point: long waves everywhere, and in Gökkent stronger ones that wander across the blocks.</summary>
        /// <summary>Straight streets: the bent ones let plots spill onto the roads (user wish).</summary>
        public const bool Straight = true;

        private static Vector2 Offset(Vector2 p)
        {
            if (Straight) return Vector2.zero;
            float b = MegaBlend(p.x);
            float ox = Dx(p.y) * (1f + 0.9f * b) + b * 8f * Mathf.Sin(p.y / 43f + p.x / 160f);
            float oz = Dz(p.x) * ShoreFade(p.y) * (1f + 0.9f * b) + b * 7f * Mathf.Sin(p.x / 51f + 1.1f + p.y / 190f);
            return new Vector2(ox, oz);
        }

        public static Vector2 Warp(Vector2 p) => p + Offset(p);

        /// <summary>The plan point under a bent point (a few fixed-point steps; the bend is gentle).</summary>
        public static Vector2 ToPlan(Vector2 w)
        {
            var p = w;
            for (int i = 0; i < 8; i++) p = w - Offset(p);
            return p;
        }

        /// <summary>How much the plan's x axis is turned at a point, as a yaw in degrees (Unity: positive turns +x toward -z).</summary>
        public static float Angle(Vector2 p)
        {
            // the mean turn of the plan's two axes (the bend skews them a little apart): squares sit straight in their block
            var a = Warp(p);
            var ex = Warp(p + new Vector2(1f, 0f)) - a;
            var ez = Warp(p + new Vector2(0f, 1f)) - a;
            float ax = Mathf.Atan2(-ex.y, ex.x) * Mathf.Rad2Deg, az = Mathf.Atan2(ez.x, ez.y) * Mathf.Rad2Deg;
            return (ax + az) / 2f;
        }

        // ---------- the hills ----------

        public const bool Level = true;

        private static float Hill(float x, float z, float cx, float cz, float h, float sigma)
        {
            float d2 = (x - cx) * (x - cx) + (z - cz) * (z - cz);
            return h * Mathf.Exp(-d2 / (2f * sigma * sigma));
        }

        /// <summary>Ground height above the city's base at a plan point (0 by the lake and along the edges).</summary>
        public static float Height(Vector2 p)
        {
            if (Level) return 0f;   // the hills caused too many troubles (stairs over streets, sinking): the land is level now
            float x = p.x, z = p.y;
            float h = Hill(x, z, 100f, -100f, 12f, 58f) + Hill(x, z, -118f, -36f, 8f, 50f) + Hill(x, z, 128f, 50f, 4f, 36f)
                      + 5f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((-40f - z) / 120f));                   // rising toward the forest
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
            var uv = new Vector2[outV.Count];
            var idx = new int[outV.Count];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = outV[i];
                verts[i] = Point(new Vector2(v.x, v.z), v.y);
                uv[i] = new Vector2(v.x, v.z) * 0.08f;                 // plan-aligned texture coordinates (the water's ripples)
                idx[i] = i;
            }
            var mesh = new Mesh { name = m.name + " bent", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = idx;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Flat(Vector3 d) => d.x * d.x + d.z * d.z;
    }
}
