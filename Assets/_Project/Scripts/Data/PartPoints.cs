using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Each part's points bar (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 2): part items fill it; once it holds what the
    /// next level asks, LEVEL UP (gold, and from level 11 core parts) raises the part and takes those points. Points over
    /// the bar are never lost: they carry to the next level.
    /// </summary>
    public static class PartPoints
    {
        private static string Key(RobotPart p) => "sb_part_pts_" + p;

        /// <summary>The points on the part's bar (any over what the next level asks included).</summary>
        public static int Points(RobotPart p) => Mathf.Max(0, Prefs.GetInt(Key(p), 0));

        /// <summary>What the part's next level asks (0 at the top).</summary>
        public static int Need(RobotPart p) => RobotParts.IsMaxed(p) ? 0 : ItemCatalog.Current.PointsFor(RobotParts.Level(p) + 1);

        /// <summary>The bar is full: the next level waits only for its gold and core parts.</summary>
        public static bool Full(RobotPart p) => !RobotParts.IsMaxed(p) && Points(p) >= Need(p);

        /// <summary>The points over the bar ("+35"), carried to the next level.</summary>
        public static int Over(RobotPart p) => Full(p) ? Points(p) - Need(p) : 0;

        public static void Add(RobotPart p, int points)
        {
            if (points <= 0) return;
            Prefs.SetInt(Key(p), Points(p) + points);
            Prefs.Save();
        }

        /// <summary>The level up took the bar's points (the rest carries).</summary>
        internal static void Take(RobotPart p, int points) => Prefs.SetInt(Key(p), Mathf.Max(0, Points(p) - points));
    }
}
