using System.Collections.Generic;
using UnityEngine;

namespace BrickMuseum.City
{
    /// <summary>
    /// The streets as a graph on the plan: every crossing of a north-south and an east-west street is a node, the street
    /// between two crossings an edge (the east-west streets cross the creek on their bridges). Routes go along the streets,
    /// turning at the crossings (shortest path), so a driver can follow them.
    /// </summary>
    public static class StreetGraph
    {
        private static List<Vector2> nodes;
        private static List<(int a, int b)> edges;
        private static List<List<(int to, float len)>> links;

        /// <summary>Forget the graph (the far district was opened: its streets join it).</summary>
        public static void Reset() => nodes = null;

        private static void Build()
        {
            if (nodes != null) return;
            nodes = new List<Vector2>();
            edges = new List<(int, int)>();
            var xs = new List<float>(CityLayout.StreetXs());
            var zs = new List<float>(CityLayout.StreetZs());
            zs.Sort();
            float south = CityLayout.ZOf(0) + CityLayout.Block / 2f - 4f;     // the north-south streets run on to the quay
            var index = new Dictionary<(int, int), int>();
            for (int i = 0; i < xs.Count; i++)
            {
                for (int j = 0; j < zs.Count; j++) { index[(i, j)] = nodes.Count; nodes.Add(new Vector2(xs[i], zs[j])); }
                index[(i, zs.Count)] = nodes.Count; nodes.Add(new Vector2(xs[i], south));
            }
            for (int i = 0; i < xs.Count; i++)
                for (int j = 0; j < zs.Count; j++)
                {
                    edges.Add((index[(i, j)], index[(i, j + 1)]));                      // along a north-south street
                    if (i + 1 < xs.Count) edges.Add((index[(i, j)], index[(i + 1, j)]));  // along an east-west street
                }
            if (District.IsOpen)
            {
                // the far district's grid, and the country road out to it from the town's east street
                int Node(Vector2 v) { for (int i = 0; i < nodes.Count; i++) if ((nodes[i] - v).sqrMagnitude < 0.01f) return i; nodes.Add(v); return nodes.Count - 1; }
                var fx = new List<float>(CityLayout.FarStreetXs());
                var fz = new List<float>(CityLayout.FarStreetZs());
                for (int i = 0; i < fx.Count; i++)
                    for (int j = 0; j < fz.Count; j++)
                    {
                        if (j + 1 < fz.Count) edges.Add((Node(new Vector2(fx[i], fz[j])), Node(new Vector2(fx[i], fz[j + 1]))));
                        if (i + 1 < fx.Count) edges.Add((Node(new Vector2(fx[i], fz[j])), Node(new Vector2(fx[i + 1], fz[j]))));
                    }
                edges.Add((Node(new Vector2(CityLayout.CountryX, zs[0])), Node(new Vector2(CityLayout.CountryX, CityLayout.FarSouth - CityLayout.Road / 2f))));   // the country road north
            }
            if (Mega.IsOpen)
            {
                // Gökkent's grid, and the highway to it from the village's east street
                int Node(Vector2 v) { for (int i = 0; i < nodes.Count; i++) if ((nodes[i] - v).sqrMagnitude < 0.01f) return i; nodes.Add(v); return nodes.Count - 1; }
                var mx = new List<float>(CityLayout.MegaStreetXs());
                var mz = new List<float>(CityLayout.MegaStreetZs());
                mz.Sort();
                for (int i = 0; i < mx.Count; i++)
                    for (int j = 0; j < mz.Count; j++)
                    {
                        if (j + 1 < mz.Count) edges.Add((Node(new Vector2(mx[i], mz[j])), Node(new Vector2(mx[i], mz[j + 1]))));
                        if (i + 1 < mx.Count) edges.Add((Node(new Vector2(mx[i], mz[j])), Node(new Vector2(mx[i + 1], mz[j]))));
                    }
                edges.Add((Node(new Vector2(xs[0], CityLayout.HighwayZ)), Node(new Vector2(mx[mx.Count - 1], CityLayout.HighwayZ))));   // the highway west
            }
            links = new List<List<(int, float)>>();
            for (int n = 0; n < nodes.Count; n++) links.Add(new List<(int, float)>());
            foreach (var (a, b) in edges)
            {
                float len = Vector2.Distance(nodes[a], nodes[b]);
                links[a].Add((b, len)); links[b].Add((a, len));
            }
        }

