using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The floor score (0-100) that builds foi's army: speed against the floor's par time (40), health left (30), blows
    /// taken (15, three off for each) and the longest kill streak (15). Its best is kept per floor (Army); a finished
    /// world turns its floors' scores into development points.
    /// </summary>
    public partial class GameManager
    {
        private int hitsTaken, bestStreakRun, scoreEnemies;
        /// <summary>The last floor's score and whether it was a new best, for the result card.</summary>
        private int lastScore = -1;
        private bool lastScoreBest;

        /// <summary>A floor starts: the counters go back to zero and its crowd is counted for the par time.</summary>
        private void ResetScore()
        {
            hitsTaken = 0;
            bestStreakRun = 0;
            scoreEnemies = hunt != null ? hunt.Total : 0;
        }

        /// <summary>Seconds a good run takes: about two and a half a foe, a second and a half per point of vanG's health.</summary>
        private float ParTime => 20f + scoreEnemies * 2.5f + (level != null ? level.monsterHp * 1.5f : 0f);

        /// <summary>The last floor score's four parts: speed (of 40), health left (30), hits taken (15), longest streak (15).</summary>
        private int scoreSpeed, scoreHealth, scoreHits, scoreStreak;

        private int FloorScore()
        {
            float par = ParTime;
            float speed = elapsed <= par ? 1f : Mathf.Clamp01(1f - (elapsed - par) / par); // nothing left at twice the par
            float hp = HealthEnabled ? Mathf.Clamp01(health) : 1f;
            float hits = Mathf.Clamp01(1f - hitsTaken * 0.2f);
            float streak = Mathf.Clamp01(bestStreakRun / 15f);
            scoreSpeed = Mathf.RoundToInt(speed * 40f);
            scoreHealth = Mathf.RoundToInt(hp * 30f);
            scoreHits = Mathf.RoundToInt(hits * 15f);
            scoreStreak = Mathf.RoundToInt(streak * 15f);
            return scoreSpeed + scoreHealth + scoreHits + scoreStreak;
        }

        /// <summary>The stars a floor score is worth (the fixes design's job 9): one for winning, two and three by the score.</summary>
        private static int ScoreStars(int score)
        {
            var t = FloorTuning.Current;
            return score >= t.starThree ? 3 : score >= t.starTwo ? 2 : 1;
        }

        /// <summary>The result card's note on a hunt floor: the score's four parts (what to do better) and the next star's score.</summary>
        private string ScoreParts(int stars)
        {
            string parts = Loc.F("score.parts", scoreSpeed, scoreHealth, scoreHits, scoreStreak);
            var t = FloorTuning.Current;
            return stars >= 3 ? parts : parts + "\n" + Loc.F("star.score", stars >= 2 ? t.starThree : t.starTwo);
        }

        /// <summary>
        /// Every star of the world this floor belongs to (its ten floors at three): the world's gold and its conqueror
        /// title, once. Returns the line for the result (null: not now).
        /// </summary>
        private string WorldStarsReward()
        {
            int world = levelIndex / LevelCatalog.LevelsPerWorld;
            if (TitleBook.HasWorld(world)) return null;
            for (int i = 0; i < LevelCatalog.LevelsPerWorld; i++)
                if (Progress.Stars(world * LevelCatalog.LevelsPerWorld + i) < 3) return null;
            TitleBook.GiveWorld(world);
            int gold = FloorTuning.Current.worldStarGold;
            SaveData.Coins += gold;
            return Loc.F("stars.worldDone", gold, TitleBook.Text("world." + world));
        }

        /// <summary>The floor is won: its score kept (if a best).</summary>
        private void ScoreFloor()
        {
            if (bonusRun || level == null || level.mission != MissionType.Hunt) { lastScore = -1; return; }
            int prev = Progress.BestScore(levelIndex);
            lastScore = FloorScore();
            lastScoreBest = Progress.RecordScore(levelIndex, lastScore) > 0 && prev > 0;
        }

        /// <summary>The line on the result card: this floor's score (a new best?).</summary>
        private string ScoreLine()
        {
            if (lastScore < 0) return null;
            return Loc.F("score.floor", lastScore) + (lastScoreBest ? " · " + Loc.T("score.best") : "");
        }
    }
}
