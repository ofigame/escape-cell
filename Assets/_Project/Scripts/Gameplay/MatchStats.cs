namespace SquashBot.Gameplay
{
    /// <summary>One side's numbers in a duel (the design's §6.2), for the loss's tip and the training's challenges.</summary>
    public class MatchStats
    {
        /// <summary>Blows swung and landed; blows blocked, guarded perfectly; the shield broken; dodges.</summary>
        public int blows, hits, blocked, perfects, shieldBreaks, dodges;
        /// <summary>Breakers used, and this side's combos broken by the other's breaker.</summary>
        public int breakers, brokenByRival;
        /// <summary>Hearts used, and knocked down with hearts still left and none used.</summary>
        public int hearts, downWithHeartsUnused;
        /// <summary>Ultimates used and landed; the other's ultimates that landed on this side.</summary>
        public int ultis, ultiHits, ultisTaken;
        /// <summary>POWER HIT / KNOCKDOWN offered, used, let run out.</summary>
        public int comboOffered, comboUsed, comboMissed;
        /// <summary>The longest the ultimate stood ready unused (seconds), and the time it has been ready now.</summary>
        public float ultiIdleLongest, ultiIdleNow;
        /// <summary>The distance kept, summed and counted (once a second).</summary>
        public float distanceSum;
        public int distanceSamples;

        public float AverageDistance => distanceSamples > 0 ? distanceSum / distanceSamples : 0f;

        public void Reset()
        {
            blows = hits = blocked = perfects = shieldBreaks = dodges = breakers = brokenByRival = hearts = downWithHeartsUnused = 0;
            ultis = ultiHits = ultisTaken = comboOffered = comboUsed = comboMissed = distanceSamples = 0;
            ultiIdleLongest = ultiIdleNow = distanceSum = 0f;
        }
    }
}
