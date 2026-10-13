using System;
using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The world map's picture of the journey, drawn from the real layouts seen from straight above: for each world,
    /// from the bottom up, the road to each floor (its shape, its chest) and the floor itself (its stages, bridges,
    /// ramparts and vanG's cage), then the tunnel, the gate road and the world's battle arena, all on the world's land.
    /// Each world is one picture (three pixels a tile), worked out once; locked worlds come out grey.
    /// </summary>
    public static class JourneyMapArt
    {
        public const int Px = 3;
        private const int Gap = 6;

        public enum Kind { Road, Level, Tunnel, GateRoad, Arena }

        /// <summary>One part of a world's way: its layout rows and where it sits (tiles, from the world's bottom-left).</summary>
        public class Piece
        {
            public Kind kind;
            public int level;
            public string[] rows;
            public int x, y, w, h, entryX, exitX;
            public Vector2Int chest = new Vector2Int(-1, -1);
            public Vector2 Centre => new Vector2(x + w * 0.5f, y + h * 0.5f);
        }

        /// <summary>A world's layout of pieces (no picture): the size and where every road and floor is.</summary>
        public class WorldPlan
        {
            public int world, width, height;
            public readonly List<Piece> pieces = new List<Piece>();
        }

        private static readonly Dictionary<int, WorldPlan> plans = new Dictionary<int, WorldPlan>();

        /// <summary>The plan of world <paramref name="w"/> (worked out once; layouts come from the given sources).</summary>
        public static WorldPlan Plan(int w, Func<int, string[]> levelRows, Func<int, bool, string[]> roadRows, Func<int, string[]> arenaRows)
        {
            if (plans.TryGetValue(w, out var cached)) return cached;
            var plan = new WorldPlan { world = w };
            int per = LevelCatalog.LevelsPerWorld;
            for (int i = 0; i < per; i++)
            {
                int lvl = w * per + i;
                var road = roadRows(lvl, false);
                var rp = new Piece { kind = Kind.Road, level = lvl, rows = road, entryX = TrailMap.Start.x, exitX = TrailMap.Gate.x };
                foreach (var c in TrailMap.Clearings)
                    if (c.kind == TrailMap.ClearingKind.Chest) rp.chest = new Vector2Int(c.x, c.y);
                plan.pieces.Add(rp);
                var rows = levelRows(lvl);
                var lp = new Piece { kind = Kind.Level, level = lvl, rows = rows };
                var g = new GridModel(rows[0].Length, rows.Length, rows);
                var e = g.StartSpot ?? g.CenterFloor();
                while (g.IsFloor(e + new GridPos(0, -1))) e += new GridPos(0, -1);
                lp.entryX = e.x;
                lp.exitX = ExitX(rows);
                plan.pieces.Add(lp);
            }
            plan.pieces.Add(new Piece { kind = Kind.Tunnel, level = (w + 1) * per - 1, rows = Blank(5, 14), entryX = 2, exitX = 2 });
            var gate = roadRows((w + 1) * per - 1, true);
            plan.pieces.Add(new Piece { kind = Kind.GateRoad, level = (w + 1) * per - 1, rows = gate, entryX = TrailMap.Start.x, exitX = TrailMap.Gate.x });
            var arena = arenaRows(w);
            plan.pieces.Add(new Piece { kind = Kind.Arena, level = (w + 1) * per - 1, rows = arena, entryX = arena[0].Length / 2, exitX = arena[0].Length / 2 });
            // Stacked from the bottom up, centred in the widest piece.
            int width = 0;
            foreach (var p in plan.pieces) { p.w = p.rows[0].Length; p.h = p.rows.Length; width = Mathf.Max(width, p.w); }
            width += 8;
            int y = 2;
            foreach (var p in plan.pieces)
            {
                p.x = (width - p.w) / 2;
                p.y = y;
                y += p.h + Gap;
            }
            plan.width = width;
            plan.height = y + 2;
            plans[w] = plan;
            return plan;
        }

        /// <summary>The middle of a floor's top edge, where its bridge leaves past the cage.</summary>
        private static int ExitX(string[] rows)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                int sum = 0, n = 0;
                for (int x = 0; x < rows[r].Length; x++) if (rows[r][x] == 'B' || rows[r][x] == 'C') { sum += x; n++; }
                if (n > 0) return sum / n;
            }
            return rows[0].Length / 2;
        }

        private static string[] Blank(int w, int h)
        {
            var rows = new string[h];
            for (int i = 0; i < h; i++) rows[i] = new string('.', w);
            return rows;
        }

        /// <summary>The picture of a world: its land, its roads and floors, joined up; grey when it is still locked.</summary>
        public static Texture2D Paint(WorldPlan plan, bool open)
        {
            int tw = plan.width * Px, th = plan.height * Px;
            var px = new Color32[tw * th];
            var theme = WorldTheme.ForWorld(plan.world);
            Color land = ArenaDecor.GroundOf(plan.world);
            Color landDark = Color.Lerp(land, Color.black, 0.12f);
            Color road = new Color(0.86f, 0.83f, 0.76f);
            Color roadEdge = new Color(0.66f, 0.62f, 0.55f);
            Color floor = Color.Lerp(theme.accent, Color.white, 0.68f);
            Color floorLine = Color.Lerp(theme.accent, Color.white, 0.45f);
            Color wall = Color.Lerp(theme.accent, new Color(0.15f, 0.12f, 0.12f), 0.55f);
            Color cage = new Color(0.85f, 0.25f, 0.5f);
            Color bridge = new Color(0.62f, 0.5f, 0.38f);
            Color lava = new Color(1f, 0.55f, 0.15f);
            Color gold = new Color(1f, 0.8f, 0.25f);
            Color32 C(Color c)
            {
                if (!open) { float l = c.grayscale * 0.75f + 0.08f; c = new Color(l, l, l); }
                return c;
            }
            // Land with a soft dappled texture.
            var rng = new System.Random(plan.world * 7 + 3);
            var landC = C(land);
            var landD = C(landDark);
            for (int i = 0; i < px.Length; i++) px[i] = rng.Next(9) == 0 ? landD : landC;
            void Tile(int tx, int ty, Color c, Color? edge = null)
            {
                if (tx < 0 || ty < 0 || tx >= plan.width || ty >= plan.height) return;
                var cc = C(c);
                var ec = edge.HasValue ? C(edge.Value) : cc;
                for (int dy = 0; dy < Px; dy++)
                    for (int dx = 0; dx < Px; dx++)
                    {
                        bool rim = dx == Px - 1 || dy == Px - 1;
                        px[(ty * Px + dy) * tw + tx * Px + dx] = rim ? ec : cc;
                    }
            }
            Piece prev = null;
            foreach (var p in plan.pieces)
            {
                // The way between the last piece and this one.
                if (prev != null) Path(prev.x + prev.exitX, prev.y + prev.h, p.x + p.entryX, p.y, Tile, road, roadEdge);
                bool fortress = p.kind == Kind.Level || p.kind == Kind.Arena;
                for (int r = 0; r < p.h; r++)
                {
                    int ty = p.y + (p.h - 1 - r);
                    string row = p.rows[r];
                    for (int x = 0; x < p.w; x++)
                    {
                        char ch = row[x];
                        int tx = p.x + x;
                        if (ch == '.' || ch == ' ')
                        {
                            // Ramparts round a fortress's floor.
                            if (fortress && Near(p.rows, x, r)) Tile(tx, ty, wall);
                            continue;
                        }
                        if (!fortress) { Tile(tx, ty, road, roadEdge); continue; }
                        Color c = ch == 'C' ? cage : ch == 'B' ? bridge : ch == 'F' ? lava : ch == 'X' ? wall : floor;
                        Tile(tx, ty, c, ch == '#' || ch == 'S' || ch == 'G' || ch == 'H' ? floorLine : (Color?)null);
                    }
                }
                if (p.kind == Kind.Tunnel)
                {
                    // vanG's tunnel: a dark dashed passage.
                    for (int r = 0; r < p.h; r++) if (r % 3 != 2) Tile(p.x + 2, p.y + r, new Color(0.18f, 0.16f, 0.22f));
                }
                if (p.chest.x >= 0)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            Tile(p.x + p.chest.x + dx, p.y + p.chest.y + dy, gold);
                prev = p;
            }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static bool Near(string[] rows, int x, int r)
        {
            for (int dr = -1; dr <= 1; dr++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int rr = r + dr, xx = x + dx;
                    if (rr < 0 || rr >= rows.Length || xx < 0 || xx >= rows[rr].Length) continue;
                    char c = rows[rr][xx];
                    if (c != '.' && c != ' ') return true;
                }
            return false;
        }

        /// <summary>A three-tile road from one piece's way out to the next one's way in.</summary>
        private static void Path(int x0, int y0, int x1, int y1, Action<int, int, Color, Color?> tile, Color road, Color edge)
        {
            int steps = Mathf.Max(1, Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int dx = -1; dx <= 1; dx++) tile(x + dx, y, road, edge);
            }
        }
    }
}
