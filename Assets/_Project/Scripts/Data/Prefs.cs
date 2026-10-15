using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The game's saved values: PlayerPrefs underneath, plus a list of every key the game has used (and its kind), so the
    /// whole game can be copied to the server and brought back on a new phone (<see cref="CloudSave"/>). PlayerPrefs
    /// cannot list its keys, so the list is kept with them; keys read that already exist (from before this list) join
    /// it as they are read.
    /// </summary>
    public static class Prefs
    {
        private const string IndexKey = "sb_keyset";
        private static Dictionary<string, char> index;
        private static bool indexDirty;

        /// <summary>Something the copy holds changed since the copy was last sent.</summary>
        public static bool Dirty { get; set; }

        public static int GetInt(string key, int fallback = 0)
        {
            if (PlayerPrefs.HasKey(key)) Note(key, 'i');
            return PlayerPrefs.GetInt(key, fallback);
        }

        public static string GetString(string key, string fallback = "")
        {
            if (PlayerPrefs.HasKey(key)) Note(key, 's');
            return PlayerPrefs.GetString(key, fallback);
        }

        public static float GetFloat(string key, float fallback = 0f)
        {
            if (PlayerPrefs.HasKey(key)) Note(key, 'f');
            return PlayerPrefs.GetFloat(key, fallback);
        }

        public static void SetInt(string key, int value)
        {
            if (!PlayerPrefs.HasKey(key) || PlayerPrefs.GetInt(key) != value) Changed(key);
            PlayerPrefs.SetInt(key, value);
            Note(key, 'i');
        }

        public static void SetString(string key, string value)
        {
            if (!PlayerPrefs.HasKey(key) || PlayerPrefs.GetString(key) != value) Changed(key);
            PlayerPrefs.SetString(key, value);
            Note(key, 's');
        }

        public static void SetFloat(string key, float value)
        {
            if (!PlayerPrefs.HasKey(key) || !Mathf.Approximately(PlayerPrefs.GetFloat(key), value)) Changed(key);
            PlayerPrefs.SetFloat(key, value);
            Note(key, 'f');
        }

        public static bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public static void DeleteKey(string key)
        {
            if (PlayerPrefs.HasKey(key)) Changed(key);
            PlayerPrefs.DeleteKey(key);
            Load();
            if (index.Remove(key)) indexDirty = true;
        }

        public static void Save()
        {
            if (indexDirty) WriteIndex();
            PlayerPrefs.Save();
        }

        /// <summary>Keys that belong to this phone only and never go into the copy: who the player is to the server, the
        /// open world (it has a copy of its own), the copy's own bookkeeping, the list itself.</summary>
        public static bool Local(string key) =>
            key == IndexKey || key.StartsWith("sb_net_", StringComparison.Ordinal) || key.StartsWith("sb_cloud_", StringComparison.Ordinal)
            || key == "sb_ow" || key == "sb_test_mode" || key == "sb_gfx";

        /// <summary>Every key the copy holds, with its kind ('i', 's' or 'f').</summary>
        public static IEnumerable<KeyValuePair<string, char>> Keys
        {
            get
            {
                Load();
                foreach (var kv in index) if (!Local(kv.Key) && PlayerPrefs.HasKey(kv.Key)) yield return kv;
            }
        }

        /// <summary>Forgets every saved value the copy holds (a copy from the server is about to be written in).</summary>
        public static void ClearCopy()
        {
            Load();
            foreach (var key in new List<string>(index.Keys))
            {
                if (Local(key)) continue;
                PlayerPrefs.DeleteKey(key);
                index.Remove(key);
            }
            indexDirty = true;
        }

        private static void Changed(string key)
        {
            if (!Local(key)) Dirty = true;
        }

        private static void Note(string key, char kind)
        {
            Load();
            if (index.TryGetValue(key, out var k) && k == kind) return;
            index[key] = kind;
            indexDirty = true;
        }

        private static void Load()
        {
            if (index != null) return;
            index = new Dictionary<string, char>();
            foreach (var line in PlayerPrefs.GetString(IndexKey, "").Split('\n'))
                if (line.Length > 2 && line[1] == ':') index[line.Substring(2)] = line[0];
        }

        private static void WriteIndex()
        {
            var b = new System.Text.StringBuilder();
            foreach (var kv in index) b.Append(kv.Value).Append(':').Append(kv.Key).Append('\n');
            PlayerPrefs.SetString(IndexKey, b.ToString());
            indexDirty = false;
        }
    }
}
