using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BrickMuseum.Data
{
    /// <summary>The game's languages, in the column order of Resources/loc.txt.</summary>
    public enum Language { Turkish, English, Japanese, Arabic, German, French, Italian, Spanish, Korean, Azerbaijani }

    /// <summary>
    /// Translation. The game is written in Turkish and every text is translated as it is drawn (see
    /// <see cref="UI.LocPreprocessor"/>): the text is cut into lines and " · " parts, each part is looked up in
    /// Resources/loc.txt (Turkish → nine languages) as it is, then against the table's patterns ("{0} yıldız"), whose
    /// filled-in parts are translated in turn (a model's name inside "{0} tamamlandı!"). Unknown text stays as it is.
    /// </summary>
    public static class Loc
    {
        private const string PrefKey = "bm_lang";
        private static Language? current;
        private static Dictionary<string, string[]> exact, lower;
        private static List<(Regex re, string[] values, int literal)> patterns;
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        public static event Action Changed;

        public static readonly Language[] Order =
        {
            Language.Turkish, Language.English, Language.Japanese, Language.Arabic, Language.German,
            Language.French, Language.Italian, Language.Spanish, Language.Korean, Language.Azerbaijani,
        };

        /// <summary>Each language's own name, for the language list.</summary>
        public static string NameOf(Language l)
        {
            switch (l)
            {
                case Language.Turkish: return "Türkçe";
                case Language.English: return "English";
                case Language.Japanese: return "日本語";
                case Language.Arabic: return "العربية";
                case Language.German: return "Deutsch";
                case Language.French: return "Français";
                case Language.Italian: return "Italiano";
                case Language.Spanish: return "Español";
                case Language.Korean: return "한국어";
                default: return "Azərbaycanca";
            }
        }

        public static Language Current
        {
            get
            {
                if (current.HasValue) return current.Value;
                var saved = PlayerPrefs.GetString(PrefKey, "");
                current = Enum.TryParse(saved, out Language l) ? l : SystemDefault;
                return current.Value;
            }
        }

        public static bool IsRightToLeft => Current == Language.Arabic;

        public static void Set(Language l)
        {
            current = l;
            PlayerPrefs.SetString(PrefKey, l.ToString());
            PlayerPrefs.Save();
            cache.Clear();
            Changed?.Invoke();
        }

        /// <summary>The device's language when the game speaks it, else English.</summary>
        public static Language SystemDefault
        {
            get
            {
                switch (Application.systemLanguage)
                {
                    case SystemLanguage.Turkish: return Language.Turkish;
                    case SystemLanguage.Japanese: return Language.Japanese;
                    case SystemLanguage.Arabic: return Language.Arabic;
                    case SystemLanguage.German: return Language.German;
                    case SystemLanguage.French: return Language.French;
                    case SystemLanguage.Italian: return Language.Italian;
                    case SystemLanguage.Spanish: return Language.Spanish;
                    case SystemLanguage.Korean: return Language.Korean;
                }
                try
                {
                    if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "az") return Language.Azerbaijani;
                }
                catch { }
                return Language.English;
            }
        }

        // ---------- the table ----------

        private static void Load()
        {
            exact = new Dictionary<string, string[]>();
            lower = new Dictionary<string, string[]>();
            patterns = new List<(Regex, string[], int)>();
            var asset = Resources.Load<TextAsset>("loc");
            if (asset == null) return;
            var lines = asset.text.Replace("\r", "").Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split('\t');
                if (cols.Length < 10 || cols[0].Length == 0) continue;
                exact[cols[0]] = cols;
                lower[cols[0].ToLower(Tr)] = cols;
                if (cols[0].Contains("{0}"))
                {
                    // "{0} yıldız" → ^(.+?) yıldız$ ; the holes are numbered in the order they appear
                    var parts = Regex.Split(cols[0], @"\{\d\}");
                    var sb = new StringBuilder("^");
                    int literal = 0;
                    for (int p = 0; p < parts.Length; p++)
                    {
                        if (p > 0) sb.Append("(.+?)");
                        sb.Append(Regex.Escape(parts[p]));
                        literal += parts[p].Length;
                    }
                    sb.Append('$');
                    patterns.Add((new Regex(sb.ToString(), RegexOptions.CultureInvariant), cols, literal));
                }
            }
            patterns.Sort((a, b) => b.literal.CompareTo(a.literal));   // the most specific pattern first
        }

        /// <summary>A whole text as shown on screen, in the current language.</summary>
        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text) || Current == Language.Turkish) return text;
            if (cache.TryGetValue(text, out var done)) return done;
            if (exact == null) Load();
            var sb = new StringBuilder(text.Length + 16);
            var lines = text.Split('\n');
            for (int l = 0; l < lines.Length; l++)
            {
                if (l > 0) sb.Append('\n');
                // keep the " · " separators as they are, translate what is between them
                foreach (var part in Regex.Split(lines[l], @"(\s*·\s*)"))
                    sb.Append(part.Contains("·") ? part : Segment(part, 0));
            }
            var result = sb.ToString();
            if (cache.Count > 4000) cache.Clear();
            cache[text] = result;
            return result;
        }

        private static string Segment(string s, int depth)
        {
            var t = s.Trim();
            if (t.Length == 0 || depth > 3 || !HasLetters(t)) return s;
            int col = (int)Current;
            string lead = s.Substring(0, s.IndexOf(t[0])), trail = s.Substring(s.IndexOf(t[0]) + t.Length);
            if (exact.TryGetValue(t, out var row) || lower.TryGetValue(t.ToLower(Tr), out row)) return lead + Pick(row, col, t) + trail;
            foreach (var (re, values, _) in patterns)
            {
                var m = re.Match(t);
                if (!m.Success) continue;
                var args = new object[m.Groups.Count - 1];
                for (int g = 1; g < m.Groups.Count; g++) args[g - 1] = Segment(m.Groups[g].Value, depth + 1);
                try { return lead + string.Format(Pick(values, col, values[0]), args) + trail; }
                catch (FormatException) { return s; }
            }
            return s;
        }

        private static string Pick(string[] row, int col, string fallback) => col < row.Length && row[col].Length > 0 ? row[col] : fallback;

        private static bool HasLetters(string s) { foreach (char c in s) if (char.IsLetter(c)) return true; return false; }
    }
}
