using System.Collections.Generic;
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
            public Transform root;
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
            /// <summary>Fortress stone: this slab's own colour, its mortar and its faint light (else the theme's tile).</summary>
            public bool stone;
            public Color stoneTop, stoneFrame, stoneGlow;
            // The colours last sent to the materials: a tile is only re-coloured when something changed.
            public Color shownTop, shownTopGlow, shownFrame, shownFrameGlow;
            public bool shown;
            /// <summary>Still on the floor's one shared marble material (copied the first time this tile changes colour).</summary>
            public bool sharedMat;
            public Renderer topRenderer;
            /// <summary>The grout under a marble tile (hidden while the tile is broken).</summary>
            public GameObject grout;
        }

        private Material marbleTop, groutMat;
        private Transform groutRoot;
        private GameObject groutObj;

        private TileView[,] tiles;

        /// <summary>Dark floors: how lit each tile is (1 = normal, 0 = black). Warnings stay bright regardless.</summary>
        public System.Func<GridPos, float> Light;

        // The tiles to look at next frame (something about them changes); everything when a whole-floor change happens.
        private readonly HashSet<GridPos> busy = new HashSet<GridPos>();
        private readonly List<GridPos> busyNow = new List<GridPos>();
        private bool allBusy = true;
        private bool hadLight;

        private void Wake(GridPos p) => busy.Add(p);

        /// <summary>Every tile looked at again next frame (a change to the whole floor's look).</summary>
        public void WakeAll() => allBusy = true;

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
            if (ghost) foreach (var r in t.tile.GetComponentsInChildren<Renderer>(true)) r.enabled = color.HasValue; // (a hidden floor shows its tinted tiles)
            Wake(p);
        }

        private bool ghost;

        /// <summary>
        /// The floor's tiles and slab hidden: a stage that brings its own floor (the grove's rune platform) shows through.
        /// A tinted tile still shows (the duel's closing ring). Until the next floor is built.
        /// </summary>
        public void Ghost()
        {
            ghost = true;
            // everything built on the board (its slab, grout, and the fortress's ground round the floor's shape) hidden...
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            // ...but a tinted tile
            if (tiles != null)
                foreach (var t in tiles)
                    if (t != null && t.tinted) foreach (var r in t.tile.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }
        private FxSystem fx;

        /// <summary>A tile's centre in the world, standing on its step of the floor's relief (see <see cref="FloorRelief"/>).</summary>
        /// <summary>The fortress look for the floor (stone or metal slabs with mortar, no glowing bands): every core-loop floor.</summary>
        /// <summary>Obstacle tiles without their pillar (the arenas build their gun-post stairs on them).</summary>
        public static bool PlainObstacles;
        public static bool FortressTiles;
        /// <summary>The fortress stone's colour on this floor: light in the first worlds, each world its own hue, darker and darker later on.</summary>
        public static Color FortressStone = new Color(0.7f, 0.7f, 0.74f);
        /// <summary>The sci-fi worlds: the fortress floor is a deck of metal plates rather than stone.</summary>
        public static bool FortressMetal;
        /// <summary>The battle arenas: one polished marble floor instead of slabs.</summary>
        public static bool Marble;
        private static Material veinMat;

        public static Vector3 ToWorld(GridPos p) => new Vector3(p.x, FloorRelief.HeightAt(p) * FloorRelief.Step, p.y);

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
            busy.Clear();
            allBusy = true;
            var stone = new System.Random(grid.Width * 131 + grid.Height);
            veinMat = null; // this floor's own vein colour
            marbleTop = null;
            groutMat = null;
            groutRoot = null;
            // Dark fortress floors are decks of metal plates rather than stone: shared materials for their details.
            bool metal = FortressTiles && FortressMetal;
            var accent = WorldTheme.Current.accent;
            Material seamMat = null, wearMat = null, boltMat = null;
            if (metal)
            {

                seamMat = MaterialFactory.Create(accent, accent * 2.2f);
                wearMat = MaterialFactory.CreateSurface(FortressStone * 0.55f, Color.black, 0.1f, 0f);
                boltMat = MaterialFactory.CreateSurface(new Color(0.62f, 0.64f, 0.7f), Color.black, 0.75f, 0.85f);
            }
            // The flat tile pieces never cast a shadow anyone could see (they lie on the floor): only the pillars do. Thousands
            // fewer things drawn again for the shadow every frame.
            bool castWanted = Shapes.CastShadows;
            foreach (var p in grid.AllPositions())
            {
                if (!grid.Exists(p)) continue; // shaped platforms leave some cells empty
                Shapes.CastShadows = false;

                var root = new GameObject($"Tile {p}");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = ToWorld(p);

                var tile = new GameObject("Tile");
                tile.transform.SetParent(root.transform, false);

                Material frame, top;
                Color slabTop = default;
                if (FortressTiles && Marble)
                {
                    // Polished grey marble laid edge to edge: hairline seams, gentle shade changes, veins here and there,
                    // so the courtyard reads as one floor rather than squares.
                    // One slab per tile, no separate seam piece, and one material shared by the whole floor until a tile
                    // changes colour (a warning, paint): a big floor costs a few materials instead of thousands.
                    var marble = FortressStone;
                    marble.a = 1f;
                    slabTop = marble;
                    if (marbleTop == null) marbleTop = MaterialFactory.CreateSurface(marble, Palette.TileSelfLight * 0.15f, 0.78f, 0.05f);
                    frame = null;
                    top = marbleTop;
                    // Laid in blocks of four: the joints inside a block are hairlines, round each block a little wider,
                    // showing the darker grout beneath (a few tones darker than the marble, never heavy).
                    float gl = p.x % 2 == 0 ? 0.03f : 0.004f, gr = p.x % 2 == 1 ? 0.03f : 0.004f;
                    float gb = p.y % 2 == 0 ? 0.03f : 0.004f, gt = p.y % 2 == 1 ? 0.03f : 0.004f;
                    sharedTopRenderer = Shapes.Rounded("Top", tile.transform, new Vector3((gl - gr) * 0.5f, -0.005f, (gb - gt) * 0.5f),
                        new Vector3(1f - gl - gr, 0.11f, 1f - gb - gt), 0.006f, top).GetComponent<Renderer>();
                    if (groutRoot == null) { groutRoot = new GameObject("Grout").transform; groutRoot.SetParent(transform, false); }
                    if (groutMat == null) groutMat = MaterialFactory.CreateSurface(FortressStone * 0.8f, Color.black, 0.3f, 0f);
                    groutObj = Shapes.Primitive(PrimitiveType.Cube, "Grout", groutRoot, ToWorld(p) + new Vector3(0f, 0.025f, 0f), new Vector3(1f, 0.02f, 1f), groutMat);
                    groutObj.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // flat: its shadow could never show
                    if (veinMat == null) veinMat = MaterialFactory.CreateSurface(FortressStone * 0.62f, Color.black, 0.8f, 0f);
                    if (stone.Next(grid.FloorCount > 2500 ? 9 : 4) == 0) // fewer veins on the big floors (memory)
                        for (int v = 0; v < 1; v++)
                            Shapes.Rounded("Vein", tile.transform, new Vector3((float)stone.NextDouble() * 0.5f - 0.25f, 0.051f, (float)stone.NextDouble() * 0.5f - 0.25f),
                                new Vector3(0.5f + (float)stone.NextDouble() * 0.45f, 0.002f, 0.012f + (float)stone.NextDouble() * 0.012f), 0.004f, veinMat)
                                .transform.localRotation = Quaternion.Euler(0f, stone.Next(180), 0f);
                    foreach (Transform v in tile.transform) if (v.name == "Vein") v.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else if (FortressTiles)
                {
                    // Fortress stone: dark mortar between big slabs, each slab its own shade, rough and matt, a few cracked.
                    float shade = 0.86f + (float)stone.NextDouble() * 0.24f;
                    var stoneColour = FortressStone * shade;
                    stoneColour.a = 1f;
                    slabTop = stoneColour;
                    frame = MaterialFactory.CreateSurface(FortressStone * 0.42f, Color.black, metal ? 0.35f : 0.05f, metal ? 0.4f : 0f);
                    top = MaterialFactory.CreateSurface(stoneColour, Palette.TileSelfLight * 0.25f, metal ? 0.5f : 0.18f, metal ? 0.35f : 0f);
                    Shapes.Rounded("Frame", tile.transform, new Vector3(0f, -0.04f, 0f), new Vector3(0.98f, 0.1f, 0.98f), 0.02f, frame);
                    Shapes.Rounded("Top", tile.transform, new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.1f, 0.9f), 0.035f, top);
                    if (metal)
                    {
                        // A raised inner plate catches the light on its bevel; corner bolts; now and then a light strip
                        // sunk along one edge in the world's colour, and scuffs of wear.
                        int kind = stone.Next(10);
                        if (kind < 6) Shapes.Rounded("Plate", tile.transform, new Vector3(0f, 0.035f, 0f), new Vector3(0.62f, 0.05f, 0.62f), 0.02f, top); // the tile's own material: warnings still light it
                        else if (kind < 8)
                        {
                            bool alongX = stone.Next(2) == 0;
                            Shapes.Rounded("Grille", tile.transform, new Vector3(0f, 0.04f, 0f), alongX ? new Vector3(0.66f, 0.04f, 0.3f) : new Vector3(0.3f, 0.04f, 0.66f), 0.01f, top);
                            for (int i = -1; i <= 1; i++)
                                Shapes.Rounded("Slat", tile.transform, alongX ? new Vector3(0f, 0.062f, i * 0.09f) : new Vector3(i * 0.09f, 0.062f, 0f),
                                    alongX ? new Vector3(0.62f, 0.008f, 0.03f) : new Vector3(0.03f, 0.008f, 0.62f), 0.004f, frame);
                        }
                        if (kind < 6)
                            foreach (var (bx, bz) in new[] { (-1, -1), (1, 1) })
                                Shapes.Rounded("Bolt", tile.transform, new Vector3(bx * 0.36f, 0.054f, bz * 0.36f), new Vector3(0.05f, 0.02f, 0.05f), 0.02f, boltMat);
                        if (stone.Next(7) == 0)
                        {
                            int side = stone.Next(4);
                            var at = side switch { 0 => new Vector3(0f, 0.052f, 0.41f), 1 => new Vector3(0f, 0.052f, -0.41f), 2 => new Vector3(0.41f, 0.052f, 0f), _ => new Vector3(-0.41f, 0.052f, 0f) };
                            Shapes.Rounded("Seam", tile.transform, at, side < 2 ? new Vector3(0.7f, 0.01f, 0.025f) : new Vector3(0.025f, 0.01f, 0.7f), 0.005f, seamMat);
                        }
                        if (stone.Next(5) == 0)
                            Shapes.Rounded("Wear", tile.transform, new Vector3((float)stone.NextDouble() * 0.4f - 0.2f, 0.051f, (float)stone.NextDouble() * 0.4f - 0.2f),
                                new Vector3(0.18f + (float)stone.NextDouble() * 0.2f, 0.003f, 0.1f + (float)stone.NextDouble() * 0.12f), 0.04f, wearMat)
                                .transform.localRotation = Quaternion.Euler(0f, stone.Next(180), 0f);
                    }
                    else if (stone.Next(6) == 0)
                        Shapes.Rounded("Crack", tile.transform, new Vector3((float)stone.NextDouble() * 0.3f - 0.15f, 0.052f, (float)stone.NextDouble() * 0.3f - 0.15f), new Vector3(0.38f, 0.004f, 0.025f), 0.004f, frame)
                            .transform.localRotation = Quaternion.Euler(0f, stone.Next(180), 0f);
                }
                else
                {
                    frame = MaterialFactory.Create(Palette.TileTop, Palette.TileGlow);
                    top = MaterialFactory.Create(Palette.TileTop, Palette.TileSelfLight);
                    Shapes.Rounded("Frame", tile.transform, new Vector3(0f, -0.03f, 0f), new Vector3(0.93f, 0.1f, 0.93f), 0.045f, frame);
                    Shapes.Rounded("Top", tile.transform, new Vector3(0f, 0f, 0f), new Vector3(0.78f, 0.1f, 0.78f), 0.045f, top);
                }
                if (grid.IsLava(p)) tile.SetActive(false); // the fortress's burning stairs: dressed by FortressFloor
                Shapes.CastShadows = castWanted;

                if (grid.IsWall(p) && lowWalls)
                {
                    // Maze hedges: low enough that the robot never disappears behind them.
                    Shapes.Rounded("Hedge", tile.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.86f, 0.34f, 0.86f), 0.1f, obstacleBody);
                    Shapes.Rounded("Cap", tile.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.6f, 0.05f, 0.6f), 0.02f, obstacleCap);
                }
                else if (grid.IsWall(p) && !PlainObstacles)
                {
                    // A fixed obstacle: a chunky stone pillar with a glowing cap.
                    Shapes.Rounded("Obstacle", tile.transform, new Vector3(0f, 0.47f, 0f), new Vector3(0.78f, 0.84f, 0.78f), 0.12f, obstacleBody);
                    Shapes.Rounded("Cap", tile.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.56f, 0.06f, 0.56f), 0.025f, obstacleCap);
                    Shapes.Rounded("Band", tile.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.82f, 0.06f, 0.82f), 0.025f, obstacleCap);
                }

                // The hole under a broken tile is made only when a tile actually breaks (see Break): thousands of hidden
                // holes waiting on a big floor cost memory for nothing.
                holeMat = holeMaterial;
                holeRimMat = holeRim;
                bool shared = FortressTiles && Marble;
                tiles[p.x, p.y] = new TileView { tile = tile, hole = null, root = root.transform, top = top, frame = frame, stone = FortressTiles,
                    sharedMat = shared, topRenderer = shared ? sharedTopRenderer : null, shown = shared, grout = shared ? groutObj : null,
                    shownTop = slabTop, shownTopGlow = Palette.TileSelfLight * 0.15f,
                    stoneTop = slabTop, stoneFrame = FortressStone * (Marble ? 0.9f : 0.42f), stoneGlow = Palette.TileSelfLight * (Marble ? 0.15f : 0.25f) };
            }
            // The grout never moves: one merged mesh (plain cubes, light) instead of a piece per tile.
            if (groutRoot != null)
            {
                if (groutRoot.childCount < 2000) StaticMerge.Batch(groutRoot.gameObject);
                else StaticMerge.Bake(groutRoot.gameObject, _ => false); // (a huge floor; a broken tile's grout line then stays, under its pit)
            }
        }

        /// <summary>The slab and its glowing edge band, built cell by cell so they follow the platform's shape (nothing hangs below it).</summary>
        private void BuildPlatform(GridModel grid)
        {
            var root = new GameObject("Platform").transform;
            root.SetParent(transform, false);

            var slab = MaterialFactory.Create(Palette.Slab, Color.black);
            var glow = MaterialFactory.Create(Palette.Slab, Palette.SlabEdgeGlow);

            bool fortress = FortressTiles;
            if (fortress) glow = slab = MaterialFactory.Create(Color.Lerp(Palette.Slab, FortressStone * 0.5f, 0.6f), Color.black); // plain stone in the floor's shade, no glowing band
            int pieces = 0;
            foreach (var p in grid.AllPositions())
            {
                if (!grid.Exists(p) || grid.IsLava(p)) continue;
                var at = ToWorld(p);
                int steps = FloorRelief.HeightAt(p);
                // Only the platform's rim is ever seen from the side: a tile with every neighbour standing at least as
                // high needs no slab or riser under it (on a 6-stage floor that is tens of thousands of pieces saved).
                int lowest = steps;
                bool edge = false;
                foreach (var d in DirectionExtensions.All)
                {
                    var n = p + d.ToOffset();
                    if (!grid.Exists(n) || grid.IsLava(n)) { edge = true; lowest = -1; break; }
                    int hn = FloorRelief.HeightAt(n);
                    if (hn < steps) { edge = true; lowest = Mathf.Min(lowest, hn); }
                }
                if (!edge && fortress && Marble) continue; // under a seamless marble floor the inner slabs are never seen
                Shapes.Rounded("SlabTop", root, at + new Vector3(0f, -0.19f, 0f), new Vector3(1.16f, 0.3f, 1.16f), 0.06f, slab);
                pieces++;
                if (!edge) continue;
                // Slightly oversized pieces overlap their neighbours, so only the outer outline shows.
                Shapes.Rounded("EdgeGlow", root, at + new Vector3(0f, -0.36f, 0f), new Vector3(1.2f, 0.05f, 1.2f), 0.025f, glow);
                Shapes.Rounded("SlabBottom", root, at + new Vector3(0f, -0.5f, 0f), new Vector3(1.17f, 0.24f, 1.17f), 0.06f, slab);
                pieces += 2;
                // A raised tile on the rim stands on a block of the slab down to the base, a glowing band at each of its
                // visible steps (at most four).
                if (steps > 0)
                {
                    float top = at.y - 0.5f, bottom = -0.6f;
                    Shapes.Rounded("Riser", root, new Vector3(at.x, (top + bottom) * 0.5f, at.z), new Vector3(1.1f, top - bottom, 1.1f), 0.05f, slab);
                    pieces++;
                    for (int s = Mathf.Max(0, Mathf.Max(lowest, steps - 4)); s < steps; s++, pieces++)
                        Shapes.Rounded("StepGlow", root, new Vector3(at.x, s * FloorRelief.Step - 0.36f, at.z), new Vector3(1.13f, 0.04f, 1.13f), 0.02f, glow);
                }
            }
            // The platform never moves: merge it into a few big meshes, so a 20x20 floor costs a handful of draw calls.
            // Under the floor and round its rim: its shadows fall into the void, never on anything seen (a shadow pass saved).
            foreach (var pr in root.GetComponentsInChildren<MeshRenderer>()) pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (pieces < 2000) StaticMerge.Batch(root.gameObject);
            else StaticMerge.Bake(root.gameObject, _ => false); // (a huge floor: thousands of renderers would cost every frame)
        }
        private Renderer sharedTopRenderer;

        /// <summary>A tile about to change colour gets its own copy of the shared marble.</summary>
        private static void OwnMaterial(TileView t)
        {
            if (!t.sharedMat || t.topRenderer == null) return;
            t.top = new Material(t.top);
            t.topRenderer.sharedMaterial = t.top;
            t.sharedMat = false;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            tiles = null;
            ghost = false;
            busy.Clear();
            allBusy = true;
        }

        /// <summary>Called every frame by hazards; 0 = calm, 1 = full red.</summary>
        public void SetWarning(GridPos p, float amount)
        {
            if (tiles == null || tiles[p.x, p.y] == null) return;
            var t = tiles[p.x, p.y];
            t.warning = Mathf.Max(t.warning, amount);
            if (amount > 0f) Wake(p);
        }

        public void Break(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t == null) return;
            fx.Burst(t.tile.transform.position, Palette.TileTop, Palette.TileGlow, 14, 3f);
            t.tile.SetActive(false);
            if (t.grout != null) t.grout.SetActive(false);
            if (t.hole == null) t.hole = MakeHole(t.root);
            t.hole.SetActive(true);
        }

        private Material holeMat, holeRimMat;

        /// <summary>A black pit with a glowing rim, under a broken tile.</summary>
        private GameObject MakeHole(Transform root)
        {
            var hole = Shapes.Rounded("Hole", root, new Vector3(0f, -0.27f, 0f), new Vector3(0.9f, 0.5f, 0.9f), 0.02f, holeMat);
            foreach (var (o, s) in new[] { (new Vector3(0f, 0.27f, 0.44f), new Vector3(0.94f, 0.05f, 0.06f)), (new Vector3(0f, 0.27f, -0.44f), new Vector3(0.94f, 0.05f, 0.06f)),
                (new Vector3(0.44f, 0.27f, 0f), new Vector3(0.06f, 0.05f, 0.94f)), (new Vector3(-0.44f, 0.27f, 0f), new Vector3(0.06f, 0.05f, 0.94f)) })
                Shapes.Rounded("Rim", hole.transform, o, s, 0.01f, holeRimMat);
            return hole;
        }

        public void Repair(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t == null) return;
            t.tile.SetActive(true);
            if (t.grout != null) t.grout.SetActive(true);
            if (t.hole != null) t.hole.SetActive(false);
            t.pop = 0f;
            Wake(p);
        }

        /// <summary>The robot lands (or a block slams down): the tile dips and springs back.</summary>
        public void Bounce(GridPos p, float strength = 1f)
        {
            if (tiles == null || tiles[p.x, p.y] == null) return;
            var t = tiles[p.x, p.y];
            t.bounce = 0f;
            t.bounceStrength = strength;
            Wake(p);
        }

        /// <summary>Paint missions: the tile floods with the world's accent color in a little splash.</summary>
        public void Paint(GridPos p)
        {
            if (tiles == null || tiles[p.x, p.y] == null) return;
            var t = tiles[p.x, p.y];
            if (t.painted) return;
            t.painted = true;
            t.paintPop = 0f;
            Wake(p);
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
            Wake(p);
            fx.Dust(t.tile.transform.position + Vector3.up * 0.1f, new Color(0.6f, 0.6f, 0.65f), 8, 1.5f);
        }

        /// <summary>The tile catches fire: glowing embers and rising flames until <see cref="Extinguish"/>.</summary>
        public void Ignite(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t == null) return;
            if (t.fire == null) t.fire = Flames.Create(t.tile.transform.parent);
            t.fire.SetActive(true);
            Wake(p);
            fx.Burst(t.tile.transform.position + Vector3.up * 0.2f, new Color(1f, 0.55f, 0.2f), new Color(2.4f, 0.9f, 0.15f), 12, 3f);
        }

        public void Extinguish(GridPos p)
        {
            var t = tiles[p.x, p.y];
            if (t == null) return;
            if (t.fire != null) t.fire.SetActive(false);
            fx.Burst(t.tile.transform.position + Vector3.up * 0.2f, new Color(0.6f, 0.6f, 0.65f), Color.black, 8, 1.5f);
            t.pop = 0.4f;
            Wake(p);
        }

        private void LateUpdate()
        {
            if (tiles == null) return;
            bool lightNow = Light != null;
            if (lightNow || hadLight) allBusy = true; // (a dark floor's light moves every frame; and once more when it goes)
            hadLight = lightNow;
            busyNow.Clear();
            if (allBusy)
            {
                for (int x = 0; x < tiles.GetLength(0); x++)
                    for (int y = 0; y < tiles.GetLength(1); y++) busyNow.Add(new GridPos(x, y));
                allBusy = false;
            }
            else busyNow.AddRange(busy);
            busy.Clear();

            foreach (var cell in busyNow)
            {
                int gx = cell.x, gy = cell.y;
                var t = tiles[gx, gy];
                if (t == null || !t.tile.activeSelf) continue;
                // still changing next frame: looked at again (a fire breathes, a dip or pop or splash plays, a warning shows)
                if ((t.fire != null && t.fire.activeSelf) || t.warning > 0f || t.bounce < 1f || t.pop < 1f || (t.painted && t.paintPop < 1f)) busy.Add(cell);
                else if (!t.shown) busy.Add(cell);
                float lit = Light == null ? 1f : Mathf.Clamp01(Light(new GridPos(gx, gy)));

                if (t.fire != null && t.fire.activeSelf)
                {
                    // Glowing embers that breathe with the flames.
                    float heat = 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
                    OwnMaterial(t);
                    MaterialFactory.SetColors(t.top, new Color(1f, 0.45f, 0.18f), new Color(1.8f, 0.55f, 0.08f) * heat);
                    if (t.frame != null) MaterialFactory.SetColors(t.frame, new Color(1f, 0.6f, 0.2f), new Color(2.4f, 0.9f, 0.15f) * heat);
                    t.warning = 0f;
                    t.shown = false;
                    continue;
                }

                var topColor = t.tinted ? t.tint : t.stone ? t.stoneTop : Palette.TileTop;
                var topGlow = t.tinted ? t.tintGlow : t.stone ? t.stoneGlow : Palette.TileSelfLight;
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
                var frameColor = t.painted ? paintColor : (t.stone ? t.stoneFrame : Palette.TileTop) * dim;
                var frameGlow = t.painted ? paintColor * 1.6f : t.stone ? Color.black : Palette.TileGlow * (dim * dim);
                var newFrame = Color.Lerp(frameColor, Palette.TileWarningTop, t.warning);
                var newFrameGlow = Color.Lerp(frameGlow, Palette.TileWarningGlow * 1.3f, t.warning);
                if (t.frame == null) { newFrame = t.shownFrame; newFrameGlow = t.shownFrameGlow; } // no seam piece on marble
                // Big floors have hundreds of tiles: only touch the materials of the ones whose colour changed.
                if (!t.shown || newTop != t.shownTop || newTopGlow != t.shownTopGlow || newFrame != t.shownFrame || newFrameGlow != t.shownFrameGlow)
                {
                    OwnMaterial(t);
                    MaterialFactory.SetColors(t.top, newTop, newTopGlow);
                    if (t.frame != null) MaterialFactory.SetColors(t.frame, newFrame, newFrameGlow);
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
        public void SetPaintColor(Color color) { paintColor = color; allBusy = true; }

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
