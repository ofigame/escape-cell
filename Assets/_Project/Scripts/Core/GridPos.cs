using System;
using UnityEngine;

namespace SquashBot.Core
{
    [Serializable]
    public struct GridPos : IEquatable<GridPos>
    {
        public int x;
        public int y;

        public GridPos(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.x + b.x, a.y + b.y);
        public static bool operator ==(GridPos a, GridPos b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);

        public bool Equals(GridPos other) => this == other;
        public override bool Equals(object obj) => obj is GridPos g && this == g;
        public override int GetHashCode() => (x * 397) ^ y;
        public override string ToString() => $"({x},{y})";

        public int Manhattan(GridPos other) => Mathf.Abs(x - other.x) + Mathf.Abs(y - other.y);
    }

    public enum Direction
    {
        PlusX,
        MinusX,
        PlusY,
        MinusY
    }

    public static class DirectionExtensions
    {
        public static readonly Direction[] All =
        {
            Direction.PlusX, Direction.MinusX, Direction.PlusY, Direction.MinusY
        };

        public static GridPos ToOffset(this Direction d)
        {
            switch (d)
            {
                case Direction.PlusX: return new GridPos(1, 0);
                case Direction.MinusX: return new GridPos(-1, 0);
                case Direction.PlusY: return new GridPos(0, 1);
                default: return new GridPos(0, -1);
            }
        }
    }
}
