using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The open world (main menu → ARENAS): the battle arenas strung on long mountain roads. A road (see TrailMap)
    /// leads from one arena to the next, winding through its world's scenery, with bands of robots at its clearings and
    /// chests guarded by poison spitters (beat the guardians, the chest opens and its gifts scatter). At its end stands
    /// the next arena's gate: open, walk in and the battle starts; locked, a sign says which level opens it. A won
    /// arena opens its exit — a pillar of light — onto the road to the next one. The menu button picks up where the
    /// player left off: on the road out of the last arena won.
    /// </summary>
    public partial class GameManager
    {
        private bool trailRun;
        private int trailTarget;
        private GridPos trailGate;
        private Transform trailRoot;
        private bool trailWired, trailEntering;
        private float trailSignAt;

        private class TrailChest
        {
            public int id;
            public GridPos tile;
            public Transform lid;
            public bool open;
            public float openT;
        }

        private readonly List<TrailChest> trailChests = new List<TrailChest>();
        // The gate's portal (part, how it moves: 0 the pulsing core, ±1 a turning ring, 10+ a spark drawn inwards) and the chests' beams.
        private readonly List<(Transform t, int kind)> portalParts = new List<(Transform, int)>();
        private readonly List<Transform> chestGlows = new List<Transform>();
        private readonly List<(GridPos tile, Transform body, int kind)> trailGifts = new List<(GridPos, Transform, int)>();
        private readonly List<(GridPos tile, float until, Transform body)> puddles = new List<(GridPos, float, Transform)>();
        private float puddleHurtAt;

        // The exit of a won arena.
        private Transform arenaExit;
        private GridPos arenaExitTile;

        // ---------- Allies (see HuntSystemAllies) ----------

        private const int MaxAllies = 24;
        private const string AlliesKey = "sb_allies";
        private int AllyHp => 3 + arenaIndex / 3;

        /// <summary>The allies foi has with it (carried from road to arena and on, kept between sessions).</summary>
        private int AlliesKept => PlayerPrefs.GetInt(AlliesKey, 0);

        private void KeepAllies(int n)
        {
            if (n != AlliesKept) PlayerPrefs.SetInt(AlliesKey, Mathf.Clamp(n, 0, MaxAllies));
        }

        /// <summary>A new road or arena: the allies foi brought along stand round it.</summary>
        private void SpawnKeptAllies()
        {
            hunt.AllyDamage = 1 + arenaIndex / 6;
            hunt.AllyPace = 1.6f; // allies swing a little slower: foi gets its share of the fight
            int n = Mathf.Min(MaxAllies, AlliesKept);
            for (int i = 0; i < n; i++) hunt.AddAlly(robot.Position, AllyHp);
        }

        /// <summary>Each frame of a fight: the allies still standing are kept.</summary>
        private void TrackAllies()
        {
            if (State == GameState.Playing && arenaRun && !arenaGiftsPhase) KeepAllies(hunt.AlliesAlive);
        }

        /// <summary>Where the open world picks up: the road out of the last arena won (or the first road).</summary>
        private int TrailResumeTarget()
        {
            int last = -1;
            for (int i = 0; i < BattleArenas.Count; i++) if (BattleArenas.Wins(i) > 0) last = i;
            return Mathf.Clamp(last + 1, 0, BattleArenas.Count - 1);
        }

        /// <summary>A road to arena <paramref name="target"/>: built in its world's look, foi at its foot.</summary>
        private void StartTrail(int target)
        {
            KeepAllies(0); // a new road: new allies from its own chests (the last ones stay behind)
            ui.Arenas.Hide();
            bonusRun = true;
            dailyRun = false;
            arenaRun = true;
            trailRun = true;
            trailEntering = false;
            trailTarget = Mathf.Clamp(target, 0, BattleArenas.Count - 1);
            arenaIndex = trailTarget;
            arenaBestStreak = 0;
            arenaGiftsPhase = arenaGiftsDone = false;
            levelIndex = BattleArenas.ThemeLevel(trailTarget);
            level = BuildTrailLevel(trailTarget);
            BeginRun();
            EnterTps();
            SetupTrail();
            hunt.AllyDamage = 1 + arenaIndex / 6;
            ui.ShowIntro(Loc.F("trail.title", Loc.T(BattleArenas.NameKey(trailTarget))), Loc.T("trail.intro"));
            AudioManager.PlaySfx(Sfx.Win, 0.5f, 1.1f);
            RefreshHud();
            ui.SkillBar.Refresh(true);
        }

        private LevelData BuildTrailLevel(int target)
        {
            var l = levelSet.levels[levelIndex].Clone();
            l.layout = TrailMap.Layout(target, out var bounds);
            l.stageBounds = bounds;
            l.fortressStages = 1;
            l.gridWidth = l.layout[0].Length;
            l.gridHeight = l.layout.Length;
            l.terrain = TerrainKind.Flat;
            l.hard = true;
            l.marathon = false;
            l.final = false;
            l.mission = MissionType.Hunt;
            l.number = levelIndex + 1;
            float d = target / (float)(BattleArenas.Count - 1);
            l.score = Mathf.RoundToInt(Mathf.Lerp(20f, 80f, d));
            l.robots = 0; // the bands are placed by the road (see SetupTrail)
            l.robotsAtOnce = 0;
            l.elites = 0;
            l.hardAtOnce = 1; // street pathfinding round the bends
            l.brutes = 0;
            l.towers = 0;
            l.bugs = 8 + target / 2;
            l.robotHp = Mathf.RoundToInt(Mathf.Lerp(1f, 4f, d));
            l.robotStep = Mathf.Lerp(1.0f, 0.6f, d);
            l.monsterHp = 1;
            Calm(l);
            return l;
        }

        /// <summary>The battle arenas and their roads are a plain fight: no crates from the sky, no holes, no floor tricks.</summary>
        private static void Calm(LevelData l)
        {
            l.rules = FloorRule.None;
            l.breakTiles = false;
            l.bombChance = 0f;
            l.fireChance = 0f;
            l.lineWaveChance = 0f;
            l.blocksPerWave = 0;
            l.sweepers = l.erasers = l.drones = l.sandworms = l.crabs = l.turrets = l.penguins = l.springbots = 0;
        }

        /// <summary>The road's people and things: robot bands, chests and their guardians, the gate, the scenery.</summary>
        private void SetupTrail()
        {
            ClearTrail();
            if (!trailWired)
            {
                trailWired = true;
                hunt.Poisoned += OnPoisoned;
                hunt.ChestFreed += OnChestFreed;
            }
            trailRoot = new GameObject("Trail").transform;
            var look = BattleArenas.Style(trailTarget);
            int chestId = 0;
            var rng = new System.Random(310 + trailTarget * 7);
            foreach (var c in TrailMap.Clearings)
            {
                var centre = new GridPos(c.x, c.y);
                if (c.kind == TrailMap.ClearingKind.Chest)
                {
                    var chest = new TrailChest { id = chestId, tile = centre };
                    chest.lid = BuildChest(centre, look.accent);
                    grid.SetOccupied(centre, true);
                    trailChests.Add(chest);
                    // Its guards: poison spitters, a ring of robots and a vanG — all of them tougher road by road.
                    int guardians = 2 + (trailTarget >= 8 ? 1 : 0) + (trailTarget >= 18 ? 1 : 0);
                    for (int i = 0; i < guardians; i++)
                    {
                        float a = i * Mathf.PI * 2f / guardians + 0.4f;
                        var p = centre + new GridPos(Mathf.RoundToInt(Mathf.Cos(a) * 2f), Mathf.RoundToInt(Mathf.Sin(a) * 2f));
                        hunt.AddTrailGuard(p, true, centre, chestId);
                    }
                    int ring = 3 + trailTarget / 4, rank = Mathf.Min(4, 1 + trailTarget / 5);
                    for (int i = 0; i < ring; i++)
                    {
                        float a = i * Mathf.PI * 2f / ring;
                        var p = centre + new GridPos(Mathf.RoundToInt(Mathf.Cos(a) * 3.4f), Mathf.RoundToInt(Mathf.Sin(a) * 3.4f));
                        hunt.AddTrailGuard(p, false, centre, chestId, rank);
                    }
                    hunt.AddChestBoss(centre + new GridPos(0, -1), centre, chestId, 14 + trailTarget * 4);
                    chestId++;
                }
                else
                {
                    int band = 4 + Mathf.Min(4, trailTarget / 4) + rng.Next(2) + Mathf.Min(6, (hunt.AlliesAlive + AlliesKept) / 2); // more against allies
                    for (int i = 0; i < band * 3 && band > 0; i++)
                    {
                        var p = centre + new GridPos(rng.Next(-c.radius + 1, c.radius), rng.Next(-c.radius + 1, c.radius));
                        if (!grid.IsStandable(p) || grid.IsOccupied(p) || HuntSystem.Chebyshev(p, robot.Position) < 6) continue;
                        hunt.AddTrailGuard(p, false, centre, -1);
                        band--;
                    }
                }
            }
            trailGate = new GridPos(TrailMap.Gate.x, TrailMap.Gate.y);
            BuildTrailGate(trailGate, look, BattleArenas.Unlocked(trailTarget));
            // The scenery is batched into a few meshes; the chests and the gate stay free to move.
            var decor = new GameObject("Scenery").transform;
            decor.SetParent(trailRoot, false);
            ArenaDecor.Trail(decor, grid, trailTarget, look.wall, look.accent);
            if (decor.GetComponentsInChildren<MeshRenderer>().Length < 6000) StaticBatchingUtility.Combine(decor.gameObject);
        }

        private void ClearTrail()
        {
            if (trailRoot != null) Destroy(trailRoot.gameObject);
            trailRoot = null;
            trailChests.Clear();
            portalParts.Clear();
            chestGlows.Clear();
            foreach (var g in trailGifts) if (g.body != null) Destroy(g.body.gameObject);
            trailGifts.Clear();
            foreach (var p in puddles) if (p.body != null) Destroy(p.body.gameObject);
            puddles.Clear();
            if (arenaExit != null) Destroy(arenaExit.gameObject);
            arenaExit = null;
        }

        /// <summary>A treasure chest on a tile (its lid returned, to swing open).</summary>
        private Transform BuildChest(GridPos at, Color accent)
        {
            var root = new GameObject("Chest").transform;
            root.position = GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY;
            // Gold all over and glowing, under a beam of light: a chest of allies is seen from far down the road.
            var wood = MaterialFactory.CreateSurface(new Color(1f, 0.78f, 0.25f), new Color(0.9f, 0.55f, 0.08f), 0.85f, 0.9f);
            var band = MaterialFactory.Create(new Color(1f, 0.95f, 0.6f), new Color(2.2f, 1.6f, 0.5f));
            var beam = MaterialFactory.Create(new Color(1f, 0.85f, 0.4f), new Color(1.8f, 1.3f, 0.35f));
            var beamT = Shapes.Primitive(PrimitiveType.Cylinder, "Beam", root, new Vector3(0f, 3.2f, 0f), new Vector3(0.22f, 3.2f, 0.22f), beam).transform;
            Shapes.Primitive(PrimitiveType.Cylinder, "Halo", root, new Vector3(0f, 0.02f, 0f), new Vector3(1.9f, 0.01f, 1.9f), band);
            chestGlows.Add(beamT);
            Shapes.Rounded("Box", root, new Vector3(0f, 0.3f, 0f), new Vector3(0.95f, 0.6f, 0.7f), 0.08f, wood);
            Shapes.Rounded("Band", root, new Vector3(0f, 0.3f, 0f), new Vector3(0.99f, 0.12f, 0.74f), 0.03f, band);
            var lid = new GameObject("Lid").transform;
            lid.SetParent(root, false);
            lid.localPosition = new Vector3(0f, 0.6f, -0.35f);
            Shapes.Rounded("LidBox", lid, new Vector3(0f, 0.12f, 0.35f), new Vector3(0.97f, 0.24f, 0.72f), 0.1f, wood);
            Shapes.Rounded("Lock", lid, new Vector3(0f, 0.05f, 0.72f), new Vector3(0.16f, 0.2f, 0.06f), 0.03f, band);
            Shapes.Primitive(PrimitiveType.Sphere, "Glow", root, new Vector3(0f, 1.3f, 0f), Vector3.one * 0.25f, MaterialFactory.Create(accent, accent * 2.5f));
            root.SetParent(trailRoot, true);
            return lid;
        }

        /// <summary>The next arena's gate at the road's end: an arch in the arena's colours, barred with a sign if locked.</summary>
        private void BuildTrailGate(GridPos at, BattleArenas.Look look, bool open)
        {
            var root = new GameObject("ArenaGate").transform;
            root.SetParent(trailRoot, false);
            root.position = GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY;
            var stone = MaterialFactory.Create(Color.Lerp(look.wall, Color.white, 0.25f), Color.black);
            var trim = MaterialFactory.Create(look.accent, look.accent * (open ? 2f : 0.4f));
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Post", root, new Vector3(s * 2.2f, 1.6f, 0.4f), new Vector3(0.6f, 3.2f, 0.6f), 0.1f, stone);
            Shapes.Rounded("Lintel", root, new Vector3(0f, 3.35f, 0.4f), new Vector3(5.2f, 0.6f, 0.7f), 0.12f, stone);
            Shapes.Rounded("Trim", root, new Vector3(0f, 3.35f, 0.02f), new Vector3(4.6f, 0.22f, 0.06f), 0.05f, trim);
            if (open)
            {
                // A portal: a deep glowing disc, rings of light turning round it and sparks drawn into its middle.
                var core = MaterialFactory.Create(Color.Lerp(look.accent, Color.white, 0.2f), look.accent * 2.2f);
                var deep = MaterialFactory.Create(new Color(0.05f, 0.02f, 0.12f), look.accent * 0.6f);
                var ringM = MaterialFactory.Create(Color.Lerp(look.accent, Color.white, 0.5f), look.accent * 3f);
                var portal = new GameObject("Portal").transform;
                portal.SetParent(root, false);
                portal.localPosition = new Vector3(0f, 1.6f, 0.45f);
                Shapes.Primitive(PrimitiveType.Cylinder, "Deep", portal, Vector3.zero, new Vector3(3.6f, 0.03f, 3.0f), deep).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var glow = Shapes.Primitive(PrimitiveType.Cylinder, "Core", portal, new Vector3(0f, 0f, -0.03f), new Vector3(1.6f, 0.02f, 1.4f), core).transform;
                glow.localRotation = Quaternion.Euler(90f, 0f, 0f);
                portalParts.Add((glow, 0));
                for (int r = 0; r < 3; r++)
                {
                    var ring = new GameObject("Ring").transform;
                    ring.SetParent(portal, false);
                    ring.localPosition = new Vector3(0f, 0f, -0.06f - r * 0.02f);
                    float rad = 1.75f - r * 0.45f;
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI * 2f / 10f;
                        Shapes.Primitive(PrimitiveType.Sphere, "Spark", ring, new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.85f, 0f), Vector3.one * (0.22f - r * 0.04f), ringM);
                    }
                    portalParts.Add((ring, r % 2 == 0 ? 1 : -1));
                }
                for (int i = 0; i < 8; i++)
                {
                    var mote = Shapes.Primitive(PrimitiveType.Sphere, "Mote", portal, Vector3.zero, Vector3.one * 0.14f, ringM).transform;
                    portalParts.Add((mote, 10 + i));
                }
                return;
            }
            var bars = MaterialFactory.Create(new Color(0.25f, 0.25f, 0.3f), Color.black);
            for (int i = 0; i < 9; i++)
                Shapes.Rounded("Bar", root, new Vector3(-1.8f + i * 0.45f, 1.5f, 0.4f), new Vector3(0.1f, 3f, 0.1f), 0.04f, bars);
            var sign = new GameObject("Sign").AddComponent<TMPro.TextMeshPro>();
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = new Vector3(0f, 4.4f, 0f);
            sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // read from the road (south)
            sign.font = UiFactory.Font;
            sign.text = Loc.F("arena.gateLocked", BattleArenas.UnlockAt(trailTarget) + 1);
            sign.fontSize = 6f;
            sign.alignment = TMPro.TextAlignmentOptions.Center;
            sign.color = new Color(1f, 0.85f, 0.35f);
            sign.outlineWidth = 0.25f;
            sign.outlineColor = new Color(0.1f, 0.05f, 0.1f);
            sign.rectTransform.sizeDelta = new Vector2(9f, 2.5f);
        }

        private void OnPoisoned(List<GridPos> tiles)
        {
            if (!trailRun) return;
            var mat = MaterialFactory.Create(new Color(0.35f, 0.85f, 0.25f), new Color(0.3f, 1.2f, 0.2f));
            foreach (var t in tiles)
            {
                var body = Shapes.Primitive(PrimitiveType.Cylinder, "Poison", trailRoot, GridView.ToWorld(t) + Vector3.up * (GridView.SurfaceY + 0.03f), new Vector3(0.95f, 0.02f, 0.95f), mat).transform;
                puddles.Add((t, Time.time + 4f, body));
            }
        }

        private void OnChestFreed(int id)
        {
            var chest = trailChests.Find(c => c.id == id);
            if (chest == null || chest.open) return;
            chest.open = true;
            grid.SetOccupied(chest.tile, false);
            var at = GridView.ToWorld(chest.tile) + Vector3.up * (GridView.SurfaceY + 1f);
            fx.Burst(at, Palette.Coin, Palette.CoinGlow, 60, 7f);
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f);
            // Robots locked in it come out to fight on foi's side (more on later roads).
            int freed = 0, want = 2 + arenaIndex / 4;
            for (int i = 0; i < want && hunt.AlliesAlive < MaxAllies; i++) if (hunt.AddAlly(chest.tile, AllyHp)) freed++;
            KeepAllies(hunt.AlliesAlive);
            FloatAt(at, Loc.T("trail.chestOpen") + (freed > 0 ? "\n" + Loc.F("trail.allies", freed) : ""), Palette.UiGold);
            // Its gifts scatter round it.
            int[] kinds = { 0, 1, 2, 2 };
            int n = 0;
            foreach (var d in new[] { new GridPos(2, 0), new GridPos(-2, 0), new GridPos(0, 2), new GridPos(0, -2), new GridPos(2, 2), new GridPos(-2, -2) })
            {
                if (n >= kinds.Length) break;
                var t = chest.tile + d;
                if (!grid.IsStandable(t)) continue;
                trailGifts.Add((t, GiftBox(t, kinds[n]), kinds[n]));
                n++;
            }
        }

        /// <summary>Each frame on a road: puddles sting and dry up, chests swing open, gifts are picked up, the gate.</summary>
        private void UpdateTrail(float dt)
        {
            if (!trailRun) return;
            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                var p = puddles[i];
                if (Time.time >= p.until) { if (p.body != null) Destroy(p.body.gameObject); puddles.RemoveAt(i); continue; }
                if (p.body != null) p.body.localScale = new Vector3(0.95f, 0.02f, 0.95f) * Mathf.Clamp01((p.until - Time.time) * 2f);
                if (p.tile == robot.Position && robot.IsAlive && !robot.InArena && Time.time >= puddleHurtAt)
                {
                    puddleHurtAt = Time.time + 0.7f;
                    hitShareOverride = 0.08f;
                    bool alive = TryTakeHealthHit(robot.Position, crushed: true);
                    hitShareOverride = -1f;
                    if (!alive) { robot.Squash(); AudioManager.PlaySfx(Sfx.Squash); Lose(Loc.T("trail.lost")); return; }
                }
            }
            AnimatePortal();
            foreach (var c in trailChests)
                if (c.open && c.openT < 1f && c.lid != null)
                {
                    c.openT = Mathf.Min(1f, c.openT + dt * 2f);
                    c.lid.localRotation = Quaternion.Euler(-100f * c.openT, 0f, 0f);
                }
            for (int i = trailGifts.Count - 1; i >= 0; i--)
            {
                var (tile, body, kind) = trailGifts[i];
                if (body == null) { trailGifts.RemoveAt(i); continue; }
                body.rotation = Quaternion.Euler(0f, Time.time * 90f + i * 40f, 0f);
                if (HuntSystem.Chebyshev(robot.Position, tile) > 1) continue;
                OpenArenaGift(kind, body.position);
                Destroy(body.gameObject);
                trailGifts.RemoveAt(i);
            }
            // The gate: walk into it.
            if (!trailEntering && HuntSystem.Chebyshev(robot.Position, trailGate) <= 1)
            {
                if (BattleArenas.Unlocked(trailTarget))
                {
                    trailEntering = true;
                    int target = trailTarget;
                    trailRun = false;
                    ClearTrail();
                    StartArena(target);
                }
                else if (Time.time >= trailSignAt)
                {
                    trailSignAt = Time.time + 3f;
                    ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f), Loc.F("arena.gateLocked", BattleArenas.UnlockAt(trailTarget) + 1), Palette.UiGold, 56f);
                    AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                }
            }
        }

        /// <summary>The gate's portal turns and draws sparks in; the chests' beams pulse (a freed chest's fades).</summary>
        private void AnimatePortal()
        {
            float t = Time.time;
            foreach (var (part, kind) in portalParts)
            {
                if (part == null) continue;
                if (kind == 0) part.localScale = new Vector3(1.6f, 0.02f, 1.4f) * (1f + Mathf.Sin(t * 3f) * 0.12f);
                else if (kind == 1 || kind == -1) part.localRotation = Quaternion.Euler(0f, 0f, t * 70f * kind);
                else
                {
                    // A spark spirals from the rim into the middle, again and again.
                    float k = Mathf.Repeat(t * 0.6f + (kind - 10) * 0.125f, 1f);
                    float a = (kind - 10) * 0.785f + k * 5f;
                    float rad = Mathf.Lerp(2.1f, 0.05f, k);
                    part.localPosition = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.85f, -0.1f - k * 0.2f);
                    part.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.04f, k);
                }
            }
            for (int i = 0; i < chestGlows.Count && i < trailChests.Count; i++)
            {
                var b = chestGlows[i];
                if (b == null) continue;
                float w = trailChests[i].open ? Mathf.Max(0f, b.localScale.x - Time.deltaTime * 0.3f) : 0.22f + Mathf.Sin(t * 2.5f + i) * 0.06f;
                b.localScale = new Vector3(w, 3.2f, w);
            }
        }

        /// <summary>
        /// The arena is won and its gifts gathered: the win is kept (and its coins paid), and its exit opens — a pillar
        /// of light a few tiles away; walking into it starts the road to the next arena.
        /// </summary>
        private void OpenArenaExit()
        {
            int coins = BattleArenas.WinCoins(arenaIndex);
            BattleArenas.Record(arenaIndex, arenaBestStreak, true);
            SaveData.Coins += coins + Earned;
            coinsThisRun = 0;
            ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.62f), Loc.F("arena.won", coins) + "\n" + Loc.T("arena.exitOpen"), Palette.UiGold, 60f);
            // The exit: the nearest free floor tile three or more tiles from foi.
            arenaExitTile = robot.Position;
            int best = int.MaxValue;
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsStandable(p) || grid.IsGap(p)) continue;
                int d = HuntSystem.Chebyshev(p, robot.Position);
                if (d >= 3 && d < best) { best = d; arenaExitTile = p; }
            }
            var accent = BattleArenas.Style(arenaIndex).accent;
            arenaExit = new GameObject("ArenaExit").transform;
            arenaExit.position = GridView.ToWorld(arenaExitTile) + Vector3.up * GridView.SurfaceY;
            Shapes.Primitive(PrimitiveType.Cylinder, "Ring", arenaExit, new Vector3(0f, 0.04f, 0f), new Vector3(1.6f, 0.03f, 1.6f), MaterialFactory.Create(accent, accent * 2.5f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Pillar", arenaExit, new Vector3(0f, 2.5f, 0f), new Vector3(0.9f, 2.5f, 0.9f), MaterialFactory.Create(Color.Lerp(accent, Color.white, 0.5f), accent * 1.6f));
            AudioManager.PlaySfx(Sfx.Win, 0.9f, 1.3f);
        }

        /// <summary>Each frame in a won arena: the exit pulses; walking into it goes on to the next road.</summary>
        private void UpdateArenaExit()
        {
            if (arenaExit == null) return;
            float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.08f;
            arenaExit.localScale = new Vector3(pulse, 1f, pulse);
            if (HuntSystem.Chebyshev(robot.Position, arenaExitTile) > 1) return;
            int next = arenaIndex + 1;
            Destroy(arenaExit.gameObject);
            arenaExit = null;
            if (next >= BattleArenas.Count) { ShowBonusResult(Loc.T("trail.allDone")); return; }
            StartTrail(next);
        }
    }
}
