using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The fortress round level 1's floor (after the sample battlefield): ramparts with merlons along every edge,
    /// broken where a flight of stairs leads off; four spire towers on the corners; the burning stairs (the floor's
    /// 'F' tiles) stepping down into a pit of fire with flames on every step; torches on the walls that really light
    /// the stone, and a heavy stone plinth under it all. Pure decoration: the play stays on the grid.
    /// </summary>
    public static class FortressFloor
    {
        private static Material M(float r, float g, float b, float smooth = 0.15f, float metal = 0f) =>
            MaterialFactory.CreateSurface(new Color(r, g, b), Color.black, smooth, metal);

        public static Transform Build(GridModel grid, Transform parent)
        {
            var root = new GameObject("Fortress").transform;
            root.SetParent(parent, false);
            var stone = M(0.42f, 0.42f, 0.47f); var stoneDark = M(0.26f, 0.26f, 0.31f); var stoneLight = M(0.55f, 0.55f, 0.6f);
            var steel = M(0.36f, 0.38f, 0.44f, 0.55f, 0.6f); var copper = M(0.72f, 0.42f, 0.24f, 0.5f, 0.7f); var dark = M(0.14f, 0.15f, 0.18f, 0.3f, 0.3f);
            var blue = MaterialFactory.Create(new Color(0.35f, 0.75f, 1f), new Color(0.6f, 1.6f, 2.6f));
            var warm = MaterialFactory.Create(new Color(1f, 0.62f, 0.25f), new Color(2.6f, 1.3f, 0.4f));
            var lava = MaterialFactory.Create(new Color(1f, 0.45f, 0.1f), new Color(3.2f, 1.2f, 0.2f));

            // The main floor's bounds (not the cage, the bridge or the stairs).
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsFloor(p) || grid.IsCage(p) || grid.IsBridge(p) || grid.IsLava(p)) continue;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
            }
            if (minX > maxX) return root;
            float y0 = GridView.SurfaceY;
            float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
            float hx = (maxX - minX) * 0.5f + 0.5f, hz = (maxZ - minZ) * 0.5f + 0.5f;

            // A heavy plinth under the whole floor.
            Box("Plinth", root, new Vector3(cx, -1.2f, cz), new Vector3(hx * 2f + 2.6f, 1.4f, hz * 2f + 2.6f), stoneDark, 0.12f);
            Box("PlinthLip", root, new Vector3(cx, -0.48f, cz), new Vector3(hx * 2f + 2.8f, 0.12f, hz * 2f + 2.8f), stone, 0.05f);

            // Ramparts: along each side, a wall block per tile, merlons on every other one; gaps where stairs or the cage are.
            bool Opening(int x, int z) => grid.IsLava(new GridPos(x, z)) || grid.IsCage(new GridPos(x, z)) || grid.IsBridge(new GridPos(x, z));
            var torchSpots = new List<Vector3>();
            void Wall(Vector3 at, bool alongX, int i)
            {
                Box("Wall", root, at, alongX ? new Vector3(1.02f, 0.75f, 0.55f) : new Vector3(0.55f, 0.75f, 1.02f), stoneDark, 0.05f);
                Box("WallCap", root, at + Vector3.up * 0.39f, alongX ? new Vector3(1.04f, 0.06f, 0.6f) : new Vector3(0.6f, 0.06f, 1.04f), stone, 0.02f);
                if (i % 2 == 0) Box("Merlon", root, at + Vector3.up * 0.6f, new Vector3(0.46f, 0.36f, 0.46f), stone, 0.05f);
            }
            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                if (!Opening(x, minZ - 1) && !(x >= minX && x <= maxX && grid.IsLava(new GridPos(x, minZ - 1)))) Wall(new Vector3(x, y0 + 0.2f, minZ - 0.8f), true, x);
                if (!Opening(x, maxZ + 1)) Wall(new Vector3(x, y0 + 0.2f, maxZ + 0.8f), true, x);
                if (x == minX + 2 || x == maxX - 2) { torchSpots.Add(new Vector3(x, y0 + 0.55f, minZ - 0.5f)); torchSpots.Add(new Vector3(x, y0 + 0.55f, maxZ + 0.5f)); }
            }
            for (int z = minZ; z <= maxZ; z++)
            {
                if (!Opening(minX - 1, z)) Wall(new Vector3(minX - 0.8f, y0 + 0.2f, z), false, z);
                if (!Opening(maxX + 1, z)) Wall(new Vector3(maxX + 0.8f, y0 + 0.2f, z), false, z);
            }

            // Corner spires (shorter at the front, so they never hide the floor from the camera).
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                Spire(root, new Vector3(sx < 0 ? minX - 1.1f : maxX + 1.1f, y0 - 0.1f, sz < 0 ? minZ - 1.1f : maxZ + 1.1f), sz < 0 ? 0.62f : 1f, stone, stoneDark, stoneLight, steel, copper, dark, blue, warm);

            // The burning stairs: steps down and outwards from each 'F' tile into a pit of fire.
            foreach (var p in grid.LavaTiles)
            {
                var at = GridView.ToWorld(p);
                var o = OutwardOf(grid, p);
                for (int s = 0; s < 4; s++)
                {
                    var step = at + new Vector3(o.x, 0f, o.y) * (s * 0.42f) + Vector3.up * (y0 - 0.1f - s * 0.28f);
                    Box("Step", root, step, o.x != 0 ? new Vector3(0.44f, 0.22f, 1f) : new Vector3(1f, 0.22f, 0.44f), stoneLight, 0.03f);
                    Box("Ember", root, step + Vector3.up * 0.115f, o.x != 0 ? new Vector3(0.3f, 0.01f, 0.8f) : new Vector3(0.8f, 0.01f, 0.3f), lava, 0.005f);
                }
                var pit = at + new Vector3(o.x, 0f, o.y) * 2.1f + Vector3.down * 1.1f;
                Box("FirePit", root, pit, new Vector3(1.6f, 0.3f, 1.6f), lava, 0.06f);
                var flames = Flames.Create(root);
                flames.transform.position = pit + Vector3.up * 0.2f;
                flames.transform.localScale = Vector3.one * 1.8f;
                var glowLight = new GameObject("FireLight").AddComponent<Light>();
                glowLight.transform.SetParent(root, false);
                glowLight.transform.position = pit + Vector3.up * 1.2f;
                glowLight.type = LightType.Point;
                glowLight.color = new Color(1f, 0.55f, 0.2f);
                glowLight.range = 4f;
                glowLight.intensity = 2.5f;
                glowLight.shadows = LightShadows.None;
            }

            // Torches on the walls: a flame on a bracket and a real warm light on the stone.
            foreach (var t in torchSpots)
            {
                Box("Bracket", root, t, new Vector3(0.08f, 0.22f, 0.08f), dark, 0.02f);
                Box("Torch", root, t + Vector3.up * 0.16f, new Vector3(0.12f, 0.12f, 0.12f), warm, 0.03f);
                var l = new GameObject("TorchLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = t + Vector3.up * 0.5f;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.62f, 0.3f);
                l.range = 3.5f;
                l.intensity = 1.6f;
                l.shadows = LightShadows.None;
            }
            return root;
        }

        /// <summary>Which way a stair tile leads off the floor (towards the side with no floor).</summary>
        private static Vector2Int OutwardOf(GridModel grid, GridPos p)
        {
            foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
            {
                var n = new GridPos(p.x + d.x, p.y + d.y);
                var back = new GridPos(p.x - d.x, p.y - d.y);
                if (!grid.Exists(n) && grid.IsFloor(back) && !grid.IsLava(back)) return d;
            }
            return new Vector2Int(0, -1);
        }

        private static Transform Box(string n, Transform p, Vector3 at, Vector3 size, Material m, float r) =>
            Shapes.Rounded(n, p, at, size, Mathf.Min(r, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f), m).transform;

        private static Transform Cyl(string n, Transform p, Vector3 at, Vector3 size, Material m) =>
            Shapes.Primitive(PrimitiveType.Cylinder, n, p, at, size, m).transform;

        private static Transform Ball(string n, Transform p, Vector3 at, Vector3 size, Material m) =>
            Shapes.Primitive(PrimitiveType.Sphere, n, p, at, size, m).transform;

        /// <summary>A corner spire, <paramref name="scale"/> of full height.</summary>
        private static void Spire(Transform root, Vector3 at, float scale, Material stone, Material stoneDark, Material stoneLight, Material steel, Material copper, Material dark, Material blue, Material warm)
        {
            var s = new GameObject("Spire").transform;
            s.SetParent(root, false);
            s.position = at;
            s.localScale = new Vector3(0.8f, 0.8f * scale, 0.8f);
            Box("Base", s, new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1.9f), stoneDark, 0.1f);
            Box("BaseTrim", s, new Vector3(0f, 1.22f, 0f), new Vector3(2f, 0.1f, 2f), copper, 0.03f);
            Cyl("Shaft", s, new Vector3(0f, 2.9f, 0f), new Vector3(1.25f, 1.7f, 1.25f), stone);
            foreach (float y in new[] { 1.6f, 2.9f, 4.2f }) Cyl("Band", s, new Vector3(0f, y, 0f), new Vector3(1.32f, 0.06f, 1.32f), copper);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                foreach (float y in new[] { 2.25f, 3.55f })
                    Box("Window", s, new Vector3(Mathf.Cos(a) * 0.62f, y, Mathf.Sin(a) * 0.62f), new Vector3(0.16f, 0.36f, 0.04f), blue, 0.03f)
                        .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            Cyl("Gallery", s, new Vector3(0f, 4.7f, 0f), new Vector3(1.75f, 0.14f, 1.75f), steel);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Box("Light", s, new Vector3(Mathf.Cos(a) * 0.86f, 4.7f, Mathf.Sin(a) * 0.86f), new Vector3(0.1f, 0.08f, 0.1f), warm, 0.02f);
            }
            Cyl("Crown", s, new Vector3(0f, 5.2f, 0f), new Vector3(0.95f, 0.45f, 0.95f), stoneLight);
            Ball("Cap", s, new Vector3(0f, 5.7f, 0f), new Vector3(0.85f, 0.55f, 0.85f), steel);
            Cyl("Antenna", s, new Vector3(0f, 6.5f, 0f), new Vector3(0.07f, 0.6f, 0.07f), dark);
            Ball("Tip", s, new Vector3(0f, 7.15f, 0f), Vector3.one * 0.18f, warm);
        }
    }
}
