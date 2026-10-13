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
        private int lastScore;
        private bool lastScoreBest;
        private int lastPointsGained;

        /// <summary>A floor starts: the counters go back to zero and its crowd is counted for the par time.</summary>
        private void ResetScore()
        {
            hitsTaken = 0;
            bestStreakRun = 0;
            scoreEnemies = hunt != null ? hunt.Total : 0;
        }

        /// <summary>Seconds a good run takes: about two and a half a foe, a second and a half per point of vanG's health.</summary>
        private float ParTime => 20f + scoreEnemies * 2.5f + (level != null ? level.monsterHp * 1.5f : 0f);

        private int FloorScore()
        {
            float par = ParTime;
            float speed = elapsed <= par ? 1f : Mathf.Clamp01(1f - (elapsed - par) / par); // nothing left at twice the par
            float hp = HealthEnabled ? Mathf.Clamp01(health) : 1f;
            float hits = Mathf.Clamp01(1f - hitsTaken * 0.2f);
            float streak = Mathf.Clamp01(bestStreakRun / 15f);
            return Mathf.RoundToInt(speed * 40f + hp * 30f + hits * 15f + streak * 15f);
        }

        /// <summary>The floor is won: its score kept (if a best) and any development points it brings counted.</summary>
        private void ScoreFloor()
        {
            if (bonusRun || level == null || level.mission != MissionType.Hunt) { lastScore = -1; return; }
            int before = Army.EarnedPoints;
            int prev = Army.BestScore(levelIndex);
            lastScore = FloorScore();
            lastScoreBest = Army.RecordScore(levelIndex, lastScore) > 0 && prev > 0;
            lastPointsGained = 0;
            // Points are counted once the world is finished: winning its tenth floor unlocks the next first.
            pointsBefore = before;
        }

        private int pointsBefore;

        /// <summary>The line on the result card: this floor's score (a new best?), and new development points.</summary>
        private string ScoreLine()
        {
            if (lastScore < 0) return null;
            lastPointsGained = Mathf.Max(0, Army.EarnedPoints - pointsBefore);
            string line = Loc.F("score.floor", lastScore) + (lastScoreBest ? " · " + Loc.T("score.best") : "");
            if (lastPointsGained > 0) line += "\n" + Loc.F("score.points", lastPointsGained);
            return line;
        }
    }
}
