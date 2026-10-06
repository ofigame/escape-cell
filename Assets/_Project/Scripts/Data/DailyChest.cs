using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>A free chest of coins once per calendar day (local time), a small reason to come back tomorrow.</summary>
    public static class DailyChest
    {
        private const string Key = "sb_daily_chest";

        private static string Today => DateTime.Now.ToString("yyyyMMdd");

        public static bool Ready => PlayerPrefs.GetString(Key, "") != Today;

        /// <summary>Opens today's chest: returns the coins it held (already added), or 0 if it was opened already.</summary>
        public static int Claim()
        {
            if (!Ready) return 0;
            int coins = UnityEngine.Random.Range(30, 81);
            PlayerPrefs.SetString(Key, Today);
            SaveData.Coins += coins;
            return coins;
        }
    }
}
