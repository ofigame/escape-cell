using System;

namespace SquashBot.Data
{
    /// <summary>
    /// A piece standing in the city. Only what cannot be worked out is saved: which piece, where, which way it
    /// faces and when it was last cared for (the care stage is computed from that on every visit).
    /// </summary>
    [Serializable]
    public class CityPlaced
    {
        public int uid;
        public string id;
        public int x, y;
        /// <summary>Quarter turns, 0-3.</summary>
        public int rot;
        /// <summary>Last care (or building) time, unix seconds.</summary>
        public long care;
        /// <summary>Gold mines: when the coins were last picked up.</summary>
        public long mined;

        public CityPiece Piece => CityCatalog.Find(id);

        public CityPlaced Copy() => (CityPlaced)MemberwiseClone();
    }
}
