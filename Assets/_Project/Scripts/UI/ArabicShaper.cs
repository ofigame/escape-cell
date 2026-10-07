using System.Collections.Generic;
using System.Text;

namespace SquashBot.UI
{
    /// <summary>
    /// Arabic for a text renderer that only places glyphs side by side: picks each letter's joined form (isolated,
    /// initial, medial, final) from the Unicode presentation forms, merges lam + alef into their ligatures, and turns a
    /// logical (typed) line into visual order, right to left, keeping numbers and Latin words left to right.
    /// </summary>
    public static class ArabicShaper
    {
        // Presentation forms per letter: isolated, final, initial, medial (0 = the letter doesn't have that form).
        private static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            ['ء'] = new[] { 'ﺀ', '\0', '\0', '\0' },
            ['آ'] = new[] { 'ﺁ', 'ﺂ', '\0', '\0' },
            ['أ'] = new[] { 'ﺃ', 'ﺄ', '\0', '\0' },
            ['ؤ'] = new[] { 'ﺅ', 'ﺆ', '\0', '\0' },
            ['إ'] = new[] { 'ﺇ', 'ﺈ', '\0', '\0' },
            ['ئ'] = new[] { 'ﺉ', 'ﺊ', 'ﺋ', 'ﺌ' },
            ['ا'] = new[] { 'ﺍ', 'ﺎ', '\0', '\0' },
            ['ب'] = new[] { 'ﺏ', 'ﺐ', 'ﺑ', 'ﺒ' },
            ['ة'] = new[] { 'ﺓ', 'ﺔ', '\0', '\0' },
            ['ت'] = new[] { 'ﺕ', 'ﺖ', 'ﺗ', 'ﺘ' },
            ['ث'] = new[] { 'ﺙ', 'ﺚ', 'ﺛ', 'ﺜ' },
            ['ج'] = new[] { 'ﺝ', 'ﺞ', 'ﺟ', 'ﺠ' },
            ['ح'] = new[] { 'ﺡ', 'ﺢ', 'ﺣ', 'ﺤ' },
            ['خ'] = new[] { 'ﺥ', 'ﺦ', 'ﺧ', 'ﺨ' },
            ['د'] = new[] { 'ﺩ', 'ﺪ', '\0', '\0' },
            ['ذ'] = new[] { 'ﺫ', 'ﺬ', '\0', '\0' },
            ['ر'] = new[] { 'ﺭ', 'ﺮ', '\0', '\0' },
            ['ز'] = new[] { 'ﺯ', 'ﺰ', '\0', '\0' },
            ['س'] = new[] { 'ﺱ', 'ﺲ', 'ﺳ', 'ﺴ' },
            ['ش'] = new[] { 'ﺵ', 'ﺶ', 'ﺷ', 'ﺸ' },
            ['ص'] = new[] { 'ﺹ', 'ﺺ', 'ﺻ', 'ﺼ' },
            ['ض'] = new[] { 'ﺽ', 'ﺾ', 'ﺿ', 'ﻀ' },
            ['ط'] = new[] { 'ﻁ', 'ﻂ', 'ﻃ', 'ﻄ' },
            ['ظ'] = new[] { 'ﻅ', 'ﻆ', 'ﻇ', 'ﻈ' },
            ['ع'] = new[] { 'ﻉ', 'ﻊ', 'ﻋ', 'ﻌ' },
            ['غ'] = new[] { 'ﻍ', 'ﻎ', 'ﻏ', 'ﻐ' },
            ['ـ'] = new[] { 'ـ', 'ـ', 'ـ', 'ـ' },
            ['ف'] = new[] { 'ﻑ', 'ﻒ', 'ﻓ', 'ﻔ' },
            ['ق'] = new[] { 'ﻕ', 'ﻖ', 'ﻗ', 'ﻘ' },
            ['ك'] = new[] { 'ﻙ', 'ﻚ', 'ﻛ', 'ﻜ' },
            ['ل'] = new[] { 'ﻝ', 'ﻞ', 'ﻟ', 'ﻠ' },
            ['م'] = new[] { 'ﻡ', 'ﻢ', 'ﻣ', 'ﻤ' },
            ['ن'] = new[] { 'ﻥ', 'ﻦ', 'ﻧ', 'ﻨ' },
            ['ه'] = new[] { 'ﻩ', 'ﻪ', 'ﻫ', 'ﻬ' },
            ['و'] = new[] { 'ﻭ', 'ﻮ', '\0', '\0' },
            ['ى'] = new[] { 'ﻯ', 'ﻰ', '\0', '\0' },
            ['ي'] = new[] { 'ﻱ', 'ﻲ', 'ﻳ', 'ﻴ' },
        };

