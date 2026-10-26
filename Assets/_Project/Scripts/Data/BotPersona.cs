using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The six bot personalities (the scenario's §5.1), picked from the rival's seed: personality = seed mod 6.</summary>
    public enum Persona { Aggressive, Defensive, Combo, Runner, Ulti, Mimic }

    /// <summary>
    /// How a computer rival fights: its personality's leanings (attack, shield, combo breaker, running away, when it
    /// spends the ultimate) and how human it plays by rank (§5.2: reaction time, blows into the air, shields at the wrong
    /// moment, hearts used early, from which blow it answers a combo), eased or hardened by the dynamic difficulty (§5.3).
    /// </summary>
    public class BotTuning
    {
        public Persona persona;
        /// <summary>0..1 leanings: pressing on, raising the shield / parrying, breaking combos, getting away.</summary>
        public float aggression, shield, breaker, flee;
        /// <summary>Seconds it takes to react (a fresh draw each time from this range).</summary>
        public float reactMin, reactMax;
        /// <summary>Shares: blows that miss, shields at the wrong moment, a heart used early.</summary>
        public float whiff, badShield, earlyHeart;
        /// <summary>How often it jumps in for a blow from the air (0..1; a copy takes its player's).</summary>
        public float jump = 0.3f;
        /// <summary>The combo blow from which it tries to break the player's combo.</summary>
        public int breakerFrom;
        /// <summary>Its health against the usual (the first duels' bots are a little softer).</summary>
        public float hpScale = 1f;
        /// <summary>The defence (the design's §3.8): the chance to block a plain blow, to guard one perfectly, to dodge an
        /// ultimate, and how well it lets the shield go before it breaks (0..1).</summary>
        public float block, perfect, ultiDodge, release;
        /// <summary>How often it reads a high blow (a crouch) or a sweep (a jump) as it starts: Er 5% .. Mareşal 35%.</summary>
        public float read;

        public float React() => Random.Range(reactMin, reactMax);

        /// <summary>The personality from a seed.</summary>
        public static Persona Of(int seed) => (Persona)(Mathf.Abs(seed) % 6);

        /// <summary>
        /// A bot's tuning: its personality, the rank tier (1..5), the difficulty shift (negative easier: each step +60 ms
        /// reaction and 3% more misses; positive harder) and whether this is one of the player's first five duels.
        /// </summary>
        public static BotTuning For(Persona p, int tier, int shift, bool firstFive)
        {
            var t = new BotTuning { persona = p };
            switch (p)
            {
                case Persona.Aggressive: t.aggression = 0.95f; t.shield = 0.2f; t.breaker = 0.2f; t.flee = 0.05f; break;
                case Persona.Defensive: t.aggression = 0.25f; t.shield = 0.8f; t.breaker = 0.8f; t.flee = 0.45f; break;
                case Persona.Combo: t.aggression = 0.75f; t.shield = 0.5f; t.breaker = 0.5f; t.flee = 0.2f; break;
                case Persona.Runner: t.aggression = 0.5f; t.shield = 0.5f; t.breaker = 0.25f; t.flee = 0.8f; break;
                case Persona.Ulti: t.aggression = 0.5f; t.shield = 0.5f; t.breaker = 0.15f; t.flee = 0.45f; break;
                default: t.aggression = 0.55f; t.shield = 0.5f; t.breaker = 0.45f; t.flee = 0.35f; break; // the mimic: shifts as it watches
            }
            // how human, by rank (Er–Başçavuş, Asteğmen–Üsteğmen, Yüzbaşı–Yarbay, Albay–Mareşal)
            int band = tier <= 2 ? 0 : tier == 3 ? 1 : tier == 4 ? 2 : 3;
            float[] rMin = { 0.32f, 0.26f, 0.22f, 0.18f }, rMax = { 0.45f, 0.38f, 0.32f, 0.28f };
            float[] whiff = { 0.10f, 0.07f, 0.05f, 0.03f }, bad = { 0.15f, 0.10f, 0.07f, 0.04f }, early = { 0.20f, 0.12f, 0.08f, 0.05f };
            int[] from = { 4, 3, 3, 2 };
            t.reactMin = rMin[band]; t.reactMax = rMax[band];
            t.whiff = whiff[band]; t.badShield = bad[band]; t.earlyHeart = early[band];
            t.breakerFrom = from[band];
            float[] reads = { 0.05f, 0.15f, 0.25f, 0.35f };
            t.read = Mathf.Clamp01(reads[band] * (p == Persona.Defensive ? 1.5f : 1f));
            // the dynamic difficulty: each step easier +60 ms and 3% more misses (harder: the other way)
            t.reactMin = Mathf.Max(0.12f, t.reactMin - 0.06f * shift);
            t.reactMax = Mathf.Max(0.16f, t.reactMax - 0.06f * shift);
            t.whiff = Mathf.Clamp(t.whiff - 0.03f * shift, 0f, 0.3f);
            if (firstFive) { t.hpScale = 0.85f; t.reactMin += 0.15f; t.reactMax += 0.15f; } // the first five: the player should just win
            // the defence by rank, its personality (the defensive one more, the aggressive one less, the runner dodges more)
            // and the difficulty step
            var d = Combat.DefenseSettings.Current;
            float lean = p == Persona.Defensive ? d.defensiveTimes : p == Persona.Aggressive ? d.aggressiveTimes : 1f;
            float step = 1f + d.difficultyStep * shift;
            t.block = Mathf.Clamp01(d.botBlock[band] * lean * step);
            t.perfect = Mathf.Clamp01(d.botPerfect[band] * lean * step);
            t.ultiDodge = Mathf.Clamp01(d.botUltiDodge[band] * (p == Persona.Runner ? d.runnerDodgeTimes : 1f) * step);
            t.release = Mathf.Clamp01(d.botRelease[band] * step);
            if (firstFive) { t.block *= 0.6f; t.perfect *= 0.5f; t.ultiDodge *= 0.6f; }
            return t;
        }
    }

    /// <summary>
    /// The dynamic difficulty (§5.3): the player's bot results keep the win rate near 50–55%. Three losses in a row make the
    /// next bot a step easier, five wins in a row a step harder; over the last 20 bot duels above 65% a step harder for
    /// good, below 40% a step easier for good.
    /// </summary>
    public static class BotDifficulty
    {
        private const string LogKey = "sb_bot_log", CountKey = "sb_bot_count";

        /// <summary>The last 20 bot results, oldest first ('W', 'L' or 'D').</summary>
        private static string Log => Prefs.GetString(LogKey, "");

        public static int Played => Prefs.GetInt(CountKey, 0);

        public static bool FirstFive => Played < 5;

        public static void Record(bool won, bool draw)
        {
            string log = Log + (won ? "W" : draw ? "D" : "L");
            if (log.Length > 20) log = log.Substring(log.Length - 20);
            Prefs.SetString(LogKey, log);
            Prefs.SetInt(CountKey, Played + 1);
            Prefs.Save();
        }

        private static int Tail(char c)
        {
            string log = Log;
            int n = 0;
            for (int i = log.Length - 1; i >= 0 && log[i] == c; i--) n++;
            return n;
        }

        /// <summary>Losses in a row.</summary>
        public static int LossesInRow => Tail('L');

        /// <summary>Losses in a row that make the next bot a step easier.</summary>
        public const int LossesToEase = 3;

        /// <summary>The losing streak eases the next bot (the one condition the consolation line and <see cref="Shift"/> share).</summary>
        public static bool NextEasier => Tail('L') >= LossesToEase;

        /// <summary>The next bot's difficulty step: negative easier, positive harder.</summary>
        public static int Shift
        {
            get
            {
                int shift = 0;
                string log = Log;
                if (log.Length >= 20)
                {
                    int wins = 0;
                    foreach (char c in log) if (c == 'W') wins++;
                    float rate = wins / (float)log.Length;
                    if (rate > 0.65f) shift++;
                    else if (rate < 0.4f) shift--;
                }
                if (NextEasier) shift--;
                if (Tail('W') >= 5) shift++;
                return shift;
            }
        }
    }
}
