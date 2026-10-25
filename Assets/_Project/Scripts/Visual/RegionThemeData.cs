using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A big region's own look over the perfect city (the fixes design's job 7): from level 101 every floor keeps the
    /// city's geometry but each region repaints it — its sky and floor colours, its sun and fog, its weather, a few props
    /// round the fortress and a quiet sound under the music. Sea Floor (worlds 9-11, carried on to the city's first world),
    /// Snowy Mountain (12-14), Crystal Cave (15-17), Cloud Bridge (18-21), Star Road (22-25); a world inside a region may
    /// change the accent, the weather or the glow (the acid of the crystal cave, the storm over the clouds, vanG's heart).
    /// One asset each in Resources/Regions (written from <see cref="Defaults"/> the first time).
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Floors/Region Theme", fileName = "Region")]
    public class RegionThemeData : ScriptableObject
    {
        [Header("Worlds (0-based)")]
        public int firstWorld, lastWorld;

        [Header("Palette")]
        public Color bgTop, bgBottom, bgGlow, bgLines, ambient, accent;
        public Color slab, tileTop, tileGlow;

        [Header("Sun and fog")]
        public Color sun = Color.white;
        public float sunIntensity = 1.2f;
        public Vector2 sunAngles = new Vector2(42f, 35f);
        public Color fog;
        /// <summary>Fog density (0: none); only on the middle and strong phones.</summary>
        public float fogDensity;

        [Header("Weather, props, sound")]
        public Weather.Kind weather;
        public RegionDecor decor;
        public int decorCount = 14;
        public Color decorColour, decorGlow;
        public AmbientKind ambience;

        [Header("A world of the region (its index in the region: 0 the first)")]
        public Color[] worldAccent = new Color[0];
        public Weather.Kind[] worldWeather = new Weather.Kind[0];
        public Color[] worldGlow = new Color[0];

        private static List<RegionThemeData> all;

        /// <summary>The region a world belongs to (null: before the perfect city's regions).</summary>
        public static RegionThemeData For(int world)
        {
            if (all == null)
            {
                all = new List<RegionThemeData>(Resources.LoadAll<RegionThemeData>("Regions"));
                if (all.Count == 0) all = Defaults();
            }
            foreach (var r in all) if (world >= r.firstWorld && world <= r.lastWorld) return r;
            return null;
        }

        /// <summary>Repaints a perfect-city design in the region's colours (and a world's own accent, weather and glow).</summary>
        public void Paint(WorldTheme t, int world)
        {
            int i = world - firstWorld;
            t.bgTop = bgTop; t.bgBottom = bgBottom; t.bgGlow = Pick(worldGlow, i, bgGlow); t.bgLines = bgLines;
            t.ambient = ambient;
            t.accent = Pick(worldAccent, i, accent);
            t.slab = slab; t.tileTop = tileTop; t.tileGlow = tileGlow;
            t.slabEdgeGlow = t.accent * 1.6f;
            t.weather = i >= 0 && i < worldWeather.Length && worldWeather[i] != Weather.Kind.None ? worldWeather[i] : weather;
        }

        private static Color Pick(Color[] list, int i, Color fallback) => i >= 0 && i < list.Length && list[i].a > 0f ? list[i] : fallback;

        /// <summary>The region's sun (colour, strength, angle) and fog (the middle and strong phones only).</summary>
        public void Light(Light key, bool fogAllowed)
        {
            if (key != null)
            {
                key.color = sun;
                key.intensity = sunIntensity;
                key.transform.rotation = Quaternion.Euler(sunAngles.x, sunAngles.y, 0f);
            }
            RenderSettings.fog = fogAllowed && fogDensity > 0f;
            if (!RenderSettings.fog) return;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
        }

        /// <summary>The region's props round the fortress (outside the floor, merged with it, still).</summary>
        public void Decorate(Transform parent, GridModel grid, int seed)
        {
            if (decor == RegionDecor.None || decorCount <= 0) return;
            var rng = new System.Random(seed);
            var root = new GameObject("RegionDecor").transform;
            root.SetParent(parent, false);
            var mat = MaterialFactory.Create(decorColour, decorGlow);
            for (int n = 0; n < decorCount; n++)
            {
                // round the floor's edge: two to six tiles out, on any side
                int side = rng.Next(4), along = rng.Next(side < 2 ? grid.Height : grid.Width), out_ = 2 + rng.Next(5);
                var p = side == 0 ? new GridPos(-out_, along) : side == 1 ? new GridPos(grid.Width - 1 + out_, along)
                    : side == 2 ? new GridPos(along, -out_) : new GridPos(along, grid.Height - 1 + out_);
                var at = GridView.ToWorld(p) + Vector3.up * (GridView.SurfaceY - 0.4f);
                Prop(root, at, (float)rng.NextDouble(), mat);
            }
        }

        private void Prop(Transform root, Vector3 at, float r, Material mat)
        {
            var prop = new GameObject(decor.ToString()).transform;
            prop.SetParent(root, false);
            prop.position = at;
            prop.rotation = Quaternion.Euler(0f, r * 360f, 0f);
            float s = 0.8f + r * 0.9f;
            switch (decor)
            {
                case RegionDecor.Coral:
                    for (int i = 0; i < 4; i++)
                        Shapes.Primitive(PrimitiveType.Capsule, "Branch", prop, new Vector3((i - 1.5f) * 0.35f * s, 0.9f * s, (i % 2) * 0.3f * s), new Vector3(0.22f, 0.9f + i * 0.25f, 0.22f) * s, mat).transform.localRotation = Quaternion.Euler((i - 1.5f) * 14f, 0f, (i - 1.5f) * 18f);
                    break;
                case RegionDecor.Icicles:
                    for (int i = 0; i < 3; i++)
                        Shapes.Primitive(PrimitiveType.Capsule, "Icicle", prop, new Vector3((i - 1) * 0.4f * s, 1.2f * s, 0f), new Vector3(0.28f, 1.3f - i * 0.25f, 0.28f) * s, mat);
                    break;
                case RegionDecor.Crystals:
                    for (int i = 0; i < 3; i++)
                        Shapes.Primitive(PrimitiveType.Cube, "Crystal", prop, new Vector3((i - 1) * 0.45f * s, 0.9f * s, 0f), new Vector3(0.35f, 1.8f - i * 0.4f, 0.35f) * s, mat).transform.localRotation = Quaternion.Euler((i - 1) * 16f, 45f, (i - 1) * 12f);
                    break;
                case RegionDecor.Clouds:
                    for (int i = 0; i < 4; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Puff", prop, new Vector3((i - 1.5f) * 0.6f * s, 0.6f * s + (i % 2) * 0.3f * s, 0f), Vector3.one * (0.9f + (i % 2) * 0.35f) * s, mat);
                    break;
                case RegionDecor.Neon:
                    Shapes.Primitive(PrimitiveType.Cube, "Pillar", prop, new Vector3(0f, 1.4f * s, 0f), new Vector3(0.22f, 2.8f, 0.22f) * s, mat);
                    Shapes.Primitive(PrimitiveType.Sphere, "Light", prop, new Vector3(0f, 2.9f * s, 0f), Vector3.one * 0.45f * s, mat);
                    break;
            }
        }

        /// <summary>The five regions' starting looks (written to Resources/Regions the first time; changed there after).</summary>
        public static List<RegionThemeData> Defaults()
        {
            Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
            RegionThemeData R(int a, int b, string top, string bottom, string glow, string lines, string amb, string acc, string slab, string tile, string tileGlow,
                string sun, float sunK, Vector2 angles, string fog, float fogD, Weather.Kind w, RegionDecor d, string decor, string decorGlow, AmbientKind sound)
            {
                var r = CreateInstance<RegionThemeData>();
                r.firstWorld = a; r.lastWorld = b;
                r.bgTop = H(top); r.bgBottom = H(bottom); r.bgGlow = H(glow); r.bgLines = H(lines); r.ambient = H(amb); r.accent = H(acc);
                r.slab = H(slab); r.tileTop = H(tile); r.tileGlow = H(tileGlow);
                r.sun = H(sun); r.sunIntensity = sunK; r.sunAngles = angles; r.fog = H(fog); r.fogDensity = fogD;
                r.weather = w; r.decor = d; r.decorColour = H(decor); r.decorGlow = H(decorGlow) * 1.6f; r.ambience = sound;
                return r;
            }
            var sea = R(8, 10, "#0A2A4A", "#0B5C6E", "#3FC8D8", "#9FF6FF", "#1E5070", "#4FE0E8", "#2A6E80", "#CFEFF2", "#6FE8F0",
                "#9FE0FF", 1.05f, new Vector2(65f, 20f), "#0E4A60", 0.012f, Weather.Kind.Bubbles, RegionDecor.Coral, "#FF7A6A", "#FF9A80", AmbientKind.Water);
            sea.name = "Sea";
            var snow = R(11, 13, "#9EC4E8", "#EAF4FF", "#FFFFFF", "#FFFFFF", "#C8DCF0", "#A8E4FF", "#E8F0F8", "#F4F8FF", "#BEE8FF",
                "#DDEEFF", 1.25f, new Vector2(40f, 30f), "#DCEAF6", 0.01f, Weather.Kind.Snow, RegionDecor.Icicles, "#CFEFFF", "#EAFBFF", AmbientKind.Wind);
            snow.name = "Snow";
            snow.worldWeather = new[] { Weather.Kind.None, Weather.Kind.None, Weather.Kind.Crystal }; // (the ice crystal world: a crystal glitter)
            var crystal = R(14, 16, "#1A0E30", "#3A1A58", "#C070FF", "#FF9EF0", "#3C2460", "#E07CFF", "#4A2E6E", "#D8C8F0", "#E09CFF",
                "#E8C8FF", 0.95f, new Vector2(55f, -25f), "#2A1640", 0.014f, Weather.Kind.Crystal, RegionDecor.Crystals, "#C080FF", "#F0A0FF", AmbientKind.Hum);
            crystal.name = "Crystal";
            crystal.worldAccent = new[] { new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), H("#7CFF6A") }; // (the acid crystals: acid green)
            crystal.worldWeather = new[] { Weather.Kind.None, Weather.Kind.None, Weather.Kind.Toxic };
            var cloud = R(17, 20, "#F2A87A", "#FFE2C8", "#FFD0E8", "#FFFFFF", "#F0C8B0", "#FFB070", "#FFF0E4", "#FFF6EE", "#FFD8B0",
                "#FFD8B0", 1.3f, new Vector2(30f, 60f), "#FFE0CC", 0.008f, Weather.Kind.Sparkle, RegionDecor.Clouds, "#FFFFFF", "#FFF2E8", AmbientKind.Wind);
            cloud.name = "Cloud";
            cloud.worldWeather = new[] { Weather.Kind.None, Weather.Kind.None, Weather.Kind.Thunder, Weather.Kind.None }; // (the storm world: lightning)
            cloud.worldGlow = new[] { new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), H("#8090C0"), new Color(0f, 0f, 0f, 0f) };
            var star = R(21, 24, "#05030F", "#120A30", "#6040FF", "#40FFF0", "#201A40", "#40F0FF", "#1A1640", "#C8C8E8", "#60F0FF",
                "#A0B0FF", 0.9f, new Vector2(60f, 10f), "#0A0820", 0.012f, Weather.Kind.Cosmic, RegionDecor.Neon, "#40F0FF", "#80FFFF", AmbientKind.Space);
            star.name = "Star";
            star.worldAccent = new[] { H("#40FFB0"), H("#C8A0FF"), new Color(0f, 0f, 0f, 0f), H("#FF3040") }; // (the grid, lavender space, the galaxy, vanG's red heart)
            star.worldWeather = new[] { Weather.Kind.DataRain, Weather.Kind.None, Weather.Kind.None, Weather.Kind.Embers };
            star.worldGlow = new[] { new Color(0f, 0f, 0f, 0f), H("#B080FF"), new Color(0f, 0f, 0f, 0f), H("#FF2040") };
            return new List<RegionThemeData> { sea, snow, crystal, cloud, star };
        }
    }
}
