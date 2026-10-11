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
            levelIndex = BattleArenas.ThemeLevel(i);
            level = BuildArenaLevel(i);
            BeginRun();
            ui.ShowIntro(Loc.T("arena." + i), Loc.T("arena.intro"));
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
            l.number = Mathf.Min(250, levelIndex + 30); // tougher bugs and warlords than the level it borrows
            float d = Mathf.Clamp01(0.55f + i * 0.05f);
            l.score = Mathf.RoundToInt(d * 100f);
            l.robots = 70 + i * 14;
            l.robotsAtOnce = 8 + i / 2;
            l.elites = Mathf.RoundToInt(l.robots * 0.15f);
            l.brutes = 2 + i / 3;
            l.towers = 4 + i / 2;
            l.bugs = 20 + i * 3;
            l.robotHp = 2 + Mathf.RoundToInt(d * 4f);
            l.robotStep = Mathf.Lerp(0.8f, 0.45f, d);
            l.monsterHp = 30 + i * 6;
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

        /// <summary>The arena ended: its record kept, the result card says how it went.</summary>
        private void EndArena(bool won)
        {
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
