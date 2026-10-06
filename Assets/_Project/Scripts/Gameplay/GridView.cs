using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Visual platform (any shape the layout gives): a thick pastel slab with a glowing edge band and a pillar underneath,
    /// topped by tiles that each have a soft cyan inner frame. The walkable surface is at y = 0.05.
    /// </summary>
    public class GridView : MonoBehaviour
    {
        public const float SurfaceY = 0.05f;

        private class TileView
        {
            public GameObject tile;
            public GameObject hole;
            public Material top;
            public Material frame;
            public float warning;
            public float pop = 1f;
            public GameObject fire;
            public bool painted;
            public float paintPop = 1f;
            public float bounce = 1f;
            public float bounceStrength = 1f;
        }

        private TileView[,] tiles;
        private FxSystem fx;

        public static Vector3 ToWorld(GridPos p) => new Vector3(p.x, 0f, p.y);

        public void Build(GridModel grid, FxSystem fxSystem)
        {
            fx = fxSystem;
            Clear();
            BuildPlatform(grid);

            var holeMaterial = MaterialFactory.Create(Palette.BgBottom * 0.75f, Color.black);
            var obstacleBody = MaterialFactory.Create(Color.Lerp(Palette.Pillar, new Color(0.12f, 0.1f, 0.2f), 0.45f), Color.black);
            var obstacleCap = MaterialFactory.Create(WorldTheme.Current.accent, WorldTheme.Current.accent * 1.2f);

            tiles = new TileView[grid.Width, grid.Height];
            foreach (var p in grid.AllPositions())
            {
                if (!grid.Exists(p)) continue; // shaped platforms leave some cells empty

                var root = new GameObject($"Tile {p}");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = ToWorld(p);

                var tile = new GameObject("Tile");
                tile.transform.SetParent(root.transform, false);

                var frame = MaterialFactory.Create(Palette.TileTop, Palette.TileGlow);
                var top = MaterialFactory.Create(Palette.TileTop, Palette.TileSelfLight);
                Shapes.Rounded("Frame", tile.transform, new Vector3(0f, -0.03f, 0f), new Vector3(0.93f, 0.1f, 0.93f), 0.045f, frame);
                Shapes.Rounded("Top", tile.transform, new Vector3(0f, 0f, 0f), new Vector3(0.78f, 0.1f, 0.78f), 0.045f, top);

                if (grid.IsWall(p))
                {
                    // A fixed obstacle: a chunky stone pillar with a glowing cap.
                    Shapes.Rounded("Obstacle", tile.transform, new Vector3(0f, 0.47f, 0f), new Vector3(0.78f, 0.84f, 0.78f), 0.12f, obstacleBody);
                    Shapes.Rounded("Cap", tile.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.56f, 0.06f, 0.56f), 0.025f, obstacleCap);
                    Shapes.Rounded("Band", tile.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.82f, 0.06f, 0.82f), 0.025f, obstacleCap);
                }

                var hole = Shapes.Rounded("Hole", root.transform, new Vector3(0f, -0.035f, 0f), new Vector3(0.94f, 0.02f, 0.94f), 0.01f, holeMaterial);
                hole.SetActive(false);

                tiles[p.x, p.y] = new TileView { tile = tile, hole = hole, top = top, frame = frame };
            }
        }

        /// <summary>The slab, glowing edge band and pillar, built cell by cell so they follow the platform's shape.</summary>
        private void BuildPlatform(GridModel grid)
        {
            var root = new GameObject("Platform").transform;
            root.SetParent(transform, false);

            var slab = MaterialFactory.Create(Palette.Slab, Color.black);
            var glow = MaterialFactory.Create(Palette.Slab, Palette.SlabEdgeGlow);
            var pillar = MaterialFactory.Create(Palette.Pillar, Color.black);

            foreach (var p in grid.AllPositions())
            {
                if (!grid.Exists(p)) continue;
                var at = ToWorld(p);
                // Slightly oversized pieces overlap their neighbours, so only the outer outline shows.
                Shapes.Rounded("SlabTop", root, at + new Vector3(0f, -0.19f, 0f), new Vector3(1.16f, 0.3f, 1.16f), 0.06f, slab);
                Shapes.Rounded("EdgeGlow", root, at + new Vector3(0f, -0.36f, 0f), new Vector3(1.2f, 0.05f, 1.2f), 0.025f, glow);
                Shapes.Rounded("SlabBottom", root, at + new Vector3(0f, -0.5f, 0f), new Vector3(1.17f, 0.24f, 1.17f), 0.06f, slab);
                Shapes.Rounded("Pillar", root, at + new Vector3(0f, -4.6f, 0f), new Vector3(1.0f, 8f, 1.0f), 0.04f, pillar);
            }
        }
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            tiles = null;
        }

        /// <summary>Called every frame by hazards; 0 = calm, 1 = full red.</summary>
        public void SetWarning(GridPos p, float amount)
        {
            if (tiles == null || tiles[p.x, p.y] == null) return;
            var t = tiles[p.x, p.y];
            t.warning = Mathf.Max(t.warning, amount);
        }

        public void Break(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t == null) return;
            fx.Burst(t.tile.transform.position, Palette.TileTop, Palette.TileGlow, 14, 3f);
            t.tile.SetActive(false);
            t.hole.SetActive(true);
        }

        public void Repair(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t == null) return;
            t.tile.SetActive(true);
            t.hole.SetActive(false);
            t.pop = 0f;
        }

        /// <summary>The robot lands (or a block slams down): the tile dips and springs back.</summary>
        public void Bounce(GridPos p, float strength = 1f)
        {
            if (tiles == null || tiles[p.x, p.y] == null) return;
            var t = tiles[p.x, p.y];
            t.bounce = 0f;
            t.bounceStrength = strength;
        }

        /// <summary>Paint missions: the tile floods with the world's accent color in a little splash.</summary>
        public void Paint(GridPos p)
        {
            if (tiles == null || tiles[p.x, p.y] == null) return;
            var t = tiles[p.x, p.y];
            if (t.painted) return;
            t.painted = true;
            t.paintPop = 0f;
            var accent = WorldTheme.Current.accent;
            fx.Burst(t.tile.transform.position + Vector3.up * 0.1f, accent, accent * 1.6f, 10, 2.2f);
        }

        /// <summary>The tile catches fire: glowing embers and rising flames until <see cref="Extinguish"/>.</summary>
        public void Ignite(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t == null) return;
            if (t.fire == null) t.fire = Flames.Create(t.tile.transform.parent);
            t.fire.SetActive(true);
            fx.Burst(t.tile.transform.position + Vector3.up * 0.2f, new Color(1f, 0.55f, 0.2f), new Color(2.4f, 0.9f, 0.15f), 12, 3f);
        }

        public void Extinguish(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t == null) return;
            if (t.fire != null) t.fire.SetActive(false);
            fx.Burst(t.tile.transform.position + Vector3.up * 0.2f, new Color(0.6f, 0.6f, 0.65f), Color.black, 8, 1.5f);
            t.pop = 0.4f;
        }

        private void LateUpdate()
        {
            if (tiles == null) return;

            foreach (var t in tiles)
            {
                if (t == null || !t.tile.activeSelf) continue;

                if (t.fire != null && t.fire.activeSelf)
                {
                    // Glowing embers that breathe with the flames.
                    float heat = 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
                    MaterialFactory.SetColors(t.top, new Color(1f, 0.45f, 0.18f), new Color(1.8f, 0.55f, 0.08f) * heat);
                    MaterialFactory.SetColors(t.frame, new Color(1f, 0.6f, 0.2f), new Color(2.4f, 0.9f, 0.15f) * heat);
                    t.warning = 0f;
                    continue;
                }

                var topColor = Palette.TileTop;
                var topGlow = Palette.TileSelfLight;
                if (t.painted)
                {
                    // Painted tiles glow in the accent color; a fresh coat flashes brighter for a moment.
                    if (t.paintPop < 1f) t.paintPop = Mathf.Min(1f, t.paintPop + Time.deltaTime * 3f);
                    var accent = WorldTheme.Current.accent;
                    float flash = 1f - t.paintPop;
                    // A saturated, glowing coat that reads clearly on light and dark worlds alike.
                    topColor = Paint(accent);
                    topGlow = Paint(accent) * (0.45f + flash * 1.4f);
                }
                MaterialFactory.SetColors(t.top,
                    Color.Lerp(topColor, Palette.TileWarningTop, t.warning),
                    Color.Lerp(topGlow, Color.black, t.warning));
                MaterialFactory.SetColors(t.frame,
                    Color.Lerp(Palette.TileTop, Palette.TileWarningTop, t.warning),
                    Color.Lerp(Palette.TileGlow, Palette.TileWarningGlow, t.warning));
                t.warning = 0f;

                if (t.bounce < 1f)
                {
                    // A quick dip and a springy rebound.
                    t.bounce = Mathf.Min(1f, t.bounce + Time.deltaTime / 0.32f);
                    float dip = Mathf.Sin(t.bounce * Mathf.PI * 2.2f) * (1f - t.bounce) * 0.07f * t.bounceStrength;
                    t.tile.transform.localPosition = new Vector3(0f, -dip, 0f);
                }

                if (t.paintPop < 1f && t.painted)
                {
                    float s = 1f + Mathf.Sin(t.paintPop * Mathf.PI) * 0.08f;
                    t.tile.transform.localScale = new Vector3(s, 1f, s);
                }

                if (t.pop < 1f)
                {
                    t.pop = Mathf.Min(1f, t.pop + Time.deltaTime * 4f);
                    float s = EaseOutBack(t.pop);
                    t.tile.transform.localScale = new Vector3(s, 1f, s);
                }
            }
        }

        /// <summary>The world accent pushed to a vivid paint color.</summary>
        private static Color Paint(Color accent)
        {
            Color.RGBToHSV(accent, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Max(0.75f, s), Mathf.Clamp01(v * 0.95f));
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
