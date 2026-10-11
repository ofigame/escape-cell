using System;
using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The fortress round every floor, growing with the campaign in three stages (after the user's reference art):
    /// <list type="bullet">
    /// <item>0 — a stone fortress (worlds 1-5): ramparts with merlons, stone spires, torches;</item>
    /// <item>1 — an advanced fortress (worlds 6-10): darker walls with copper caps and orange lights, copper-trimmed spires
    /// with beacons, metal walkways from the middle to the edges, a hexagonal pad round foi's cell, pods on the walls;</item>
    /// <item>2 — a sci-fi complex (world 11 on): low dark-metal barriers with a strip of light, consoles and server blocks,
    /// tech towers with screens, conveyor walkways, lights in the floor's accent.</item>
    /// </list>
    /// The walls follow the floor's own shape (every edge with no floor beyond it), leaving gaps at the stairs, the cage,
    /// the bridge and the leaps to healing islands; the burning stairs ('F') step down into a pit of fire. Decoration only.
    /// </summary>
    public static class FortressFloor
    {
        public static int StageFor(int world) => world < 5 ? 0 : world < 10 ? 1 : 2;

        private static Material M(Color c, float smooth = 0.15f, float metal = 0f) => MaterialFactory.CreateSurface(c, Color.black, smooth, metal);
        private static Material G(Color c, float glow) => MaterialFactory.Create(c, c * glow);

        private class Kit
        {
            public Material stone, stoneDark, stoneLight, steel, copper, dark, blue, warm, light, lava, screen;
            public Color lightColour;
        }

        /// <param name="surface">The tile transform of a grid cell (walkway plates hang on it).</param>
        public static Transform Build(GridModel grid, Transform parent, int stage, Color accent, GridPos start, Func<GridPos, Transform> surface)
        {
            var root = new GameObject("Fortress").transform;
            root.SetParent(parent, false);
            var k = new Kit
            {
                stone = M(new Color(0.42f, 0.42f, 0.47f)), stoneDark = M(new Color(0.26f, 0.26f, 0.31f)), stoneLight = M(new Color(0.55f, 0.55f, 0.6f)),
                steel = M(new Color(0.36f, 0.38f, 0.44f), 0.55f, 0.6f), copper = M(new Color(0.72f, 0.42f, 0.24f), 0.5f, 0.7f), dark = M(new Color(0.13f, 0.14f, 0.17f), 0.35f, 0.4f),
                blue = G(new Color(0.35f, 0.75f, 1f), 2.2f), warm = G(new Color(1f, 0.6f, 0.25f), 2.4f), lava = G(new Color(1f, 0.45f, 0.1f), 3f),
            };
            k.lightColour = stage == 2 ? Color.Lerp(accent, new Color(0.4f, 0.9f, 1f), 0.4f) : new Color(1f, 0.6f, 0.28f);
            k.light = G(k.lightColour, 2.4f);
            k.screen = G(stage == 2 ? k.lightColour : new Color(0.35f, 0.75f, 1f), 1.6f);
            if (stage == 2)
            {
                k.stoneDark = M(new Color(0.16f, 0.18f, 0.22f), 0.45f, 0.5f);
                k.stone = M(new Color(0.24f, 0.26f, 0.31f), 0.45f, 0.5f);
                k.stoneLight = M(new Color(0.34f, 0.36f, 0.42f), 0.45f, 0.5f);
            }
            float y0 = GridView.SurfaceY;

            bool Main(GridPos p) => grid.IsFloor(p) && !grid.IsCage(p) && !grid.IsBridge(p) && !grid.IsLava(p) && !grid.IsSafe(p);
            bool Keep(GridPos p) => grid.IsLava(p) || grid.IsCage(p) || grid.IsBridge(p) || grid.IsSafe(p) || grid.IsFloor(p);

            // Walls on every outer edge of the floor.
            var lightSpots = new List<Vector3>();
            int n = 0;
            foreach (var p in grid.AllPositions())
            {
                if (!Main(p)) continue;
                foreach (var d in new[] { new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) })
                {
                    var nb = p + d;
                    if (Keep(nb)) continue;
                    if (grid.IsSafe(nb + d) || grid.IsFloor(nb + d)) continue; // a gap foi leaps across: leave it open
                    var at = GridView.ToWorld(p) + new Vector3(d.x, 0f, d.y) * 0.78f + Vector3.up * (y0 + 0.2f);
                    bool alongX = d.y != 0;
                    Wall(root, k, stage, at, alongX, n, new Vector3(d.x, 0f, d.y));
                    if (n % 9 == 4) lightSpots.Add(at + new Vector3(-d.x, 0f, -d.y) * 0.3f + Vector3.up * 0.35f);
                    n++;
                }
            }

            // Towers on the floor's four corners.
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var p in grid.AllPositions())
                if (Main(p)) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
            if (minX <= maxX)
                foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    // The floor tile nearest that corner, the tower just outside it.
                    var corner = new Vector2(sx < 0 ? minX : maxX, sz < 0 ? minZ : maxZ);
                    GridPos best = start;
                    float bestD = float.MaxValue;
                    foreach (var p in grid.AllPositions())
                        if (Main(p)) { float dd = Vector2.Distance(corner, new Vector2(p.x, p.y)); if (dd < bestD) { bestD = dd; best = p; } }
                    var at = GridView.ToWorld(best) + new Vector3(sx * 1.1f, 0f, sz * 1.1f) + Vector3.up * (y0 - 0.1f);
                    float height = sz < 0 ? 0.62f : 1f; // shorter at the front: they never hide the floor
                    if (stage == 2) TechTower(root, k, at, height);
                    else Spire(root, k, stage, at, height);
                }

            // Walkways (stage 1 and 2): the row and the column through foi's cell.
            if (stage >= 1 && surface != null)
            {
                var plate = stage == 1 ? M(new Color(0.3f, 0.32f, 0.37f), 0.5f, 0.6f) : M(new Color(0.12f, 0.13f, 0.16f), 0.4f, 0.4f);
                foreach (var p in grid.AllPositions())
                {
                    if (!Main(p) || (p.x != start.x && p.y != start.y) || p == start) continue;
                    var s = surface(p);
                    if (s == null) continue;
                    bool alongX = p.y == start.y;
                    Box("Walkway", s, new Vector3(0f, 0.053f, 0f), alongX ? new Vector3(1f, 0.012f, 0.46f) : new Vector3(0.46f, 0.012f, 1f), plate, 0.01f);
                    for (int i = -1; i <= 1; i++)
                    {
                        // Grating bars (stage 1) or conveyor chevrons (stage 2).
                        var bar = Box(stage == 1 ? "Grate" : "Chevron", s, new Vector3(alongX ? i * 0.3f : 0f, 0.061f, alongX ? 0f : i * 0.3f),
                            alongX ? new Vector3(0.05f, 0.006f, 0.4f) : new Vector3(0.4f, 0.006f, 0.05f), stage == 1 ? k.dark : k.light, 0.004f);
                        if (stage == 2) bar.localRotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
                    }
                    foreach (float e in new[] { -0.24f, 0.24f })
                        Box("Rail", s, alongX ? new Vector3(0f, 0.062f, e) : new Vector3(e, 0.062f, 0f), alongX ? new Vector3(1f, 0.01f, 0.025f) : new Vector3(0.025f, 0.01f, 1f), stage == 1 ? k.copper : k.light, 0.004f);
                }
                // The hexagonal pad round foi's cell.
                var c = GridView.ToWorld(start) + Vector3.up * (y0 + 0.065f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    var mid = c + new Vector3(Mathf.Cos(a + Mathf.PI / 6f), 0f, Mathf.Sin(a + Mathf.PI / 6f)) * 0.95f;
                    Box("HexEdge", root, mid, new Vector3(1.1f, 0.03f, 0.09f), stage == 1 ? k.copper : k.light, 0.01f)
                        .localRotation = Quaternion.Euler(0f, -(a + Mathf.PI / 6f) * Mathf.Rad2Deg + 90f, 0f);
                }
            }

            // The burning stairs: steps down and out from each 'F' tile into a pit of fire.
            foreach (var p in grid.LavaTiles)
            {
                var at = GridView.ToWorld(p);
                var o = OutwardOf(grid, p);
                for (int s = 0; s < 4; s++)
                {
                    var step = at + new Vector3(o.x, 0f, o.y) * (s * 0.42f) + Vector3.up * (y0 - 0.1f - s * 0.28f);
                    Box("Step", root, step, o.x != 0 ? new Vector3(0.44f, 0.22f, 1f) : new Vector3(1f, 0.22f, 0.44f), k.stoneLight, 0.03f);
                    Box("Ember", root, step + Vector3.up * 0.115f, o.x != 0 ? new Vector3(0.3f, 0.01f, 0.8f) : new Vector3(0.8f, 0.01f, 0.3f), k.lava, 0.005f);
                }
                var pit = at + new Vector3(o.x, 0f, o.y) * 2.1f + Vector3.down * 1.1f;
                Box("FirePit", root, pit, new Vector3(1.6f, 0.3f, 1.6f), k.lava, 0.06f);
                var flames = Flames.Create(root);
                flames.transform.position = pit + Vector3.up * 0.2f;
                flames.transform.localScale = Vector3.one * 1.8f;
            }
            // One warm light over the fire pits of each side (a few lights in all: cheap on phones).
            foreach (var p in grid.LavaTiles)
            {
                var o = OutwardOf(grid, p);
                var nb = new GridPos(p.x + (o.y != 0 ? 1 : 0), p.y + (o.x != 0 ? 1 : 0));
                if (grid.IsLava(nb)) continue; // the end of a run: one light per flight
                AddLight(root, GridView.ToWorld(p) + new Vector3(o.x, 0f, o.y) * 1.8f + Vector3.up * 0.4f, new Color(1f, 0.55f, 0.2f), 4f, 2.4f);
            }
            for (int i = 0; i < Mathf.Min(4, lightSpots.Count); i++) AddLight(root, lightSpots[i], k.lightColour, 3.5f, 1.6f);
            return root;
        }

        private static void AddLight(Transform root, Vector3 at, Color c, float range, float intensity)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = at;
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        /// <summary>One block of wall on a tile edge, in the stage's style.</summary>
        private static void Wall(Transform root, Kit k, int stage, Vector3 at, bool alongX, int i, Vector3 outward)
        {
            Vector3 S(float along, float h, float across) => alongX ? new Vector3(along, h, across) : new Vector3(across, h, along);
            switch (stage)
            {
                case 0:
                    Box("Wall", root, at, S(1.02f, 0.75f, 0.55f), k.stoneDark, 0.05f);
                    Box("WallCap", root, at + Vector3.up * 0.39f, S(1.04f, 0.06f, 0.6f), k.stone, 0.02f);
                    if (i % 2 == 0) Box("Merlon", root, at + Vector3.up * 0.6f, new Vector3(0.46f, 0.36f, 0.46f), k.stone, 0.05f);
                    if (i % 6 == 3) Box("Torch", root, at - outward * 0.3f + Vector3.up * 0.25f, new Vector3(0.12f, 0.14f, 0.12f), k.warm, 0.03f);
                    break;
                case 1:
                    Box("Wall", root, at, S(1.02f, 0.75f, 0.55f), k.stoneDark, 0.05f);
                    Box("WallCap", root, at + Vector3.up * 0.39f, S(1.04f, 0.07f, 0.62f), k.copper, 0.02f);
                    Box("Strip", root, at - outward * 0.28f + Vector3.up * 0.1f, S(0.7f, 0.05f, 0.02f), k.light, 0.01f);
                    if (i % 3 == 0) Box("Panel", root, at - outward * 0.28f + Vector3.up * -0.12f, S(0.3f, 0.18f, 0.02f), k.blue, 0.02f);
                    if (i % 4 == 2)
                    {
                        // A pod on the wall.
                        Shapes.Primitive(PrimitiveType.Cylinder, "Pod", root, at + Vector3.up * 0.55f, new Vector3(0.42f, 0.14f, 0.42f), k.steel);
                        Shapes.Primitive(PrimitiveType.Sphere, "PodTop", root, at + Vector3.up * 0.72f, new Vector3(0.32f, 0.22f, 0.32f), k.dark);
                        Shapes.Primitive(PrimitiveType.Sphere, "PodLight", root, at + Vector3.up * 0.86f, Vector3.one * 0.08f, k.light);
                    }
                    break;
                default:
                    // A low barrier of dark metal with a strip of light; every few a console or a server block.
                    Box("Barrier", root, at + Vector3.down * 0.15f, S(1.02f, 0.45f, 0.5f), k.stoneDark, 0.04f);
                    Box("Glow", root, at + Vector3.up * 0.09f, S(0.96f, 0.03f, 0.08f), k.light, 0.01f);
                    if (i % 5 == 1)
                    {
                        Box("Server", root, at + outward * 0.15f + Vector3.up * 0.35f, new Vector3(0.6f, 0.95f, 0.6f), k.stone, 0.05f);
                        for (int s = 0; s < 3; s++)
                            Box("Rack", root, at + outward * 0.15f - outward * 0.31f + Vector3.up * (0.1f + s * 0.25f), S(0.45f, 0.05f, 0.02f), k.screen, 0.01f);
                    }
                    else if (i % 5 == 3)
                    {
                        Box("Console", root, at + Vector3.up * 0.2f, S(0.6f, 0.3f, 0.4f), k.stone, 0.04f);
                        Box("Screen", root, at + Vector3.up * 0.38f - outward * 0.1f, S(0.48f, 0.02f, 0.28f), k.screen, 0.01f)
                            .localRotation = Quaternion.LookRotation(-outward) * Quaternion.Euler(-60f, 0f, 0f);
                    }
                    break;
            }
        }

        /// <summary>A corner spire (stone, or with copper and beacons at stage 1), <paramref name="height"/> of full height.</summary>
        private static void Spire(Transform root, Kit k, int stage, Vector3 at, float height)
        {
            var s = new GameObject("Spire").transform;
            s.SetParent(root, false);
            s.position = at;
            s.localScale = new Vector3(0.8f, 0.8f * height, 0.8f);
            Box("Base", s, new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1.9f), k.stoneDark, 0.1f);
            Box("BaseTrim", s, new Vector3(0f, 1.22f, 0f), new Vector3(2f, 0.1f, 2f), k.copper, 0.03f);
            Shapes.Primitive(PrimitiveType.Cylinder, "Shaft", s, new Vector3(0f, 2.9f, 0f), new Vector3(1.25f, 1.7f, 1.25f), stage == 1 ? k.stoneDark : k.stone);
            foreach (float y in new[] { 1.6f, 2.9f, 4.2f }) Shapes.Primitive(PrimitiveType.Cylinder, "Band", s, new Vector3(0f, y, 0f), new Vector3(1.32f, 0.06f, 1.32f), k.copper);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                foreach (float y in new[] { 2.25f, 3.55f })
                    Box("Window", s, new Vector3(Mathf.Cos(a) * 0.62f, y, Mathf.Sin(a) * 0.62f), new Vector3(0.16f, 0.36f, 0.04f), k.blue, 0.03f)
                        .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            Shapes.Primitive(PrimitiveType.Cylinder, "Gallery", s, new Vector3(0f, 4.7f, 0f), new Vector3(1.75f, 0.14f, 1.75f), k.steel);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Box("Light", s, new Vector3(Mathf.Cos(a) * 0.86f, 4.7f, Mathf.Sin(a) * 0.86f), new Vector3(0.1f, 0.08f, 0.1f), k.warm, 0.02f);
            }
            Shapes.Primitive(PrimitiveType.Cylinder, "Crown", s, new Vector3(0f, 5.2f, 0f), new Vector3(0.95f, 0.45f, 0.95f), stage == 1 ? k.copper : k.stoneLight);
            Shapes.Primitive(PrimitiveType.Sphere, "Cap", s, new Vector3(0f, 5.7f, 0f), new Vector3(0.85f, 0.55f, 0.85f), k.steel);
            Shapes.Primitive(PrimitiveType.Cylinder, "Antenna", s, new Vector3(0f, stage == 1 ? 6.9f : 6.5f, 0f), new Vector3(0.07f, stage == 1 ? 1f : 0.6f, 0.07f), k.dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", s, new Vector3(0f, stage == 1 ? 7.95f : 7.15f, 0f), Vector3.one * (stage == 1 ? 0.26f : 0.18f), stage == 1 ? k.light : k.warm);
            if (stage == 1)
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2f;
                    Box("Fin", s, new Vector3(Mathf.Cos(a) * 0.55f, 5.6f, Mathf.Sin(a) * 0.55f), new Vector3(0.08f, 0.9f, 0.3f), k.copper, 0.02f)
                        .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                }
        }

        /// <summary>A sci-fi tower: stacked dark blocks with screens and light bands, a dish and an antenna on top.</summary>
        private static void TechTower(Transform root, Kit k, Vector3 at, float height)
        {
            var s = new GameObject("TechTower").transform;
            s.SetParent(root, false);
            s.position = at;
            s.localScale = new Vector3(0.8f, 0.8f * height, 0.8f);
            Box("Base", s, new Vector3(0f, 0.5f, 0f), new Vector3(1.9f, 1f, 1.9f), k.stoneDark, 0.06f);
            Box("Band", s, new Vector3(0f, 1.02f, 0f), new Vector3(1.95f, 0.06f, 1.95f), k.light, 0.02f);
            Box("Mid", s, new Vector3(0f, 2.3f, 0f), new Vector3(1.35f, 2.5f, 1.35f), k.stone, 0.06f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f;
                var face = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Box("Screen", s, face * 0.68f + Vector3.up * 2.1f, new Vector3(Mathf.Abs(face.z) * 0.8f + 0.03f, 1.2f, Mathf.Abs(face.x) * 0.8f + 0.03f), k.screen, 0.02f);
                Box("Strip", s, face * 0.68f + Vector3.up * 3.05f, new Vector3(Mathf.Abs(face.z) * 1.1f + 0.03f, 0.06f, Mathf.Abs(face.x) * 1.1f + 0.03f), k.light, 0.01f);
            }
            Box("Top", s, new Vector3(0f, 3.85f, 0f), new Vector3(0.95f, 0.6f, 0.95f), k.stoneDark, 0.05f);
            Shapes.Primitive(PrimitiveType.Sphere, "Dish", s, new Vector3(0.2f, 4.35f, 0f), new Vector3(0.7f, 0.18f, 0.7f), k.steel);
            Shapes.Primitive(PrimitiveType.Cylinder, "Antenna", s, new Vector3(-0.25f, 4.9f, 0.2f), new Vector3(0.06f, 0.8f, 0.06f), k.dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", s, new Vector3(-0.25f, 5.75f, 0.2f), Vector3.one * 0.16f, k.light);
        }

        /// <summary>Which way a stair tile leads off the floor (towards the side with no floor).</summary>
        private static Vector2Int OutwardOf(GridModel grid, GridPos p)
        {
            foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
            {
                var nb = new GridPos(p.x + d.x, p.y + d.y);
                var back = new GridPos(p.x - d.x, p.y - d.y);
                if (!grid.Exists(nb) && grid.IsFloor(back) && !grid.IsLava(back)) return d;
            }
            return new Vector2Int(0, -1);
        }

        private static Transform Box(string n, Transform p, Vector3 at, Vector3 size, Material m, float r) =>
            Shapes.Rounded(n, p, at, size, Mathf.Min(r, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f), m).transform;
    }
}
