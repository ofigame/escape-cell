using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// foi's battle wear (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 7): ranked losses since the last repair leave
    /// scratches (2), wear (4) and rust (6+) on its steel, three steps at most. A look only: no strength, matchmaking or
    /// reward ever depends on it. Repaired by ranked wins, repair potions and Bip's daily service; "keep the battle
    /// wear" stops the win and Bip from repairing it (the potions still do, by hand). The rival sees at most step 2.
    /// </summary>
    public static class BattleWear
    {
        public const int MaxLevel = 3;
        /// <summary>Losses per step (2: scratched, 4: worn, 6: rusty).</summary>
        public const int LossesPerStep = 2;

        private const string LossesKey = "sb_wear_losses", LevelKey = "sb_wear_level", KeepKey = "sb_wear_keep", ServiceKey = "sb_bip_service_day";

        /// <summary>The wear's step: 0 shiny, 1 scratched, 2 worn, 3 rusty.</summary>
        public static int Level => Mathf.Clamp(Prefs.GetInt(LevelKey, 0), 0, MaxLevel);

        /// <summary>The step the rival sees: rust shows as wear (only the player's own screen rusts).</summary>
        public static int Shown => Mathf.Min(Level, 2);

        /// <summary>Ranked losses counted since the last full repair.</summary>
        public static int Losses => Mathf.Max(0, Prefs.GetInt(LossesKey, 0));

        /// <summary>"Keep the battle wear": wins and Bip leave it on.</summary>
        public static bool Keep
        {
            get => Prefs.GetInt(KeepKey, 0) == 1;
            set { Prefs.SetInt(KeepKey, value ? 1 : 0); Prefs.Save(); }
        }

        /// <summary>A ranked duel ended (the others never touch the wear): a loss counts, a win repairs a step (not when
        /// kept). Returns the step before.</summary>
        public static int OnRankedEnd(bool won, bool draw)
        {
            int was = Level;
            if (won) { if (!Keep) Repair(1); }
            else if (!draw)
            {
                int losses = Losses + 1;
                Set(losses, Mathf.Min(MaxLevel, Mathf.Max(was, losses / LossesPerStep)));
            }
            return was;
        }

        /// <summary>Takes <paramref name="steps"/> steps of wear off (the losses' count with it); the steps taken.</summary>
        public static int Repair(int steps)
        {
            int was = Level;
            int now = Mathf.Max(0, was - Mathf.Max(0, steps));
            Set(Mathf.Min(Losses, now * LossesPerStep), now);
            return was - now;
        }

        /// <summary>Bip's daily service is waiting: once a day, free, a step off (not while the wear is kept).</summary>
        public static bool ServiceReady => Level > 0 && !Keep && Prefs.GetString(ServiceKey, "") != GameClock.Today;

        /// <summary>Bip's daily service: a step off (true when done).</summary>
        public static bool Service()
        {
            if (!ServiceReady) return false;
            Prefs.SetString(ServiceKey, GameClock.Today);
            Repair(1);
            return true;
        }

        /// <summary>Sets the losses' count and the step (kept together).</summary>
        internal static void Set(int losses, int level)
        {
            Prefs.SetInt(LossesKey, Mathf.Max(0, losses));
            Prefs.SetInt(LevelKey, Mathf.Clamp(level, 0, MaxLevel));
            Prefs.Save();
        }

        /// <summary>A rival's step from its look ("wear2" among its pieces); a computer rival's from its seed (0–2).</summary>
        public static int OfRival(string look, int seed, bool bot)
        {
            if (bot) return Mathf.Abs(seed) % 3;
            if (string.IsNullOrEmpty(look)) return 0;
            foreach (var part in look.Split(','))
                if (part.StartsWith("wear") && int.TryParse(part.Substring(4), out int n)) return Mathf.Clamp(n, 0, 2);
            return 0;
        }
    }
}
