using SquashBot.Core;
using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The grand look of a battle arena (after the user's reference art): white, orange and blue sci-fi towers on every
    /// corner of the octagonal courtyard — tall spires at the back, drum towers with blue domes at the sides, squat
    /// turrets at the front so they never hide the fight — a giant spire and a domed hall rising behind the far wall,
    /// heavy cannon bastions on the two front corners, and a hexagonal pad round foi's cell. Decoration only.
    /// </summary>
    public static class ArenaDecor
    {
        private static Material white, orange, blue, dark, glowBlue, glowOrange;

        private static void Materials(Color accent, Color trim)
        {
            white = MaterialFactory.CreateSurface(new Color(0.92f, 0.9f, 0.86f), Color.black, 0.55f, 0.1f);
            orange = MaterialFactory.CreateSurface(accent, accent * 0.25f, 0.6f, 0.3f);
            blue = MaterialFactory.CreateSurface(trim, trim * 0.15f, 0.7f, 0.3f);
            dark = MaterialFactory.CreateSurface(new Color(0.24f, 0.25f, 0.3f), Color.black, 0.5f, 0.55f);
            glowBlue = MaterialFactory.Create(Color.Lerp(trim, Color.white, 0.3f), trim * 2.6f);
            glowOrange = MaterialFactory.Create(Color.Lerp(accent, Color.white, 0.2f), accent * 3f);
        }

        /// <summary>
        /// Towers all round the courtyard, whatever its shape: eight rays from the middle find its edge, and a tower
        /// stands just beyond — tall spires at the back, domed drums at the sides, cannon bastions and a low turret at
        /// the front (never in the way) — then the landmarks behind the far wall and the pad round foi's cell.
        /// </summary>
        /// <summary>The arenas are cities now: their buildings take the place of the towers and landmarks.</summary>
        public static bool CityMode = true;

        public static void Build(Transform root, GridModel grid, int[] b, int arena, GridPos start, Color accent, Color trim)
        {
            Materials(accent, trim);
            float grow = 1.9f + arena * 0.06f; // towering, above the high ramparts
            var centre = new Vector2((b[0] + b[2]) * 0.5f, (b[1] + b[3]) * 0.5f);
            for (int k = 0; k < 8 && !CityMode; k++)
            {
                if (k == 2 || k == 6) continue; // north and south: the gun posts stand there (see GameManagerGuns)
                float ang = k * 45f * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                // Walk out to the courtyard's edge along the ray.
                var last = start;
                for (float t = 0f; t < 80f; t += 0.5f)
                {
                    var q = new GridPos(Mathf.RoundToInt(centre.x + dir.x * t), Mathf.RoundToInt(centre.y + dir.y * t));
                    if (!grid.InBounds(q)) break;
                    if (grid.IsFloor(q) && !grid.IsLava(q)) last = q;
                    else if (t - Vector2.Distance(new Vector2(last.x, last.y), centre) > 3f) break; // well past the edge
                }
                var at = GridView.ToWorld(last) + new Vector3(dir.x, 0f, dir.y) * 2.2f + Vector3.up * (GridView.SurfaceY - 0.1f);
                if (k == 5 || k == 7) Bastion(root, at, k == 5 ? -1f : 1f, grow);      // front corners: cannon bastions
                else if (k == 6) Turret(root, at, grow * 0.85f);                        // front middle: a low turret
                else if (k >= 1 && k <= 3) Spire(root, at, grow * (k == 2 ? 1.35f : 1.1f)); // the back: tall spires
                else Drum(root, at, grow);                                              // the sides: domed drums
            }
            // Landmarks beyond the far wall: a giant spire to the right, a domed hall to the left (not in the city).
            if (!CityMode)
            {
            float midX = (b[0] + b[2]) * 0.5f, wide = b[2] - b[0];
            float farZ = b[3] + 4.2f, y = GridView.SurfaceY - 0.1f;
            Spire(root, new Vector3(midX + wide * 0.24f, y, farZ), grow * 1.9f);
            Hall(root, new Vector3(midX - wide * 0.22f, y, farZ + 0.5f), grow * 1.3f);
            }
            // The hexagonal pad round foi's cell, with spokes of light.
            var c = GridView.ToWorld(start) + Vector3.up * (GridView.SurfaceY + 0.06f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Mathf.PI / 6f;
                var mid = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.2f;
                Box("HexEdge", root, mid, new Vector3(2.5f, 0.04f, 0.14f), orange, 0.02f).localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                Box("Spoke", root, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.2f, new Vector3(1.4f, 0.03f, 0.06f), glowOrange, 0.01f)
                    .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
        }

        /// <summary>A tall white spire: stacked tapering drums with orange bands, a blue window strip, an antenna.</summary>
        private static void Spire(Transform root, Vector3 at, float scale)
        {
            var t = Group("Spire", root, at, scale);
            Cyl(t, 0f, 0.6f, 1.9f, dark);
            Cyl(t, 0.6f, 0.15f, 2.0f, orange);
            Cyl(t, 0.75f, 2.4f, 1.45f, white);
            Box("Window", t, new Vector3(0f, 1.9f, -0.72f), new Vector3(0.3f, 1.8f, 0.06f), glowBlue, 0.05f);
            Cyl(t, 3.15f, 0.18f, 1.6f, orange);
            Cyl(t, 3.33f, 2.0f, 1.0f, white);
            Box("Window2", t, new Vector3(0f, 4.2f, -0.5f), new Vector3(0.22f, 1.3f, 0.05f), glowBlue, 0.04f);
            Cyl(t, 5.33f, 0.16f, 1.15f, orange);
            Cyl(t, 5.49f, 0.9f, 0.6f, white);
            Shapes.Primitive(PrimitiveType.Sphere, "Cap", t, new Vector3(0f, 6.5f, 0f), new Vector3(0.7f, 0.5f, 0.7f), blue);
            Cyl(t, 6.7f, 1.4f, 0.08f, orange);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", t, new Vector3(0f, 8.2f, 0f), Vector3.one * 0.2f, glowOrange);
        }

        /// <summary>A drum tower with a blue dome and orange rings.</summary>
        private static void Drum(Transform root, Vector3 at, float scale)
        {
            var t = Group("Drum", root, at, scale);
            Cyl(t, 0f, 0.5f, 2.0f, dark);
            Cyl(t, 0.5f, 2.2f, 1.7f, white);
            for (int i = 0; i < 2; i++) Cyl(t, 1.1f + i * 1.0f, 0.14f, 1.78f, orange);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                Box("Window", t, new Vector3(Mathf.Cos(a) * 0.86f, 1.6f, Mathf.Sin(a) * 0.86f), new Vector3(0.3f, 0.5f, 0.06f), glowBlue, 0.03f)
                    .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            Cyl(t, 2.7f, 0.2f, 1.9f, orange);
            Shapes.Primitive(PrimitiveType.Sphere, "Dome", t, new Vector3(0f, 2.9f, 0f), new Vector3(1.5f, 1.1f, 1.5f), blue);
            Cyl(t, 3.4f, 0.6f, 0.12f, white);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", t, new Vector3(0f, 4.1f, 0f), Vector3.one * 0.22f, glowOrange);
        }

        /// <summary>A squat front turret (it never hides the fight): a wide drum with a gun and a blue light.</summary>
        private static void Turret(Transform root, Vector3 at, float scale)
        {
            var t = Group("Turret", root, at, scale);
            Cyl(t, 0f, 0.5f, 2.0f, dark);
            Cyl(t, 0.5f, 0.7f, 1.7f, white);
            Cyl(t, 1.2f, 0.16f, 1.85f, orange);
            Shapes.Primitive(PrimitiveType.Sphere, "Head", t, new Vector3(0f, 1.45f, 0f), new Vector3(1.2f, 0.7f, 1.2f), white);
            Shapes.Primitive(PrimitiveType.Cylinder, "Gun", t, new Vector3(0f, 1.55f, 0.75f), new Vector3(0.22f, 0.45f, 0.22f), dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Light", t, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.3f, glowBlue);
        }

        /// <summary>A domed hall: a wide white building with blue windows and an orange-ringed dome.</summary>
        private static void Hall(Transform root, Vector3 at, float scale)
        {
            var t = Group("Hall", root, at, scale);
            Box("Base", t, new Vector3(0f, 0.8f, 0f), new Vector3(3.4f, 1.6f, 2.6f), white, 0.25f);
            Box("Trim", t, new Vector3(0f, 1.65f, 0f), new Vector3(3.5f, 0.16f, 2.7f), orange, 0.06f);
            for (int i = 0; i < 3; i++) Box("Window", t, new Vector3(-1.1f + i * 1.1f, 0.95f, -1.31f), new Vector3(0.6f, 0.7f, 0.05f), glowBlue, 0.05f);
            Cyl(t, 1.7f, 0.6f, 2.2f, white);
            Cyl(t, 2.3f, 0.14f, 2.3f, orange);
            Shapes.Primitive(PrimitiveType.Sphere, "Dome", t, new Vector3(0f, 2.45f, 0f), new Vector3(2.1f, 1.5f, 2.1f), blue);
            Shapes.Primitive(PrimitiveType.Cylinder, "Mast", t, new Vector3(0f, 3.6f, 0f), new Vector3(0.1f, 0.6f, 0.1f), orange);
        }

        /// <summary>A heavy cannon bastion on a front corner: an armoured block with a big barrel pointing out.</summary>
        private static void Bastion(Transform root, Vector3 at, float side, float scale)
        {
            var t = Group("Bastion", root, at, scale);
            Box("Block", t, new Vector3(0f, 0.7f, 0f), new Vector3(2.2f, 1.4f, 2.2f), dark, 0.25f);
            Box("Armour", t, new Vector3(0f, 1.1f, 0f), new Vector3(2.0f, 0.9f, 2.0f), white, 0.3f);
            Box("Band", t, new Vector3(0f, 1.58f, 0f), new Vector3(2.1f, 0.14f, 2.1f), orange, 0.05f);
            var gun = Shapes.Primitive(PrimitiveType.Cylinder, "Barrel", t, new Vector3(side * 0.6f, 1.15f, -0.7f), new Vector3(0.7f, 0.9f, 0.7f), white).transform;
            gun.localRotation = Quaternion.Euler(70f, side * -35f, 0f);
            Shapes.Primitive(PrimitiveType.Cylinder, "Muzzle", t, new Vector3(side * 1.15f, 1.0f, -1.45f), new Vector3(0.82f, 0.12f, 0.82f), orange)
                .transform.localRotation = Quaternion.Euler(70f, side * -35f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Lens", t, new Vector3(side * 1.2f, 0.98f, -1.52f), Vector3.one * 0.4f, glowBlue);
        }

        /// <summary>The roof height (world y) over each tile a building of the arena city stands on, for the camera.</summary>
        public static readonly System.Collections.Generic.Dictionary<GridPos, float> BuildingTop = new System.Collections.Generic.Dictionary<GridPos, float>();

        /// <summary>The look of an arena city's buildings, by its world.</summary>
        private enum CityStyle { Town, Ice, Lava, Neon, Desert, Forest, Harbor, Crystal, Factory, Pearl }

        private static CityStyle StyleFor(string worldKey)
        {
            switch (worldKey)
            {
                case "world.ice": case "world.snow": return CityStyle.Ice;
                case "world.canyon": case "world.mine": case "world.heart": return CityStyle.Lava;
                case "world.cyber": case "world.server": case "world.galaxy": case "world.storm": return CityStyle.Neon;
                case "world.desert": case "world.sunset": return CityStyle.Desert;
                case "world.forest": case "world.nightforest": case "world.garden": return CityStyle.Forest;
                case "world.harbor": case "world.canal": case "world.clouds": return CityStyle.Harbor;
                case "world.crystal": case "world.mirror": case "world.aurora": case "world.lavender": return CityStyle.Crystal;
                case "world.factory": case "world.acid": return CityStyle.Factory;
                case "world.castle": return CityStyle.Town;
                default: return CityStyle.Pearl;
            }
        }

        /// <summary>
        /// The arena city's buildings (see BattleArenas.Layout), in its world's style: stone town houses with pitched
        /// roofs, ice blocks, dark lava-lit stone, neon towers, flat sandstone houses, timber houses with green roofs,
        /// white harbour houses with blue domes, crystal towers, factory halls with chimneys, pearl towers. Each has lit
        /// window bands (one band wraps the whole floor) and its roof height goes into <see cref="BuildingTop"/>.
        /// </summary>
        public static void City(Transform root, System.Collections.Generic.List<SquashBot.Data.BattleArenas.Building> list, Color wall, Color accent, Color trim, int arena)
        {
            BuildingTop.Clear();
            var rng = new System.Random(900 + arena * 13);
            var style = arena == 0 ? CityStyle.Town : StyleFor(WorldTheme.ForLevel(arena * SquashBot.Data.LevelCatalog.LevelsPerWorld).key);
            Color body0, body1, roofC, winC;
            switch (style)
            {
                case CityStyle.Ice: body0 = new Color(0.78f, 0.9f, 1f); body1 = new Color(0.62f, 0.8f, 0.95f); roofC = new Color(0.95f, 0.98f, 1f); winC = new Color(0.5f, 0.9f, 1f); break;
                case CityStyle.Lava: body0 = new Color(0.25f, 0.2f, 0.2f); body1 = new Color(0.35f, 0.25f, 0.22f); roofC = new Color(0.15f, 0.12f, 0.12f); winC = new Color(1f, 0.5f, 0.15f); break;
                case CityStyle.Neon: body0 = new Color(0.16f, 0.17f, 0.24f); body1 = new Color(0.22f, 0.2f, 0.32f); roofC = new Color(0.1f, 0.1f, 0.15f); winC = Color.Lerp(accent, new Color(0.3f, 1f, 1f), 0.4f); break;
                case CityStyle.Desert: body0 = new Color(0.92f, 0.8f, 0.6f); body1 = new Color(0.85f, 0.7f, 0.5f); roofC = new Color(0.8f, 0.62f, 0.42f); winC = new Color(1f, 0.85f, 0.5f); break;
                case CityStyle.Forest: body0 = new Color(0.6f, 0.45f, 0.3f); body1 = new Color(0.86f, 0.8f, 0.68f); roofC = new Color(0.25f, 0.5f, 0.3f); winC = new Color(1f, 0.85f, 0.5f); break;
                case CityStyle.Harbor: body0 = new Color(0.95f, 0.95f, 0.93f); body1 = new Color(0.88f, 0.92f, 0.95f); roofC = new Color(0.2f, 0.45f, 0.85f); winC = new Color(0.6f, 0.85f, 1f); break;
                case CityStyle.Crystal: body0 = Color.Lerp(accent, Color.white, 0.55f); body1 = Color.Lerp(accent, Color.white, 0.35f); roofC = accent; winC = Color.Lerp(accent, Color.white, 0.3f); break;
                case CityStyle.Factory: body0 = new Color(0.45f, 0.45f, 0.48f); body1 = new Color(0.55f, 0.4f, 0.32f); roofC = new Color(0.3f, 0.3f, 0.33f); winC = new Color(1f, 0.9f, 0.5f); break;
                case CityStyle.Pearl: body0 = new Color(0.95f, 0.94f, 0.92f); body1 = new Color(0.9f, 0.92f, 0.97f); roofC = new Color(0.95f, 0.8f, 0.45f); winC = new Color(0.7f, 0.9f, 1f); break;
                default: body0 = new Color(0.85f, 0.78f, 0.66f); body1 = new Color(0.78f, 0.7f, 0.6f); roofC = new Color(0.72f, 0.32f, 0.22f); winC = new Color(1f, 0.85f, 0.55f); break; // Town
            }
            var bodies = new[]
            {
                MaterialFactory.CreateSurface(body0, Color.black, 0.35f, 0.05f),
                MaterialFactory.CreateSurface(body1, Color.black, 0.35f, 0.05f),
                MaterialFactory.CreateSurface(Color.Lerp(body0, body1, 0.5f) * 0.92f, Color.black, 0.35f, 0.05f),
            };
            bool glassy = style == CityStyle.Ice || style == CityStyle.Crystal;
            if (glassy) bodies[0] = MaterialFactory.Create(body0, body0 * 0.25f);
            var roof = MaterialFactory.Create(roofC, roofC * 0.15f);
            var window = MaterialFactory.Create(winC, winC * (style == CityStyle.Neon || style == CityStyle.Lava ? 2.2f : 1.4f));
            var dark = MaterialFactory.Create(new Color(0.1f, 0.1f, 0.14f), Color.black);
            var lit = MaterialFactory.Create(winC, winC * 2.5f);
            float y0 = GridView.SurfaceY;
            foreach (var b in list)
            {
                var a = GridView.ToWorld(new GridPos(b.x0, b.y0));
                var c = GridView.ToWorld(new GridPos(b.x1, b.y1));
                var centre = new Vector3((a.x + c.x) * 0.5f, y0, (a.z + c.z) * 0.5f);
                float w = Mathf.Abs(c.x - a.x) + 0.96f, d = Mathf.Abs(c.z - a.z) + 0.96f, h = b.height;
                if (style == CityStyle.Desert || style == CityStyle.Forest || style == CityStyle.Town) h *= 0.75f; // low houses
                if (style == CityStyle.Neon || style == CityStyle.Crystal || style == CityStyle.Pearl) h *= 1.25f; // towers
                var t = new GameObject("Building").transform;
                t.SetParent(root, false);
                t.position = centre;
                var body = bodies[rng.Next(bodies.Length)];
                Box("Body", t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), body, 0.1f);
                // One lit band round each floor (a few dark).
                for (float fy = 0.9f; fy < h - 0.4f; fy += 1.1f)
                    Box("Windows", t, new Vector3(0f, fy, 0f), new Vector3(w + 0.03f, 0.32f, d + 0.03f), rng.Next(6) == 0 ? dark : window, 0.03f);
                float top = h;
                switch (style)
                {
                    case CityStyle.Town:
                    case CityStyle.Forest:
                    {
                        // A pitched roof along the long side.
                        bool alongX = w >= d;
                        float span = alongX ? d : w, len = alongX ? w : d;
                        var ridge = Box("Roof", t, new Vector3(0f, h + span * 0.25f, 0f), new Vector3(span * 0.72f, span * 0.72f, len + 0.1f), roof, 0.04f);
                        ridge.localRotation = Quaternion.Euler(alongX ? 0f : 0f, alongX ? 90f : 0f, 45f);
                        ridge.localScale = new Vector3(1f, 1f, 1f);
                        top = h + span * 0.76f;
                        if (rng.Next(3) == 0) Box("Chimney", t, new Vector3(w * 0.25f, h + span * 0.45f, d * 0.2f), new Vector3(0.3f, span * 0.6f, 0.3f), body, 0.04f);
                        break;
                    }
                    case CityStyle.Harbor:
                        Box("Cornice", t, new Vector3(0f, h + 0.06f, 0f), new Vector3(w + 0.1f, 0.12f, d + 0.1f), body, 0.04f);
                        if (rng.Next(2) == 0) { Shapes.Primitive(PrimitiveType.Sphere, "Dome", t, new Vector3(0f, h + 0.05f, 0f), new Vector3(Mathf.Min(w, d) * 0.7f, Mathf.Min(w, d) * 0.55f, Mathf.Min(w, d) * 0.7f), roof); top = h + Mathf.Min(w, d) * 0.3f; }
                        break;
                    case CityStyle.Neon:
                    case CityStyle.Crystal:
                    case CityStyle.Pearl:
                        // A slimmer upper tier and a lit tip.
                        Box("Tier", t, new Vector3(0f, h + h * 0.18f, 0f), new Vector3(w * 0.6f, h * 0.36f, d * 0.6f), body, 0.08f);
                        Box("Cap", t, new Vector3(0f, h * 1.37f, 0f), new Vector3(w * 0.66f, 0.12f, d * 0.66f), roof, 0.04f);
                        Shapes.Primitive(PrimitiveType.Sphere, "Tip", t, new Vector3(0f, h * 1.4f + 0.25f, 0f), Vector3.one * 0.22f, lit);
                        top = h * 1.4f;
                        break;
                    case CityStyle.Factory:
                        Box("Roof", t, new Vector3(0f, h + 0.08f, 0f), new Vector3(w + 0.1f, 0.16f, d + 0.1f), roof, 0.04f);
                        if (rng.Next(2) == 0) Shapes.Primitive(PrimitiveType.Cylinder, "Chimney", t, new Vector3(w * 0.25f, h + 1.0f, 0f), new Vector3(0.35f, 1f, 0.35f), dark);
                        break;
                    case CityStyle.Lava:
                        Box("Roof", t, new Vector3(0f, h + 0.08f, 0f), new Vector3(w + 0.12f, 0.16f, d + 0.12f), roof, 0.04f);
                        Box("Glow", t, new Vector3(0f, h + 0.18f, 0f), new Vector3(w * 0.5f, 0.06f, d * 0.5f), lit, 0.02f);
                        break;
                    case CityStyle.Ice:
                        Box("Snow", t, new Vector3(0f, h + 0.1f, 0f), new Vector3(w + 0.16f, 0.22f, d + 0.16f), roof, 0.1f);
                        break;
                    default: // Desert: flat roof with a low parapet
                        Box("Parapet", t, new Vector3(0f, h + 0.1f, 0f), new Vector3(w + 0.08f, 0.2f, d + 0.08f), roof, 0.04f);
                        break;
                }
                for (int x = b.x0; x <= b.x1; x++)
                    for (int y = b.y0; y <= b.y1; y++)
                        BuildingTop[new GridPos(x, y)] = y0 + top + 0.2f;
            }
        }

        private static Transform Group(string name, Transform root, Vector3 at, float scale)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.position = at;
            t.localScale = Vector3.one * scale * 0.8f;
            return t;
        }

        private static void Cyl(Transform t, float y, float h, float d, Material m) =>
            Shapes.Primitive(PrimitiveType.Cylinder, "Drum", t, new Vector3(0f, y + h * 0.5f, 0f), new Vector3(d, h * 0.5f, d), m);

        private static Transform Box(string n, Transform p, Vector3 at, Vector3 size, Material m, float r)
        {
            var go = Shapes.Rounded(n, p, Vector3.zero, size, Mathf.Min(r, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f), m);
            go.transform.localPosition = at;
            return go.transform;
        }
    }
}
