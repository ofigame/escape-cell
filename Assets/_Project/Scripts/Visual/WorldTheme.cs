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
                bgTop = Hex("#5B4C86"), bgBottom = Hex("#3A2F60"), bgGlow = Hex("#8C6FB8"),
                bgLines = Hex("#C0A0E8"), bgPlanet = Hex("#A88AD0"), bgStar = Hex("#F4E6FF"),
                ambient = Hex("#D6CCEE"),
                slab = Hex("#DCD2F0"), slabEdgeGlow = new Color(1.3f, 0.8f, 1.8f), pillar = Hex("#6E5C9A"),
                tileTop = Hex("#F4EEFF"), tileSelfLight = new Color(0.2f, 0.17f, 0.24f), tileGlow = new Color(1.4f, 0.8f, 1.9f),
                accent = Hex("#D9A6FF"),
            },
            new WorldTheme
            {
                key = "world.lava",
                bgTop = Hex("#7A4636"), bgBottom = Hex("#4A2A22"), bgGlow = Hex("#B86A4A"),
                bgLines = Hex("#E8A07A"), bgPlanet = Hex("#D88A62"), bgStar = Hex("#FFE4CC"),
                ambient = Hex("#EDC8B4"),
                slab = Hex("#EED2C2"), slabEdgeGlow = new Color(1.9f, 0.9f, 0.4f), pillar = Hex("#8A5040"),
                tileTop = Hex("#FFF0E6"), tileSelfLight = new Color(0.24f, 0.17f, 0.13f), tileGlow = new Color(1.9f, 0.95f, 0.45f),
                accent = Hex("#FFA46B"),
            },
            Make("world.forest", "#2F5D50", "#1C3B33", "#4F9A80", "#8FD9B8", "#5FA88C", "#E2FFF0", "#CBEBDD",
                "#CFEFE0", new Color(0.4f, 1.9f, 1.2f), "#3E7A66", "#E8FFF4", new Color(0.15f, 0.2f, 0.17f), new Color(0.4f, 1.9f, 1.2f), "#8CFFD0"),
            Make("world.desert", "#9A6A3A", "#5E3E22", "#D6A05A", "#F0C890", "#E0A060", "#FFF0D8", "#F2DCC0",
                "#F2DEB8", new Color(2.1f, 1.5f, 0.5f), "#8E6338", "#FFF4DE", new Color(0.22f, 0.19f, 0.14f), new Color(2.1f, 1.5f, 0.5f), "#FFD27A"),
            Make("world.ocean", "#1F4F7A", "#0E2A48", "#2E7FB0", "#6FBFE6", "#3E8FC0", "#E0F6FF", "#B8D8EE",
                "#BFE0F2", new Color(0.3f, 1.4f, 2.2f), "#2C5E85", "#DDF3FF", new Color(0.14f, 0.19f, 0.24f), new Color(0.3f, 1.4f, 2.2f), "#7FD8FF"),
            Make("world.candy", "#B0578F", "#6E2F63", "#E28AC0", "#FFC0E4", "#F09AD0", "#FFF0FA", "#F4CDE6",
                "#F7D4EA", new Color(2.1f, 0.7f, 1.6f), "#9A4C80", "#FFF0FA", new Color(0.24f, 0.17f, 0.22f), new Color(2.1f, 0.7f, 1.6f), "#FF9ED8"),
            Make("world.toxic", "#4E6A44", "#2E4228", "#7FA060", "#B8D890", "#90B070", "#F0FFE0", "#D2E4C4",
                "#DCEACF", new Color(1.0f, 1.8f, 0.6f), "#5E7A4C", "#F3FBEA", new Color(0.18f, 0.22f, 0.15f), new Color(1.0f, 1.8f, 0.6f), "#C6F08A"),
            Make("world.midnight", "#4A5A86", "#2C3658", "#7088B8", "#A8BCE8", "#90A4D0", "#F0F4FF", "#CCD6EE",
                "#D6DEF2", new Color(0.8f, 1.2f, 1.9f), "#5A6A98", "#F2F5FF", new Color(0.18f, 0.2f, 0.26f), new Color(0.8f, 1.2f, 1.9f), "#A8C4FF"),
            Make("world.aurora", "#1E3F5A", "#0F2033", "#3FA08A", "#B08CFF", "#3E7F90", "#E6FFF8", "#C8E6E0",
                "#CDEDE6", new Color(0.5f, 2.0f, 1.6f), "#2A5A6A", "#E6FFF8", new Color(0.15f, 0.2f, 0.2f), new Color(0.5f, 2.0f, 1.6f), "#9CFFE6"),
            Make("world.storm", "#3A3F55", "#1E2133", "#5E6690", "#A8B0D8", "#6A7298", "#FFF8C8", "#C6CADA",
                "#C8CDE0", new Color(1.8f, 1.8f, 0.6f), "#4A5070", "#E4E8F4", new Color(0.18f, 0.18f, 0.2f), new Color(1.8f, 1.8f, 0.6f), "#FFF07A"),
            Make("world.cyber", "#2F6A6E", "#1C4246", "#4FA0A0", "#90D8D0", "#6FBAB4", "#E8FFFC", "#C4E4E0",
                "#CFEDEA", new Color(0.5f, 1.6f, 1.5f), "#3E7C7C", "#EEFBF9", new Color(0.15f, 0.21f, 0.2f), new Color(0.5f, 1.6f, 1.5f), "#8FF0E0"),
            Make("world.galaxy", "#5A4A88", "#362A5E", "#8C72C0", "#C8A8F0", "#A890D8", "#FFF2FF", "#DCCFF0",
                "#E2D6F6", new Color(1.3f, 0.9f, 1.9f), "#6E5AA0", "#F6F0FF", new Color(0.21f, 0.18f, 0.25f), new Color(1.3f, 0.9f, 1.9f), "#E2B8FF"),
            // Floors 16-19.
            Make("world.crystal", "#2E5C6E", "#1A3448", "#4FB3BF", "#8FE3E8", "#6FC7D1", "#D8FBFF", "#BFE6EC",
                "#CDEFF3", new Color(0.5f, 1.7f, 1.9f), "#3F7C8C", "#EAFBFD", new Color(0.14f, 0.2f, 0.22f), new Color(0.5f, 1.7f, 1.9f), "#7FF0E8"),
            Make("world.festival", "#5B2C6F", "#331848", "#B05BC4", "#E79BF2", "#D58FE0", "#FBE2FF", "#E3C4EA",
                "#F0D6F5", new Color(1.7f, 0.6f, 1.9f), "#7A3E8A", "#FBEAFE", new Color(0.22f, 0.15f, 0.24f), new Color(1.7f, 0.7f, 1.9f), "#FF9BF0"),
            Make("world.factory", "#6E4A3A", "#3E281F", "#C4835B", "#F2B48F", "#E39A6C", "#FFE6D2", "#EED2C2",
                "#F1DCCF", new Color(1.9f, 0.9f, 0.4f), "#8A5A44", "#FBEEE6", new Color(0.24f, 0.18f, 0.14f), new Color(1.9f, 1.0f, 0.45f), "#FFB36B"),
            Make("world.funfair", "#6A2E4A", "#3A1830", "#D25A7A", "#FFC36B", "#FF8FA0", "#FFF0C4", "#F2CFD6",
                "#F8DCE2", new Color(2.0f, 0.7f, 0.9f), "#8A3A5A", "#FFF0F3", new Color(0.24f, 0.16f, 0.18f), new Color(2.0f, 0.9f, 1.0f), "#FFD36B"),
            // Floors 20-24: five daylight floors added below the roof.
            Make("world.garden", "#6A8A5A", "#3E5A3A", "#A0C080", "#E8C8E0", "#C8A8D0", "#FFF6FA", "#E2EED8",
                "#E8F0DE", new Color(1.6f, 1.1f, 1.4f), "#6E8A5E", "#FAFFF4", new Color(0.2f, 0.23f, 0.18f), new Color(1.6f, 1.1f, 1.4f), "#FFB8D8"),
            Make("world.canyon", "#9A5A3E", "#5E3424", "#D88A5A", "#F2B890", "#E0986A", "#FFEEDD", "#F0D0B8",
                "#F2D8C4", new Color(2.0f, 1.0f, 0.5f), "#9A5C40", "#FFF2E8", new Color(0.25f, 0.18f, 0.14f), new Color(2.0f, 1.0f, 0.5f), "#FFB070"),
            Make("world.snow", "#6E8AAE", "#46607E", "#A0BCDA", "#D8E8F8", "#B8D0E8", "#FFFFFF", "#DDE8F4",
                "#E6EEF8", new Color(0.9f, 1.4f, 1.9f), "#6A84A6", "#FBFDFF", new Color(0.2f, 0.22f, 0.26f), new Color(0.9f, 1.4f, 1.9f), "#CFE6FF"),
            Make("world.harbor", "#4A6A8E", "#2A3E5A", "#E8A060", "#F2C890", "#F0B070", "#FFF0DA", "#E8D8C8",
                "#EADFD2", new Color(1.9f, 1.2f, 0.5f), "#5A6A84", "#FFF8EE", new Color(0.22f, 0.2f, 0.17f), new Color(1.9f, 1.2f, 0.5f), "#FFC27A"),
            Make("world.clouds", "#7A9AD0", "#5272A8", "#B8D0F0", "#F0F6FF", "#D8E6FA", "#FFFFFF", "#E6EEFA",
                "#EEF3FC", new Color(1.2f, 1.5f, 2.0f), "#7A90C0", "#FFFFFF", new Color(0.22f, 0.23f, 0.27f), new Color(1.2f, 1.5f, 2.0f), "#FFE2A0"),
            Make("world.roof", "#3E4A8A", "#262C60", "#F2B66B", "#FFE0A0", "#FFD27A", "#FFF6DA", "#F5E2C8",
                "#F7E6CF", new Color(2.0f, 1.4f, 0.5f), "#6A5A9A", "#FFF7EA", new Color(0.25f, 0.22f, 0.16f), new Color(2.0f, 1.5f, 0.6f), "#FFE07A"),
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
