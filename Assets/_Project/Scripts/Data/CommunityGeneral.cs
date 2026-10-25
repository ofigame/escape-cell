using System;

namespace SquashBot.Data
{
    /// <summary>
    /// The Community General (the design's §11.2): the season's General every player fights together — each duel won by
    /// anyone is a point of damage on it (the server counts). At a quarter, a half, three quarters and the whole, everyone
    /// who fought a duel this season takes a reward (once each: the server keeps who took what): gold, a bronze chest, a
    /// gold chest, a cape of the season.
    /// </summary>
    public static class CommunityGeneral
    {
        public const int Steps = 4;

        /// <summary>The last the server said (null: not heard yet).</summary>
        public static FoiNet.GeneralState Last { get; set; }

        /// <summary>The next step reached and not taken (0: none), for a player who fought this season.</summary>
        public static int Waiting
        {
            get
            {
                var g = Last;
                if (g == null || !g.fought) return 0;
                for (int s = 1; s <= Math.Min(Steps, g.reached); s++)
                    if (g.taken == null || Array.IndexOf(g.taken, s) < 0) return s;
                return 0;
            }
        }

        /// <summary>The menu's line: everyone's damage on the General (millions written short).</summary>
        public static string Line()
        {
            var g = Last;
            if (g == null) return null;
            return Loc.F("gen.line", Loc.T("gen.name"), Short(g.damage), Short(g.goal));
        }

        public static float Share => Last == null || Last.goal <= 0 ? 0f : Math.Min(1f, Last.damage / (float)Last.goal);

        private static string Short(long n)
        {
            if (n >= 1000000) return (n / 1000000f).ToString(n >= 10000000 ? "0" : "0.#") + Loc.T("gen.million");
            if (n >= 10000) return (n / 1000f).ToString("0") + Loc.T("gen.thousand");
            return n.ToString("N0");
        }

        /// <summary>A step's reward handed out (after the server gave it): its words.</summary>
        public static string Give(int step)
        {
            switch (step)
            {
                case 1:
                    SaveData.Coins += 500;
                    return "+500 " + Loc.T("ch.gold");
                case 2:
                    DuelChests.Add(ChestTier.Mid);
                    return Loc.T("chests.tier1");
                case 3:
                    DuelChests.Add(ChestTier.Advanced);
                    return Loc.T("chests.tier2");
                default:
                {
                    // a cape of the season (one not owned yet), else gold
                    var capes = Cosmetics.InSlot(Slot.Back).FindAll(c => c.id.StartsWith("back.cape") && !c.IsDefault && !Cosmetics.Owns(c) && c.rank <= 0);
                    if (capes.Count > 0)
                    {
                        var c = capes[(DateTime.Now.Month * 11) % capes.Count];
                        Cosmetics.Give(c.id);
                        return Loc.T("cos." + c.id);
                    }
                    SaveData.Coins += 3000;
                    return "+3000 " + Loc.T("ch.gold");
                }
            }
        }
    }
}
