using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The look of one floor (10 levels): backdrop, landscape, weather, blocks, platform and lighting colours.
    /// Every floor has two designs on the same theme: <see cref="ForWorld"/> is the first (levels 1-5) and its
    /// <see cref="variant"/> the second (levels 6-10), picked by <see cref="ForLevel"/>.
    /// The 25 floors make up the story's eight big worlds: Channel (1-2), Forest (3-5), Red Canyon (6-8),
    /// Sea Floor (9-11), Snowy Mountain (12-14), Crystal Cave (15-17), Cloud Bridge (18-21) and Star Road (22-25).
    /// </summary>
    public class WorldTheme
    {
        public string key;

        // Backdrop
        public Color bgTop, bgBottom, bgGlow, bgLines, bgPlanet, bgStar;
        public Color ambient;
        public Scenery scenery;
        public Weather.Kind weather;

        // Platform
        public Color slab, slabEdgeGlow, pillar;
        public Color tileTop, tileSelfLight, tileGlow;
        public BlockStyle block;

        /// <summary>Accent used for map nodes and highlights.</summary>
        public Color accent;

        /// <summary>The second design of the floor (levels 6-10); null on the second design itself.</summary>
        public WorldTheme variant;

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static readonly WorldTheme[] All =
        {
            // ---------- Channel World ----------
            Pair(P("world.canal", Scenery.Pipes, Weather.Kind.Sparkle, BlockStyle.Crate,
                    "#5E6A7E", "#3C4556", "#8A97AC", "#B8C4D6", "#7E8BA0", "#E8EEF8", "#CDD5E2", "#D3D9E3", "#5C6676", "#EEF2F7", "#8FD3FF", new Color(0.5f, 1.4f, 2.0f)),
                P("world.canal", Scenery.PipesFire, Weather.Kind.Embers, BlockStyle.MeteorLava,
                    "#6E5A5A", "#3E3236", "#C07A52", "#E0A880", "#B87050", "#FFE2CC", "#E2CFC4", "#E2D3CA", "#6E5552", "#F7EEE8", "#FFA060", new Color(2.0f, 1.0f, 0.45f))),
            Pair(P("world.factory", Scenery.Factory, Weather.Kind.Smoke, BlockStyle.Crate,
                    "#6E4A3A", "#3E281F", "#C4835B", "#F2B48F", "#E39A6C", "#FFE6D2", "#EED2C2", "#F1DCCF", "#8A5A44", "#FBEEE6", "#FFB36B", new Color(1.9f, 1.0f, 0.45f)),
                P("world.factory", Scenery.Gate, Weather.Kind.Sparkle, BlockStyle.Crate,
                    "#5E5A44", "#33301F", "#9CC07A", "#CDE6A8", "#A8C888", "#F2FFE2", "#E2E2CC", "#E8E2D0", "#6E6248", "#F8F6EC", "#B8F07A", new Color(1.0f, 1.9f, 0.6f))),

            // ---------- Forest World ----------
            Pair(P("world.forest", Scenery.Trees, Weather.Kind.Petals, BlockStyle.Boulder,
                    "#6FA88C", "#3E6E5A", "#A8D8B8", "#D8F4E0", "#F4F0C0", "#FFFFF0", "#DDEEDF", "#DDF0E2", "#4E826A", "#F2FFF6", "#8CFFD0", new Color(0.4f, 1.9f, 1.2f)),
                P("world.forest", Scenery.CubeTrees, Weather.Kind.DataRain, BlockStyle.DataCube,
                    "#4E8072", "#2A4E44", "#7FBAA4", "#B8E8D4", "#9AD0BC", "#EAFFF6", "#D2E8DE", "#D6EEE4", "#3E6E60", "#EEFCF6", "#7FF0D8", new Color(0.4f, 1.8f, 1.5f))),
            Pair(P("world.garden", Scenery.Flowers, Weather.Kind.Petals, BlockStyle.Crate,
                    "#6A8A5A", "#3E5A3A", "#A0C080", "#E8C8E0", "#C8A8D0", "#FFF6FA", "#E2EED8", "#E8F0DE", "#6E8A5E", "#FAFFF4", "#FFB8D8", new Color(1.6f, 1.1f, 1.4f)),
                P("world.garden", Scenery.Ruins, Weather.Kind.Fireflies, BlockStyle.Boulder,
                    "#5A7A5E", "#344A38", "#90B488", "#D0E8B8", "#B8D0A0", "#F6FFEE", "#DCE8D2", "#E2ECD8", "#56725A", "#F6FCF0", "#D8F08A", new Color(1.4f, 1.8f, 0.6f))),
            Pair(P("world.nightforest", Scenery.NightTrees, Weather.Kind.Fireflies, BlockStyle.Boulder,
                    "#24384A", "#142230", "#3E6A70", "#7FC8B0", "#C8E6D0", "#E8FFF0", "#B8CCD0", "#C4D6D4", "#2E4A50", "#E4F2EE", "#A8FFB8", new Color(0.6f, 1.9f, 0.9f)),
                P("world.nightforest", Scenery.Mushrooms, Weather.Kind.Fireflies, BlockStyle.Boulder,
                    "#2E2A4E", "#18162E", "#6A4E8A", "#C8A0F0", "#E0C8FF", "#F4E8FF", "#C8C0DC", "#D2CCE2", "#3E3660", "#EEE8F8", "#E0A8FF", new Color(1.5f, 0.9f, 1.9f))),

            // ---------- Red Canyon ----------
            Pair(P("world.canyon", Scenery.Mesas, Weather.Kind.Sand, BlockStyle.Sandstone,
                    "#9A5A3E", "#5E3424", "#D88A5A", "#F2B890", "#E0986A", "#FFEEDD", "#F0D0B8", "#F2D8C4", "#9A5C40", "#FFF2E8", "#FFB070", new Color(2.0f, 1.0f, 0.5f)),
                P("world.canyon", Scenery.MesasStorm, Weather.Kind.Sand, BlockStyle.Sandstone,
                    "#B07848", "#6E4528", "#E8B078", "#F6D0A0", "#F0C080", "#FFF0DC", "#F2D8C0", "#F2DCC6", "#9A6040", "#FFF4E8", "#FFB878", new Color(2.0f, 1.2f, 0.55f))),
            Pair(P("world.desert", Scenery.Dunes, Weather.Kind.Sand, BlockStyle.Sandstone,
                    "#9A6A3A", "#5E3E22", "#D6A05A", "#F0C890", "#E0A060", "#FFF0D8", "#F2DCC0", "#F2DEB8", "#8E6338", "#FFF4DE", "#FFD27A", new Color(2.1f, 1.5f, 0.5f)),
                P("world.desert", Scenery.DunesSun, Weather.Kind.Sand, BlockStyle.Sandstone,
                    "#8A4A5A", "#4A2838", "#F09A60", "#FFC890", "#FFB060", "#FFE8D0", "#F0C8B8", "#F2D2C2", "#8A5048", "#FFF0E6", "#FFA870", new Color(2.1f, 1.0f, 0.5f))),
            Pair(P("world.mine", Scenery.Arches, Weather.Kind.Sand, BlockStyle.Boulder,
                    "#6A5040", "#3A2A20", "#B08050", "#E0B080", "#D09060", "#FFE8C8", "#E2CCB4", "#E6D4C0", "#6A4E3A", "#F8EEE2", "#FFC070", new Color(2.0f, 1.3f, 0.5f)),
                P("world.mine", Scenery.Tablets, Weather.Kind.Crystal, BlockStyle.Sandstone,
                    "#3E4A52", "#222A30", "#4FA0A8", "#90E0E0", "#7FC8C8", "#E0FFFF", "#C8D6D8", "#D2DCDC", "#3E5056", "#ECF4F4", "#8FF0F0", new Color(0.5f, 1.8f, 1.8f))),

            // ---------- Sea Floor ----------
            Pair(P("world.harbor", Scenery.Harbor, Weather.Kind.Fireflies, BlockStyle.Barrel,
                    "#4A6A8E", "#2A3E5A", "#E8A060", "#F2C890", "#F0B070", "#FFF0DA", "#E8D8C8", "#EADFD2", "#5A6A84", "#FFF8EE", "#FFC27A", new Color(1.9f, 1.2f, 0.5f)),
                P("world.harbor", Scenery.HarborFog, Weather.Kind.Smoke, BlockStyle.Barrel,
                    "#7A8A98", "#4E5C6A", "#C8D0D6", "#E8EEF2", "#D8E0E6", "#FFFFFF", "#DCE2E8", "#E2E6EA", "#5E6C7A", "#F6F8FA", "#FFD08A", new Color(1.9f, 1.4f, 0.6f))),
            Pair(P("world.ocean", Scenery.Corals, Weather.Kind.Bubbles, BlockStyle.DivingWeight,
                    "#1F4F7A", "#0E2A48", "#2E7FB0", "#6FBFE6", "#3E8FC0", "#E0F6FF", "#B8D8EE", "#BFE0F2", "#2C5E85", "#DDF3FF", "#7FD8FF", new Color(0.3f, 1.4f, 2.2f)),
                P("world.ocean", Scenery.Jellyfish, Weather.Kind.Bubbles, BlockStyle.DivingWeight,
                    "#143A5E", "#081C30", "#2E6A9A", "#9AD8FF", "#C8A8FF", "#E8F4FF", "#A8C4DE", "#B8D2E6", "#1E4466", "#DCEEFA", "#C8A0FF", new Color(1.4f, 1.0f, 2.2f))),
            Pair(P("world.server", Scenery.Server, Weather.Kind.Bubbles, BlockStyle.DataCube,
                    "#1E4A5A", "#0E2834", "#2E8A9A", "#7FE0E8", "#4FB8C8", "#D8FFFF", "#B0D4DA", "#C0DEE2", "#24505C", "#E2F4F6", "#6FF0F8", new Color(0.4f, 1.8f, 2.0f)),
                P("world.server", Scenery.ServerRed, Weather.Kind.DataRain, BlockStyle.DataCube,
                    "#3A3050", "#1C1830", "#8A4A6A", "#F08AA0", "#FF7A90", "#FFE0E8", "#D0C4D4", "#D8D0DE", "#3E3458", "#F2EEF6", "#FF8AA0", new Color(2.0f, 0.6f, 0.8f))),

            // ---------- Snowy Mountain ----------
            Pair(P("world.aurora", Scenery.SnowHills, Weather.Kind.Snow, BlockStyle.IceCube,
                    "#8AA8C8", "#5A7898", "#C8DEF0", "#F0F8FF", "#E8F0FA", "#FFFFFF", "#E2ECF6", "#E8F0F8", "#6A86A6", "#FAFDFF", "#A8E0FF", new Color(0.8f, 1.5f, 2.0f)),
                P("world.aurora", Scenery.Aurora, Weather.Kind.Snow, BlockStyle.IceCube,
                    "#1E3F5A", "#0F2033", "#3FA08A", "#B08CFF", "#3E7F90", "#E6FFF8", "#C8E6E0", "#CDEDE6", "#2A5A6A", "#E6FFF8", "#9CFFE6", new Color(0.5f, 2.0f, 1.6f))),
            Pair(P("world.snow", Scenery.Pines, Weather.Kind.Snow, BlockStyle.IceCube,
                    "#6E8AAE", "#46607E", "#A0BCDA", "#D8E8F8", "#B8D0E8", "#FFFFFF", "#DDE8F4", "#E6EEF8", "#6A84A6", "#FBFDFF", "#CFE6FF", new Color(0.9f, 1.4f, 1.9f)),
                P("world.snow", Scenery.PinesBlizzard, Weather.Kind.Snow, BlockStyle.IceCube,
                    "#8E98A8", "#5E6878", "#C8CED8", "#EEF2F6", "#DDE2EA", "#FFFFFF", "#E0E4EA", "#E6EAF0", "#687284", "#F8FAFC", "#B8D8F8", new Color(0.9f, 1.4f, 1.9f))),
            Pair(P("world.ice", Scenery.Icicles, Weather.Kind.Crystal, BlockStyle.IceCube,
                    "#3F7AA6", "#24496F", "#6FB2DA", "#A9DDF6", "#8CC8E8", "#EFFAFF", "#CFE6F7", "#D3EAF7", "#4C7EA6", "#F2FAFF", "#BDEBFF", new Color(0.75f, 1.7f, 2.2f)),
                P("world.ice", Scenery.Towers, Weather.Kind.Snow, BlockStyle.IceCube,
                    "#2E5A86", "#18344E", "#5A9AC8", "#A8DCF8", "#8CC8E8", "#EFFAFF", "#C4DCEE", "#CCE2F2", "#3A6A96", "#EEF8FF", "#A8E8FF", new Color(0.7f, 1.6f, 2.1f))),

            // ---------- Crystal Cave ----------
            Pair(P("world.crystal", Scenery.Shards, Weather.Kind.Crystal, BlockStyle.Crystal,
                    "#2E5C6E", "#1A3448", "#4FB3BF", "#8FE3E8", "#6FC7D1", "#D8FBFF", "#BFE6EC", "#CDEFF3", "#3F7C8C", "#EAFBFD", "#7FF0E8", new Color(0.5f, 1.7f, 1.9f)),
                P("world.crystal", Scenery.Prisms, Weather.Kind.Crystal, BlockStyle.Crystal,
                    "#4A3E7A", "#2A2248", "#8A7AD0", "#C8B8FF", "#A8E8F0", "#F4F0FF", "#D4CCEC", "#DCD6F0", "#4E4486", "#F2EFFC", "#B8A8FF", new Color(1.2f, 1.0f, 2.0f))),
            Pair(P("world.mirror", Scenery.Mirrors, Weather.Kind.Sparkle, BlockStyle.Crystal,
                    "#5A6A7E", "#343E50", "#A8B8CE", "#DCE6F2", "#C8D4E2", "#FFFFFF", "#D8DEE8", "#E0E6EE", "#4E5A6E", "#F6F8FB", "#CFE8FF", new Color(1.2f, 1.5f, 2.0f)),
                P("world.mirror", Scenery.MirrorsDark, Weather.Kind.Crystal, BlockStyle.Crystal,
                    "#2E2A40", "#16141F", "#5A4E7A", "#9A8AC8", "#6A5A9A", "#E0D8FF", "#BEB8D0", "#CAC4DA", "#2E2846", "#E8E4F2", "#9AF0FF", new Color(0.5f, 1.7f, 2.0f))),
            Pair(P("world.acid", Scenery.AcidShards, Weather.Kind.Toxic, BlockStyle.Crystal,
                    "#4E6A44", "#2E4228", "#7FA060", "#B8D890", "#90B070", "#F0FFE0", "#D2E4C4", "#DCEACF", "#5E7A4C", "#F3FBEA", "#C6F08A", new Color(1.0f, 1.8f, 0.6f)),
                P("world.acid", Scenery.AcidPools, Weather.Kind.Toxic, BlockStyle.Crystal,
                    "#3A5030", "#1E2E18", "#7AB050", "#C8F090", "#A8E070", "#F0FFE0", "#CCDCC0", "#D6E4CC", "#3E5A34", "#EEF6E8", "#C8FF7A", new Color(1.0f, 2.0f, 0.5f))),

            // ---------- Cloud Bridge ----------
            Pair(P("world.clouds", Scenery.Clouds, Weather.Kind.Sparkle, BlockStyle.StormCube,
                    "#7A9AD0", "#5272A8", "#B8D0F0", "#F0F6FF", "#D8E6FA", "#FFFFFF", "#E6EEFA", "#EEF3FC", "#7A90C0", "#FFFFFF", "#FFE2A0", new Color(1.2f, 1.5f, 2.0f)),
                P("world.clouds", Scenery.Islands, Weather.Kind.Sparkle, BlockStyle.StormCube,
                    "#6A9AE0", "#3E6AB0", "#B0D4FA", "#F0F8FF", "#FFF0C0", "#FFFFFF", "#E2ECFA", "#EAF0FA", "#6A86B8", "#FFFFFF", "#FFD890", new Color(1.9f, 1.5f, 0.7f))),
            Pair(P("world.sunset", Scenery.SunsetClouds, Weather.Kind.Petals, BlockStyle.Crate,
                    "#7B4A78", "#4A2D5C", "#C47A86", "#E7A0A8", "#E59A7E", "#FFE2D2", "#F1C9C4", "#F1D2CC", "#8E5A7A", "#FFF1EA", "#FFB48A", new Color(2.0f, 0.9f, 0.55f)),
                P("world.sunset", Scenery.Balloons, Weather.Kind.Confetti, BlockStyle.Gumdrop,
                    "#8A5A8A", "#4E3058", "#F0A0A0", "#FFD0C0", "#FFC090", "#FFF0E8", "#F2D2D2", "#F4DAD6", "#8A5A7A", "#FFF4F0", "#FFB0A0", new Color(2.0f, 0.9f, 0.8f))),
            Pair(P("world.storm", Scenery.StormClouds, Weather.Kind.Thunder, BlockStyle.StormCube,
                    "#3A3F55", "#1E2133", "#5E6690", "#A8B0D8", "#6A7298", "#FFF8C8", "#C6CADA", "#C8CDE0", "#4A5070", "#E4E8F4", "#FFF07A", new Color(1.8f, 1.8f, 0.6f)),
                P("world.storm", Scenery.Vortex, Weather.Kind.Thunder, BlockStyle.StormCube,
                    "#2E4048", "#162026", "#4E7A80", "#A8D8D0", "#7AA8A8", "#F0FFF8", "#BCCCCC", "#C6D6D6", "#2E4A50", "#E6F0F0", "#9AF0E0", new Color(0.6f, 1.8f, 1.6f))),
            Pair(P("world.castle", Scenery.Castle, Weather.Kind.Sparkle, BlockStyle.Drone,
                    "#6A7AB0", "#3E4A80", "#C0B0E0", "#F0E8FF", "#FFE0A0", "#FFFFFF", "#E0DCEE", "#E8E4F2", "#5A5A8E", "#FAF8FF", "#FFD27A", new Color(2.0f, 1.4f, 0.6f)),
                P("world.castle", Scenery.CastleGate, Weather.Kind.Sparkle, BlockStyle.Drone,
                    "#3E3A6E", "#22204A", "#E0A060", "#FFD090", "#FFC870", "#FFF0D0", "#DCD0DC", "#E4DAE2", "#4A4278", "#F8F2F2", "#FFC060", new Color(2.1f, 1.3f, 0.4f))),

            // ---------- Star Road ----------
            Pair(P("world.cyber", Scenery.CyberCity, Weather.Kind.DataRain, BlockStyle.DataCube,
                    "#2F6A6E", "#1C4246", "#4FA0A0", "#90D8D0", "#6FBAB4", "#E8FFFC", "#C4E4E0", "#CFEDEA", "#3E7C7C", "#EEFBF9", "#8FF0E0", new Color(0.5f, 1.6f, 1.5f)),
                P("world.cyber", Scenery.DataStreams, Weather.Kind.DataRain, BlockStyle.DataCube,
                    "#2E3A6A", "#161C3A", "#4E6AC0", "#8AB8FF", "#6A8AE0", "#E0EAFF", "#C0C8E6", "#CCD4EE", "#2E3A6E", "#ECF0FC", "#8AD0FF", new Color(0.5f, 1.3f, 2.2f))),
            Pair(P("world.lavender", Scenery.Planets, Weather.Kind.Sparkle, BlockStyle.MeteorNight,
                    "#5A5790", "#4E4C85", "#7472AE", "#9A93D6", "#8584BF", "#D6D3FA", "#B9B8E3", "#C9CCEC", "#6E6EA8", "#E4E7FA", "#9FF3FF", new Color(0.45f, 1.6f, 1.9f)),
                P("world.lavender", Scenery.Meteors, Weather.Kind.Cosmic, BlockStyle.MeteorNight,
                    "#3A3466", "#1E1A3E", "#6A5AA8", "#B0A0E8", "#E0A080", "#F4ECFF", "#C8C2E2", "#D4CEEA", "#3A3470", "#F0EDFA", "#FFB890", new Color(2.0f, 1.1f, 0.8f))),
            Pair(P("world.galaxy", Scenery.Spiral, Weather.Kind.Cosmic, BlockStyle.MeteorGalaxy,
                    "#5A4A88", "#362A5E", "#8C72C0", "#C8A8F0", "#A890D8", "#FFF2FF", "#DCCFF0", "#E2D6F6", "#6E5AA0", "#F6F0FF", "#E2B8FF", new Color(1.3f, 0.9f, 1.9f)),
                P("world.galaxy", Scenery.BlackHole, Weather.Kind.Cosmic, BlockStyle.MeteorGalaxy,
                    "#241A3A", "#0E0A1A", "#5A3A8A", "#C090F0", "#FF9A60", "#F0E0FF", "#B8B0CC", "#C6BED8", "#2A2046", "#E6E0F2", "#FFA870", new Color(2.0f, 1.0f, 0.6f))),
            Pair(P("world.heart", Scenery.CoreRings, Weather.Kind.Cosmic, BlockStyle.MeteorHeart,
                    "#2A1830", "#120A18", "#7A2A4A", "#F07090", "#FF5A7A", "#FFD0DC", "#C4B2BE", "#D0C0CA", "#3A1E36", "#EEE2EA", "#FF7A9A", new Color(2.2f, 0.5f, 0.8f)),
                P("world.heart", Scenery.VanGFace, Weather.Kind.Thunder, BlockStyle.MeteorHeart,
                    "#1E1428", "#0A0610", "#A03050", "#FF90A8", "#FF4060", "#FFE0E8", "#BCA8B6", "#CABAC6", "#301830", "#ECDEE6", "#FF5A80", new Color(2.2f, 0.4f, 0.7f))),
        };

        /// <summary>One design: the backdrop colours (top, bottom, glow, lines, planet, stars), ambient light, platform
        /// colours (slab, pillar, tile), the accent and the glow of the platform's edges and tiles.</summary>
        private static WorldTheme P(string key, Scenery scenery, Weather.Kind weather, BlockStyle block, string bgTop, string bgBottom,
            string bgGlow, string bgLines, string bgPlanet, string bgStar, string ambient, string slab, string pillar, string tileTop,
            string accent, Color glow)
        {
            var amb = Hex(ambient);
            return new WorldTheme
            {
                key = key,
                scenery = scenery,
                weather = weather,
                block = block,
                bgTop = Hex(bgTop), bgBottom = Hex(bgBottom), bgGlow = Hex(bgGlow),
                bgLines = Hex(bgLines), bgPlanet = Hex(bgPlanet), bgStar = Hex(bgStar),
                ambient = amb,
                slab = Hex(slab), slabEdgeGlow = glow, pillar = Hex(pillar),
                tileTop = Hex(tileTop), tileSelfLight = new Color(amb.r * 0.2f, amb.g * 0.2f, amb.b * 0.2f), tileGlow = glow,
                accent = Hex(accent),
            };
        }

        private static WorldTheme Pair(WorldTheme first, WorldTheme second)
        {
            first.variant = second;
            return first;
        }

        public static WorldTheme Current { get; private set; } = All[0];

        /// <summary>A floor's first design (its map island, menus and story scenes).</summary>
        public static WorldTheme ForWorld(int worldIndex) => All[Mathf.Clamp(worldIndex, 0, All.Length - 1)];

        /// <summary>The design a level is played in: the floor's first one for its levels 1-5, the second for 6-10.</summary>
        public static WorldTheme ForLevel(int levelIndex)
        {
            var first = ForWorld(levelIndex / Data.LevelCatalog.LevelsPerWorld);
            return levelIndex % Data.LevelCatalog.LevelsPerWorld >= SecondDesignFrom && first.variant != null ? first.variant : first;
        }

        /// <summary>The level of a floor (0-based) from which its second design is used.</summary>
        public const int SecondDesignFrom = 5;

        public static void SetCurrent(int worldIndex) => Current = ForWorld(worldIndex);

        public static void SetCurrent(WorldTheme theme) => Current = theme ?? All[0];
    }
}
