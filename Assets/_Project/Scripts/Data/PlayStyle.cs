using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// How the player fights, recorded over its duels (the friend copy, §5.5 steps 2–3): blows a second, shields a minute,
    /// the health at which it takes a heart, the distance it keeps, combo breakers and jumps a minute, and the rival's
    /// health when it spends its ultimate. Kept as slow-moving averages, sent with the profile as seven numbers (0..100)
    /// so a friend's copy of this foi fights the way the player does — its personality chosen from them too.
    /// </summary>
    public static class PlayStyle
    {
        private const string Key = "sb_style2";
        private const int Fields = 7;
        private static readonly int[] Defaults = { 55, 40, 35, 40, 30, 30, 50 };

        private static int blows, shields, hearts, breakers, jumps, ultis, samples;
        private static float heartAt, distance, ultiAt;

        public static void Blow() => blows++;
        public static void Shield() => shields++;
        public static void Heart(float health) { hearts++; heartAt += health; }
        public static void Breaker() => breakers++;
        public static void Jump() => jumps++;
        public static void Ulti(float rivalHealth) { ultis++; ultiAt += rivalHealth; }

        /// <summary>Where the player stood from the rival (tiles), sampled now and then.</summary>
        public static void Sample(int tiles) { samples++; distance += tiles; }

        private static void Clear() { blows = shields = hearts = breakers = jumps = ultis = samples = 0; heartAt = distance = ultiAt = 0f; }

        /// <summary>A duel is over (its seconds of fighting): its numbers go into the averages.</summary>
        public static void EndDuel(float seconds)
        {
            if (seconds < 20f) { Clear(); return; }
            var v = Read();
            float minutes = seconds / 60f;
            float?[] now =
            {
                Mathf.Clamp01(blows / seconds / 3f),
                Mathf.Clamp01(shields / minutes / 1.5f),
                hearts > 0 ? Mathf.Clamp01(heartAt / hearts) : (float?)null,
                samples > 0 ? Mathf.Clamp01((distance / samples - 2f) / 4f) : (float?)null,
                Mathf.Clamp01(breakers / minutes / 2f),
                Mathf.Clamp01(jumps / minutes / 6f),
                ultis > 0 ? Mathf.Clamp01(ultiAt / ultis) : (float?)null,
            };
            const float k = 0.25f; // (each duel a quarter of the average)
            for (int i = 0; i < Fields; i++) if (now[i].HasValue) v[i] = Mathf.RoundToInt(Mathf.Lerp(v[i], now[i].Value * 100f, k));
            Prefs.SetString(Key, string.Join(",", v));
            Prefs.Save();
            Clear();
        }

        private static int[] Read() => Parse(Prefs.GetString(Key, ""));

        /// <summary>The seven numbers (older profiles sent three: the rest take their usual values).</summary>
        public static int[] Parse(string code)
        {
            var p = (code ?? "").Split(',');
            var v = (int[])Defaults.Clone();
            for (int i = 0; i < Fields && i < p.Length; i++) if (int.TryParse(p[i], out int x)) v[i] = Mathf.Clamp(x, 0, 100);
            return v;
        }

        public static string Code => string.Join(",", Read());

        /// <summary>
        /// A copy's tuning from its player's style: how much it presses and shields, when it takes a heart, the distance it
        /// keeps, how often it breaks combos and jumps — and the personality that fits (it spends its ultimate like them).
        /// </summary>
        public static void Apply(BotTuning t, string code)
        {
            if (t == null || string.IsNullOrEmpty(code)) return;
            var v = Parse(code);
            t.aggression = v[0] / 100f;
            t.shield = v[1] / 100f;
            t.earlyHeart = v[2] >= 50 ? 0.8f : v[2] >= 35 ? 0.3f : 0.05f;
            t.flee = Mathf.Clamp01(v[3] / 100f * 0.8f + (1f - t.aggression) * 0.2f);
            t.breaker = v[4] / 100f;
            t.jump = v[5] / 100f;
            // the personality that plays like them
            t.persona = v[6] <= 35 ? Persona.Ulti : t.flee >= 0.6f ? Persona.Runner : t.breaker >= 0.6f ? Persona.Defensive : t.aggression >= 0.75f ? Persona.Aggressive : Persona.Combo;
        }
    }
}
