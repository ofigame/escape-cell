using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// One kind of building piece for the city: its tray tab, what it does, the cells it covers and how it unlocks.
    /// Normal pieces unlock with their world and cost coins; special pieces also need stars from that world.
    /// </summary>
    public class CityPiece
    {
        public string id;
        public CityCategory category;
        public CityRole role;
        public CityPerk perk;
        /// <summary>Cells covered before rotating (x along the plot's width, y along its depth).</summary>
        public int w = 1, h = 1;
        public int price;
        /// <summary>The world (0-based) whose arrival puts the piece in the tray.</summary>
        public int world;
        /// <summary>Stars from that world needed as well (special pieces).</summary>
        public int stars;
        /// <summary>Lights up in the evening.</summary>
        public bool glow;

        public bool IsSpecial => stars > 0;

        /// <summary>Days from fresh to "needs care" (0 = never).</summary>
        public float CareDays
        {
            get
            {
                switch (role)
                {
                    case CityRole.Home: return 3f;
                    case CityRole.Producer: return 2f;
                    case CityRole.Special: return 4f;
                    default: return 0f;
                }
            }
        }

        /// <summary>Care cost before the repair shop's discount.</summary>
        public int BaseCareFee
        {
            get
            {
                float share = role == CityRole.Home ? 0.10f : role == CityRole.Producer ? 0.15f : role == CityRole.Special ? 0.08f : 0f;
                return Mathf.Max(role == CityRole.Decor ? 0 : 5, Mathf.RoundToInt(price * share));
            }
        }

        /// <summary>The footprint for a rotation (0-3 quarter turns): odd turns swap width and depth.</summary>
        public Vector2Int Size(int rot) => rot % 2 == 0 ? new Vector2Int(w, h) : new Vector2Int(h, w);
    }
}
