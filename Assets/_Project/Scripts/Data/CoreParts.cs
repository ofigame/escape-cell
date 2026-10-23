using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Çekirdek Parçası (core parts): the second currency, earned in duels, the login calendar and quests; spent on the
    /// robot's parts and the weapons' upper levels.
    /// </summary>
    public static class CoreParts
    {
        private const string Key = "sb_cores";

        public static int Count
        {
            get => Prefs.GetInt(Key, 0);
            set { Prefs.SetInt(Key, Mathf.Max(0, value)); Prefs.Save(); }
        }

        public static void Add(int n) { if (n > 0) Count += n; }

        /// <summary>Spends <paramref name="n"/> if there are that many (true), or nothing (false).</summary>
        public static bool Spend(int n)
        {
            if (n < 0 || Count < n) return false;
            Count -= n;
            return true;
        }
    }
}
