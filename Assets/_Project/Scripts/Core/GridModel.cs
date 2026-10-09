using System.Collections.Generic;

namespace SquashBot.Core
{
    public enum TileState
    {
        Solid,
        Broken,
        /// <summary>Burning for a few seconds, then solid again. Deadly to step on, can be jumped over.</summary>
        Fire,
        /// <summary>Poisoned for the rest of the level (toxic floors): deadly to step on, can be jumped over.</summary>
        Poison
    }

    /// <summary>
    /// Pure game-state for the platform. No Unity objects, so it can be reasoned about and tested in isolation.
    /// The platform is a rectangle of cells, but a level layout can leave cells out (shaping the platform into
    /// an L, a T, a ring...) and put fixed obstacles (walls) on others.
    /// Layout rows use '#' for a floor tile, '.' for no tile and 'X' for a tile with an obstacle; row 0 is the far edge.
    /// 'S', 'K' and 'D' are floor tiles that also mark the start, a key and the door.
    /// </summary>
    public class GridModel
    {
        public int Width { get; }
        public int Height { get; }
        public int TileCount => Width * Height;

        /// <summary>Cells the robot can ever stand on (part of the platform, not an obstacle).</summary>
        public int FloorCount { get; }

        /// <summary>Spots a layout may mark: where the robot starts ('S'), where keys lie ('K'), where the door is ('D').</summary>
        public GridPos? StartSpot { get; set; }
        public GridPos? DoorSpot { get; }
        public List<GridPos> KeySpots { get; } = new List<GridPos>();

        private readonly TileState[,] tiles;
        private readonly bool[,] occupied;
        private readonly bool[,] exists;
        private readonly bool[,] wall;
        private readonly bool[,] safe;
        private readonly bool[,] cage, bridge;

        public GridModel(int width, int height, string[] layout = null)
        {
            Width = width;
            Height = height;
            tiles = new TileState[width, height];
            occupied = new bool[width, height];
            exists = new bool[width, height];
            wall = new bool[width, height];
            safe = new bool[width, height];
            cage = new bool[width, height];
            bridge = new bool[width, height];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    char c = '#';
                    if (layout != null && layout.Length == height && layout[height - 1 - y].Length == width)
                        c = layout[height - 1 - y][x];
                    exists[x, y] = c != '.';
                    wall[x, y] = c == 'X';
                    safe[x, y] = c == 'H';
                    cage[x, y] = c == 'C';
                    bridge[x, y] = c == 'B';
                    if (cage[x, y]) CageTiles.Add(new GridPos(x, y));
                    if (bridge[x, y]) BridgeTiles.Add(new GridPos(x, y));
                    var p = new GridPos(x, y);
                    if (c == 'S') StartSpot = p;
                    else if (c == 'D') DoorSpot = p;
                    else if (c == 'K') KeySpots.Add(p);
                    if (exists[x, y] && !wall[x, y]) FloorCount++;
                }
        }

        /// <summary>A tile of a healing island ('H' in a layout): nothing falls on it and no enemy reaches it.</summary>
        public bool IsSafe(GridPos p) => InBounds(p) && safe[p.x, p.y];

        /// <summary>A tile of the monster's cage ('C'): shut until the crowd is beaten.</summary>
        public bool IsCage(GridPos p) => InBounds(p) && cage[p.x, p.y];

        /// <summary>A tile of the bridge to the tunnel ('B'): open once the monster is beaten.</summary>
        public bool IsBridge(GridPos p) => InBounds(p) && bridge[p.x, p.y];

        public List<GridPos> CageTiles { get; } = new List<GridPos>();
        /// <summary>The bridge from the cage to the tunnel, nearest the floor first.</summary>
        public List<GridPos> BridgeTiles { get; } = new List<GridPos>();

        public bool InBounds(GridPos p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;

        /// <summary>The cell is part of the platform (it may still hold an obstacle).</summary>
        public bool Exists(GridPos p) => InBounds(p) && exists[p.x, p.y];

        /// <summary>A fixed obstacle sits here for the whole level.</summary>
        public bool IsWall(GridPos p) => InBounds(p) && wall[p.x, p.y];

        /// <summary>A floor tile: on the platform and not an obstacle (whatever its current state).</summary>
        public bool IsFloor(GridPos p) => Exists(p) && !wall[p.x, p.y];

        public TileState GetTile(GridPos p) => tiles[p.x, p.y];
        public void SetTile(GridPos p, TileState state) => tiles[p.x, p.y] = state;

        /// <summary>Something blocks the tile: a landed block or a fixed obstacle.</summary>
        public bool IsOccupied(GridPos p) => occupied[p.x, p.y] || wall[p.x, p.y];
        public void SetOccupied(GridPos p, bool value) => occupied[p.x, p.y] = value;

        /// <summary>A tile the robot can stand on right now: a solid floor tile with nothing on it.</summary>
        public bool IsStandable(GridPos p) => IsFloor(p) && tiles[p.x, p.y] == TileState.Solid && !occupied[p.x, p.y];

        /// <summary>A hole, a fire or empty space beside the platform: deadly to land on, but the robot can leap over it.</summary>
        public bool IsGap(GridPos p) => InBounds(p) && (!exists[p.x, p.y] || (!wall[p.x, p.y] && tiles[p.x, p.y] != TileState.Solid));

        public IEnumerable<GridPos> AllPositions()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    yield return new GridPos(x, y);
        }

        /// <summary>Floor tiles that are currently solid (not broken, not burning).</summary>
        public int SolidCount()
        {
            int count = 0;
            foreach (var p in AllPositions())
                if (IsFloor(p) && GetTile(p) == TileState.Solid) count++;
            return count;
        }

        /// <summary>The floor tile closest to the middle of the platform (where the robot starts).</summary>
        public GridPos CenterFloor()
        {
            // The middle of the main floor: the bridge rows to the north don't count, and never inside the cage.
            var center = new GridPos(Width / 2, (Height - BridgeTiles.Count) / 2);
            GridPos best = center;
            int bestDistance = int.MaxValue;
            foreach (var p in AllPositions())
            {
                if (!IsFloor(p) || IsCage(p) || IsBridge(p) || IsSafe(p)) continue;
                int d = p.Manhattan(center);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            return best;
        }
    }
}
