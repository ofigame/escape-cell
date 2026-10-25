using System.Collections.Generic;

namespace SquashBot.Data
{
    /// <summary>
    /// The titles a player has earned and the one shown on its card: the duel level's (10–50), a weapon family's master
    /// title (mastery 10), the weekend cup's champion, the training room's "Wall", and a world's conqueror (every star of
    /// its ten floors). The pick is kept (an id: "duel", "master.Sword", "champion", "wall", "world.3"); none picked, the
    /// duel level's title shows as before.
    /// </summary>
    public static class TitleBook
    {
        private const string PickKey = "sb_title_pick";

        /// <summary>A world's conqueror title earned (its 30 stars).</summary>
        public static bool HasWorld(int world) => Prefs.GetInt("sb_title_world" + world, 0) == 1;

        public static void GiveWorld(int world)
        {
            Prefs.SetInt("sb_title_world" + world, 1);
            Prefs.Save();
        }

        /// <summary>Every title earned, as ids (the duel level's first when there is one).</summary>
        public static List<string> Earned()
        {
            var list = new List<string>();
            if (DuelLevel.TitleKey(DuelLevel.Level) != null) list.Add("duel");
            foreach (WeaponFamily f in System.Enum.GetValues(typeof(WeaponFamily)))
                if (MasteredFamily(f)) list.Add("master." + f);
            if (Prefs.GetInt("sb_title_champion", 0) == 1) list.Add("champion");
            if (Prefs.GetInt("sb_title_title.wall", 0) == 1) list.Add("wall");
            for (int w = 0; w < LevelCatalog.WorldCount; w++) if (HasWorld(w)) list.Add("world." + w);
            return list;
        }

        /// <summary>A family mastered: a weapon of it at mastery 10.</summary>
        private static bool MasteredFamily(WeaponFamily f)
        {
            foreach (var w in Armory.All)
                if (w.kind != WeaponKind.Gun && Armory.Family(w) == f && Mastery.Level(w) >= 10) return true;
            return false;
        }

        /// <summary>The picked title's id (or the first earned one; null: none earned).</summary>
        public static string Picked
        {
            get
            {
                var earned = Earned();
                string pick = Prefs.GetString(PickKey, "");
                return earned.Contains(pick) ? pick : earned.Count > 0 ? earned[0] : null;
            }
        }

        /// <summary>The next earned title becomes the shown one (round again after the last). Returns its text.</summary>
        public static string PickNext()
        {
            var earned = Earned();
            if (earned.Count == 0) return null;
            int i = earned.IndexOf(Picked);
            string next = earned[(i + 1) % earned.Count];
            Prefs.SetString(PickKey, next);
            Prefs.Save();
            return Text(next);
        }

        /// <summary>The shown title's words (null: none).</summary>
        public static string Shown => Text(Picked);

        /// <summary>A title id's words in the current language.</summary>
        public static string Text(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id == "duel") { var k = DuelLevel.TitleKey(DuelLevel.Level); return k != null ? Loc.T(k) : null; }
            if (id == "champion") return Loc.T("title.champion");
            if (id == "wall") return Loc.T("title.wall");
            if (id.StartsWith("master.")) return Loc.T("title.master." + id.Substring(7));
            if (id.StartsWith("world.") && int.TryParse(id.Substring(6), out int w))
                return Loc.F("title.world", Loc.T(Visual.WorldTheme.ForWorld(w).key));
            return null;
        }
    }
}
