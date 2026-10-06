using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>A piece of the roof camp: bought once, it stands on its own tile of the 6x6 camp.</summary>
    public class CampDecor
    {
        public string id;
        public int price;
        public int x, y;
    }

    /// <summary>
    /// The roof camp: every WARDEN boss beaten (floors 2-20) frees a cellmate who moves into the camp on the roof.
    /// Coins build and decorate the camp; friends and decorations bring a daily harvest, and one friend can
    /// run alongside in the bonus tunnels, picking up the coins in its lane.
    /// </summary>
    public static class Camp
    {
        public const int Size = 6;
        public const int CoinsPerFriend = 4;
        public const int CoinsPerDecor = 2;

        /// <summary>The cellmates, in the order their floors' bosses free them.</summary>
        public static readonly string[] Friends =
        {
            "Bip", "Zip", "Tik", "Vin", "Pit", "Lop", "Fir", "Cit", "Dit", "Tin",
            "Bom", "Kivi", "Zap", "Pof", "Cik", "Fiu", "Nok", "Tuk", "Mavi",
        };

        public static readonly CampDecor[] Decor =
        {
            D("flag", 150, 0, 5), D("campfire", 200, 2, 2), D("lamps", 250, 5, 5), D("tent", 300, 1, 4),
            D("antenna", 400, 5, 0), D("hammock", 450, 4, 4), D("garden", 500, 0, 0), D("solar", 600, 4, 1),
            D("telescope", 800, 0, 2), D("fountain", 1000, 3, 0), D("statue", 1500, 5, 2),
        };

        private static CampDecor D(string id, int price, int x, int y) => new CampDecor { id = id, price = price, x = x, y = y };

        /// <summary>The boss level (0-based) whose win frees friend <paramref name="i"/>.</summary>
        public static int BossLevel(int i) => (i + 2) * LevelCatalog.LevelsPerWorld - 1;

        /// <summary>The friend freed by beating this level, or -1 when it is not a boss level.</summary>
        public static int FriendFreedBy(int levelIndex)
        {
            for (int i = 0; i < Friends.Length; i++)
                if (BossLevel(i) == levelIndex) return i;
            return -1;
        }

        /// <summary>Freed: its boss has been beaten (test builds free everyone, so the camp can be tried out).</summary>
        public static bool Rescued(int i) => SaveData.TestMode || Progress.Stars(BossLevel(i)) > 0;

        public static int RescuedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Friends.Length; i++) if (Rescued(i)) n++;
                return n;
            }
        }

        /// <summary>The camp opens with the first friend.</summary>
        public static bool Open => RescuedCount > 0;

        /// <summary>The world look a friend wears (the floor it was freed on).</summary>
        public static int FriendWorld(int i) => i + 1;

        // ---------- Decorations ----------

        public static bool Owns(CampDecor d) => PlayerPrefs.GetInt("sb_camp_" + d.id, 0) == 1;

        public static int DecorCount
        {
            get
            {
                int n = 0;
                foreach (var d in Decor) if (Owns(d)) n++;
                return n;
            }
        }

        public static bool TryBuy(CampDecor d)
        {
            if (Owns(d) || !Shop.Spend(d.price)) return false;
            PlayerPrefs.SetInt("sb_camp_" + d.id, 1);
            PlayerPrefs.Save();
            return true;
        }

        // ---------- Daily harvest ----------

        private const string HarvestKey = "sb_camp_harvest";
        private static string Today => DateTime.Now.ToString("yyyyMMdd");

        public static int DailyCoins => RescuedCount * CoinsPerFriend + DecorCount * CoinsPerDecor;

        public static bool CanHarvest => Open && PlayerPrefs.GetString(HarvestKey, "") != Today;

        /// <summary>Collects today's harvest (added to the coins); 0 when already collected today.</summary>
        public static int Harvest()
        {
            if (!CanHarvest) return 0;
            int coins = DailyCoins;
            PlayerPrefs.SetString(HarvestKey, Today);
            SaveData.Coins += coins;
            return coins;
        }

        // ---------- Tunnel companion ----------

        /// <summary>The friend who runs along in bonus tunnels (-1 = nobody).</summary>
        public static int Companion
        {
            get
            {
                int i = PlayerPrefs.GetInt("sb_camp_companion", -1);
                return i >= 0 && i < Friends.Length && Rescued(i) ? i : -1;
            }
            set { PlayerPrefs.SetInt("sb_camp_companion", value); PlayerPrefs.Save(); }
        }
    }
}
