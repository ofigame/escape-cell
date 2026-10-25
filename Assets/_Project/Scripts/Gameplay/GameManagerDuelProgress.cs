using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The duel's growth around the match (the scenario's §3.7, §3.8, §4.2, §5): the match's own small challenge, what
    /// the player did in it (specials, ultimates, combos, knock-downs, hearts, the worst deficit), the computer rival's
    /// personality and how human it plays, and after the match the XP, the weapon's mastery and the bot results for the
    /// dynamic difficulty — gathered for the reward screen.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The match's small challenge (shown at the start, worth 50 XP).</summary>
        public enum MatchGoal { Ultis, Specials, Combo, Knockdowns, NoHeart }

        private MatchGoal matchGoal;
        private int mcSpecials, mcUltis, mcCombos, mcHearts, matchWorst, mcPerfects, mcUltiDodges;
        private BotTuning botTuning;

        /// <summary>What the last duel brought, for the reward screen.</summary>
        public class DuelRewards
        {
            public int trophies, gold, cores, xp, levelBefore, xpBefore, levelAfter, xpAfter;
            public bool trophiesKnown, firstWin, goalDone, comeback, streakBreaker;
            public List<DuelLevel.LevelUp> levelUps = new List<DuelLevel.LevelUp>();
            public WeaponDef weapon;
            public int masteryBefore, masteryAfter, masteryPointsBefore, masteryPointsAfter;
            public ChestTier? chest;
            public string goalText;
            public int seasonPoints, seasonBefore;
            public string eventLine;
            /// <summary>Wins in a row now (a band from 2), and the losing streak's easing coming (a loss's consolation).</summary>
            public int streak;
            public bool consolation;
        }

        private DuelRewards lastRewards;

        private static int GoalTarget(MatchGoal g) => g == MatchGoal.Ultis ? 2 : g == MatchGoal.Specials ? 3 : g == MatchGoal.Knockdowns ? 2 : 1;

        private static string GoalText(MatchGoal g) => Loc.F("mc." + g, GoalTarget(g), DuelLevel.Challenge);

        /// <summary>The match starts: its challenge drawn, the counts at zero.</summary>
        private void BeginMatchProgress(int seed)
        {
            stats.Reset();
            mcSpecials = mcUltis = mcCombos = mcHearts = matchWorst = mcPerfects = mcUltiDodges = 0;
            matchGoal = (MatchGoal)(Mathf.Abs(seed / 7) % 5);
        }

        /// <summary>A duel deed: counted for the quests and for the match's challenge.</summary>
        private void NoteDuel(DuelGoal g)
        {
            ChallengeQuests.Report(g);
            if (g == DuelGoal.Special) mcSpecials++;
            else if (g == DuelGoal.Ultimate) mcUltis++;
            else if (g == DuelGoal.Combo5) mcCombos++;
        }

        private bool GoalDone(bool won) => matchGoal switch
        {
            MatchGoal.Ultis => mcUltis >= 2,
            MatchGoal.Specials => mcSpecials >= 3,
            MatchGoal.Combo => mcCombos >= 1,
            MatchGoal.Knockdowns => chDuelMe >= 2,
            _ => won && mcHearts == 0,
        };

        /// <summary>
        /// The computer rival's personality and humanity for this duel: its seed picks the personality, the rival's rank
        /// tier how quick and clean it plays, the player's recent bot results how hard (the first five duels softer).
        /// </summary>
        private void TuneBot()
        {
            if (LiveActive || rival == null) { botTuning = null; hunt.FoeWhiff = 0f; hunt.FoeReactScale = 1f; return; }
            botTuning = BotTuning.For(BotTuning.Of(rival.seed), DuelPower.TierOf(rival.Rank), BotDifficulty.Shift - (ReturningPlayer.Rusty ? 1 : 0), BotDifficulty.FirstFive); // (back after days away: the first three a step easier)
            if (copyMatch) PlayStyle.Apply(botTuning, rival.style); // a friend's copy: the friend's own way of fighting
            hunt.FoeBoldness = botTuning.aggression;
            hunt.FoeSkill = Mathf.Clamp01(0.25f + 0.6f * botTuning.shield + 0.03f * rival.Rank);
            hunt.FoeWhiff = botTuning.whiff;
            hunt.FoeReactScale = (botTuning.reactMin + botTuning.reactMax) * 0.5f / 0.3f; // (0.3 s: the old pace)
            hunt.FoeFlee = botTuning.flee;
        }

        /// <summary>
        /// After the match: XP (the result, the day's first win, the match's challenge, a comeback), the weapon's
        /// mastery, the bot result for the dynamic difficulty. Returns everything for the reward screen.
        /// </summary>
        private DuelRewards SettleProgress(bool won, bool draw, bool friendly, int gold, int cores, bool firstWin)
        {
            var r = new DuelRewards { gold = gold, cores = cores, firstWin = firstWin, chest = chestWon };
            r.goalText = GoalText(matchGoal);
            r.goalDone = GoalDone(won);
            r.comeback = won && matchWorst >= 2;
            int xp = won ? DuelLevel.Win : draw ? DuelLevel.Draw : DuelLevel.Loss;
            if (firstWin) xp += DuelLevel.FirstWin;
            if (r.goalDone) xp += DuelLevel.Challenge;
            if (r.comeback) xp += DuelLevel.Comeback;
            r.xp = xp;
            r.levelBefore = DuelLevel.Level; r.xpBefore = DuelLevel.Xp;
            r.levelUps = DuelLevel.Add(xp);
            r.levelAfter = DuelLevel.Level; r.xpAfter = DuelLevel.Xp;
            // the weapon's mastery (every duel counts, a friend's too)
            r.weapon = Armory.Equipped;
            r.masteryPointsBefore = Mastery.Points(r.weapon);
            r.masteryBefore = Mastery.Level(r.weapon);
            Mastery.Add(r.weapon, won ? 3 : draw ? 2 : 1);
            r.masteryPointsAfter = Mastery.Points(r.weapon);
            r.masteryAfter = Mastery.Level(r.weapon);
            // the streak breaker: a computer rival on a 5+ winning streak beaten
            if (won && !friendly && rival != null && rival.streak >= 5) { r.streakBreaker = true; SaveData.Coins += 200; r.gold += 200; }
            if (botTuning != null) { BotDifficulty.Record(won, draw); ReturningPlayer.BotPlayed(); }
            // the season road
            r.seasonPoints = won ? SeasonRoad.Win : draw ? SeasonRoad.Draw : SeasonRoad.Loss;
            r.seasonBefore = SeasonRoad.Points;
            SeasonRoad.Add(r.seasonPoints);
            r.streak = won && !friendly ? DuelChests.Streak : 0;
            r.consolation = !won && !draw && botTuning != null && BotDifficulty.LossesInRow >= 2;
            lastRewards = r;
            return r;
        }

        /// <summary>The rival's card at the match's start: rank, title, its winning streak's flame, its mastery badge.</summary>
        private string RivalCard(Challenge.Opponent o)
        {
            string line = o.name + "\n" + Challenge.RankName(o.Rank) + " · " + Loc.F("ch.trophies", o.trophies) + " · " + Loc.F("dl.short", o.duelLevel);
            var extras = new List<string>();
            if (!string.IsNullOrEmpty(o.titleKey)) extras.Add(Loc.T(o.titleKey));
            if (o.streak >= 3) extras.Add(Loc.F(o.streak >= 10 ? "card.streak3" : o.streak >= 5 ? "card.streak2" : "card.streak1", o.streak));
            if (o.mastery >= 7) extras.Add(Loc.F("card.mastery", o.mastery));
            // its move set, so the player can plan against it
            var set = !string.IsNullOrEmpty(o.moves) ? MoveSet.Parse(o.moves) : MoveSet.ForBot(BotTuning.Of(o.seed), o.seed, o.duelLevel);
            extras.Add(Loc.T("move.s." + set.s) + " / " + Loc.T("move.u." + set.u) + (set.p != PassiveKind.None ? " / " + Loc.T("move.p." + set.p) : ""));
            if (extras.Count > 0) line += "\n" + string.Join(" · ", extras);
            return line;
        }

        /// <summary>The player's own card: rank, trophies, duel level and title.</summary>
        private string MyCard()
        {
            string line = Challenge.PlayerName + "\n" + Challenge.RankName(Challenge.Rank) + " · " + Loc.F("ch.trophies", Challenge.Trophies) + " · " + Loc.F("dl.short", DuelLevel.Level);
            string t = DuelLevel.TitleKey(DuelLevel.Level);
            int m = Mastery.Level(Armory.Equipped);
            var extras = new List<string>();
            if (t != null) extras.Add(Loc.T(t));
            if (m >= 7) extras.Add(Loc.F("card.mastery", m));
            if (extras.Count > 0) line += "\n" + string.Join(" · ", extras);
            return line;
        }

        /// <summary>
        /// The menu's goal line (§4.6): the nearest next goal — the day's first win, the next rank, the weapon's next
        /// mastery level, the next duel level.
        /// </summary>
        public string GoalLine()
        {
            if (DuelDay.FirstWinWaiting) return Loc.T("goal.firstWin");
            int rank = Challenge.Rank;
            if (rank + 1 < Challenge.RankAt.Length)
            {
                int need = Challenge.RankAt[rank + 1] - Challenge.Trophies;
                int wins = Mathf.Max(1, Mathf.CeilToInt(need / 20f));
                if (wins <= 5) return Loc.F("goal.rank", Challenge.RankName(rank + 1), wins);
            }
            var w = Armory.Equipped;
            int ml = Mastery.Level(w);
            if (ml < Mastery.Max)
            {
                int left = Mastery.NextAt(ml) - Mastery.Points(w);
                if (left <= 15) return Loc.F("goal.mastery", Loc.T("weapon." + w.id), ml + 1, left);
            }
            if (rank + 1 < Challenge.RankAt.Length)
                return Loc.F("goal.rank", Challenge.RankName(rank + 1), Mathf.Max(1, Mathf.CeilToInt((Challenge.RankAt[rank + 1] - Challenge.Trophies) / 20f)));
            return Loc.F("goal.level", DuelLevel.Level + 1, DuelLevel.Need(DuelLevel.Level) - DuelLevel.Xp);
        }
    }
}
