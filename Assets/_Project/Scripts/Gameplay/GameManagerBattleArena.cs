using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The battle arenas on the game's side: picked from their own screen (main menu → BATTLE ARENAS), played like a
    /// bonus (free, nothing to lose, no progress), on a huge courtyard with tall ramparts and towers, far more robots
    /// and warlords than the levels round them, vanG at the end. Kill streaks bring gifts: coins at 5, a boost at 10, a
    /// defence at 15, a chest at 25 (and every 10 after).
    /// </summary>
    public partial class GameManager
    {
        private bool arenaRun;
        private int arenaIndex = -1, arenaBestStreak;

        private void WireArenaUi()
        {
            ui.ArenasPressed += ShowArenas;
            ui.Arenas.BackPressed += () => ui.Arenas.Hide();
            ui.Arenas.ArenaChosen += StartArena;
        }

        private void ShowArenas()
        {
            if (State != GameState.Menu) ShowMenu();
            ui.Arenas.Show();
        }

        private void StartArena(int i)
        {
            if (!BattleArenas.Unlocked(i)) return;
            ui.Arenas.Hide();
            bonusRun = true;
            dailyRun = false;
            arenaRun = true;
            arenaIndex = i;
            arenaBestStreak = 0;
            arenaGiftsPhase = arenaGiftsDone = false;
            levelIndex = BattleArenas.ThemeLevel(i);
            level = BuildArenaLevel(i);
            BeginRun();
            EnterTps(); // fought in landscape, over foi's shoulder
            ui.ShowIntro(Loc.T(BattleArenas.NameKey(i)), Loc.T("arena.intro"));
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 0.9f);
            RefreshHud();
            ui.SkillBar.Refresh(true);
        }

        /// <summary>The arena's level: the look of the level it borrows, its own huge courtyard, a much bigger, tougher crowd.</summary>
        private LevelData BuildArenaLevel(int i)
        {
            var l = levelSet.levels[levelIndex].Clone();
            l.layout = BattleArenas.Layout(i, out var bounds); // no cage, no tunnel
            l.stageBounds = bounds;
            l.fortressStages = 1;
            l.gridWidth = l.layout[0].Length;
            l.gridHeight = l.layout.Length;
            l.terrain = TerrainKind.Flat;
            l.hard = true;
            l.marathon = false;
            l.final = false;
            l.mission = MissionType.Hunt;
            // Two worlds harder than its own world: every arena fights like the world two steps ahead (the first world's
            // arena as hard as world 3, the last ones capped at the last world). Everything grows with that — the
            // crowd, how many stand at once (never more than 10 easy and 5 warlords), their health, pace and armour,
            // the towers, vanG — while the robots keep the look of the arena's own world.
            int harder = Mathf.Min(BattleArenas.Count - 1, i + 2);
            int strengthLevel = Mathf.Min(LevelCatalog.LevelCount - 1, harder * LevelCatalog.LevelsPerWorld + 5);
            l.number = strengthLevel + 1;
            l.armor = levelSet.levels[strengthLevel].armor;
            float d = harder / (float)(BattleArenas.Count - 1); // 0 … 1 across the worlds
            l.score = Mathf.RoundToInt(Mathf.Lerp(20f, 95f, d));
            l.elites = Mathf.RoundToInt(Mathf.Lerp(4f, 24f, d));
            l.robots = Mathf.RoundToInt(Mathf.Lerp(24f, 90f, d)); // the easy ones (the warlords come on top)
            l.robotsAtOnce = Mathf.RoundToInt(Mathf.Lerp(5f, 10f, d));
            l.hardAtOnce = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, d));
            l.brutes = 0;
            l.towers = Mathf.RoundToInt(Mathf.Lerp(0f, 8f, d));
            l.bugs = Mathf.RoundToInt(Mathf.Lerp(10f, 40f, d));
            l.robotHp = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, d));
            l.robotStep = Mathf.Lerp(1.05f, 0.5f, d);
            l.monsterHp = Mathf.RoundToInt(Mathf.Lerp(18f, 130f, d));
            return l;
        }

        /// <summary>A kill streak in an arena: gifts at 5, 10, 15, 25 (and every 10 after).</summary>
        private void ArenaStreakGift(int n)
        {
            if (!arenaRun) return;
            arenaBestStreak = Mathf.Max(arenaBestStreak, n);
            string text = null;
            if (n == 5)
            {
                int c = 50 + arenaIndex * 10;
                coinsThisRun += c;
                text = Loc.F("arena.giftCoins", c);
            }
            else if (n == 10)
            {
                var counter = new System.Collections.Generic.List<Boost>(Shop.Counter());
                counter.RemoveAll(Shop.Full);
                if (counter.Count > 0)
                {
                    var b = counter[Random.Range(0, counter.Count)];
                    Shop.Give(b);
                    text = Loc.F("arena.giftItem", Loc.T("shop." + b));
                }
                else { coinsThisRun += 100; text = Loc.F("arena.giftCoins", 100); }
            }
            else if (n == 15)
            {
                var open = new System.Collections.Generic.List<DefenseKind>();
                foreach (var k in Defenses.All) if (Defenses.Unlocked(k)) open.Add(k);
                var kind = open[Random.Range(0, open.Count)];
                Defenses.AddStock(kind, 1);
                text = Loc.F("arena.giftItem", "+1 " + Loc.T("def." + kind));
            }
            else if (n >= 25 && (n - 25) % 10 == 0)
            {
                var reward = LevelChest.Roll(levelIndex);
                LevelChest.Grant(reward);
                text = Loc.T("arena.giftChest") + "\n" + LevelChest.Describe(reward, reward.coins);
            }
            if (text == null) return;
            ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), text, Palette.UiGold, 64f);
            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.4f);
            fx.Burst(robot.transform.position + Vector3.up * 1f, Palette.Coin, Palette.CoinGlow, 40, 6f);
        }

        // ---------- The winner's gifts ----------

        private bool arenaGiftsPhase, arenaGiftsDone;
        private float arenaGiftsUntil;
        private readonly System.Collections.Generic.List<(GridPos tile, Transform body, int kind)> arenaGifts = new System.Collections.Generic.List<(GridPos, Transform, int)>();

        /// <summary>
        /// The arena is won: the first time, gifts burst out over the courtyard (boosts, defences and coin bags that
        /// make the next world easier); won again, a shower of coin bags instead. foi runs round collecting them (the
        /// fight is over: nothing moves but foi); when all are picked up (or after half a minute) the result card comes.
        /// </summary>
        private void StartArenaGifts()
        {
            arenaGiftsPhase = true;
            LeavePostNow();
            hunt.Freeze();
            hazards.Freeze();
            enemies.Freeze();
            floorRules.Freeze();
            robot.CheerBrief(); // a little dance, and foi is free to run for the gifts
            AudioManager.PlaySfx(Sfx.Win);
            fx.Burst(robot.transform.position + Vector3.up * 0.8f, Palette.Coin, Palette.CoinGlow, 50, 7f);
            var spots = new System.Collections.Generic.List<GridPos>();
            foreach (var p in grid.AllPositions())
            {
                int d = HuntSystem.Chebyshev(p, robot.Position);
                if (d >= 2 && d <= 8 && grid.IsStandable(p) && !grid.IsGap(p)) spots.Add(p);
            }
            bool first = BattleArenas.Wins(arenaIndex) == 0;
            int[] kinds = first ? new[] { 0, 0, 0, 1, 1, 2, 2, 2 } : new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }; // gifts once, then gold
            foreach (int kind in kinds)
            {
                if (spots.Count == 0) break;
                var at = spots[Random.Range(0, spots.Count)];
                spots.RemoveAll(q => HuntSystem.Chebyshev(q, at) < 2); // spread out
                arenaGifts.Add((at, GiftBox(at, kind), kind));
            }
            arenaGiftsUntil = Time.time + 30f;
            ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.62f), Loc.T(first ? "arena.collect" : "arena.collectGold"), Palette.UiGold, 70f);
        }

        /// <summary>A gift box on a tile (coloured by what it holds, ribboned) or a coin bag, glowing and bobbing.</summary>
        private Transform GiftBox(GridPos at, int kind)
        {
            var colour = kind == 0 ? new Color(0.35f, 0.75f, 1f) : kind == 1 ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.82f, 0.25f);
            var t = new GameObject("ArenaGift").transform;
            t.position = GridView.ToWorld(at) + Vector3.up * (GridView.SurfaceY + 0.35f);
            var body = MaterialFactory.Create(colour, colour * 0.6f);
            var ribbon = MaterialFactory.Create(Color.white, new Color(0.6f, 0.6f, 0.6f));
            if (kind == 2)
            {
                // A fat sack of gold, tied at the neck, coins spilling on top.
                Shapes.Rounded("Sack", t, Vector3.zero, new Vector3(0.5f, 0.48f, 0.5f), 0.22f, body);
                Shapes.Rounded("Neck", t, new Vector3(0f, 0.28f, 0f), new Vector3(0.22f, 0.12f, 0.22f), 0.05f, ribbon);
                Shapes.Rounded("Coin", t, new Vector3(0f, 0.38f, 0f), new Vector3(0.24f, 0.05f, 0.24f), 0.1f, MaterialFactory.Create(Palette.Coin, Palette.CoinGlow));
            }
            else
            {
                Shapes.Rounded("Box", t, Vector3.zero, new Vector3(0.5f, 0.42f, 0.5f), 0.06f, body);
                Shapes.Rounded("Lid", t, new Vector3(0f, 0.24f, 0f), new Vector3(0.56f, 0.1f, 0.56f), 0.04f, body);
                Shapes.Rounded("RibbonA", t, Vector3.zero, new Vector3(0.1f, 0.54f, 0.58f), 0.02f, ribbon);
                Shapes.Rounded("RibbonB", t, Vector3.zero, new Vector3(0.58f, 0.54f, 0.1f), 0.02f, ribbon);
                Shapes.Rounded("Bow", t, new Vector3(0f, 0.34f, 0f), new Vector3(0.22f, 0.12f, 0.22f), 0.05f, ribbon);
            }
            fx.Burst(t.position, colour, colour * 2f, 14, 4f);
            return t;
        }

        /// <summary>Each frame of the gift phase: the boxes spin and bob, foi on one opens it.</summary>
        private void UpdateArenaGifts()
        {
            if (!arenaGiftsPhase) return;
            for (int i = arenaGifts.Count - 1; i >= 0; i--)
            {
                var (tile, body, kind) = arenaGifts[i];
                if (body == null) { arenaGifts.RemoveAt(i); continue; }
                body.rotation = Quaternion.Euler(0f, Time.time * 90f + i * 40f, 0f);
                body.position = GridView.ToWorld(tile) + Vector3.up * (GridView.SurfaceY + 0.4f + Mathf.Sin(Time.time * 3f + i) * 0.1f);
                if (robot.Position != tile) continue;
                OpenArenaGift(kind, body.position);
                Destroy(body.gameObject);
                arenaGifts.RemoveAt(i);
            }
            if (arenaGifts.Count > 0 && Time.time < arenaGiftsUntil) return;
            foreach (var g in arenaGifts) if (g.body != null) Destroy(g.body.gameObject);
            arenaGifts.Clear();
            arenaGiftsPhase = false;
            arenaGiftsDone = true;
            Win();
        }

        private void OpenArenaGift(int kind, Vector3 at)
        {
            if (kind == 0)
            {
                var counter = new System.Collections.Generic.List<Boost>(Shop.Counter());
                counter.RemoveAll(Shop.Full);
                if (counter.Count > 0)
                {
                    var b = counter[Random.Range(0, counter.Count)];
                    Shop.Give(b);
                    Gifted(Loc.F("arena.giftItem", Loc.T("shop." + b)), at);
                    return;
                }
                kind = 2; // nothing more fits in the bag: gold instead
            }
            if (kind == 1)
            {
                var open = new System.Collections.Generic.List<DefenseKind>();
                foreach (var k in Defenses.All) if (Defenses.Unlocked(k)) open.Add(k);
                var dk = open[Random.Range(0, open.Count)];
                Defenses.AddStock(dk, 1);
                Gifted(Loc.F("arena.giftItem", "+1 " + Loc.T("def." + dk)), at);
                return;
            }
            int c = 40 + arenaIndex * 8;
            coinsThisRun += c;
            Gifted(Loc.F("arena.giftCoins", c), at);
        }

        private void Gifted(string text, Vector3 at)
        {
            FloatAt(at, text, Palette.UiGold);
            fx.Burst(at, Palette.Coin, Palette.CoinGlow, 30, 5f);
            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.3f);
            Haptics.Light();
        }

        /// <summary>The arena ended: its record kept, the result card says how it went.</summary>
        private void EndArena(bool won)
        {
            ExitTps();
            BattleArenas.Record(arenaIndex, arenaBestStreak, won);
            if (won)
            {
                int c = BattleArenas.WinCoins(arenaIndex);
                SaveData.Coins += c;
                ShowBonusResult(Loc.F("arena.won", c));
            }
            else ShowBonusResult(Loc.F("arena.lost", arenaBestStreak));
        }
    }
}
