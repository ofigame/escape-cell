using System.Text;

namespace SquashBot.Data
{
    /// <summary>
    /// The items' analytics events (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 11): item_dropped, item_used,
    /// part_level_up, bag_opened, bag_unused_count, wear_changed, repair_used, wear_keep_toggled. The game has no
    /// analytics service, so each is counted on the phone (sb_ev_*, by its main value too) with the last few kept as a
    /// short line; they reach the server inside the cloud copy, as every other key does.
    /// </summary>
    public static class ItemEvents
    {
        private const string RecentKey = "sb_ev_recent";
        private const int RecentMax = 40;

        /// <summary>An event was counted (its name and its values, "item_used item_body x10 bulk").</summary>
        public static event System.Action<string> Logged;

        private static void Count(string name, string by = null)
        {
            Prefs.SetInt("sb_ev_" + name, Prefs.GetInt("sb_ev_" + name, 0) + 1);
            if (by != null) Prefs.SetInt("sb_ev_" + name + "_" + by, Prefs.GetInt("sb_ev_" + name + "_" + by, 0) + 1);
        }

        private static void Log(string line)
        {
            var lines = Prefs.GetString(RecentKey, "").Split('|');
            var sb = new StringBuilder(line);
            for (int i = 0; i < lines.Length && i < RecentMax - 1; i++) if (lines[i].Length > 0) sb.Append('|').Append(lines[i]);
            Prefs.SetString(RecentKey, sb.ToString());
            Logged?.Invoke(line);
        }

        /// <summary>The last events, the newest first.</summary>
        public static string[] Recent => Prefs.GetString(RecentKey, "").Split('|');

        public static int Total(string name) => Prefs.GetInt("sb_ev_" + name, 0);

        public static void Dropped(string source, ItemKind k, ItemRarity r, int n)
        {
            Count("item_dropped", source);
            Count("item_dropped_" + r.ToString().ToLowerInvariant());
            Log("item_dropped " + source + " " + ItemBag.Id(k) + " " + r + " x" + n);
        }

        public static void Used(ItemKind k, int n, bool bulk)
        {
            Count("item_used", ItemBag.Id(k));
            if (bulk) Count("item_used_bulk");
            Log("item_used " + ItemBag.Id(k) + " x" + n + (bulk ? " bulk" : ""));
        }

        public static void LevelUp(RobotPart p, int level)
        {
            Count("part_level_up", p.ToString().ToLowerInvariant());
            Log("part_level_up " + p + " " + level);
        }

        public static void BagOpened()
        {
            Count("bag_opened");
            Log("bag_opened " + ItemBag.Total);
        }

        /// <summary>At a session's start: how many items wait unused.</summary>
        public static void UnusedAtStart(int n)
        {
            Count("bag_unused_count");
            Prefs.SetInt("sb_ev_bag_unused_last", n);
            Prefs.SetInt("sb_ev_bag_unused_sum", Prefs.GetInt("sb_ev_bag_unused_sum", 0) + n);
            Log("bag_unused_count " + n);
        }

        /// <summary>The wear's step changed (reason: loss, win, potion, great_potion, bip).</summary>
        public static void WearChanged(int was, int now, string reason)
        {
            if (was == now) return;
            Count("wear_changed", reason);
            Log("wear_changed " + was + ">" + now + " " + reason);
        }

        /// <summary>A repair used (way: potion, great_potion, bip, win).</summary>
        public static void RepairUsed(string way)
        {
            Count("repair_used", way);
            Log("repair_used " + way);
        }

        public static void KeepToggled(bool on)
        {
            Count("wear_keep_toggled", on ? "on" : "off");
            Log("wear_keep_toggled " + (on ? "on" : "off"));
        }
    }
}
