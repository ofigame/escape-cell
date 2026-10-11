using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Where the player's defences may stand in a fortress: an archer on each stage's four corner towers, cannons along
    /// its ramparts (every third stretch, clear of the stairs, bridges and vanG's cage), a tower on any plain floor tile
    /// of a stage away from foi's cell.
    /// </summary>
    public static class DefenseSlots
    {
        public struct Slot
        {
            public DefenseKind kind;
            public int stage;
            public GridPos tile, dir;
            public Vector3 world;
            public Defenses.Placement Placement => new Defenses.Placement(kind, tile.x, tile.y, dir.x, dir.y);
        }

        private static readonly GridPos[] Sides = { new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) };

        public static bool InRect(int[] r, int s, GridPos p) => p.x >= r[s * 4] && p.x <= r[s * 4 + 2] && p.y >= r[s * 4 + 1] && p.y <= r[s * 4 + 3];

        private static bool Plain(GridModel g, GridPos p) =>
            g.IsFloor(p) && !g.IsLava(p) && !g.IsCage(p) && !g.IsBridge(p) && !g.IsGate(p) && !g.IsSafe(p);

        /// <summary>The archer and cannon spots of every stage (towers go on any plain tile, see <see cref="TowerOk"/>).</summary>
        public static List<Slot> Fixed(GridModel g, int[] rects)
        {
            var list = new List<Slot>();
            int n = rects.Length / 4;
            for (int s = 0; s < n; s++)
            {
                for (int c = 0; c < 4; c++)
                {
                    var (sx, sz) = FortressFloor.Corners[c];
                    var tile = new GridPos(sx < 0 ? rects[s * 4] : rects[s * 4 + 2], sz < 0 ? rects[s * 4 + 1] : rects[s * 4 + 3]);
                    list.Add(new Slot { kind = DefenseKind.Archer, stage = s, tile = tile, dir = new GridPos(sx, sz), world = FortressFloor.CornerTowerTop(rects, s, c) });
                }
                for (int x = rects[s * 4]; x <= rects[s * 4 + 2]; x++)
                    for (int y = rects[s * 4 + 1]; y <= rects[s * 4 + 3]; y++)
                    {
                        var p = new GridPos(x, y);
                        if (!Plain(g, p) || (x + y) % 3 != 0) continue;
                        foreach (var d in Sides)
                        {
                            var o = p + d;
                            if (g.IsFloor(o) || g.IsLava(o) || g.IsCage(o) || g.IsBridge(o)) continue; // no rampart there
                            if (NearAny(g, p, 2)) continue;
                            list.Add(new Slot { kind = DefenseKind.Cannon, stage = s, tile = p, dir = d, world = FortressFloor.WallTop(p, d) });
                        }
                    }
            }
            return list;
        }

        /// <summary>Stairs, bridges, gates or the cage within <paramref name="r"/> tiles.</summary>
        private static bool NearAny(GridModel g, GridPos p, int r)
        {
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    var q = p + new GridPos(x, y);
                    if (!g.InBounds(q)) continue;
                    if (g.IsLava(q) || g.IsBridge(q) || g.IsGate(q) || g.IsCage(q)) return true;
                }
            return false;
        }

        /// <summary>Can a tower stand on this tile (plain floor of a stage, away from foi's cell)?</summary>
        public static bool TowerOk(GridModel g, int[] rects, GridPos p, GridPos start, out int stage)
        {
            stage = -1;
            if (!Plain(g, p) || HuntSystem.Chebyshev(p, start) <= 2 || NearAny(g, p, 1)) return false;
            for (int s = 0; s < rects.Length / 4; s++)
                if (InRect(rects, s, p)) { stage = s; return true; }
            return false;
        }

        /// <summary>The stage a placement belongs to and where its model stands (false: it no longer fits this floor).</summary>
        public static bool Resolve(GridModel g, int[] rects, List<Slot> fixedSlots, GridPos start, Defenses.Placement pl, out Slot slot)
        {
            slot = default;
            if (pl.kind == DefenseKind.Tower)
            {
                var p = new GridPos(pl.x, pl.y);
                if (!TowerOk(g, rects, p, start, out int s)) return false;
                slot = new Slot { kind = DefenseKind.Tower, stage = s, tile = p, world = GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY };
                return true;
            }
            foreach (var f in fixedSlots)
                if (f.kind == pl.kind && f.tile.x == pl.x && f.tile.y == pl.y && f.dir.x == pl.dx && f.dir.y == pl.dy) { slot = f; return true; }
            return false;
        }
    }
}
