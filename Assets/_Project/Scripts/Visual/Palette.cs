using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>Game colors. Backdrop and platform colors follow the current <see cref="WorldTheme"/>; emission above 1 blooms.</summary>
    public static class Palette
    {
        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // Background
        public static Color BgTop => WorldTheme.Current.bgTop;
        public static Color BgBottom => WorldTheme.Current.bgBottom;
        public static Color BgGlow => WorldTheme.Current.bgGlow;
        public static Color BgLines => WorldTheme.Current.bgLines;
        public static Color BgPlanet => WorldTheme.Current.bgPlanet;
        public static Color BgStar => WorldTheme.Current.bgStar;
        public static Color Ambient => WorldTheme.Current.ambient;

        // Platform
        public static Color Slab => WorldTheme.Current.slab;
        public static Color SlabEdgeGlow => WorldTheme.Current.slabEdgeGlow;
        public static Color Pillar => WorldTheme.Current.pillar;
        public static Color TileTop => WorldTheme.Current.tileTop;
        public static Color TileSelfLight => WorldTheme.Current.tileSelfLight;
        public static Color TileGlow => WorldTheme.Current.tileGlow;
        public static readonly Color TileWarningTop = Hex("#F7A3AE");
        public static readonly Color TileWarningGlow = new Color(2.6f, 0.25f, 0.3f);
        public static readonly Color Shadow = Hex("#A6A9D2");

        // Hazards & pickups
        public static readonly Color Block = Hex("#C42A2E");
        public static readonly Color BlockGlow = new Color(1.45f, 0.1f, 0.12f);
        public static readonly Color Marker = Hex("#FF9AA3");
        public static readonly Color MarkerGlow = new Color(2.2f, 0.45f, 0.5f);
        public static readonly Color Coin = Hex("#FFC531");
        public static readonly Color CoinGlow = new Color(1.0f, 0.62f, 0.05f);
        public static readonly Color CoinRim = Hex("#E59A12");

        // Power-ups
        public static readonly Color ShieldBubble = new Color(0.55f, 0.92f, 1f, 0.42f);
        public static readonly Color ShieldGlow = new Color(0.45f, 1.3f, 1.7f);
        public static readonly Color ShieldPickup = Hex("#7FE9FF");
        public static readonly Color ShieldPickupGlow = new Color(0.5f, 1.8f, 2.2f);

        // Robot
        public static readonly Color RobotBody = Hex("#A3AFCF");
        public static readonly Color RobotLight = Hex("#C3CCE6");
        public static readonly Color RobotDark = Hex("#3E4566");
        public static readonly Color RobotEye = new Color(0.4f, 1.6f, 2.2f);

        // UI
        public static readonly Color UiText = Hex("#EEF0FF");
        public static readonly Color UiCyan = Hex("#9FF3FF");
        public static readonly Color UiRed = Hex("#FF7A85");
        public static readonly Color UiGold = Hex("#FFD45C");
        public static readonly Color UiButton = new Color(0.93f, 0.94f, 1f, 0.16f);
        public static readonly Color UiDim = new Color(0.2f, 0.19f, 0.36f, 0.55f);
    }
}
