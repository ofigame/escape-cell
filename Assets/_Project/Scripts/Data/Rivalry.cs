using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The score with each friend (the scenario's §10.2): every friend match and copy duel counts, shown on the friends list
    /// ("with Ahmet: 7 – 5"); a loss offers revenge. Kept on the phone, by the friend's name.
    /// </summary>
    public static class Rivalry
    {
        private static string Key(string name) => "sb_riv_" + (name ?? "").ToLowerInvariant();

        public static (int wins, int losses) Of(string name)
        {
            var p = Prefs.GetString(Key(name), "0-0").Split('-');
            int V(int i) => p.Length == 2 && int.TryParse(p[i], out int v) ? v : 0;
            return (V(0), V(1));
        }

        public static void Record(string name, bool won, bool draw)
        {
            if (string.IsNullOrEmpty(name) || draw) return;
            var (w, l) = Of(name);
            Prefs.SetString(Key(name), won ? (w + 1) + "-" + l : w + "-" + (l + 1));
            Prefs.Save();
        }

        public static string Text(string name)
        {
            var (w, l) = Of(name);
            return w + l == 0 ? "" : Loc.F("riv.score", w, l);
        }
    }
}
