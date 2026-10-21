using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The challenge mode's record on the phone: the player's name, trophies and the highest rank reached (the server's,
    /// copied over after each online challenge; see <see cref="FoiNet"/>), and the opponents. An opponent is a name, a
    /// rank, six groups with their development points and foi's strength: a real player's army, or a bot made to match.
    /// </summary>
    public static class Challenge
    {
        private const string NameKey = "sb_ch_name", TrophyKey = "sb_ch_trophies", RankKey = "sb_ch_rank",
            WinsKey = "sb_ch_wins", PlayedKey = "sb_ch_played", ChosenKey = "sb_ch_named",
            DefWinsKey = "sb_ch_defwins", DefLossesKey = "sb_ch_deflosses", EnabledKey = "sb_ch_enabled";

        /// <summary>The defence record (the server's): attacks on the player's army held, and lost.</summary>
        public static int DefWins => Prefs.GetInt(DefWinsKey, 0);
        public static int DefLosses => Prefs.GetInt(DefLossesKey, 0);

        /// <summary>Challenges on (the default) or turned off in the settings: off, nobody fights the player's army and the mode is closed.</summary>
        public static bool Enabled
        {
            get => Prefs.GetInt(EnabledKey, 1) == 1;
            set { Prefs.SetInt(EnabledKey, value ? 1 : 0); Prefs.Save(); }
        }

        /// <summary>How the squad stands at a duel's start: 0 attack (a row in front, charging), 1 guard (a ring round foi,
        /// guarding it), 2 flanks (two wings at foi's sides, charging round the rival).</summary>
        public static int DuelFormation
        {
            get => Mathf.Clamp(Prefs.GetInt("sb_duel_formation", 0), 0, 2);
            set { Prefs.SetInt("sb_duel_formation", Mathf.Clamp(value, 0, 2)); Prefs.Save(); }
        }

        /// <summary>Duels with each side's squad behind its foi (true, the default) or foi against foi alone.</summary>
        public static bool DuelSquads
        {
            get => Prefs.GetInt("sb_duel_squads2", 0) == 1; // (foi against foi alone unless chosen; a new key: alone for everyone at first)
            set { Prefs.SetInt("sb_duel_squads2", value ? 1 : 0); Prefs.Save(); }
        }

        /// <summary>Whether the player has picked their name (asked on the first challenge).</summary>
        public static bool NameChosen
        {
            get => Prefs.GetInt(ChosenKey, 0) == 1;
            set { Prefs.SetInt(ChosenKey, value ? 1 : 0); Prefs.Save(); }
        }

        /// <summary>Why a name cannot be used here (null: it can): 3–16 letters, digits or _ (the server checks the rest).</summary>
        public static string NameProblem(string name)
        {
            if (name == null || name.Length < 3 || name.Length > 16) return "shape";
            foreach (char c in name) if (!char.IsLetterOrDigit(c) && c != '_') return "shape";
            return null;
        }

        /// <summary>Takes the server's record (it decides trophies; the rank reached is kept).</summary>
        private const string OtherKey = "sb_ch_other";
        private const string RenameKey = "sb_ch_renameat";

        /// <summary>Days until the name can be changed again (the server's word, last heard; 0: now).</summary>
        public static int RenameDays => long.TryParse(Prefs.GetString(RenameKey, "0"), out long t) ? Mathf.Max(0, Mathf.CeilToInt((float)new System.TimeSpan(t - System.DateTime.UtcNow.Ticks).TotalDays)) : 0;

        /// <summary>The record outside the ranked matches: won / played against the AI and against friends.</summary>
        public static (int aiWins, int aiPlayed, int friendWins, int friendPlayed) OtherRecord
        {
            get
            {
                var p = Prefs.GetString(OtherKey, "").Split('|');
                int V(int i) => p.Length == 4 && int.TryParse(p[i], out int v) ? v : 0;
                return (V(0), V(1), V(2), V(3));
            }
        }

        public static void Sync(FoiNet.Profile p)
        {
            if (p == null) return;
            Prefs.SetInt(TrophyKey, Mathf.Max(0, p.trophies));
            Prefs.SetInt(WinsKey, p.wins);
            Prefs.SetInt(PlayedKey, p.played);
            Prefs.SetInt(DefWinsKey, p.defWins);
            Prefs.SetInt(DefLossesKey, p.defLosses);
            Prefs.SetString(OtherKey, p.aiWins + "|" + p.aiPlayed + "|" + p.friendWins + "|" + p.friendPlayed);
            Prefs.SetInt(RankKey, Mathf.Max(Prefs.GetInt(RankKey, 0), p.rank));
            if (!string.IsNullOrEmpty(p.name)) Prefs.SetString(NameKey, p.name);
            Prefs.SetString(RenameKey, (System.DateTime.UtcNow.Ticks + System.TimeSpan.FromDays(p.renameDays).Ticks).ToString());
            Prefs.Save();
        }

        /// <summary>Forgets the challenge record and the name (the online profile was deleted).</summary>
        public static void ResetLocal()
        {
            foreach (var k in new[] { NameKey, TrophyKey, RankKey, WinsKey, PlayedKey, ChosenKey, DefWinsKey, DefLossesKey, OtherKey }) Prefs.DeleteKey(k);
            Prefs.Save();
        }

        /// <summary>foi's strength in the duel for a player who has reached <paramref name="world"/>.</summary>
        public static int FoiHpFor(int world) => 18 + world * 3;
        public static float FoiHitFor(int world) => Mathf.Lerp(0.09f, 0.14f, world / 24f);

        /// <summary>Opens once the first world is finished (its development points are the army's first).</summary>
        public static bool Open => SaveData.UnlockedLevel >= LevelCatalog.LevelsPerWorld || SaveData.TestMode;

        public static string PlayerName
        {
            get
            {
                string n = Prefs.GetString(NameKey, "");
                if (n.Length > 0) return n;
                n = "foi" + Random.Range(1000, 10000);
                Prefs.SetString(NameKey, n);
                Prefs.Save();
                return n;
            }
            set { Prefs.SetString(NameKey, value); Prefs.Save(); }
        }

        public static int Trophies => Prefs.GetInt(TrophyKey, 0);
        public static int Wins => Prefs.GetInt(WinsKey, 0);
        public static int Played => Prefs.GetInt(PlayedKey, 0);

        /// <summary>Trophies needed for each rank (Private → Marshal). A rank once reached is kept.</summary>
        public static readonly int[] RankAt = { 0, 150, 375, 675, 1050, 1500, 2025, 2625, 3300, 4050, 4875, 5775, 6750 };

        public static int RankFor(int trophies)
        {
            int r = 0;
            for (int i = 0; i < RankAt.Length; i++) if (trophies >= RankAt[i]) r = i;
            return r;
        }

        /// <summary>The player's rank: the highest reached (trophies may drop, the rank stays).</summary>
        public static int Rank => Mathf.Max(Prefs.GetInt(RankKey, 0), RankFor(Trophies));

        /// <summary>
        /// Both foi's health in a duel, times this by the match's level (the higher rank of the two): twice up to
        /// Başçavuş, five times up to Binbaşı, ten times above — so a strong weapon does not end a round in a few blows.
        /// As the server's Rules.DuelHpTimes.
        /// </summary>
        public static int DuelHpTimes(int rank) => rank <= 3 ? 2 : rank <= 8 ? 5 : 10;

        public static string RankName(int rank) => Loc.T("rank." + Mathf.Clamp(rank, 0, RankAt.Length - 1));

        /// <summary>The trophy league of the trophies held now (it falls with them, unlike the rank).</summary>
        public static int League => RankFor(Trophies);

        /// <summary>
        /// The whole army's strength from the challenges: 4% a rank step of the trophies held now — winning challenges
        /// raises it, losing enough trophies to drop a step lowers it again. (The floors' points are spread by hand.)
        /// </summary>
        public static float ArmyBonus => BonusFor(Trophies);
        public static float BonusFor(int trophies) => 0.04f * RankFor(trophies);

        /// <summary>
        /// foi's own strength from the rank reached (kept, like the rank), beside the workshop's weapons and armour: 2% a
        /// rank more damage and 2% a rank less taken — on the floors and in the duels.
        /// </summary>
        public static float FoiBonus => 0.02f * Rank;

        /// <summary>A blow's damage with the rank's strength (the fraction a chance of one more, so small blows gain too).</summary>
        public static int RankBoost(int damage)
        {
            float d = damage * (1f + FoiBonus);
            int whole = Mathf.FloorToInt(d);
            return whole + (Random.value < d - whole ? 1 : 0);
        }

        /// <summary>A share of health a blow takes, less the rank's strength.</summary>
        public static float RankGuard(float share) => share / (1f + FoiBonus);

        // ---------- foi's health points ----------

        /// <summary>
        /// foi's health in points: 100 at the start, one more for every floor won, 15 for every level of an upgrade
        /// bought in the workshop and 20 for every rank reached. Blows take their damage off it (a share of it on the bar).
        /// </summary>
        public static int FoiMaxHp => FoiHpFor(SaveData.UnlockedLevel, UpgradeLevels, Rank);

        public static int FoiHpFor(int floorsWon, int upgradeLevels, int rank) =>
            100 + Mathf.Clamp(floorsWon, 0, LevelCatalog.LevelCount) + 15 * Mathf.Max(0, upgradeLevels) + 20 * Mathf.Clamp(rank, 0, RankAt.Length - 1);

        /// <summary>Every level of every workshop upgrade bought.</summary>
        public static int UpgradeLevels
        {
            get
            {
                int n = 0;
                foreach (Upgrade u in System.Enum.GetValues(typeof(Upgrade))) n += Shop.Level(u);
                return n;
            }
        }
        public static string LeagueName(int league) => Loc.T("cup." + Mathf.Clamp(league, 0, RankAt.Length - 1));

        /// <summary>Trophies a result is worth: a win +20 (more against a stronger foe), a loss -15 (the server's rule).</summary>
        public static int TrophyChange(bool won, int opponentTrophies)
        {
            int diff = Mathf.Clamp((opponentTrophies - Trophies) / 30, -5, 5);
            return won ? 20 + diff : -Mathf.Max(8, 15 + diff);
        }

        /// <summary>Records a challenge: trophies (never below zero), wins, the rank reached. Returns the change.</summary>
        public static int Record(bool won, int opponentTrophies)
        {
            int change = TrophyChange(won, opponentTrophies);
            int t = Mathf.Max(0, Trophies + change);
            Prefs.SetInt(TrophyKey, t);
            Prefs.SetInt(PlayedKey, Played + 1);
            if (won) Prefs.SetInt(WinsKey, Wins + 1);
            Prefs.SetInt(RankKey, Mathf.Max(Prefs.GetInt(RankKey, 0), RankFor(t)));
            Prefs.Save();
            return change;
        }

        /// <summary>A drawn challenge (1-1): played, nothing gained or lost. Returns the change (none).</summary>
        public static int RecordDraw()
        {
            Prefs.SetInt(PlayedKey, Played + 1);
            Prefs.Save();
            return 0;
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
            /// <summary>Its foi's weapon (id), that weapon's level and its armour (0 none … 0.5).</summary>
            public string weapon = "mallet";
            public int weaponLevel = 1;
            public float armor;
            /// <summary>Blows its foi's shield takes before it breaks (0: none).</summary>
            public int shield;
            /// <summary>What its foi wears (a player's own outfit; null for a computer rival or an older game).</summary>
            public string look;

            /// <summary>Soldiers of each group it brings into the duel.</summary>
            public readonly int[] squad = { SoldierPool.PerDuel, SoldierPool.PerDuel, SoldierPool.PerDuel, SoldierPool.PerDuel, SoldierPool.PerDuel, SoldierPool.PerDuel };

            public float Power(int g) => 1f + points[g] * Army.PerPoint + BonusFor(trophies);
            public int Rank => RankFor(trophies);
        }

        /// <summary>A made-up user name, like a player's own: a few letters, a dot, digits and maybe letters ("asd.782ss").</summary>
        public static string BotName(System.Random rng)
        {
            const string letters = "abcdefghijklmnoprstuvyz", consonants = "bcdfgklmnprstvyz", vowels = "aeiou";
            var sb = new System.Text.StringBuilder();
            int head = 3 + rng.Next(4);
            bool pronounce = rng.Next(2) == 0;
            for (int i = 0; i < head; i++)
                sb.Append(pronounce ? (i % 2 == 0 ? consonants[rng.Next(consonants.Length)] : vowels[rng.Next(vowels.Length)]) : letters[rng.Next(letters.Length)]);
            int style = rng.Next(4);
            if (style == 3) return sb.Append(rng.Next(10, 1000)).ToString();
            sb.Append(style == 2 ? '_' : '.');
            int digits = 2 + rng.Next(3);
            for (int i = 0; i < digits; i++) sb.Append((char)('0' + rng.Next(10)));
            int tail = rng.Next(3);
            for (int i = 0; i < tail; i++) sb.Append(letters[rng.Next(letters.Length)]);
            return sb.ToString();
        }

        /// <summary>
        /// A virtual rival (no real player behind it): a made-up name, the player's own rank, and a little stronger than
        /// the player — the same weapon (sometimes a level up), a touch more armour, its soldiers a tenth stronger, two
        /// of every group.
        /// </summary>
        public static Opponent MakeBot(int seed, int trophies = -1)
        {
            var rng = new System.Random(seed);
            var o = new Opponent { bot = true };
            o.name = BotName(rng);
            o.trophies = trophies >= 0 ? trophies : Mathf.Max(0, Trophies + rng.Next(-60, 61));
            for (int g = 0; g < Army.Groups; g++) o.points[g] = Mathf.RoundToInt(Army.GroupPoints(g) * 1.1f) + 5;
            var w = Armory.Equipped;
            o.weapon = w.id;
            o.weaponLevel = Mathf.Min(Armory.MaxLevel, Armory.Level(w) + (rng.Next(10) < 4 ? 1 : 0));
            o.armor = Mathf.Min(0.5f, Shop.ArmorShare + 0.05f);
            o.boldness = (float)rng.NextDouble();
            o.foiHp = Mathf.RoundToInt(FoiMaxHp * 1.05f); // a touch tougher than the player
            o.foiHit = 0f;
            return o;
        }


        /// <summary>A live rival as the server sent it (its own game plays it).</summary>
        public static Opponent FromRival(FoiNet.Rival r)
        {
            var o = new Opponent { bot = false, name = r.name, trophies = Mathf.Max(0, r.trophies), look = r.look };
            var parts = (r.army ?? "").Split(',');
            for (int g = 0; g < Army.Groups && g < parts.Length; g++)
                o.points[g] = int.TryParse(parts[g], out int v) ? Mathf.Clamp(v, 0, 2500) : 0;
            o.foiHp = r.foiHp >= 100 ? Mathf.Clamp(r.foiHp, 100, 1500) : 100 + r.world * 10; // its health points (an old profile: about right for its world)
            o.foiHit = r.foiHit > 0f ? Mathf.Clamp(r.foiHit, 0.05f, 0.15f) : FoiHitFor(r.world);
            o.boldness = Mathf.Clamp01(r.boldness);
            var w = string.IsNullOrEmpty(r.weapon) ? null : Armory.All.Find(x => x.id == r.weapon && x.kind != WeaponKind.Gun);
            o.weapon = w != null ? w.id : "mallet";
            o.weaponLevel = Mathf.Clamp(r.weaponLevel, 1, Armory.MaxLevel);
            o.armor = Mathf.Clamp(r.armor, 0f, 0.1f * Shop.MaxLevel(Upgrade.Armor));
            o.shield = Mathf.Clamp(r.shield, 0, 3);
            return o;
        }
    }
}
