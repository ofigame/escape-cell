using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>The look of one world (10 levels): backdrop, platform and lighting colors.</summary>
    public class WorldTheme
    {
        public string key;

        // Backdrop
        public Color bgTop, bgBottom, bgGlow, bgLines, bgPlanet, bgStar;
        public Color ambient;

        // Platform
        public Color slab, slabEdgeGlow, pillar;
        public Color tileTop, tileSelfLight, tileGlow;

        /// <summary>Accent used for map nodes and highlights.</summary>
        public Color accent;

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static readonly WorldTheme[] All =
        {
            new WorldTheme
            {
                key = "world.lavender",
                bgTop = Hex("#5A5790"), bgBottom = Hex("#4E4C85"), bgGlow = Hex("#7472AE"),
                bgLines = Hex("#9A93D6"), bgPlanet = Hex("#8584BF"), bgStar = Hex("#D6D3FA"),
                ambient = Hex("#B9B8E3"),
                slab = Hex("#C9CCEC"), slabEdgeGlow = new Color(0.45f, 1.5f, 1.8f), pillar = Hex("#6E6EA8"),
                tileTop = Hex("#E4E7FA"), tileSelfLight = new Color(0.16f, 0.17f, 0.24f), tileGlow = new Color(0.45f, 1.6f, 1.9f),
                accent = Hex("#9FF3FF"),
            },
            new WorldTheme
            {
                key = "world.sunset",
                bgTop = Hex("#7B4A78"), bgBottom = Hex("#4A2D5C"), bgGlow = Hex("#C47A86"),
                bgLines = Hex("#E7A0A8"), bgPlanet = Hex("#E59A7E"), bgStar = Hex("#FFE2D2"),
                ambient = Hex("#F1C9C4"),
                slab = Hex("#F1D2CC"), slabEdgeGlow = new Color(1.9f, 0.85f, 0.55f), pillar = Hex("#8E5A7A"),
                tileTop = Hex("#FFF1EA"), tileSelfLight = new Color(0.22f, 0.16f, 0.14f), tileGlow = new Color(2.0f, 0.9f, 0.55f),
                accent = Hex("#FFB48A"),
            },
            new WorldTheme
            {
                key = "world.ice",
                bgTop = Hex("#3F7AA6"), bgBottom = Hex("#24496F"), bgGlow = Hex("#6FB2DA"),
                bgLines = Hex("#A9DDF6"), bgPlanet = Hex("#8CC8E8"), bgStar = Hex("#EFFAFF"),
                ambient = Hex("#CFE6F7"),
                slab = Hex("#D3EAF7"), slabEdgeGlow = new Color(0.7f, 1.6f, 2.1f), pillar = Hex("#4C7EA6"),
                tileTop = Hex("#F2FAFF"), tileSelfLight = new Color(0.16f, 0.2f, 0.24f), tileGlow = new Color(0.75f, 1.7f, 2.2f),
                accent = Hex("#BDEBFF"),
            },
            new WorldTheme
            {
                key = "world.neon",
                bgTop = Hex("#1B1842"), bgBottom = Hex("#0D0B24"), bgGlow = Hex("#3A2F82"),
                bgLines = Hex("#7A4DD8"), bgPlanet = Hex("#4B3A9A"), bgStar = Hex("#F2B8FF"),
                ambient = Hex("#7C78C4"),
                slab = Hex("#3B3872"), slabEdgeGlow = new Color(2.0f, 0.4f, 2.1f), pillar = Hex("#231F52"),
                tileTop = Hex("#4A4790"), tileSelfLight = new Color(0.1f, 0.08f, 0.2f), tileGlow = new Color(1.9f, 0.45f, 2.2f),
                accent = Hex("#F08CFF"),
            },
            new WorldTheme
            {
                key = "world.lava",
                bgTop = Hex("#4A1F22"), bgBottom = Hex("#240D10"), bgGlow = Hex("#8A3524"),
                bgLines = Hex("#D86A3A"), bgPlanet = Hex("#A8452C"), bgStar = Hex("#FFD2A0"),
                ambient = Hex("#C49A8E"),
                slab = Hex("#5A4245"), slabEdgeGlow = new Color(2.4f, 0.95f, 0.2f), pillar = Hex("#331E22"),
                tileTop = Hex("#6E5558"), tileSelfLight = new Color(0.12f, 0.06f, 0.04f), tileGlow = new Color(2.4f, 1.0f, 0.2f),
                accent = Hex("#FFB25C"),
            },
            Make("world.forest", "#2F5D50", "#1C3B33", "#4F9A80", "#8FD9B8", "#5FA88C", "#E2FFF0", "#CBEBDD",
                "#CFEFE0", new Color(0.4f, 1.9f, 1.2f), "#3E7A66", "#E8FFF4", new Color(0.15f, 0.2f, 0.17f), new Color(0.4f, 1.9f, 1.2f), "#8CFFD0"),
            Make("world.desert", "#9A6A3A", "#5E3E22", "#D6A05A", "#F0C890", "#E0A060", "#FFF0D8", "#F2DCC0",
                "#F2DEB8", new Color(2.1f, 1.5f, 0.5f), "#8E6338", "#FFF4DE", new Color(0.22f, 0.19f, 0.14f), new Color(2.1f, 1.5f, 0.5f), "#FFD27A"),
            Make("world.ocean", "#1F4F7A", "#0E2A48", "#2E7FB0", "#6FBFE6", "#3E8FC0", "#E0F6FF", "#B8D8EE",
                "#BFE0F2", new Color(0.3f, 1.4f, 2.2f), "#2C5E85", "#DDF3FF", new Color(0.14f, 0.19f, 0.24f), new Color(0.3f, 1.4f, 2.2f), "#7FD8FF"),
            Make("world.candy", "#B0578F", "#6E2F63", "#E28AC0", "#FFC0E4", "#F09AD0", "#FFF0FA", "#F4CDE6",
                "#F7D4EA", new Color(2.1f, 0.7f, 1.6f), "#9A4C80", "#FFF0FA", new Color(0.24f, 0.17f, 0.22f), new Color(2.1f, 0.7f, 1.6f), "#FF9ED8"),
            Make("world.toxic", "#23402A", "#101F14", "#3F7A3A", "#7FD860", "#4F8A40", "#D8FFC0", "#9EB89E",
                "#4A5C4C", new Color(0.9f, 2.3f, 0.4f), "#1F2E22", "#3A4A3C", new Color(0.06f, 0.1f, 0.05f), new Color(0.9f, 2.3f, 0.4f), "#B6FF6A"),
            Make("world.midnight", "#10183A", "#070B1E", "#24356E", "#5068C0", "#2A3C80", "#C8D6FF", "#7A86BA",
                "#2A3462", new Color(0.6f, 0.9f, 2.4f), "#141C40", "#303C6E", new Color(0.06f, 0.08f, 0.18f), new Color(0.6f, 0.9f, 2.4f), "#8FB0FF"),
            Make("world.aurora", "#1E3F5A", "#0F2033", "#3FA08A", "#B08CFF", "#3E7F90", "#E6FFF8", "#C8E6E0",
                "#CDEDE6", new Color(0.5f, 2.0f, 1.6f), "#2A5A6A", "#E6FFF8", new Color(0.15f, 0.2f, 0.2f), new Color(0.5f, 2.0f, 1.6f), "#9CFFE6"),
            Make("world.storm", "#3A3F55", "#1E2133", "#5E6690", "#A8B0D8", "#6A7298", "#FFF8C8", "#C6CADA",
                "#C8CDE0", new Color(1.8f, 1.8f, 0.6f), "#4A5070", "#E4E8F4", new Color(0.18f, 0.18f, 0.2f), new Color(1.8f, 1.8f, 0.6f), "#FFF07A"),
            Make("world.cyber", "#0A2A2E", "#03161A", "#0E5F66", "#30C8C0", "#14707A", "#C0FFFA", "#7FB3B3",
                "#1E4A4E", new Color(0.2f, 2.2f, 2.0f), "#0B2528", "#183C40", new Color(0.03f, 0.1f, 0.1f), new Color(0.2f, 2.2f, 2.0f), "#5CFFF0"),
            Make("world.galaxy", "#2A1450", "#120626", "#6A2FA0", "#C080FF", "#7A40B0", "#FFE8FF", "#D2C2EA",
                "#DCCBF5", new Color(1.6f, 0.9f, 2.4f), "#4A2A80", "#F2E8FF", new Color(0.2f, 0.16f, 0.24f), new Color(1.6f, 0.9f, 2.4f), "#E0B0FF"),
        };

        private static WorldTheme Make(string key, string bgTop, string bgBottom, string bgGlow, string bgLines, string bgPlanet,
            string bgStar, string ambient, string slab, Color slabEdgeGlow, string pillar, string tileTop, Color tileSelfLight,
            Color tileGlow, string accent)
        {
            return new WorldTheme
            {
                key = key,
                bgTop = Hex(bgTop), bgBottom = Hex(bgBottom), bgGlow = Hex(bgGlow),
                bgLines = Hex(bgLines), bgPlanet = Hex(bgPlanet), bgStar = Hex(bgStar),
                ambient = Hex(ambient),
                slab = Hex(slab), slabEdgeGlow = slabEdgeGlow, pillar = Hex(pillar),
                tileTop = Hex(tileTop), tileSelfLight = tileSelfLight, tileGlow = tileGlow,
                accent = Hex(accent),
            };
        }

        public static WorldTheme Current { get; private set; } = All[0];

        public static WorldTheme ForWorld(int worldIndex) => All[Mathf.Clamp(worldIndex, 0, All.Length - 1)];

        public static void SetCurrent(int worldIndex) => Current = ForWorld(worldIndex);
    }
}