        /// <summary>The nearest point on any street to a plan point, and which edge it is on.</summary>
        private static (Vector2 at, int edge, float t) Nearest(Vector2 p)
        {
            Build();
            float best = float.MaxValue; Vector2 at = p; int edge = 0; float bt = 0f;
            for (int e = 0; e < edges.Count; e++)
            {
                Vector2 a = nodes[edges[e].a], b = nodes[edges[e].b], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                var q = a + ab * t;
                float d = (q - p).sqrMagnitude;
                if (d < best) { best = d; at = q; edge = e; bt = t; }
            }
            return (at, edge, bt);
        }

        /// <summary>The way along the streets from one plan point to another (plan points, the first and last on the street).</summary>
        public static List<Vector2> Route(Vector2 from, Vector2 to)
        {
            var (a, ea, _) = Nearest(from);
            var (b, eb, _) = Nearest(to);
            var path = new List<Vector2> { a };
            if (ea == eb) { path.Add(b); return path; }
            // Dijkstra from both ends of the start edge to both ends of the end edge
            int n = nodes.Count;
            var dist = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            foreach (int s in new[] { edges[ea].a, edges[ea].b }) dist[s] = Vector2.Distance(a, nodes[s]);
            for (int k = 0; k < n; k++)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!done[i] && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || dist[u] == float.MaxValue) break;
                done[u] = true;
                foreach (var (v, len) in links[u])
                    if (dist[u] + len < dist[v]) { dist[v] = dist[u] + len; prev[v] = u; }
            }
            int end = -1; float bestEnd = float.MaxValue;
            foreach (int s in new[] { edges[eb].a, edges[eb].b })
            {
                float total = dist[s] + Vector2.Distance(nodes[s], b);
                if (total < bestEnd) { bestEnd = total; end = s; }
            }
            var chain = new List<Vector2>();
            for (int v = end; v >= 0; v = prev[v]) chain.Add(nodes[v]);
            chain.Reverse();
            path.AddRange(chain);
            path.Add(b);
            return path;
        }

        public static float Length(List<Vector2> path)
        {
            float l = 0f;
            for (int i = 1; i < path.Count; i++) l += Vector2.Distance(path[i - 1], path[i]);
            return l;
        }

        /// <summary>
        /// A spot on a pavement beside a street, for a taxi passenger: the plan point where they stand and the point on the
        /// street in front of them. Never on a bridge.
        /// </summary>
        public static (Vector2 stand, Vector2 street) RandomKerb(System.Random r)
        {
            Build();
            for (int tries = 0; tries < 200; tries++)
            {
                var (ia, ib) = edges[r.Next(edges.Count)];
                Vector2 a = nodes[ia], b = nodes[ib];
                var on = Vector2.Lerp(a, b, 0.2f + (float)r.NextDouble() * 0.6f);
                if (CityLayout.InCreek(on) || (on.y < CityLayout.MinZ && on.y > CityLayout.FarSouth) || (on.x < CityLayout.MinX && on.x > CityLayout.MegaMaxX)) continue;   // not on a bridge, the country road or the highway
                var dir = (b - a).normalized;
                var side = new Vector2(-dir.y, dir.x) * (r.Next(2) == 0 ? 1f : -1f);
                var stand = on + side * (CityLayout.Road / 2f + 1.2f);
                if (CityLayout.InCreek(stand)) continue;
                return (stand, on);
            }
            return (nodes[0], nodes[0]);
        }
    }
}
