using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Visual platform: a thick pastel slab with a glowing edge band and a pillar underneath,
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
        }

        private TileView[,] tiles;
        private FxSystem fx;

        public static Vector3 ToWorld(GridPos p) => new Vector3(p.x, 0f, p.y);

        public void Build(GridModel grid, FxSystem fxSystem)
        {
            fx = fxSystem;
            Clear();
            BuildPlatform(grid.Width, grid.Height);

            var holeMaterial = MaterialFactory.Create(Palette.BgBottom * 0.75f, Color.black);
            tiles = new TileView[grid.Width, grid.Height];
            foreach (var p in grid.AllPositions())
            {
                var root = new GameObject($"Tile {p}");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = ToWorld(p);

                var tile = new GameObject("Tile");
                tile.transform.SetParent(root.transform, false);

                var frame = MaterialFactory.Create(Palette.TileTop, Palette.TileGlow);
                var top = MaterialFactory.Create(Palette.TileTop, Palette.TileSelfLight);
                Shapes.Rounded("Frame", tile.transform, new Vector3(0f, -0.03f, 0f), new Vector3(0.93f, 0.1f, 0.93f), 0.045f, frame);
                Shapes.Rounded("Top", tile.transform, new Vector3(0f, 0f, 0f), new Vector3(0.78f, 0.1f, 0.78f), 0.045f, top);

                var hole = Shapes.Rounded("Hole", root.transform, new Vector3(0f, -0.035f, 0f), new Vector3(0.94f, 0.02f, 0.94f), 0.01f, holeMaterial);
                hole.SetActive(false);

                tiles[p.x, p.y] = new TileView { tile = tile, hole = hole, top = top, frame = frame };
            }
        }

        private void BuildPlatform(int w, int h)
        {
            var root = new GameObject("Platform").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3((w - 1) * 0.5f, 0f, (h - 1) * 0.5f);

            var slab = MaterialFactory.Create(Palette.Slab, Color.black);
            var glow = MaterialFactory.Create(Palette.Slab, Palette.SlabEdgeGlow);
            var pillar = MaterialFactory.Create(Palette.Pillar, Color.black);

            // Upper slab: its top shows through the grooves between tiles.
            Shapes.Rounded("SlabTop", root, new Vector3(0f, -0.19f, 0f), new Vector3(w + 0.4f, 0.3f, h + 0.4f), 0.1f, slab);
            Shapes.Rounded("EdgeGlow", root, new Vector3(0f, -0.36f, 0f), new Vector3(w + 0.46f, 0.05f, h + 0.46f), 0.025f, glow);
            Shapes.Rounded("SlabBottom", root, new Vector3(0f, -0.5f, 0f), new Vector3(w + 0.42f, 0.24f, h + 0.42f), 0.1f, slab);
            Shapes.Rounded("Pillar", root, new Vector3(0f, -4.6f, 0f), new Vector3(w - 0.2f, 8f, h - 0.2f), 0.18f, pillar);
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
            if (tiles == null) return;
            var t = tiles[p.x, p.y];
            t.warning = Mathf.Max(t.warning, amount);
        }

        public void Break(GridPos p)
        {
            var t = tiles[p.x, p.y];
            fx.Burst(t.tile.transform.position, Palette.TileTop, Palette.TileGlow, 14, 3f);
            t.tile.SetActive(false);
            t.hole.SetActive(true);
        }

        public void Repair(GridPos p)
        {
            var t = tiles[p.x, p.y];
            t.tile.SetActive(true);
            t.hole.SetActive(false);
            t.pop = 0f;
        }

        /// <summary>The tile catches fire: glowing embers and rising flames until <see cref="Extinguish"/>.</summary>
        public void Ignite(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t.fire == null) t.fire = Flames.Create(t.tile.transform.parent);
            t.fire.SetActive(true);
            fx.Burst(t.tile.transform.position + Vector3.up * 0.2f, new Color(1f, 0.55f, 0.2f), new Color(2.4f, 0.9f, 0.15f), 12, 3f);
        }

        public void Extinguish(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t.fire != null) t.fire.SetActive(false);
            fx.Burst(t.tile.transform.position + Vector3.up * 0.2f, new Color(0.6f, 0.6f, 0.65f), Color.black, 8, 1.5f);
            t.pop = 0.4f;
        }

        private void LateUpdate()
        {
            if (tiles == null) return;

            foreach (var t in tiles)
            {
                if (!t.tile.activeSelf) continue;

                if (t.fire != null && t.fire.activeSelf)
                {
                    // Glowing embers that breathe with the flames.
                    float heat = 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
                    MaterialFactory.SetColors(t.top, new Color(1f, 0.45f, 0.18f), new Color(1.8f, 0.55f, 0.08f) * heat);
                    MaterialFactory.SetColors(t.frame, new Color(1f, 0.6f, 0.2f), new Color(2.4f, 0.9f, 0.15f) * heat);
                    t.warning = 0f;
                    continue;
                }

                MaterialFactory.SetColors(t.top,
                    Color.Lerp(Palette.TileTop, Palette.TileWarningTop, t.warning),
                    Palette.TileSelfLight);
                MaterialFactory.SetColors(t.frame,
                    Color.Lerp(Palette.TileTop, Palette.TileWarningTop, t.warning),
                    Color.Lerp(Palette.TileGlow, Palette.TileWarningGlow, t.warning));
                t.warning = 0f;

                if (t.pop < 1f)
                {
                    t.pop = Mathf.Min(1f, t.pop + Time.deltaTime * 4f);
                    float s = EaseOutBack(t.pop);
                    t.tile.transform.localScale = new Vector3(s, 1f, s);
                }
            }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
