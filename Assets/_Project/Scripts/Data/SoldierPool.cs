using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The army's soldiers the duels (and later the floors) draw on: thirty in each group. A soldier who falls is gone
    /// from his group for twenty minutes (counted from his own fall), then he is back. A duel takes up to five of each group
    /// from what is there; a group with none left stays at home.
    /// </summary>
    public static class SoldierPool
    {
        /// <summary>How long a fallen soldier is away.</summary>
        public static readonly TimeSpan Away = TimeSpan.FromMinutes(20);

        /// <summary>Soldiers of each group a duel takes (all of them charge from the start).</summary>
        public const int PerDuel = 5;

        private const string Key = "sb_pool_";

        /// <summary>When each fallen soldier of a group comes back (UTC ticks), the ones still away.</summary>
        private static List<long> Returns(int group)
        {
            var list = new List<long>();
            long now = DateTime.UtcNow.Ticks;
            foreach (var part in Prefs.GetString(Key + group, "").Split(','))
                if (long.TryParse(part, out long t) && t > now && t - now <= Away.Ticks + TimeSpan.FromMinutes(5).Ticks) list.Add(t);
            return list;
        }

        private static void Save(int group, List<long> list)
        {
            Prefs.SetString(Key + group, string.Join(",", list));
            Prefs.Save();
        }

        /// <summary>Soldiers of a group at home now.</summary>
        public static int Available(int group) => Mathf.Max(0, Army.Soldiers - Returns(group).Count);

        /// <summary>Soldiers a duel can take from a group (at most five).</summary>
        public static int ForDuel(int group) => Mathf.Min(PerDuel, Available(group));

        /// <summary>A soldier of a group fell: away for twenty minutes.</summary>
        public static void Lose(int group)
        {
            if (group < 0 || group >= Army.Groups) return;
            var list = Returns(group);
            if (list.Count >= Army.Soldiers) return;
            list.Add((DateTime.UtcNow + Away).Ticks);
            Save(group, list);
        }

    }
}
