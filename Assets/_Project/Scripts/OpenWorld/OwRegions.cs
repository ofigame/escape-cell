using UnityEngine;

namespace SquashBot.OpenWorld
{
    public enum RegionState { Safe = 0, Border = 1, Bandit = 2 }

    /// <summary>
    /// The 7 × 7 regions: safe, border or held by bandits. At first the middle and its ring are safe, the next ring is
    /// the border and the outer ring is the bandits'. Every six hours each bandit region tries to spread to a neighbour
    /// (a quarter of the time; a tenth where foi keeps soldiers or defences); the middle never falls. A camp stands in
    /// every bandit region: beat it and the region is free. The far corner is the Bandit Fortress.
    /// </summary>
    public static class OwRegions
    {
        public const int N = OwWorld.Regions;
        public const long SpreadSeconds = 6 * 3600;
        public static readonly Vector2Int Fortress = new Vector2Int(N - 1, N - 1);
        public static readonly Vector2Int Middle = new Vector2Int(N / 2, N / 2);

        public static int Ring(Vector2Int r) => Mathf.Max(Mathf.Abs(r.x - Middle.x), Mathf.Abs(r.y - Middle.y));

        public static Vector2Int Of(Vector2 p) => new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(p.x / OwWorld.RegionSize), 0, N - 1), Mathf.Clamp(Mathf.FloorToInt(p.y / OwWorld.RegionSize), 0, N - 1));

        public static Vector2 Centre(Vector2Int r) => new Vector2((r.x + 0.5f) * OwWorld.RegionSize, (r.y + 0.5f) * OwWorld.RegionSize);

        /// <summary>A bandit camp's middle: off the region's centre so it never sits on the farm sites or the co-op.</summary>
        public static Vector2 CampAt(Vector2Int r) => Centre(r) + new Vector2(r.x >= Middle.x ? 6f : -6f, r.y >= Middle.y ? 6f : -6f);

        public static RegionState Get(OwSave s, Vector2Int r) => (RegionState)s.regions[r.y * N + r.x];
        public static void Set(OwSave s, Vector2Int r, RegionState v) => s.regions[r.y * N + r.x] = (int)v;

        /// <summary>The first visit: the rings.</summary>
        public static void Init(OwSave s)
        {
            if (s.regions != null && s.regions.Length == N * N) return;
            s.regions = new int[N * N];
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    int ring = Ring(new Vector2Int(x, y));
                    s.regions[y * N + x] = ring <= 1 ? 0 : ring == 2 ? 1 : 2;
                }
            s.spreadAt = OwSave.Now + SpreadSeconds;
        }

        /// <summary>How strong a bandit region's camp is: further out, stronger; the fortress the strongest.</summary>
        public static float Strength(Vector2Int r) => r == Fortress ? 2.6f : (Ring(r) >= 3 ? 1f : Ring(r) == 2 ? 0.8f : 0.7f) + (Ring(r) >= 3 && Mathf.Abs(r.x - Middle.x) == Mathf.Abs(r.y - Middle.y) ? 0.3f : 0f);

        /// <summary>The bandits spread for every six hours gone by (at most eight steps at once). Returns regions taken.</summary>
        public static int Spread(OwSave s)
        {
            long now = OwSave.Now;
            if (s.spreadAt <= 0) s.spreadAt = now + SpreadSeconds;
            int taken = 0, steps = 0;
            var rng = new System.Random((int)(s.spreadAt % 100000));
            var farm = Of(OwWorld.Sites[Mathf.Clamp(s.site, 0, OwWorld.Sites.Length - 1)]);
            while (s.spreadAt <= now && steps < 8)
            {
                steps++;
                s.spreadAt += SpreadSeconds;
                var from = new System.Collections.Generic.List<Vector2Int>();
                for (int x = 0; x < N; x++) for (int y = 0; y < N; y++) if (s.regions[y * N + x] == 2) from.Add(new Vector2Int(x, y));
                foreach (var b in from)
                {
                    var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                    var to = b + dirs[rng.Next(4)];
                    if (to.x < 0 || to.y < 0 || to.x >= N || to.y >= N || to == Middle) continue;
                    var state = Get(s, to);
                    if (state == RegionState.Bandit) continue;
                    bool guarded = to == farm && (s.SoldierCount > 0 || s.defences.Count > 0);
                    if (rng.NextDouble() >= (guarded ? 0.1 : 0.25)) continue;
                    Set(s, to, state == RegionState.Safe ? RegionState.Border : RegionState.Bandit);
                    taken++;
                }
            }
            if (steps > 0) s.Save();
            return taken;
        }

        public static int Count(OwSave s, RegionState state)
        {
            int n = 0;
            foreach (var v in s.regions) if (v == (int)state) n++;
            return n;
        }

        /// <summary>Bandit regions next to the farm's make its raids bigger.</summary>
        public static int BanditNeighbours(OwSave s)
        {
            var farm = Of(OwWorld.FarmCentre);
            int n = 0;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var r = farm + d;
                if (r.x >= 0 && r.y >= 0 && r.x < N && r.y < N && Get(s, r) == RegionState.Bandit) n++;
            }
            return n + (Get(s, farm) == RegionState.Bandit ? 2 : 0);
        }
    }
}
