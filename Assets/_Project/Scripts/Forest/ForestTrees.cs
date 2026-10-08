using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Forest
{
    /// <summary>
    /// Phone-friendly trees built in code and dressed in photo textures: a tapering bark trunk (submesh 0) and
    /// alpha-clipped foliage cards (submesh 1). Firs carry tiers of drooping needle sprays; broadleaf trees fork into a
    /// few limbs under a rounded crown of leaf cards whose normals point out of the crown, so it lights like a volume.
    /// A few variants of each are made once and shared by every tree in the forest.
    /// </summary>
    public static class ForestTrees
    {
        // The photo textures are atlases: each card shows one spray (u0, v0, u1, v1), its stem at v0.
        private static readonly Vector4[] FirSprays =
        {
            new Vector4(0.29f, 0.2f, 0.66f, 0.6f), new Vector4(0.63f, 0.17f, 0.98f, 0.57f), new Vector4(0.64f, 0.6f, 0.94f, 0.97f),
        };
        private static readonly Vector4 LeafCluster = new Vector4(0.0f, 0.05f, 0.44f, 0.95f);
        private static readonly Vector4 LeafTwig = new Vector4(0.55f, 0.33f, 0.86f, 0.96f);

        private static readonly List<Mesh> firs = new List<Mesh>();
        private static readonly List<Mesh> broadleaves = new List<Mesh>();

        public static Mesh Fir(int variant)
        {
            while (firs.Count < 3) firs.Add(BuildFir(firs.Count * 31 + 7));
            return firs[variant % firs.Count];
        }

        public static Mesh Broadleaf(int variant)
        {
            while (broadleaves.Count < 3) broadleaves.Add(BuildBroadleaf(broadleaves.Count * 17 + 3));
            return broadleaves[variant % broadleaves.Count];
        }

        private class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> bark = new List<int>();
            public readonly List<int> leaf = new List<int>();

            /// <summary>A tapering tube from a to b (radii ra → rb), bark UVs tiled along its length.</summary>
            public void Tube(Vector3 a, Vector3 b, float ra, float rb, int sides, float vStart)
            {
                var axis = (b - a).normalized;
                var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var side2 = Vector3.Cross(axis, side);
                float len = Vector3.Distance(a, b);
                int start = v.Count;
                for (int ring = 0; ring < 2; ring++)
                {
                    var c = ring == 0 ? a : b;
                    float r = ring == 0 ? ra : rb;
                    for (int s = 0; s <= sides; s++)
                    {
                        float ang = s * Mathf.PI * 2f / sides;
                        var dir = side * Mathf.Cos(ang) + side2 * Mathf.Sin(ang);
                        v.Add(c + dir * r);
                        n.Add(dir);
                        uv.Add(new Vector2(s / (float)sides * Mathf.Max(1f, ra * 6f), vStart + (ring == 0 ? 0f : len / 1.4f)));
                    }
                }
                for (int s = 0; s < sides; s++)
                {
                    int i0 = start + s, i1 = start + s + 1, j0 = start + sides + 1 + s, j1 = j0 + 1;
                    bark.Add(i0); bark.Add(j0); bark.Add(i1);
                    bark.Add(i1); bark.Add(j0); bark.Add(j1);
                }
            }

            /// <summary>
            /// A foliage card from <paramref name="root"/> along <paramref name="dir"/> (texture's v runs along it),
            /// <paramref name="width"/> wide across <paramref name="across"/>; lighting normal given separately.
            /// </summary>
            public void Card(Vector3 root, Vector3 dir, Vector3 across, float length, float width, Vector3 normal, Vector4 rect)
            {
                int i = v.Count;
                var half = across.normalized * (width * 0.5f);
                var tip = root + dir.normalized * length;
                v.Add(root - half); v.Add(root + half); v.Add(tip + half); v.Add(tip - half);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(rect.x, rect.y)); uv.Add(new Vector2(rect.z, rect.y)); uv.Add(new Vector2(rect.z, rect.w)); uv.Add(new Vector2(rect.x, rect.w));
                leaf.Add(i); leaf.Add(i + 2); leaf.Add(i + 1);
                leaf.Add(i); leaf.Add(i + 3); leaf.Add(i + 2);
            }

            public Mesh Finish(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.subMeshCount = 2;
                m.SetTriangles(bark, 0);
                m.SetTriangles(leaf, 1);
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }

        /// <summary>A fir: a straight trunk and whorls of needle sprays, long and drooping low, short and perky at the top.</summary>
        private static Mesh BuildFir(int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var b = new Builder();
            float h = R(9f, 12f);
            float baseR = R(0.22f, 0.3f);
            // The trunk in three tapering segments, a slight lean for life.
            var lean = new Vector3(R(-0.25f, 0.25f), 0f, R(-0.25f, 0.25f));
            Vector3 P(float t) => new Vector3(0f, t * h, 0f) + lean * (t * t);
            float vAt = 0f;
            for (int s = 0; s < 3; s++)
            {
                float t0 = s / 3f, t1 = (s + 1) / 3f;
                b.Tube(P(t0), P(t1), Mathf.Lerp(baseR, 0.03f, t0), Mathf.Lerp(baseR, 0.03f, t1), 9, vAt);
                vAt += h / 3f / 1.4f;
            }
            // Whorls of branches: each branch is two crossed needle cards.
            int tiers = Mathf.RoundToInt(R(16f, 20f));
            for (int i = 0; i < tiers; i++)
            {
                float t = Mathf.Lerp(0.18f, 0.97f, i / (float)(tiers - 1));
                float len = Mathf.Lerp(2.9f, 0.55f, Mathf.Pow(t, 0.9f)) * R(0.85f, 1.1f);
                int count = t > 0.85f ? 5 : 8;
                float spin = R(0f, 360f);
                for (int k = 0; k < count; k++)
                {
                    float yaw = (spin + k * 360f / count + R(-15f, 15f)) * Mathf.Deg2Rad;
                    var outDir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                    float droop = Mathf.Lerp(-0.42f, 0.05f, t) + R(-0.08f, 0.08f);
                    var dir = (outDir + Vector3.up * droop).normalized;
                    var root = P(t) + outDir * 0.05f;
                    var across = Vector3.Cross(dir, Vector3.up).normalized;
                    var up = (Vector3.up * 0.8f + outDir * 0.6f).normalized; // soft light: mostly sky-facing
                    var spray = FirSprays[rng.Next(FirSprays.Length)];
                    b.Card(root, dir, across, len, len * 0.95f, up, spray);
                    // The second card, tipped up 50 degrees around the branch, makes the spray full from any angle.
                    var across2 = Quaternion.AngleAxis(55f, dir) * across;
                    b.Card(root, dir, across2, len * 0.9f, len * 0.85f, up, FirSprays[rng.Next(FirSprays.Length)]);
                }
            }
            // A top spike.
            b.Card(P(0.92f), Vector3.up, Vector3.right, h * 0.12f, 0.8f, Vector3.up, FirSprays[0]);
            b.Card(P(0.92f), Vector3.up, Vector3.forward, h * 0.12f, 0.8f, Vector3.up, FirSprays[0]);
            return b.Finish("Fir" + seed);
        }

        /// <summary>A broadleaf: the trunk forks into three or four limbs under a rounded crown of leaf cards.</summary>
        private static Mesh BuildBroadleaf(int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            var b = new Builder();
            float h = R(6.5f, 8.5f);
            float fork = h * R(0.38f, 0.48f);
            var top = new Vector3(R(-0.3f, 0.3f), fork, R(-0.3f, 0.3f));
            b.Tube(Vector3.zero, top, 0.3f, 0.2f, 10, 0f);
            var crown = new Vector3(top.x, h * 0.72f, top.z);
            float crownR = h * R(0.33f, 0.4f);
            int limbs = rng.Next(3, 5);
            var ends = new List<Vector3>();
            for (int i = 0; i < limbs; i++)
            {
                float yaw = (i * 360f / limbs + R(-20f, 20f)) * Mathf.Deg2Rad;
                var end = crown + new Vector3(Mathf.Cos(yaw) * crownR * 0.6f, R(-0.3f, 1.2f), Mathf.Sin(yaw) * crownR * 0.6f);
                b.Tube(top, end, 0.17f, 0.05f, 7, 2f);
                ends.Add(end);
            }
            // The crown: leaf cards scattered through an ellipsoid, facing outward.
            int cards = 95;
            for (int i = 0; i < cards; i++)
            {
                var d = new Vector3(R(-1f, 1f), R(-0.6f, 1f), R(-1f, 1f));
                if (d.sqrMagnitude > 1f) d = d.normalized * R(0.6f, 1f);
                var pos = crown + Vector3.Scale(d, new Vector3(crownR, crownR * 0.75f, crownR));
                var outward = (pos - crown).normalized;
                if (outward.sqrMagnitude < 0.01f) outward = Vector3.up;
                var dir = (Quaternion.AngleAxis(R(0f, 360f), outward) * Vector3.Cross(outward, Vector3.up)).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                float size = R(1.3f, 2f);
                var across = Vector3.Cross(dir, outward);
                var rect = rng.NextDouble() < 0.75 ? LeafCluster : LeafTwig;
                float aspect = (rect.z - rect.x) / (rect.w - rect.y);
                b.Card(pos - dir * size * 0.5f, dir, across, size, size * aspect * 1.6f, (outward + Vector3.up * 0.4f).normalized, rect);
            }
            return b.Finish("Broadleaf" + seed);
        }
    }
}
