using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// How the player fights, measured over its duels (the friend copy, §5.5 step 2): blows a second, shields a minute and
    /// the health at which it takes a heart. Kept as slow-moving averages; sent with the profile as "aggression,shield,heart"
    /// (0..100 each) so a friend's copy of this foi fights the way the player does.
    /// </summary>
    public static class PlayStyle
    {
        private const string Key = "sb_style";
        private static int blows, shields, hearts;
        private static float heartAt;

        public static void Blow() => blows++;
        public static void Shield() => shields++;
        public static void Heart(float health) { hearts++; heartAt += health; }

        /// <summary>A duel is over (its seconds of fighting): its numbers go into the averages.</summary>
        public static void EndDuel(float seconds)
        {
            if (seconds < 20f) { blows = shields = hearts = 0; heartAt = 0f; return; }
            var (a, s, h) = Read();
            float aggression = Mathf.Clamp01(blows / seconds / 3f), shield = Mathf.Clamp01(shields / (seconds / 60f) / 1.5f);
            float heart = hearts > 0 ? Mathf.Clamp01(heartAt / hearts) : h / 100f;
            const float k = 0.25f; // (each duel a quarter of the average)
            a = Mathf.RoundToInt(Mathf.Lerp(a, aggression * 100f, k));
            s = Mathf.RoundToInt(Mathf.Lerp(s, shield * 100f, k));
            h = Mathf.RoundToInt(Mathf.Lerp(h, heart * 100f, k));
            Prefs.SetString(Key, a + "," + s + "," + h);
            Prefs.Save();
            blows = shields = hearts = 0;
            heartAt = 0f;
        }

        private static (int a, int s, int h) Read() => Parse(Prefs.GetString(Key, "55,40,35"));

        public static (int a, int s, int h) Parse(string code)
        {
            var p = (code ?? "").Split(',');
            int V(int i, int d) => p.Length == 3 && int.TryParse(p[i], out int v) ? Mathf.Clamp(v, 0, 100) : d;
            return (V(0, 55), V(1, 40), V(2, 35));
        }

        public static string Code { get { var (a, s, h) = Read(); return a + "," + s + "," + h; } }

        /// <summary>A copy's tuning from its player's style: how much it presses, shields, and when it takes a heart.</summary>
        public static void Apply(BotTuning t, string code)
        {
            if (t == null || string.IsNullOrEmpty(code)) return;
            var (a, s, h) = Parse(code);
            t.aggression = a / 100f;
            t.shield = s / 100f;
            t.flee = Mathf.Clamp01(0.6f - t.aggression * 0.5f);
            t.earlyHeart = h >= 50 ? 0.8f : h >= 35 ? 0.3f : 0.05f;
        }
    }
}
