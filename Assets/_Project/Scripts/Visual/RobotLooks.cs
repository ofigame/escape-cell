using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// How the robot evolves: every world gives it a signature accessory and tints it with the world's accent,
    /// and gear accumulates as the player progresses (backpack → shoulder pads → jets → glowing core).
    /// Head: centre y 0.5, half size 0.23 x 0.21 x 0.22, visor facing +Z. Body: centre y 0.2.
    /// </summary>
    public static class RobotLooks
    {
        private static readonly Color BaseBody = new Color(0.64f, 0.69f, 0.81f);
        private static readonly Color BaseLight = new Color(0.76f, 0.8f, 0.9f);
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        /// <summary>The floor from which the Observer glows gold: the First Observer gave it all its energy (the Star Road).</summary>
        public const int GoldenFrom = 21;

        public static Color BodyColor(int world)
        {
            var tint = Color.Lerp(BaseBody, WorldTheme.ForWorld(world).accent, 0.3f);
            return world >= GoldenFrom ? Color.Lerp(tint, new Color(0.91f, 0.75f, 0.41f), 0.75f) : tint;
        }

        public static Color LightColor(int world) => Color.Lerp(BaseLight, WorldTheme.ForWorld(world).accent, 0.2f);

        public static Color EyeColor(int world)
        {
            var a = WorldTheme.ForWorld(world).accent;
            if (world >= GoldenFrom) return new Color(2.4f, 1.7f, 0.5f);
            return world == 0 ? new Color(0.4f, 1.6f, 2.2f) : new Color(a.r * 2.2f, a.g * 2.2f, a.b * 2.2f);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static Material M(string key, Color color, Color emission)
        {
            if (!Materials.TryGetValue(key, out var m) || m == null)
                Materials[key] = m = MaterialFactory.Create(color, emission);
            return m;
        }

        private static Material T(string key, Color color, Color emission)
        {
            if (!Materials.TryGetValue(key, out var m) || m == null)
                Materials[key] = m = MaterialFactory.CreateTransparent(color, emission);
            return m;
        }

        private static GameObject Box(Transform p, Vector3 pos, Vector3 size, float r, Material m, float rotZ = 0f)
        {
            var go = Shapes.Rounded("Gear", p, pos, size, r, m);
            if (rotZ != 0f) go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            return go;
        }

        private static GameObject Ball(Transform p, Vector3 pos, float d, Material m) => Shapes.Primitive(PrimitiveType.Sphere, "Gear", p, pos, Vector3.one * d, m);
        private static GameObject Cyl(Transform p, Vector3 pos, Vector3 scale, Material m) => Shapes.Primitive(PrimitiveType.Cylinder, "Gear", p, pos, scale, m);

        private static void Rod(Transform p, float x, Material m) => Cyl(p, new Vector3(x, 0.79f, 0f), new Vector3(0.025f, 0.08f, 0.025f), m);

        /// <summary>Builds the accessories for <paramref name="world"/> under <paramref name="root"/> (which is emptied first).</summary>
        /// <summary>The signature item per floor: 0 none, 1 antenna bulb, 2 earmuffs, 3 glowing brow, 4 horns, 5 sprout, 6 explorer cap,
        /// 7 diving dome, 8 lollipop, 9 gas mask, 10 crescent moon, 11 halo of lights, 12 lightning bolt, 13 visor, 14 star crown.</summary>
        private static readonly int[] Signature =
        {
            0, 1,           // Grey Channel, Rust Factory
            5, 8, 10,       // Mint Forest, Spring Garden, Night Forest
            6, 6, 4,        // Red Canyon, Golden Desert, Ancient Mine
            1, 7, 7,        // Harbour Lights, Deep Ocean, Sunken Server
            2, 2, 11,       // Aurora, Snowy Peak, Ice Crystal
            3, 13, 9,       // Crystal Cave, Hall of Mirrors, Acid Crystals
            11, 1, 12, 14,  // Cloud Meadow, Sunset Skies, Thunder Storm, Sky Castle
            13, 10, 14, 14, // Cyber Grid, Lavender Space, Galaxy Core, vanG's Heart
        };

        public static void Build(Transform root, int world)
        {
            for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);

            var theme = WorldTheme.ForWorld(world);
            string w = world.ToString();
            var accentGlow = M("accent" + w, theme.accent, new Color(theme.accent.r * 1.6f, theme.accent.g * 1.6f, theme.accent.b * 1.6f));
            var dark = M("dark", Hex("#3E4566"), Color.black);
            var light = M("light" + w, LightColor(world), Color.black);
            var metal = M("metal", Hex("#C9CFE0"), Color.black);

            // Gear that accumulates with progress.
            if (world >= 3) Box(root, new Vector3(0f, 0.24f, -0.17f), new Vector3(0.24f, 0.2f, 0.09f), 0.04f, light);
            if (world >= 6)
            {
                Box(root, new Vector3(-0.21f, 0.3f, 0f), new Vector3(0.1f, 0.06f, 0.14f), 0.025f, dark);
                Box(root, new Vector3(0.21f, 0.3f, 0f), new Vector3(0.1f, 0.06f, 0.14f), 0.025f, dark);
            }
            if (world >= 9)
            {
                foreach (float x in new[] { -0.06f, 0.06f })
                {
                    Cyl(root, new Vector3(x, 0.13f, -0.2f), new Vector3(0.06f, 0.04f, 0.06f), dark);
                    Ball(root, new Vector3(x, 0.08f, -0.2f), 0.06f, accentGlow).AddComponent<Flicker>();
                }
            }
            if (world >= 12) Box(root, new Vector3(0f, 0.22f, 0.125f), new Vector3(0.08f, 0.08f, 0.02f), 0.02f, accentGlow);

            // The world's signature item (the looks below were made for the first floors; each floor borrows the one that suits it).
            switch (Signature[Mathf.Clamp(world, 0, Signature.Length - 1)])
            {
                case 1: // Sunset: classic antenna with a warm bulb
                    Rod(root, 0f, metal);
                    Ball(root, new Vector3(0f, 0.89f, 0f), 0.09f, accentGlow);
                    break;
                case 2: // Ice: earmuffs
                {
                    var fluff = M("fluff", Color.white, new Color(0.25f, 0.3f, 0.35f));
                    Box(root, new Vector3(-0.25f, 0.52f, 0f), new Vector3(0.07f, 0.17f, 0.17f), 0.035f, fluff);
                    Box(root, new Vector3(0.25f, 0.52f, 0f), new Vector3(0.07f, 0.17f, 0.17f), 0.035f, fluff);
                    Box(root, new Vector3(0f, 0.73f, 0f), new Vector3(0.52f, 0.03f, 0.05f), 0.012f, accentGlow);
                    break;
                }
                case 3: // Neon: glowing brow stripe and side fins
                    Box(root, new Vector3(0f, 0.645f, 0.226f), new Vector3(0.36f, 0.025f, 0.02f), 0.01f, accentGlow);
                    Box(root, new Vector3(-0.245f, 0.52f, -0.05f), new Vector3(0.02f, 0.16f, 0.12f), 0.008f, accentGlow);
                    Box(root, new Vector3(0.245f, 0.52f, -0.05f), new Vector3(0.02f, 0.16f, 0.12f), 0.008f, accentGlow);
                    break;
                case 4: // Lava: little glowing horns
                    Box(root, new Vector3(-0.15f, 0.78f, 0.02f), new Vector3(0.06f, 0.17f, 0.06f), 0.025f, accentGlow, 20f);
                    Box(root, new Vector3(0.15f, 0.78f, 0.02f), new Vector3(0.06f, 0.17f, 0.06f), 0.025f, accentGlow, -20f);
                    break;
                case 5: // Forest: sprout antenna
                    Rod(root, 0.06f, M("stem", Hex("#3E7A44"), Color.black));
                    Box(root, new Vector3(0.12f, 0.88f, 0f), new Vector3(0.14f, 0.02f, 0.07f), 0.01f, accentGlow, 30f);
                    Box(root, new Vector3(0.0f, 0.86f, 0f), new Vector3(0.11f, 0.02f, 0.06f), 0.01f, accentGlow, -30f);
                    break;
                case 6: // Desert: explorer cap with goggles
                    Box(root, new Vector3(0f, 0.725f, 0.06f), new Vector3(0.5f, 0.03f, 0.34f), 0.012f, M("canvas", Hex("#D9B27A"), Color.black));
                    Box(root, new Vector3(0f, 0.66f, 0f), new Vector3(0.48f, 0.05f, 0.46f), 0.02f, M("leather", Hex("#6B4A2E"), Color.black));
                    Ball(root, new Vector3(-0.08f, 0.66f, 0.23f), 0.09f, accentGlow);
                    Ball(root, new Vector3(0.08f, 0.66f, 0.23f), 0.09f, accentGlow);
                    break;
                case 7: // Ocean: diving dome and snorkel
                    Ball(root, new Vector3(0f, 0.5f, 0.01f), 0.7f, T("dome", new Color(0.7f, 0.9f, 1f, 0.28f), new Color(0.1f, 0.3f, 0.5f)));
                    Cyl(root, new Vector3(0.2f, 0.8f, -0.06f), new Vector3(0.045f, 0.1f, 0.045f), M("snorkel", Hex("#FF9A4A"), new Color(0.4f, 0.15f, 0.02f)));
                    break;
                case 8: // Candy: lollipop antenna
                {
                    Rod(root, 0f, M("stick", Color.white, new Color(0.3f, 0.3f, 0.3f)));
                    var pop = Cyl(root, new Vector3(0f, 0.95f, 0f), new Vector3(0.2f, 0.02f, 0.2f), accentGlow);
                    pop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                }
                case 9: // Toxic lab: gas mask filter
                {
                    var filter = Cyl(root, new Vector3(0f, 0.37f, 0.24f), new Vector3(0.13f, 0.04f, 0.13f), accentGlow);
                    filter.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Box(root, new Vector3(0f, 0.42f, 0f), new Vector3(0.48f, 0.03f, 0.46f), 0.012f, dark);
                    break;
                }
                case 10: // Midnight: crescent moon antenna
                    Rod(root, 0f, metal);
                    Ball(root, new Vector3(0f, 0.92f, 0f), 0.15f, accentGlow);
                    Ball(root, new Vector3(0.045f, 0.94f, 0.03f), 0.13f, M("night", WorldTheme.ForWorld(10).bgBottom, Color.black));
                    break;
                case 11: // Aurora: orbiting halo of lights
                {
                    var halo = new GameObject("Halo").transform;
                    halo.SetParent(root, false);
                    halo.localPosition = new Vector3(0f, 0.84f, 0f);
                    halo.gameObject.AddComponent<Spinner>().speed = 120f;
                    var violet = M("violet", Hex("#C8A0FF"), new Color(1.4f, 0.9f, 2.2f));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Ball(halo, new Vector3(Mathf.Cos(a) * 0.2f, 0f, Mathf.Sin(a) * 0.2f), 0.06f, i % 2 == 0 ? accentGlow : violet);
                    }
                    break;
                }
                case 12: // Storm: lightning-bolt antenna
                    Box(root, new Vector3(0f, 0.78f, 0f), new Vector3(0.04f, 0.1f, 0.04f), 0.01f, accentGlow, 25f);
                    Box(root, new Vector3(0.03f, 0.86f, 0f), new Vector3(0.04f, 0.1f, 0.04f), 0.01f, accentGlow, -25f);
                    Box(root, new Vector3(0f, 0.94f, 0f), new Vector3(0.04f, 0.1f, 0.04f), 0.01f, accentGlow, 25f);
                    break;
                case 13: // Cyber: holographic visor and ear antennas
                    Box(root, new Vector3(0f, 0.5f, 0.26f), new Vector3(0.42f, 0.17f, 0.02f), 0.01f, T("holo", new Color(0.4f, 1f, 0.95f, 0.35f), new Color(0.2f, 1.4f, 1.3f)));
                    Cyl(root, new Vector3(-0.26f, 0.66f, 0f), new Vector3(0.02f, 0.09f, 0.02f), accentGlow);
                    Cyl(root, new Vector3(0.26f, 0.66f, 0f), new Vector3(0.02f, 0.09f, 0.02f), accentGlow);
                    break;
                case 14: // Galaxy: star crown and a tiny orbiting planet
                {
                    var gold = M("gold", Hex("#FFC94A"), new Color(2.4f, 1.6f, 0.35f));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2f / 5f;
                        Box(root, new Vector3(Mathf.Cos(a) * 0.16f, 0.77f, Mathf.Sin(a) * 0.16f), new Vector3(0.06f, 0.11f, 0.06f), 0.02f, gold);
                    }
                    var orbit = new GameObject("Orbit").transform;
                    orbit.SetParent(root, false);
                    orbit.localPosition = new Vector3(0f, 0.5f, 0f);
                    orbit.gameObject.AddComponent<Spinner>().speed = 90f;
                    Ball(orbit, new Vector3(0.42f, 0.12f, 0f), 0.11f, accentGlow);
                    break;
                }
            }
        }
    }
}
