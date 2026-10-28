using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The developer's blow log (section 3.4): the last ten blows' outcomes and why ("MISSED: distance 3.4 > 3", "BLOCKED:
    /// durability 64 → 54"), for debugging and as the test report's evidence. A fixed ring: no allocation per blow but the
    /// reason text the caller made.
    /// </summary>
    public static class HitLog
    {
        public const int Size = 10;
        private static readonly HitOutcome[] outcomes = new HitOutcome[Size];
        private static readonly string[] reasons = new string[Size];
        private static readonly float[] times = new float[Size];
        private static int next, count;

        /// <summary>Every outcome noted so far, by kind (the tests' tally).</summary>
        public static readonly int[] Totals = new int[System.Enum.GetValues(typeof(HitOutcome)).Length];

        /// <summary>A blow's outcome and its reason.</summary>
        public static void Add(HitOutcome outcome, string reason)
        {
            outcomes[next] = outcome; reasons[next] = reason; times[next] = Time.time;
            next = (next + 1) % Size;
            if (count < Size) count++;
            Totals[(int)outcome]++;
            Changed?.Invoke();
        }

        /// <summary>A blow was noted (the panel redraws).</summary>
        public static event System.Action Changed;

        /// <summary>The log as lines, newest first.</summary>
        public static string Text()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                int k = (next - 1 - i + Size) % Size;
                sb.Append(times[k].ToString("0.0")).Append("  ").Append(outcomes[k].ToString().ToUpperInvariant());
                if (!string.IsNullOrEmpty(reasons[k])) sb.Append(": ").Append(reasons[k]);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>The log emptied (a new duel).</summary>
        public static void Clear() { next = count = 0; for (int i = 0; i < Totals.Length; i++) Totals[i] = 0; Changed?.Invoke(); }
    }
}
