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
            public bool tinted;
            public Color tint, tintGlow;
            // The colours last sent to the materials: a tile is only re-coloured when something changed.
            public Color shownTop, shownTopGlow, shownFrame, shownFrameGlow;
            public bool shown;
        }

        private TileView[,] tiles;

        /// <summary>Dark floors: how lit each tile is (1 = normal, 0 = black). Warnings stay bright regardless.</summary>
        public System.Func<GridPos, float> Light;

        /// <summary>The tile's own transform (null for empty cells): floor rules hang their markings on it.</summary>
        public Transform Surface(GridPos p) => tiles != null && InRange(p) && tiles[p.x, p.y] != null ? tiles[p.x, p.y].tile.transform : null;

        private bool InRange(GridPos p) => p.x >= 0 && p.y >= 0 && p.x < tiles.GetLength(0) && p.y < tiles.GetLength(1);

        /// <summary>Special tiles (ice, candy, glass, poison...) wear their own colour; null restores the normal look.</summary>
        public void SetTint(GridPos p, Color? color, Color glow = default)
        {
            if (tiles == null || !InRange(p) || tiles[p.x, p.y] == null) return;
            var t = tiles[p.x, p.y];
            t.tinted = color.HasValue;
            if (color.HasValue) { t.tint = color.Value; t.tintGlow = glow; }
        }
        private FxSystem fx;

        public static Vector3 ToWorld(GridPos p) => new Vector3(p.x, 0f, p.y);

        public void Build(GridModel grid, FxSystem fxSystem, bool lowWalls = false)
        {
            fx = fxSystem;
            Clear();
            BuildPlatform(grid);

            // A hole is a black pit with a glowing hazard-orange rim: unmistakable on light and dark floors alike.
            var holeMaterial = MaterialFactory.Create(new Color(0.02f, 0.02f, 0.04f), Color.black);
            var holeRim = MaterialFactory.Create(new Color(1f, 0.4f, 0.2f), new Color(2.6f, 0.75f, 0.2f));
            paintColor = PaintFor(WorldTheme.Current);
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

                if (grid.IsWall(p) && lowWalls)
                {
                    // Maze hedges: low enough that the robot never disappears behind them.
                    Shapes.Rounded("Hedge", tile.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.86f, 0.34f, 0.86f), 0.1f, obstacleBody);
                    Shapes.Rounded("Cap", tile.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.6f, 0.05f, 0.6f), 0.02f, obstacleCap);
                }
                else if (grid.IsWall(p))
                {
                    // A fixed obstacle: a chunky stone pillar with a glowing cap.
                    Shapes.Rounded("Obstacle", tile.transform, new Vector3(0f, 0.47f, 0f), new Vector3(0.78f, 0.84f, 0.78f), 0.12f, obstacleBody);
                    Shapes.Rounded("Cap", tile.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.56f, 0.06f, 0.56f), 0.025f, obstacleCap);
                    Shapes.Rounded("Band", tile.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.82f, 0.06f, 0.82f), 0.025f, obstacleCap);
                }

                var hole = Shapes.Rounded("Hole", root.transform, new Vector3(0f, -0.27f, 0f), new Vector3(0.9f, 0.5f, 0.9f), 0.02f, holeMaterial);
                foreach (var (o, s) in new[] { (new Vector3(0f, 0.27f, 0.44f), new Vector3(0.94f, 0.05f, 0.06f)), (new Vector3(0f, 0.27f, -0.44f), new Vector3(0.94f, 0.05f, 0.06f)),
                    (new Vector3(0.44f, 0.27f, 0f), new Vector3(0.06f, 0.05f, 0.94f)), (new Vector3(-0.44f, 0.27f, 0f), new Vector3(0.06f, 0.05f, 0.94f)) })
                    Shapes.Rounded("Rim", hole.transform, o, s, 0.01f, holeRim);
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
            // The platform never moves: merge it into a few big meshes, so a 20x20 floor costs a handful of draw calls.
            StaticBatchingUtility.Combine(root.gameObject);
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

        /// <summary>A Silgi-bot rolled over the tile: the paint is gone, back to grey.</summary>
        public void Unpaint(GridPos p)
        {
            if (tiles == null || tiles[p.x, p.y] == null) return;
            var t = tiles[p.x, p.y];
            if (!t.painted) return;
            t.painted = false;
            t.paintPop = 1f;
            fx.Dust(t.tile.transform.position + Vector3.up * 0.1f, new Color(0.6f, 0.6f, 0.65f), 8, 1.5f);
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

            for (int gx = 0; gx < tiles.GetLength(0); gx++)
            for (int gy = 0; gy < tiles.GetLength(1); gy++)
            {
                var t = tiles[gx, gy];
                if (t == null || !t.tile.activeSelf) continue;
                float lit = Light == null ? 1f : Mathf.Clamp01(Light(new GridPos(gx, gy)));

                if (t.fire != null && t.fire.activeSelf)
                {
                    // Glowing embers that breathe with the flames.
                    float heat = 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
                    MaterialFactory.SetColors(t.top, new Color(1f, 0.45f, 0.18f), new Color(1.8f, 0.55f, 0.08f) * heat);
                    MaterialFactory.SetColors(t.frame, new Color(1f, 0.6f, 0.2f), new Color(2.4f, 0.9f, 0.15f) * heat);
                    t.warning = 0f;
                    t.shown = false;
                    continue;
                }

                var topColor = t.tinted ? t.tint : Palette.TileTop;
                var topGlow = t.tinted ? t.tintGlow : Palette.TileSelfLight;
                if (t.painted)
                {
                    // Painted tiles glow in the accent color; a fresh coat flashes brighter for a moment.
                    if (t.paintPop < 1f) t.paintPop = Mathf.Min(1f, t.paintPop + Time.deltaTime * 3f);
                    float flash = 1f - t.paintPop;
                    // A vivid coat in a colour picked to stand apart from this world's tiles, glowing on light and dark floors alike.
                    topColor = paintColor;
                    topGlow = paintColor * (0.7f + flash * 1.4f);
                }
                // Darkness dims the tile itself; a warning still shows at full strength.
                float dim = Mathf.Lerp(0.07f, 1f, lit);
                var newTop = Color.Lerp(topColor * dim, Palette.TileWarningTop, t.warning);
                var newTopGlow = Color.Lerp(topGlow * dim, Color.black, t.warning);
                var frameColor = t.painted ? paintColor : Palette.TileTop * dim;
                var frameGlow = t.painted ? paintColor * 1.6f : Palette.TileGlow * (dim * dim);
                var newFrame = Color.Lerp(frameColor, Palette.TileWarningTop, t.warning);
                var newFrameGlow = Color.Lerp(frameGlow, Palette.TileWarningGlow * 1.3f, t.warning);
                // Big floors have hundreds of tiles: only touch the materials of the ones whose colour changed.
                if (!t.shown || newTop != t.shownTop || newTopGlow != t.shownTopGlow || newFrame != t.shownFrame || newFrameGlow != t.shownFrameGlow)
                {
                    MaterialFactory.SetColors(t.top, newTop, newTopGlow);
                    MaterialFactory.SetColors(t.frame, newFrame, newFrameGlow);
                    t.shownTop = newTop; t.shownTopGlow = newTopGlow; t.shownFrame = newFrame; t.shownFrameGlow = newFrameGlow;
                    t.shown = true;
                }
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

        private Color paintColor = Color.magenta;

        /// <summary>Paints with <paramref name="color"/> instead of the floor's own paint (the finale's gold).</summary>
        public void SetPaintColor(Color color) => paintColor = color;

        /// <summary>
        /// The paint for a world: of a few vivid colours, the one whose hue is furthest from the tiles and their glow, so a
        /// painted tile never looks like a plain one.
        /// </summary>
        private static Color PaintFor(WorldTheme theme)
        {
            var options = new[] { new Color(0.45f, 1f, 0.3f), new Color(1f, 0.3f, 0.85f), new Color(1f, 0.78f, 0.2f), new Color(0.25f, 0.9f, 1f), new Color(1f, 0.45f, 0.3f) };
            Color.RGBToHSV(theme.tileTop, out float h1, out float s1, out _);
            Color.RGBToHSV(theme.tileGlow, out float h2, out _, out _);
            Color.RGBToHSV(theme.accent, out float h3, out _, out _);
            float HueGap(float a, float b) { float d = Mathf.Abs(a - b); return Mathf.Min(d, 1f - d); }
            Color best = options[0];
            float bestScore = -1f;
            foreach (var c in options)
            {
                Color.RGBToHSV(c, out float h, out _, out _);
                // Pale tiles have little hue of their own, so mostly avoid the glow and accent colours there.
                float score = Mathf.Min(HueGap(h, h2), HueGap(h, h3)) + (s1 > 0.15f ? HueGap(h, h1) : 0.25f);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
