using System.Collections.Generic;

namespace SquashBot.Core
{
    public enum TileState
    {
        Solid,
        Broken,
        /// <summary>Burning for a few seconds, then solid again. Deadly to step on, can be jumped over.</summary>
        Fire
    }

    /// <summary>
    /// Pure game-state for the platform. No Unity objects, so it can be reasoned about and tested in isolation.
    /// </summary>
    public class GridModel
    {
        public int Width { get; }
        public int Height { get; }
        public int TileCount => Width * Height;
        public GridPos Center => new GridPos(Width / 2, Height / 2);

        private readonly TileState[,] tiles;
        private readonly bool[,] occupied;

        public GridModel(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TileState[width, height];
            occupied = new bool[width, height];
        }

        public bool InBounds(GridPos p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;

        public TileState GetTile(GridPos p) => tiles[p.x, p.y];
        public void SetTile(GridPos p, TileState state) => tiles[p.x, p.y] = state;

        public bool IsOccupied(GridPos p) => occupied[p.x, p.y];
        public void SetOccupied(GridPos p, bool value) => occupied[p.x, p.y] = value;

        /// <summary>A tile the robot can stand on right now: inside the grid, not broken, no block sitting on it.</summary>
        public bool IsStandable(GridPos p) => InBounds(p) && tiles[p.x, p.y] == TileState.Solid && !occupied[p.x, p.y];

        /// <summary>A hole or a fire: deadly to land on, but the robot can leap over it.</summary>
        public bool IsGap(GridPos p) => InBounds(p) && tiles[p.x, p.y] != TileState.Solid;

        public IEnumerable<GridPos> AllPositions()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    yield return new GridPos(x, y);
        }

        public int SolidCount()
        {
            int count = 0;
            foreach (var p in AllPositions())
                if (GetTile(p) == TileState.Solid) count++;
            return count;
        }
    }
}
