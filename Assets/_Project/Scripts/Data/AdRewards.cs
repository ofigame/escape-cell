using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Rewarded ads the player chooses outside the run's own offers: coins from the workshop (a few times a day), the
    /// daily chest doubled, and a health refill when it runs low in a level (see GameManager).
    /// </summary>
    public static class AdRewards
    {
        /// <summary>Coins for one workshop ad: they grow with the campaign (50 at the start, about 550 by level 100), so an
        /// ad is always worth a real step towards the next weapon or upgrade.</summary>
        public static int ShopCoins => 50 + 5 * Mathf.Min(SaveData.UnlockedLevel, 200);
        /// <summary>Workshop ads per day.</summary>
        public const int ShopAdsPerDay = 5;

        private const string DayKey = "sb_ad_coins_day", CountKey = "sb_ad_coins_count";

        private static string Today => GameClock.Today;

        /// <summary>Workshop coin ads left today.</summary>
        public static int ShopAdsLeft => Prefs.GetString(DayKey, "") == Today ? Mathf.Max(0, ShopAdsPerDay - Prefs.GetInt(CountKey, 0)) : ShopAdsPerDay;

        /// <summary>A workshop ad was watched to the end: the coins go in and today's count goes up.</summary>
        public static void GrantShopCoins()
        {
            int used = Prefs.GetString(DayKey, "") == Today ? Prefs.GetInt(CountKey, 0) : 0;
            Prefs.SetString(DayKey, Today);
            Prefs.SetInt(CountKey, used + 1);
            SaveData.Coins += ShopCoins;
            Prefs.Save();
        }
    }
}
