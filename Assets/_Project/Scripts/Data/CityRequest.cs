using System;

namespace SquashBot.Data
{
    /// <summary>One resident's wish, shown in a speech bubble over its head.</summary>
    [Serializable]
    public class CityRequest
    {
        public CityRequestKind kind;
        public int resident;
        /// <summary>The piece to build (Build), to place next to a house (Near), or the building to fix (Repair: its uid).</summary>
        public string piece;
        public int uid;
        /// <summary>MainMode: the world and how many of its levels must have 3 stars.</summary>
        public int world, target;
        /// <summary>Build: how many of the piece stood when the wish was made.</summary>
        public int baseline;
        public int reward;
        public long created;
    }
}
