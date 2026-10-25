using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Keeps both sides' <see cref="MatchStats"/> through a duel: the game tells it what happened (a blow, a block, a
    /// heart, a combo move offered and used …) and it counts. Reset as a duel starts.
    /// </summary>
    public class MatchStatsRecorder
    {
        public readonly MatchStats mine = new MatchStats(), theirs = new MatchStats();

        public void Reset() { mine.Reset(); theirs.Reset(); }

        public MatchStats Of(bool me) => me ? mine : theirs;

        /// <summary>Each second: the distance kept; each frame: how long each side's ultimate has stood ready unused.</summary>
        public void Tick(float dt, bool myUltiReady, bool theirUltiReady)
        {
            Idle(mine, myUltiReady, dt);
            Idle(theirs, theirUltiReady, dt);
        }

        private static void Idle(MatchStats s, bool ready, float dt)
        {
            if (!ready) { s.ultiIdleNow = 0f; return; }
            s.ultiIdleNow += dt;
            s.ultiIdleLongest = Mathf.Max(s.ultiIdleLongest, s.ultiIdleNow);
        }

        public void Distance(float tiles)
        {
            mine.distanceSum += tiles; mine.distanceSamples++;
            theirs.distanceSum += tiles; theirs.distanceSamples++;
        }
    }
}