        // Lam followed by these alefs becomes one ligature: isolated, final.
        private static readonly Dictionary<char, char[]> LamAlef = new Dictionary<char, char[]>
        {
            ['آ'] = new[] { 'ﻵ', 'ﻶ' },
            ['أ'] = new[] { 'ﻷ', 'ﻸ' },
            ['إ'] = new[] { 'ﻹ', 'ﻺ' },
            ['ا'] = new[] { 'ﻻ', 'ﻼ' },
        };

        public static bool IsArabic(char c) => (c >= '؀' && c <= 'ۿ') || (c >= 'ﭐ' && c <= '﻿');

        public static bool HasArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (IsArabic(c)) return true;
            return false;
        }

        private static bool IsMark(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ';

        private static bool JoinsBoth(char c) => Forms.TryGetValue(c, out var f) && f[2] != '\0';

        private static bool Joins(char c) => Forms.ContainsKey(c);

        /// <summary>Letters swapped for their joined forms (still in logical order).</summary>
        public static string Shape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!Forms.ContainsKey(c))
                {
                    sb.Append(c);
                    continue;
                }
                char prev = Neighbour(s, i, -1), next = Neighbour(s, i, +1);
                bool joinPrev = JoinsBoth(prev);
                if (c == 'ل' && LamAlef.TryGetValue(next, out var lig))
                {
                    sb.Append(lig[joinPrev ? 1 : 0]);
                    // Skip the alef (and any marks before it).
                    int j = i + 1;
                    while (j < s.Length && IsMark(s[j])) sb.Append(s[j++]);
                    i = j;
                    continue;
                }
                bool joinNext = JoinsBoth(c) && Joins(next);
                var f = Forms[c];
                char form = joinPrev && joinNext ? f[3] : joinPrev ? f[1] : joinNext ? f[2] : f[0];
                sb.Append(form == '\0' ? f[0] : form);
            }
            return sb.ToString();
        }

        private static char Neighbour(string s, int i, int step)
        {
            for (int j = i + step; j >= 0 && j < s.Length; j += step)
                if (!IsMark(s[j])) return s[j];
            return '\0';
        }

        /// <summary>
        /// One line in visual order: the whole line runs right to left, while runs of digits and Latin letters keep
        /// their own left-to-right order. Brackets are mirrored.
        /// </summary>
        public static string ToVisual(string line)
        {
            var runs = new List<(string text, bool ltr)>();
            var cur = new StringBuilder();
            bool? ltr = null;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                bool strongLtr = char.IsDigit(c) || (c < 0x0590 && char.IsLetter(c));
                bool neutral = !strongLtr && !IsArabic(c);
                // Neutrals between two LTR characters (e.g. "3/5", "2.5") stay inside the LTR run.
                if (neutral && ltr == true && i + 1 < line.Length && (char.IsDigit(line[i + 1]) || (line[i + 1] < 0x0590 && char.IsLetter(line[i + 1])))
                    && c != ' ')
                {
                    cur.Append(c);
                    continue;
                }
                bool isLtr = strongLtr;
                if (ltr.HasValue && isLtr != ltr.Value)
                {
                    runs.Add((cur.ToString(), ltr.Value));
                    cur.Clear();
                }
                ltr = isLtr;
                cur.Append(c);
            }
            if (cur.Length > 0) runs.Add((cur.ToString(), ltr ?? false));

            var sb = new StringBuilder(line.Length);
            for (int r = runs.Count - 1; r >= 0; r--)
            {
                if (runs[r].ltr) sb.Append(runs[r].text);
                else
                    for (int k = runs[r].text.Length - 1; k >= 0; k--) sb.Append(Mirror(runs[r].text[k]));
            }
            return sb.ToString();
        }

        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '<': return '>';
                case '>': return '<';
                case '«': return '»';
                case '»': return '«';
                default: return c;
            }
        }
    }
}
