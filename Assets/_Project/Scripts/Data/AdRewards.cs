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
        /// <summary>Coins for one workshop ad.</summary>
        public const int ShopCoins = 50;
        /// <summary>Workshop ads per day.</summary>
        public const int ShopAdsPerDay = 5;

        private const string DayKey = "sb_ad_coins_day", CountKey = "sb_ad_coins_count";

        private static string Today => DateTime.Now.ToString("yyyyMMdd");

        /// <summary>Workshop coin ads left today.</summary>
        public static int ShopAdsLeft => PlayerPrefs.GetString(DayKey, "") == Today ? Mathf.Max(0, ShopAdsPerDay - PlayerPrefs.GetInt(CountKey, 0)) : ShopAdsPerDay;

        /// <summary>A workshop ad was watched to the end: the coins go in and today's count goes up.</summary>
        public static void GrantShopCoins()
        {
            int used = PlayerPrefs.GetString(DayKey, "") == Today ? PlayerPrefs.GetInt(CountKey, 0) : 0;
            PlayerPrefs.SetString(DayKey, Today);
            PlayerPrefs.SetInt(CountKey, used + 1);
            SaveData.Coins += ShopCoins;
            PlayerPrefs.Save();
        }
    }
}
