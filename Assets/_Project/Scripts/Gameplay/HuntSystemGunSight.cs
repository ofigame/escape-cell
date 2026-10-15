using SquashBot.Core;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>The gun's target on the floors: the nearest awake enemy within the gun's range, in foi's stage.</summary>
    public partial class HuntSystem
    {
        public bool GunTarget(GridPos from, int range, out GridPos target)
        {
            var pick = from;
            int best = int.MaxValue;
            int stage = stageCount > 1 ? StageOf(from) : 0;
            void Consider(GridPos p)
            {
                if (stageCount > 1 && StageOf(p) != stage) return;
                int d = Chebyshev(p, from);
                if (d > range || d < 1) return;
                // The nearest first; among those as near, the straightest line.
                int score = d * 100 + Mathf.Abs(p.x - from.x) + Mathf.Abs(p.y - from.y);
                if (score >= best) return;
                best = score;
                pick = p;
            }
            foreach (var g in guards) if (!g.dead && !g.ally && !g.squad) Consider(g.pos);
            foreach (var t in towers) if (!t.dead) Consider(t.pos);
            foreach (var b in bugs) if (!b.dead) Consider(b.pos);
            if (MonsterUp && MonsterAwake) Consider(monsterPos);
            target = pick;
            return best != int.MaxValue;
        }

        /// <summary>Up to <paramref name="count"/> enemies within <paramref name="range"/> of a tile (not on it), nearest first: a chain
        /// weapon's lightning jumps to them.</summary>
        public System.Collections.Generic.List<GridPos> TargetsNear(GridPos at, int range, int count)
        {
            var all = new System.Collections.Generic.List<GridPos>();
            foreach (var g in guards) if (!g.dead && !g.ally && g.pos != at && Chebyshev(g.pos, at) <= range && !all.Contains(g.pos)) all.Add(g.pos);
            foreach (var t in towers) if (!t.dead && t.pos != at && Chebyshev(t.pos, at) <= range) all.Add(t.pos);
            if (MonsterUp && MonsterAwake && monsterPos != at && Chebyshev(monsterPos, at) <= range) all.Add(monsterPos);
            all.Sort((a, b) => Chebyshev(a, at).CompareTo(Chebyshev(b, at)));
            if (all.Count > count) all.RemoveRange(count, all.Count - count);
            return all;
        }
    }
}
