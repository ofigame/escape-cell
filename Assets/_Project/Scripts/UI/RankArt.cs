using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The challenge's pictures, drawn from the UI's shapes: a rank insignia for each of the thirteen ranks (Er's plain
    /// bar, the sergeants' chevrons, the officers' stars, the senior officers' crescent, the generals' wreath, the
    /// marshal's batons) and a trophy for each of the thirteen leagues, every one its own design (material, shape,
    /// handles, base and crown).
    /// </summary>
    public static class RankArt
    {
        public const int Count = 13;

        private static readonly Color Gold = new Color(1f, 0.8f, 0.25f), GoldDark = new Color(0.62f, 0.42f, 0.08f), Silver = new Color(0.86f, 0.9f, 0.96f);

        private static Image Part(RectTransform parent, Vector2 pos, Vector2 size, Color c, Sprite sprite, float rot = 0f, float ppu = 1f)
        {
            var r = UiFactory.Box("Part", parent, new Vector2(0.5f, 0.5f), pos, size);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.localRotation = Quaternion.Euler(0f, 0f, rot);
            var img = UiFactory.Fill(r, c, sprite, ppu);
            img.raycastTarget = false;
            return img;
        }

        private static RectTransform Root(Transform parent, string name, Vector2 anchor, Vector2 position, float size)
        {
            var root = UiFactory.Box(name, parent, anchor, position, new Vector2(size, size));
            root.pivot = new Vector2(0.5f, 0.5f);
            return root;
        }

        // ---------- Rank insignia ----------

        /// <summary>The insignia of <paramref name="rank"/> (0 Er … 12 Mareşal), <paramref name="size"/> tall.</summary>
        public static RectTransform Badge(Transform parent, int rank, Vector2 anchor, Vector2 position, float size)
        {
            rank = Mathf.Clamp(rank, 0, Count - 1);
            var root = Root(parent, "Rank" + rank, anchor, position, size);
            float s = size;
            bool enlisted = rank <= 3, general = rank >= 11;
            var plate = enlisted ? new Color(0.28f, 0.34f, 0.2f) : general ? new Color(0.55f, 0.08f, 0.12f) : new Color(0.1f, 0.14f, 0.32f);
            var rim = enlisted ? new Color(0.62f, 0.66f, 0.45f) : rank >= 8 ? Gold : Silver;
            if (rank == 12) Part(root, Vector2.zero, new Vector2(s * 1.05f, s * 1.05f), new Color(1f, 0.8f, 0.3f, 0.35f), UiSprites.Circle);
            // The shoulder board: a rounded plate with a pointed top (a diamond over its upper edge) and a button.
            Part(root, new Vector2(0f, s * 0.3f), new Vector2(s * 0.42f, s * 0.42f), rim, UiSprites.Rounded, 45f, 3f);
            Part(root, new Vector2(0f, s * 0.3f), new Vector2(s * 0.36f, s * 0.36f), plate, UiSprites.Rounded, 45f, 3f);
            Part(root, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.62f, s * 0.82f), rim, UiSprites.Rounded, 0f, 2.5f);
            Part(root, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.54f, s * 0.74f), plate, UiSprites.Rounded, 0f, 2.5f);
            Part(root, new Vector2(0f, s * 0.36f), new Vector2(s * 0.1f, s * 0.1f), rim, UiSprites.Circle);

            switch (rank)
            {
                case 0: // Er: one plain bar
                    Part(root, new Vector2(0f, -s * 0.1f), new Vector2(s * 0.36f, s * 0.07f), Silver, UiSprites.Rounded, 0f, 6f);
                    break;
                case 1: Chevrons(root, s, 1, Gold); break;
                case 2: Chevrons(root, s, 2, Gold); break;
                case 3:
                    Chevrons(root, s, 3, Gold);
                    Part(root, new Vector2(0f, s * 0.2f), new Vector2(s * 0.16f, s * 0.16f), Gold, UiSprites.Star);
                    break;
                case 4: // Asteğmen: a hollow star
                    Part(root, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.3f, s * 0.3f), Silver, UiSprites.Star);
                    Part(root, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.16f, s * 0.16f), plate, UiSprites.Star);
                    break;
                case 5: case 6: case 7: // Teğmen 1, Üsteğmen 2, Yüzbaşı 3 stars
                    Stars(root, s, rank - 4, Silver, 0f);
                    break;
                case 8: case 9: case 10: // Binbaşı, Yarbay, Albay: the crescent and star, then 1–3 stars
                    Crescent(root, new Vector2(0f, s * 0.12f), s * 0.24f, Gold, plate);
                    Stars(root, s * 0.8f, rank - 7, Gold, -s * 0.14f);
                    break;
                case 11: // General: wreath, crescent, two stars
                    Wreath(root, s, Gold);
                    Crescent(root, new Vector2(0f, s * 0.1f), s * 0.22f, Gold, plate);
                    Part(root, new Vector2(-s * 0.08f, -s * 0.12f), new Vector2(s * 0.13f, s * 0.13f), Gold, UiSprites.Star);
                    Part(root, new Vector2(s * 0.08f, -s * 0.12f), new Vector2(s * 0.13f, s * 0.13f), Gold, UiSprites.Star);
                    break;
                case 12: // Mareşal: wreath, crossed batons, crescent
                    Wreath(root, s, Gold);
                    foreach (float r in new[] { 38f, -38f })
                    {
                        Part(root, new Vector2(0f, -s * 0.06f), new Vector2(s * 0.07f, s * 0.42f), new Color(0.75f, 0.1f, 0.15f), UiSprites.Rounded, r, 6f);
                        var tip = Quaternion.Euler(0f, 0f, r) * new Vector3(0f, s * 0.21f, 0f);
                        Part(root, new Vector2(tip.x, tip.y - s * 0.06f), new Vector2(s * 0.08f, s * 0.08f), Gold, UiSprites.Circle);
                    }
                    Crescent(root, new Vector2(0f, s * 0.17f), s * 0.18f, Gold, plate);
                    break;
            }
            return root;
        }

        private static void Chevrons(RectTransform root, float s, int n, Color c)
        {
            for (int i = 0; i < n; i++)
            {
                float y = -s * 0.2f + i * s * 0.14f;
                Part(root, new Vector2(-s * 0.095f, y), new Vector2(s * 0.24f, s * 0.07f), c, UiSprites.Rounded, 32f, 6f);
                Part(root, new Vector2(s * 0.095f, y), new Vector2(s * 0.24f, s * 0.07f), c, UiSprites.Rounded, -32f, 6f);
            }
        }

        private static void Stars(RectTransform root, float s, int n, Color c, float centreY)
        {
            float step = s * 0.2f;
            for (int i = 0; i < n; i++)
                Part(root, new Vector2(0f, centreY + (i - (n - 1) * 0.5f) * step), new Vector2(s * 0.18f, s * 0.18f), c, UiSprites.Star);
        }

        private static void Crescent(RectTransform root, Vector2 at, float d, Color c, Color plate)
        {
            Part(root, at, new Vector2(d, d), c, UiSprites.Circle);
            Part(root, at + new Vector2(d * 0.22f, 0f), new Vector2(d * 0.8f, d * 0.8f), plate, UiSprites.Circle);
            Part(root, at + new Vector2(d * 0.3f, 0f), new Vector2(d * 0.38f, d * 0.38f), c, UiSprites.Star);
        }

        private static void Wreath(RectTransform root, float s, Color c)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 5; i++)
                {
                    float a = (-70f + i * 32f) * Mathf.Deg2Rad;
                    var p = new Vector2(side * Mathf.Cos(a) * s * 0.22f, Mathf.Sin(a) * s * 0.26f - s * 0.04f);
                    Part(root, p, new Vector2(s * 0.06f, s * 0.12f), c, UiSprites.Circle, side * (20f + i * 30f));
                }
        }

        // ---------- Trophies ----------

        private struct Cup
        {
            public Color metal;
            public int shape;    // 0 cup, 1 tall goblet, 2 wide bowl
            public int handles;  // 0 none, 1 rings, 2 wings
            public int steps;    // base steps
            public int topper;   // 0 none, 1 star, 2 gem, 3 crown, 4 flame
            public Color gem;
            public bool glow;
        }

        private static readonly Cup[] Cups =
        {
            new Cup { metal = new Color(0.6f, 0.4f, 0.22f), shape = 0, handles = 0, steps = 1 },                                                 // wood
            new Cup { metal = new Color(0.6f, 0.62f, 0.64f), shape = 0, handles = 1, steps = 1 },                                                // stone
            new Cup { metal = new Color(0.82f, 0.5f, 0.26f), shape = 2, handles = 1, steps = 2 },                                                // bronze
            new Cup { metal = new Color(0.55f, 0.62f, 0.72f), shape = 1, handles = 0, steps = 2, topper = 2, gem = new Color(0.9f, 0.2f, 0.25f) }, // iron
            new Cup { metal = Silver, shape = 0, handles = 1, steps = 2, topper = 1 },                                                           // silver
            new Cup { metal = Silver, shape = 1, handles = 2, steps = 2 },                                                                       // silver wing
            new Cup { metal = Gold, shape = 0, handles = 1, steps = 2 },                                                                         // gold
            new Cup { metal = Gold, shape = 2, handles = 1, steps = 3, topper = 2, gem = new Color(0.2f, 0.85f, 0.45f) },                         // emerald gold
            new Cup { metal = Gold, shape = 1, handles = 1, steps = 3, topper = 3 },                                                             // crown gold
            new Cup { metal = new Color(0.82f, 0.9f, 0.95f), shape = 0, handles = 2, steps = 3, topper = 2, gem = new Color(0.25f, 0.5f, 1f) },     // platinum
            new Cup { metal = new Color(0.55f, 0.92f, 1f, 0.85f), shape = 1, handles = 2, steps = 3, topper = 1, glow = true },                   // crystal
            new Cup { metal = new Color(0.85f, 0.12f, 0.22f), shape = 2, handles = 1, steps = 3, topper = 3, gem = Gold },                        // ruby
            new Cup { metal = new Color(0.16f, 0.12f, 0.22f), shape = 0, handles = 2, steps = 3, topper = 4, gem = Gold, glow = true },           // legend
        };

        /// <summary>The trophy of league <paramref name="league"/> (0 … 12), <paramref name="size"/> tall.</summary>
        public static RectTransform Trophy(Transform parent, int league, Vector2 anchor, Vector2 position, float size)
        {
            league = Mathf.Clamp(league, 0, Count - 1);
            var cup = Cups[league];
            var root = Root(parent, "Cup" + league, anchor, position, size);
            float s = size;
            var m = cup.metal;
            var dark = new Color(m.r * 0.6f, m.g * 0.6f, m.b * 0.6f, 1f);
            var trim = league == 12 ? Gold : dark;
            if (cup.glow) Part(root, new Vector2(0f, s * 0.08f), new Vector2(s, s), new Color(m.r, m.g, m.b, 0.3f), UiSprites.Circle);

            // Base steps, stem.
            float y = -s * 0.42f;
            for (int i = 0; i < cup.steps; i++)
            {
                float w = s * (0.56f - i * 0.1f);
                Part(root, new Vector2(0f, y), new Vector2(w, s * 0.08f), i == 0 ? trim : dark, UiSprites.Rounded, 0f, 6f);
                y += s * 0.075f;
            }
            float stemH = cup.shape == 1 ? s * 0.22f : s * 0.13f;
            Part(root, new Vector2(0f, y + stemH * 0.5f), new Vector2(s * 0.08f, stemH), dark, UiSprites.Rounded, 0f, 8f);
            Part(root, new Vector2(0f, y + stemH), new Vector2(s * 0.18f, s * 0.06f), m, UiSprites.Rounded, 0f, 8f);
            float bodyBottom = y + stemH;

            // The body.
            float bw = cup.shape == 2 ? s * 0.64f : cup.shape == 1 ? s * 0.4f : s * 0.5f;
            float bh = cup.shape == 2 ? s * 0.3f : cup.shape == 1 ? s * 0.42f : s * 0.38f;
            float cy = bodyBottom + bh * 0.5f;
            // Handles behind the body.
            if (cup.handles == 1)
                foreach (int side in new[] { -1, 1 })
                    Part(root, new Vector2(side * bw * 0.5f, cy + bh * 0.08f), new Vector2(s * 0.2f, s * 0.22f), dark, UiSprites.Ring, 0f, 0.5f);
            else if (cup.handles == 2)
                foreach (int side in new[] { -1, 1 })
                    for (int f = 0; f < 3; f++)
                        Part(root, new Vector2(side * (bw * 0.5f + s * 0.06f + f * s * 0.04f), cy + bh * 0.1f + f * s * 0.05f),
                            new Vector2(s * 0.08f, s * (0.24f - f * 0.04f)), new Color(m.r, m.g, m.b, 0.9f), UiSprites.Circle, side * (-40f - f * 18f));
            // The bowl: a rounded block with a round bottom, then its rim.
            Part(root, new Vector2(0f, cy - bh * 0.18f), new Vector2(bw * 0.92f, bh * 0.75f), m, UiSprites.Circle);
            Part(root, new Vector2(0f, cy + bh * 0.12f), new Vector2(bw, bh * 0.66f), m, UiSprites.Rounded, 0f, 2.5f);
            Part(root, new Vector2(0f, cy + bh * 0.44f), new Vector2(bw * 1.06f, s * 0.06f), trim, UiSprites.Rounded, 0f, 8f);
            // Shine.
            Part(root, new Vector2(-bw * 0.26f, cy + bh * 0.04f), new Vector2(s * 0.05f, bh * 0.5f), new Color(1f, 1f, 1f, 0.45f), UiSprites.Rounded, 0f, 8f);
            // A gem on the front.
            if (cup.topper == 2 || league == 11 || league == 12)
            {
                Part(root, new Vector2(0f, cy), new Vector2(s * 0.12f, s * 0.12f), trim, UiSprites.Rounded, 45f, 4f);
                Part(root, new Vector2(0f, cy), new Vector2(s * 0.08f, s * 0.08f), cup.gem.a > 0f ? cup.gem : Gold, UiSprites.Rounded, 45f, 4f);
            }
            // On top.
            float top = cy + bh * 0.5f + s * 0.04f;
            switch (cup.topper)
            {
                case 1:
                    Part(root, new Vector2(0f, top + s * 0.08f), new Vector2(s * 0.2f, s * 0.2f), league == 10 ? Color.white : Gold, UiSprites.Star);
                    break;
                case 3:
                    for (int i = -1; i <= 1; i++)
                        Part(root, new Vector2(i * s * 0.08f, top + s * 0.05f + (i == 0 ? s * 0.03f : 0f)), new Vector2(s * 0.08f, s * 0.08f), Gold, UiSprites.Rounded, 45f, 4f);
                    Part(root, new Vector2(0f, top), new Vector2(s * 0.26f, s * 0.05f), Gold, UiSprites.Rounded, 0f, 8f);
                    break;
                case 4:
                    Part(root, new Vector2(0f, top + s * 0.08f), new Vector2(s * 0.18f, s * 0.22f), new Color(1f, 0.45f, 0.1f), UiSprites.Circle);
                    Part(root, new Vector2(0f, top + s * 0.06f), new Vector2(s * 0.1f, s * 0.13f), new Color(1f, 0.9f, 0.35f), UiSprites.Circle);
                    break;
            }
            return root;
        }
    }
}
