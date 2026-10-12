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
            SpawnKeptAllies();
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
                    int guardians = 2 + (trailTarget >= 8 ? 1 : 0) + (trailTarget >= 18 ? 1 : 0);
                    for (int i = 0; i < guardians; i++)
                    {
                        float a = i * Mathf.PI * 2f / guardians + 0.4f;
                        var p = centre + new GridPos(Mathf.RoundToInt(Mathf.Cos(a) * 2f), Mathf.RoundToInt(Mathf.Sin(a) * 2f));
                        hunt.AddTrailGuard(p, true, centre, chestId);
                    }
                    chestId++;
                }
                else
                {
                    int band = 4 + Mathf.Min(4, trailTarget / 4) + rng.Next(2);
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
            ArenaDecor.Trail(trailRoot, grid, trailTarget, look.wall, look.accent);
            if (trailRoot.GetComponentsInChildren<MeshRenderer>().Length < 6000) StaticBatchingUtility.Combine(trailRoot.gameObject);
        }

        private void ClearTrail()
        {
            if (trailRoot != null) Destroy(trailRoot.gameObject);
            trailRoot = null;
            trailChests.Clear();
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
            var wood = MaterialFactory.Create(new Color(0.55f, 0.33f, 0.18f), Color.black);
            var band = MaterialFactory.Create(new Color(1f, 0.8f, 0.3f), new Color(0.6f, 0.4f, 0.05f));
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
                var light = MaterialFactory.Create(new Color(look.accent.r, look.accent.g, look.accent.b, 1f), look.accent * 1.4f);
                Shapes.Rounded("Light", root, new Vector3(0f, 1.5f, 0.45f), new Vector3(3.7f, 3f, 0.08f), 0.05f, light);
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
