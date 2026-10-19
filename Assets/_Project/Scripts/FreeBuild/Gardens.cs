using System.Collections.Generic;
using System.Linq;
using BrickMuseum.City;
using BrickMuseum.Data;
using UnityEngine;

namespace BrickMuseum.FreeBuild
{
    /// <summary>
    /// Gardens of the city's houses (from level 3): the pieces laid round a house on its plot — fences, hedges, a pool,
    /// trees, flowers, paths, lamps — built in the editor with the house standing fixed in the middle.
    /// </summary>
    public static class Gardens
    {
        public const int UnlockLevel = 3;

        /// <summary>The garden plot's size in studs (square, inside the slot's pad).</summary>
        public static int Size(Slot s) => Mathf.FloorToInt(Mathf.Min(s.w, s.d) - 1f);

        public static List<BuildPieceData> Pieces(string slot, bool create)
        {
            var g = Progress.Data.gardens.FirstOrDefault(x => x.slot == slot);
            if (g == null)
            {
                if (!create) return null;
                g = new GardenSave { slot = slot };
                Progress.Data.gardens.Add(g);
            }
            return g.pieces;
        }
    }
}
