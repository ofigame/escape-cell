using System.Collections.Generic;

namespace SquashBot.Core
{
    /// <summary>
    /// Guarantees fairness: a wave of blocks is only allowed if the robot can still reach a safe tile in time.
    /// </summary>
    public static class SafetyChecker
    {
        public static bool HasEscape(GridModel grid, GridPos start, ICollection<GridPos> danger, int maxSteps)
        {
            if (!danger.Contains(start) && grid.IsStandable(start)) return true;

            var visited = new HashSet<GridPos> { start };
            var frontier = new List<GridPos> { start };

            for (int step = 1; step <= maxSteps; step++)
            {
                var next = new List<GridPos>();
                foreach (var p in frontier)
                {
                    foreach (var d in DirectionExtensions.All)
                    {
                        var n = p + d.ToOffset();
                        if (!grid.IsStandable(n) || !visited.Add(n)) continue;
                        if (!danger.Contains(n)) return true;
                        next.Add(n);
                    }
                }

                if (next.Count == 0) return false;
                frontier = next;
            }

            return false;
        }
    }
}
