using SquashBot.Core;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>What the player's defences need from the crowd: targets in reach, robots crowding a tower, extra robots.</summary>
    public partial class HuntSystem
    {
        /// <summary>The crowd is moving (not frozen, not over).</summary>
        public bool Live => running && !frozen;

        /// <summary>The nearest bug, robot or enemy tower within <paramref name="range"/> tiles of a point, in a stage.</summary>
        public bool NearestEnemy(Vector3 from, float range, int stage, out GridPos target, out Vector3 at)
        {
            var bestP = default(GridPos);
            var bestAt = from;
            float best = range * range;
            bool found = false;
            void Consider(GridPos p, Vector3 pos)
            {
                if (stageCount > 1 && StageOf(p) != stage) return;
                var d = pos - from;
                d.y = 0f;
                float dd = d.sqrMagnitude;
                if (dd > best) return;
                best = dd;
                bestP = p;
                bestAt = pos;
                found = true;
            }
            foreach (var g in guards) if (!g.dead && g.moveT >= 1f) Consider(g.pos, g.root.position + Vector3.up * 0.6f);
            foreach (var t in towers) if (!t.dead) Consider(t.pos, At(t.pos) + Vector3.up * 0.8f);
            foreach (var b in bugs) if (!b.dead) Consider(b.pos, b.model.transform.position + Vector3.up * 0.1f);
            target = bestP;
            at = bestAt;
            return found;
        }

        /// <summary>How hard the robots next to a tile are hitting it (an elite counts three, an enforcer two).</summary>
        public int PressureAt(GridPos p)
        {
            int n = 0;
            foreach (var g in guards)
                if (!g.dead && Chebyshev(g.pos, p) <= 1) n += g.rank >= 99 ? 3 : g.brute ? 2 : 1;
            return n;
        }

        /// <summary>More robots for a stage (they drop in like the rest, while foi is there).</summary>
        public void AddRobots(int stage, int n)
        {
            if (n <= 0 || !running) return;
            if (stageCount > 1 && stageToCome != null) stageToCome[Mathf.Clamp(stage, 0, stageCount - 1)] += n;
            guardsToCome += n;
            Total += n;
        }
    }
}
