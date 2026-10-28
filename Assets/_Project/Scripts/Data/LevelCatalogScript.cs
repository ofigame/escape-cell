using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using SquashBot.Core;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Turns the scenario document's level cards (<see cref="LevelScript"/>) into level data. Each card's difficulty
    /// score (1-100) sets the block tempo through the document's table (warning time, wave interval, blocks per wave,
    /// aiming, line and bomb chances), corrected per mission as the document asks; the map text gives the floor's
    /// shape and size; the goal text the numbers to reach; the hazard text the floor rules, the blocks and the moving
    /// enemies. Runs in the editor when the level set is built.
    /// </summary>
    public static partial class LevelCatalog
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        // The document's difficulty table: score → warning time, wave interval, blocks per wave, aiming, lines, bombs.
        private static readonly float[] ScoreKeys = { 5, 15, 25, 40, 55, 70, 85, 100 };
        private static readonly float[] WarnKeys = { 1.62f, 1.54f, 1.46f, 1.37f, 1.28f, 1.19f, 1.11f, 1.03f };
        private static readonly float[] IntervalKeys = { 2.1f, 1.96f, 1.82f, 1.66f, 1.52f, 1.39f, 1.27f, 1.16f };
        private static readonly float[] PerWaveKeys = { 1, 1, 2, 2, 3, 3, 4, 4 };
        private static readonly float[] AimKeys = { 0.18f, 0.22f, 0.25f, 0.28f, 0.31f, 0.34f, 0.37f, 0.4f };
        private static readonly float[] LineKeys = { 0f, 0.02f, 0.06f, 0.12f, 0.18f, 0.24f, 0.3f, 0.35f };
        private static readonly float[] BombKeys = { 0f, 0.03f, 0.06f, 0.09f, 0.12f, 0.15f, 0.18f, 0.22f };

        private static float Table(float[] values, float score)
        {
            if (score <= ScoreKeys[0]) return values[0];
            for (int i = 1; i < ScoreKeys.Length; i++)
                if (score <= ScoreKeys[i])
                    return Mathf.Lerp(values[i - 1], values[i], (score - ScoreKeys[i - 1]) / (ScoreKeys[i] - ScoreKeys[i - 1]));
            return values[values.Length - 1];
        }

        private static int Num(string text, string pattern, int fallback)
        {
            var m = Regex.Match(text, pattern);
            return m.Success && int.TryParse(m.Groups[1].Value, out int v) ? v : fallback;
        }

        private static bool Has(string text, params string[] words)
        {
            foreach (var w in words) if (text.Contains(w)) return true;
            return false;
        }

        /// <summary>A plain rectangle with a few stone pillars, never on the edge and never touching each other.</summary>
        private static string[] Rect(int w, int h, int pillars, int seed)
        {
            var grid = new char[h, w];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid[y, x] = '#';
            var rng = new System.Random(seed * 13 + 5);
            for (int attempt = 0; attempt < 200 && pillars > 0; attempt++)
            {
                int x = 1 + rng.Next(Mathf.Max(1, w - 2)), y = 1 + rng.Next(Mathf.Max(1, h - 2));
                bool free = true;
                for (int yy = y - 1; yy <= y + 1; yy++) for (int xx = x - 1; xx <= x + 1; xx++)
                    if (yy >= 0 && yy < h && xx >= 0 && xx < w && grid[yy, xx] == 'X') free = false;
                if (!free) continue;
                grid[y, x] = 'X';
                pillars--;
            }
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var chars = new char[w];
                for (int x = 0; x < w; x++) chars[x] = grid[y, x];
                rows[y] = new string(chars);
            }
            return rows;
        }

        /// <summary>The finale's floor: vanG's heart, a heart shape inside a <paramref name="size"/> square.</summary>
        private static string[] Heart(int size)
        {
            var rows = new string[size];
            for (int y = 0; y < size; y++)
            {
                var chars = new char[size];
                for (int x = 0; x < size; x++)
                {
                    // The classic heart curve (x² + y² − 1)³ − x²y³ ≤ 0, row 0 at the top (the far edge).
                    float fx = (x + 0.5f) / size * 2.6f - 1.3f, fy = 1.25f - (y + 0.5f) / size * 2.6f;
                    float a = fx * fx + fy * fy - 1f;
                    chars[x] = a * a * a - fx * fx * fy * fy * fy <= 0f ? '#' : '.';
                }
                rows[y] = new string(chars);
            }
            return rows;
        }
    }
}
