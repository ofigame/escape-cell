using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The challenge mode's own record (kept on the phone until the online layer comes): the player's name, trophies and
    /// the highest rank reached, and the opponents. An opponent is a name, a rank, six groups with their development
    /// points and foi's strength; for now they are bots made to match the player (trophies and army power close).
    /// </summary>
    public static class Challenge
    {
        private const string NameKey = "sb_ch_name", TrophyKey = "sb_ch_trophies", RankKey = "sb_ch_rank",
            WinsKey = "sb_ch_wins", PlayedKey = "sb_ch_played";

        /// <summary>Opens once the first world is finished (its development points are the army's first).</summary>
        public static bool Open => SaveData.UnlockedLevel >= LevelCatalog.LevelsPerWorld || SaveData.TestMode;

        public static string PlayerName
        {
            get
            {
                string n = PlayerPrefs.GetString(NameKey, "");
                if (n.Length > 0) return n;
                n = "foi" + Random.Range(1000, 10000);
                PlayerPrefs.SetString(NameKey, n);
                PlayerPrefs.Save();
                return n;
            }
            set { PlayerPrefs.SetString(NameKey, value); PlayerPrefs.Save(); }
        }

        public static int Trophies => PlayerPrefs.GetInt(TrophyKey, 0);
        public static int Wins => PlayerPrefs.GetInt(WinsKey, 0);
        public static int Played => PlayerPrefs.GetInt(PlayedKey, 0);

        /// <summary>Trophies needed for each rank (Private → Marshal). A rank once reached is kept.</summary>
        public static readonly int[] RankAt = { 0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700, 3250, 3850, 4500 };

        public static int RankFor(int trophies)
        {
            int r = 0;
            for (int i = 0; i < RankAt.Length; i++) if (trophies >= RankAt[i]) r = i;
            return r;
        }

        /// <summary>The player's rank: the highest reached (trophies may drop, the rank stays).</summary>
        public static int Rank => Mathf.Max(PlayerPrefs.GetInt(RankKey, 0), RankFor(Trophies));

        public static string RankName(int rank) => Loc.T("rank." + Mathf.Clamp(rank, 0, RankAt.Length - 1));

        /// <summary>Trophies a result is worth: a win +30 (more against a stronger foe), a loss -20.</summary>
        public static int TrophyChange(bool won, int opponentTrophies)
        {
            int diff = Mathf.Clamp((opponentTrophies - Trophies) / 25, -8, 8);
            return won ? 30 + diff : -Mathf.Max(10, 20 + diff);
        }

        /// <summary>Records a challenge: trophies (never below zero), wins, the rank reached. Returns the change.</summary>
        public static int Record(bool won, int opponentTrophies)
        {
            int change = TrophyChange(won, opponentTrophies);
            int t = Mathf.Max(0, Trophies + change);
            PlayerPrefs.SetInt(TrophyKey, t);
            PlayerPrefs.SetInt(PlayedKey, Played + 1);
            if (won) PlayerPrefs.SetInt(WinsKey, Wins + 1);
            PlayerPrefs.SetInt(RankKey, Mathf.Max(PlayerPrefs.GetInt(RankKey, 0), RankFor(t)));
            PlayerPrefs.Save();
            return change;
        }

        /// <summary>Gold a win brings (more the higher the rank).</summary>
        public static int WinGold => 80 + Rank * 20;

        // ---------- Opponents ----------

        public class Opponent
        {
            public string name;
            public int trophies;
            public readonly int[] points = new int[Army.Groups];
            /// <summary>foi's health in the duel (blows it takes) and how hard it hits (share of foi's health).</summary>
            public int foiHp;
            public float foiHit;
            /// <summary>How it picks its groups: 0 saves its strongest for last … 1 sends it first.</summary>
            public float boldness;
            public bool bot;

            public float Power(int g) => 1f + points[g] * Army.PerPoint;
            public int Rank => RankFor(trophies);
        }

        private static readonly string[] BotNames =
        {
            "GeceKartali", "DemirYumruk", "KumFirtinasi", "BuzKurdu", "AtesTopu", "YildizAvcisi", "GolgeSavasci", "KaplanPence",
            "SteelFox", "NightHawk", "IronMole", "SkyRider", "RustKnight", "PixelSamurai", "BoltRunner", "MintViper",
            "LunaBlade", "RoboRex", "TurboTaco", "CometKid", "NovaWolf", "ByteBear", "ZapZebra", "GearGhost",
        };

        /// <summary>A bot shaped like the player: trophies close, the same development points spread its own way.</summary>
        public static Opponent MakeBot(int seed, int playerPoints, int world)
        {
            var rng = new System.Random(seed);
            var o = new Opponent { bot = true };
            o.name = BotNames[rng.Next(BotNames.Length)] + (rng.Next(3) == 0 ? rng.Next(10, 99).ToString() : "");
            o.trophies = Mathf.Max(0, Trophies + rng.Next(-120, 121));
            int pts = Mathf.Max(0, Mathf.RoundToInt(playerPoints * (0.85f + (float)rng.NextDouble() * 0.3f)));
            // Spread: a favourite group or two, the rest across.
            int fav = rng.Next(Army.Groups);
            int left = pts;
            int toFav = Mathf.RoundToInt(pts * (0.2f + (float)rng.NextDouble() * 0.3f));
            o.points[fav] += toFav;
            left -= toFav;
            while (left > 0)
            {
                int g = rng.Next(Army.Groups);
                int n = Mathf.Min(left, 1 + rng.Next(5));
                o.points[g] += n;
                left -= n;
            }
            o.foiHp = 18 + world * 3;
            o.foiHit = Mathf.Lerp(0.09f, 0.14f, world / 24f);
            o.boldness = (float)rng.NextDouble();
            return o;
        }
    }
}
