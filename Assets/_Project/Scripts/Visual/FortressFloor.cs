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
        /// <param name="stageBounds">The floor's fortress stages (x0, y0, x1, y1 each): four towers round every stage.</param>
        /// <param name="wallHeight">How tall the ramparts stand (1 = the first worlds'; they rise further on).</param>
        public static Transform Build(GridModel grid, Transform parent, int stage, Color accent, GridPos start, Func<GridPos, Transform> surface, int[] stageBounds = null, float wallHeight = 1f)
        {
            lightsLeft = MaxLights;
            WallHeight = wallHeight;
            lastGrid = grid;
            flamesLeft = 40;
            LastStage = stage;
            var root = new GameObject("Fortress").transform;
            root.SetParent(parent, false);
            var k = new Kit
            {
                stone = M(new Color(0.42f, 0.42f, 0.47f)), stoneDark = M(new Color(0.26f, 0.26f, 0.31f)), stoneLight = M(new Color(0.55f, 0.55f, 0.6f)),
                steel = M(new Color(0.36f, 0.38f, 0.44f), 0.55f, 0.6f), copper = M(new Color(0.72f, 0.42f, 0.24f), 0.5f, 0.7f), dark = M(new Color(0.13f, 0.14f, 0.17f), 0.35f, 0.4f),
                blue = G(new Color(0.35f, 0.75f, 1f), 2.2f), warm = G(new Color(1f, 0.6f, 0.25f), 2.4f), lava = G(new Color(1f, 0.45f, 0.1f), 3f),
            };
            // Warm orange lights everywhere (a touch of the world's colour on the far worlds), like lamps in dusk.
            k.lightColour = stage == 2 ? Color.Lerp(new Color(1f, 0.55f, 0.22f), accent, 0.25f) : new Color(1f, 0.6f, 0.28f);
            k.light = G(k.lightColour, 2.4f);
            k.screen = G(stage == 2 ? k.lightColour : new Color(0.35f, 0.75f, 1f), 1.6f);
            if (stage >= 1)
            {
                // The ramparts are clearly darker than the floor they ring, in the floor's own hue: foi fights inside them.
                var f = GridView.FortressStone;
                float metal = stage == 2 ? 0.5f : 0.15f, smooth = stage == 2 ? 0.45f : 0.2f;
                k.stoneDark = M(f * 0.34f, smooth, metal);
                k.stone = M(f * 0.48f, smooth, metal);
                k.stoneLight = M(f * 0.62f, smooth, metal);
            }
            if (WallTint.HasValue)
            {
                // The ramparts' own colour (each arena, each world): stone in that tint — or blocks of ice.
                var t = WallTint.Value;
                k.stoneDark = Fx == WallFx.Ice ? MaterialFactory.CreateTransparent(new Color(t.r, t.g, t.b, 0.62f), t * 0.35f) : M(t * 0.82f, 0.35f, 0f);
                k.stone = Fx == WallFx.Ice ? MaterialFactory.CreateTransparent(new Color(t.r, t.g, t.b, 0.75f), t * 0.45f) : M(t, 0.4f, 0f);
                k.stoneLight = M(Color.Lerp(t, Color.white, 0.3f), 0.5f, 0f);
                k.dark = M(t * 0.55f, 0.2f, 0f);
                fxMat = MaterialFactory.Create(FxColour, FxColour * 2.4f);
            }
            else if (WallThick > 1.5f)
            {
                // The arenas: thick ramparts of pale grey dressed stone (never brown), lighter caps.
                k.stoneDark = M(new Color(0.55f, 0.56f, 0.59f), 0.3f, 0f);
                k.stone = M(new Color(0.68f, 0.69f, 0.72f), 0.35f, 0f);
                k.stoneLight = M(new Color(0.8f, 0.8f, 0.83f), 0.4f, 0f);
                k.dark = M(new Color(0.36f, 0.37f, 0.4f), 0.2f, 0f);
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
                    Wall(root, k, stage, at, alongX, n, new Vector3(d.x, 0f, d.y), front: d.x < 0 || d.y < 0, scale: WallScaleAt != null ? WallScaleAt(p) : 1f);
                    if (n % 9 == 4) lightSpots.Add(at + new Vector3(-d.x, 0f, -d.y) * 0.3f + Vector3.up * 0.35f);
                    n++;
                }
            }

            // Towers on the floor's four corners.
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var p in grid.AllPositions())
                if (Main(p)) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
            if (SkipCornerTowers) { }
            else if (stageBounds != null && stageBounds.Length >= 4)
            {
                // A fortress of stages: four towers round each, just off its corners (clear of the fire stairs).
                for (int s = 0; s < stageBounds.Length / 4; s++)
                    foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                    {
                        var at = CornerTowerBase(stageBounds, s, sx, sz, out float height);
                        if (stage == 2) TechTower(root, k, at, height);
                        else Spire(root, k, stage, at, height);
                    }
            }
            else if (minX <= maxX)
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

            // The hexagonal pad round foi's cell (stage 1 and 2).
            if (stage >= 1)
            {
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
                if (Inner(grid, p))
                {
                    // A fire pit sunk into the middle of a stage (hard floors' last stages): a stone rim round a pool of fire.
                    foreach (var d in new[] { new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f) })
                        if (!grid.IsLava(p + new GridPos((int)d.x, (int)d.z)))
                        Box("PitRim", root, at + d * 0.45f + Vector3.up * (y0 - 0.02f), d.x != 0 ? new Vector3(0.12f, 0.16f, 1f) : new Vector3(1f, 0.16f, 0.12f), k.stoneLight, 0.03f);
                    // Sunk deep: dark stone sides down to a pool of fire, flames licking up out of it.
                    foreach (var d in new[] { new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) })
                        if (!grid.IsLava(p + d))
                            Box("PitSide", root, at + new Vector3(d.x * 0.47f, y0 - 0.3f, d.y * 0.47f), d.x != 0 ? new Vector3(0.06f, 0.6f, 1f) : new Vector3(1f, 0.6f, 0.06f), k.stoneDark, 0.01f);
                    Box("FirePool", root, at + Vector3.up * (y0 - 0.55f), new Vector3(1.0f, 0.06f, 1.0f), k.lava, 0.005f);
                    var f = Flames.Create(root);
                    f.transform.position = at + Vector3.up * (y0 - 0.5f);
                    f.transform.localScale = Vector3.one * 1.1f;
                    if (!grid.IsLava(p + new GridPos(-1, 0)) && !grid.IsLava(p + new GridPos(0, -1))) AddLight(root, at + new Vector3(0.5f, 0.6f, 0.5f), new Color(1f, 0.5f, 0.15f), 3.5f, 2.4f); // one per pit
                    continue;
                }
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
                if (Inner(grid, p)) continue;
                var o = OutwardOf(grid, p);
                var nb = new GridPos(p.x + (o.y != 0 ? 1 : 0), p.y + (o.x != 0 ? 1 : 0));
                if (grid.IsLava(nb)) continue; // the end of a run: one light per flight
                AddLight(root, GridView.ToWorld(p) + new Vector3(o.x, 0f, o.y) * 1.8f + Vector3.up * 0.4f, new Color(1f, 0.55f, 0.2f), 4f, 2.4f);
            }
            for (int i = 0; i < Mathf.Min(4, lightSpots.Count); i++) AddLight(root, lightSpots[i], k.lightColour, 3.5f, 1.6f);
            return root;
        }

        /// <summary>Point lights still allowed on this floor (a handful in all keeps phones smooth).</summary>
        private const int MaxLights = 10;
        private static int lightsLeft;

        // ---------- Where the player's defences stand (see DefenseSlots) ----------

        /// <summary>The style of the fortress built last (0 stone, 1 copper, 2 sci-fi).</summary>
        public static int LastStage { get; private set; }
        private static GridModel lastGrid;
        /// <summary>How much bigger the corner towers stand on this floor (they grow world by world).</summary>
        public static float TowerGrow = 1f;
        /// <summary>The battle arenas put up their own towers (see ArenaDecor).</summary>
        public static bool SkipCornerTowers;
        /// <summary>How thick the ramparts are (1 = the first levels'; thicker further in, much thicker in the arenas).</summary>
        public static float WallThick = 1f;
        /// <summary>What crowns the ramparts: nothing, fire all along them, ice (see-through blocks), a glowing edge.</summary>
        public enum WallFx { None, Flames, Ice, Glow }
        public static WallFx Fx;
        /// <summary>The ramparts' own colour (null: from the floor), and the colour of their glow.</summary>
        public static Color? WallTint;
        public static Color FxColour = new Color(1f, 0.55f, 0.2f);
        /// <summary>Taller, thicker ramparts per floor tile (each fortress stage stands higher than the one before).</summary>
        public static Func<GridPos, float> WallScaleAt;
        private static float TowerWide => 1f + (TowerGrow - 1f) * 0.4f;
        public static readonly (int sx, int sz)[] Corners = { (-1, -1), (1, -1), (-1, 1), (1, 1) };

        /// <summary>The foot of a stage's corner tower and its height (the front ones are shorter).</summary>
        public static Vector3 CornerTowerBase(int[] b, int s, int sx, int sz, out float height)
        {
            var c = new GridPos(sx < 0 ? b[s * 4] : b[s * 4 + 2], sz < 0 ? b[s * 4 + 1] : b[s * 4 + 3]);
            if (lastGrid != null && !lastGrid.IsFloor(c))
            {
                // A shaped stage (hexagon, octagon): the tower stands off the floor's own corner nearest the box's.
                float best = float.MaxValue;
                var near = c;
                for (int x = b[s * 4]; x <= b[s * 4 + 2]; x++)
                    for (int y = b[s * 4 + 1]; y <= b[s * 4 + 3]; y++)
                    {
                        var q = new GridPos(x, y);
                        if (!lastGrid.IsFloor(q) || lastGrid.IsLava(q)) continue;
                        float d = (x - c.x) * (x - c.x) + (y - c.y) * (y - c.y);
                        if (d < best) { best = d; near = q; }
                    }
                c = near;
            }
            height = sz < 0 ? 0.55f : 0.85f;
            return GridView.ToWorld(c) + new Vector3(sx, 0f, sz) * (1.35f * TowerWide) + Vector3.up * (GridView.SurfaceY - 0.1f);
        }

        /// <summary>Where an archer stands on a corner tower: its gallery (or the tech tower's roof), on the stage's side.</summary>
        public static Vector3 CornerTowerTop(int[] b, int s, int corner)
        {
            var (sx, sz) = Corners[corner];
            var at = CornerTowerBase(b, s, sx, sz, out float height);
            float top = (LastStage == 2 ? 4.15f : 4.78f) * 0.8f * height * TowerGrow * 1.15f;
            return at + Vector3.up * top - new Vector3(sx, 0f, sz).normalized * (LastStage == 2 ? 0.15f : 0.5f);
        }

        /// <summary>The top of the rampart running along a floor tile's edge (outward one step from it).</summary>
        public static Vector3 WallTop(GridPos p, GridPos outward)
        {
            var o = new Vector3(outward.x, 0f, outward.y);
            var at = GridView.ToWorld(p) + o * 0.78f + Vector3.up * (GridView.SurfaceY + 0.2f);
            bool front = outward.x < 0 || outward.y < 0;
            float h = (LastStage == 2 ? 0.95f : 0.8f) * WallHeight * (WallScaleAt != null ? WallScaleAt(p) : 1f) * (front ? 0.55f : 1f);
            return new Vector3(at.x, at.y - 0.35f + h + 0.07f, at.z);
        }

        private static void AddLight(Transform root, Vector3 at, Color c, float range, float intensity)
        {
            if (lightsLeft <= 0) return;
            lightsLeft--;
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
        private static float WallHeight = 1f;

        /// <param name="front">A wall on the camera's side of the floor: kept lower so it never hides foi.</param>
        private static Material fxMat;
        private static int flamesLeft;

        private static void Wall(Transform root, Kit k, int stage, Vector3 at, bool alongX, int i, Vector3 outward, bool front = false, float scale = 1f)
        {
            Vector3 S(float along, float h, float across) => alongX ? new Vector3(along, h, across) : new Vector3(across, h, along);
            if (true) // every style now builds real ramparts that grow (the old low stone walls are kept below for reference)
            {
                // A thick rampart of dark plates with soft edges, a band of warm light along its inner face, round
                // portholes and now and then a taller buttress — taller further into the campaign.
                float h = (stage == 2 ? 0.95f : 0.8f) * WallHeight * scale * (front ? 0.55f : 1f);
                float bottom = at.y - 0.35f;
                float thick = 0.62f * WallThick * scale;
                var o = outward * (thick - 0.62f) * 0.5f; // a thicker wall grows outward, its inner face stays put
                var mid = new Vector3(at.x, bottom + h * 0.5f, at.z) + o;
                Box("Rampart", root, mid, S(1.04f, h, thick), k.stoneDark, 0.12f);
                bool grand = WallThick * scale > 1.45f;
                Box("Cap", root, new Vector3(at.x, bottom + h + 0.03f, at.z) + o, S(1.06f, 0.1f, thick + 0.06f), grand || WallTint.HasValue ? k.stoneLight : stage == 1 ? k.copper : k.stone, 0.04f);
                if (Fx == WallFx.Flames && i % 2 == 0 && flamesLeft-- > 0) // a capped number of fires (phones)
                {
                    // Fire all along the top of the ramparts.
                    var fl = Flames.Create(root);
                    fl.transform.position = new Vector3(at.x, bottom + h + 0.1f, at.z) + o;
                    fl.transform.localScale = Vector3.one * (0.55f + thick * 0.15f);
                }
                else if (Fx == WallFx.Glow && fxMat != null)
                    Box("TopGlow", root, new Vector3(at.x, bottom + h + 0.09f, at.z) + o - outward * (thick * 0.5f - 0.04f), S(1.04f, 0.05f, 0.07f), fxMat, 0.01f);
                if (grand)
                {
                    // Dressed stone: courses along the inner face, staggered joints, merlons on top.
                    var face = new Vector3(at.x, 0f, at.z) - outward * 0.315f;
                    for (float y = 0.7f; y < h - 0.1f; y += 0.7f)
                        Box("Course", root, new Vector3(face.x, bottom + y, face.z), S(1.04f, 0.035f, 0.02f), k.dark, 0.005f);
                    int row = 0;
                    for (float y = 0.35f; y < h - 0.1f && i % 2 == 0; y += 0.7f, row++)
                        Box("Joint", root, new Vector3(face.x, bottom + y, face.z) + (alongX ? Vector3.right : Vector3.forward) * ((row + i) % 2 == 0 ? -0.25f : 0.25f),
                            S(0.035f, 0.66f, 0.02f), k.dark, 0.005f);
                    if (i % 2 == 0) Box("Merlon", root, new Vector3(at.x, bottom + h + 0.3f, at.z) + o, S(0.5f, 0.45f, thick * 0.8f), k.stone, 0.06f);
                }
                Box("Band", root, new Vector3(at.x, bottom + h * 0.62f, at.z) - outward * 0.315f, S(0.84f, 0.05f, 0.02f), k.light, 0.01f);
                Box("Plinth", root, new Vector3(at.x, bottom + 0.42f, at.z) - outward * 0.3f, S(1.0f, 0.12f, 0.06f), k.stone, 0.03f);
                if (i % 3 == 1 && !front)
                {
                    var port = Shapes.Primitive(PrimitiveType.Cylinder, "Port", root, new Vector3(at.x, bottom + h * 0.4f, at.z) - outward * 0.32f, new Vector3(0.2f, 0.015f, 0.2f), k.light).transform;
                    port.rotation = Quaternion.LookRotation(Vector3.up, -outward);
                    Shapes.Primitive(PrimitiveType.Cylinder, "PortRim", root, new Vector3(at.x, bottom + h * 0.4f, at.z) - outward * 0.31f, new Vector3(0.28f, 0.012f, 0.28f), k.stone).transform
                        .rotation = Quaternion.LookRotation(Vector3.up, -outward);
                }
                if (i % 6 == 4)
                {
                    float bh = h * 1.3f;
                    Box("Buttress", root, new Vector3(at.x, bottom + bh * 0.5f, at.z) + outward * 0.05f, S(0.42f, bh, 0.78f), k.stone, 0.12f);
                    Box("ButtressLight", root, new Vector3(at.x, bottom + bh - 0.12f, at.z) - outward * 0.345f, S(0.2f, 0.08f, 0.02f), k.light, 0.01f);
                }
                if (stage == 2 && i % 4 == 2)
                    Box("Vent", root, new Vector3(at.x, bottom + h * 0.82f, at.z) - outward * 0.315f, S(0.32f, 0.1f, 0.02f), k.dark, 0.01f);
                return;
            }
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
            s.localScale = new Vector3(0.8f * TowerWide, 0.8f * height * 1.15f * TowerGrow, 0.8f * TowerWide); // much taller, a little wider
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
            s.localScale = new Vector3(0.8f * TowerWide, 0.8f * height * 1.15f * TowerGrow, 0.8f * TowerWide); // much taller, a little wider
            Box("Base", s, new Vector3(0f, 0.5f, 0f), new Vector3(1.9f, 1f, 1.9f), k.stoneDark, 0.3f);
            Box("Band", s, new Vector3(0f, 1.02f, 0f), new Vector3(1.95f, 0.06f, 1.95f), k.light, 0.02f);
            Box("Mid", s, new Vector3(0f, 2.3f, 0f), new Vector3(1.35f, 2.5f, 1.35f), k.stone, 0.4f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f;
                var face = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Box("Screen", s, face * 0.68f + Vector3.up * 2.1f, new Vector3(Mathf.Abs(face.z) * 0.8f + 0.03f, 1.2f, Mathf.Abs(face.x) * 0.8f + 0.03f), k.screen, 0.02f);
                Box("Strip", s, face * 0.68f + Vector3.up * 3.05f, new Vector3(Mathf.Abs(face.z) * 1.1f + 0.03f, 0.06f, Mathf.Abs(face.x) * 1.1f + 0.03f), k.light, 0.01f);
            }
            Box("Top", s, new Vector3(0f, 3.85f, 0f), new Vector3(0.95f, 0.6f, 0.95f), k.stoneDark, 0.25f);
            Shapes.Primitive(PrimitiveType.Sphere, "Dish", s, new Vector3(0.2f, 4.35f, 0f), new Vector3(0.7f, 0.18f, 0.7f), k.steel);
            Shapes.Primitive(PrimitiveType.Cylinder, "Antenna", s, new Vector3(-0.25f, 4.9f, 0.2f), new Vector3(0.06f, 0.8f, 0.06f), k.dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", s, new Vector3(-0.25f, 5.75f, 0.2f), Vector3.one * 0.16f, k.light);
        }

        /// <summary>A fire tile with floor all round it: a pit inside a stage rather than stairs off its edge.</summary>
        private static bool Inner(GridModel grid, GridPos p)
        {
            foreach (var d in new[] { new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) })
                if (!grid.IsFloor(p + d)) return false; // lava next to it is the same pit
            return true;
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
