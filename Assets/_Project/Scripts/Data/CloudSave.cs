using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The whole game kept on the server too, so it survives the game being deleted or the phone changed: every saved
    /// value (<see cref="Prefs"/>) but this phone's own goes up as one copy a minute after a change, and when the game
    /// goes to the background. A recovery code (made on the server, written down by the player) brings the player and
    /// the copy back on a new phone. The open world keeps a copy of its own (OwSync), found again by the same player.
    /// </summary>
    public static class CloudSave
    {
        private const string VersionKey = "sb_cloud_ver";
        private const string CodeKey = "sb_cloud_code";
        private const float Every = 60f;

        [Serializable]
        private class Copy
        {
            public int format = 1;
            public string[] k;
            public string[] t;
            public string[] v;
        }

        /// <summary>The recovery code last made on this phone ("" if none yet).</summary>
        public static string Code => Prefs.GetString(CodeKey, "");

        private static long Version
        {
            get => long.TryParse(Prefs.GetString(VersionKey, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
            set => Prefs.SetString(VersionKey, value.ToString(CultureInfo.InvariantCulture));
        }

        private static bool sending;

        /// <summary>Starts the minute-by-minute sending (once, at start).</summary>
        public static void Install()
        {
            if (UnityEngine.Object.FindAnyObjectByType<Runner>() != null) return;
            var go = new GameObject("CloudSave");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
            // Never sent yet: the first copy goes up soon.
            if (Version == 0) Prefs.Dirty = true;
        }

        /// <summary>The copy: every key with its kind and value.</summary>
        public static string Snapshot()
        {
            var k = new List<string>(); var t = new List<string>(); var v = new List<string>();
            foreach (var kv in new List<KeyValuePair<string, char>>(Prefs.Keys))
            {
                k.Add(kv.Key);
                t.Add(kv.Value.ToString());
                v.Add(kv.Value == 'i' ? PlayerPrefs.GetInt(kv.Key).ToString(CultureInfo.InvariantCulture)
                    : kv.Value == 'f' ? PlayerPrefs.GetFloat(kv.Key).ToString("R", CultureInfo.InvariantCulture)
                    : PlayerPrefs.GetString(kv.Key));
            }
            return JsonUtility.ToJson(new Copy { k = k.ToArray(), t = t.ToArray(), v = v.ToArray() });
        }

        /// <summary>Writes a copy in place of the game's own values (this phone's own stay). False if it is not a copy.</summary>
        public static bool Apply(string json)
        {
            Copy c;
            try { c = JsonUtility.FromJson<Copy>(json); }
            catch (Exception) { return false; }
            if (c?.k == null || c.t == null || c.v == null || c.k.Length != c.t.Length || c.k.Length != c.v.Length) return false;
            Prefs.ClearCopy();
            for (int i = 0; i < c.k.Length; i++)
            {
                string key = c.k[i];
                if (string.IsNullOrEmpty(key) || Prefs.Local(key)) continue;
                switch (c.t[i])
                {
                    case "i": if (int.TryParse(c.v[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) Prefs.SetInt(key, n); break;
                    case "f": if (float.TryParse(c.v[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) Prefs.SetFloat(key, f); break;
                    default: Prefs.SetString(key, c.v[i] ?? ""); break;
                }
            }
            Prefs.Dirty = false;
            Prefs.Save();
            return true;
        }

        /// <summary>Sends the copy if something changed (not two at once).</summary>
        public static IEnumerator Upload()
        {
            if (sending || !Prefs.Dirty || Application.internetReachability == NetworkReachability.NotReachable) yield break;
            sending = true;
            Prefs.Dirty = false;
            Prefs.Save();
            long version = Version + 1;
            long result = 0, theirs = 0;
            yield return FoiNet.PutSave(version, Snapshot(), (code, server) => { result = code; theirs = server; });
            if (result == 200) Version = version;
            else
            {
                // Not kept: sent again next time (after a newer one on the server, this phone's goes over it: it is the one played).
                if (result == 409) Version = theirs;
                Prefs.Dirty = true;
            }
            Prefs.Save();
            sending = false;
        }

        /// <summary>Makes a new recovery code (the old one stops working) and keeps it to show again. Null: no network.</summary>
        public static IEnumerator NewCode(Action<string> done)
        {
            string code = null;
            yield return FoiNet.NewRecoveryCode(c => code = c);
            if (!string.IsNullOrEmpty(code))
            {
                Prefs.SetString(CodeKey, code);
                Prefs.Save();
                // The copy goes up now, so the code brings back today's game.
                Prefs.Dirty = true;
                yield return Upload();
            }
            done(code);
        }

        /// <summary>A recovery code typed on this phone: the player and the whole game come back. Done with null on
        /// success, else why ("code", "wait", "net", "empty").</summary>
        public static IEnumerator Restore(string typed, Action<string> done)
        {
            FoiNet.RecoverBody r = null; string why = null;
            yield return FoiNet.Recover(typed, (body, w) => { r = body; why = w; });
            if (r == null) { done(why ?? "net"); yield break; }
            if (string.IsNullOrEmpty(r.json) || !Apply(r.json)) { Version = r.version; Prefs.Save(); done("empty"); yield break; }
            Version = r.version;
            Prefs.SetString(CodeKey, typed.Trim().ToUpperInvariant());
            Prefs.DeleteKey("sb_ow"); // the open world comes down from its own copy on entry
            Prefs.Save();
            done(null);
        }

        private class Runner : MonoBehaviour
        {
            private float wait = 20f;

            private void Update()
            {
                wait -= Time.unscaledDeltaTime;
                if (wait > 0f) return;
                wait = Every;
                if (Prefs.Dirty) StartCoroutine(Upload());
            }

            private void OnApplicationPause(bool paused)
            {
                if (!paused) return;
                Prefs.Save();
                if (Prefs.Dirty) StartCoroutine(Upload());
            }
        }
    }
}
