using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// foi's battle wear (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 7): ranked losses since the last repair leave
    /// scratches (2), wear (4) and rust (6+) on its steel, three steps at most. A look only: no strength, matchmaking or
    /// reward ever depends on it. Repaired by wins, repair potions and Bip's daily service.
    /// </summary>
    public static class BattleWear
    {
        public const int MaxLevel = 3;
        /// <summary>Losses per step (2: scratched, 4: worn, 6: rusty).</summary>
        public const int LossesPerStep = 2;

        private const string LossesKey = "sb_wear_losses", LevelKey = "sb_wear_level";

        /// <summary>The wear's step: 0 shiny, 1 scratched, 2 worn, 3 rusty.</summary>
        public static int Level => Mathf.Clamp(Prefs.GetInt(LevelKey, 0), 0, MaxLevel);

        /// <summary>Ranked losses counted since the last full repair.</summary>
        public static int Losses => Mathf.Max(0, Prefs.GetInt(LossesKey, 0));

        /// <summary>Takes <paramref name="steps"/> steps of wear off (the losses' count with it); the steps taken.</summary>
        public static int Repair(int steps)
        {
            int was = Level;
            int now = Mathf.Max(0, was - Mathf.Max(0, steps));
            Set(Mathf.Min(Losses, now * LossesPerStep), now);
            return was - now;
        }

        /// <summary>Sets the losses' count and the step (kept together).</summary>
        internal static void Set(int losses, int level)
        {
            Prefs.SetInt(LossesKey, Mathf.Max(0, losses));
            Prefs.SetInt(LevelKey, Mathf.Clamp(level, 0, MaxLevel));
            Prefs.Save();
        }
    }
}
