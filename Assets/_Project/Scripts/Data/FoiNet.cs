using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SquashBot.Data
{
    /// <summary>
    /// The challenge's online side (apifoi.ofigame.com): the device's player (an id and a secret key made by the server on
    /// the first challenge and kept on the phone), the name, the army rivals face, finding a rival and sending the result.
    /// Every call is a coroutine that reports back; with no network the game plays a bot and keeps its own record.
    /// </summary>
    public static class FoiNet
    {
        private const string Base = "https://apifoi.ofigame.com/v1";
        private const string IdKey = "sb_net_id", SecretKey = "sb_net_key", NameSyncedKey = "sb_net_named";
        private const int Timeout = 8;

        public static bool Registered => PlayerPrefs.GetString(IdKey, "").Length > 0;

        /// <summary>The name the server holds for this player (empty: none sent yet).</summary>
        public static string SyncedName
        {
            get => PlayerPrefs.GetString(NameSyncedKey, "");
            private set { PlayerPrefs.SetString(NameSyncedKey, value); PlayerPrefs.Save(); }
        }

        // ---------- Replies ----------

        [Serializable] private class RegisterReply { public string id; public string key; }

        [Serializable]
        public class Profile
        {
            public string name;
            public int trophies, rank, wins, played, defWins, defLosses;
        }

        [Serializable] private class ErrorReply { public string error; }

        [Serializable]
        public class Rival
        {
            public string name;
            public int trophies, rank, world, foiHp;
            public string army;
            public float foiHit, boldness;
            /// <summary>Its foi's hand weapon and that weapon's level, and its armour (share of a blow it takes off).</summary>
            public string weapon;
            public int weaponLevel;
            public float armor;
            public int shield;
        }

        [Serializable]
        public class Match
        {
            public long matchId;
            public int seed, trophies;
            public Rival rival;
            /// <summary>A real player's army (else the game makes a bot).</summary>
            public bool HasRival => rival != null && !string.IsNullOrEmpty(rival.name);
        }

        [Serializable] public class Result { public int change; public Profile profile; }

        [Serializable] public class Attack { public string attacker; public bool held, draw; public int change; }

        [Serializable] private class LiveAttack { public string attacker; }

        [Serializable] private class DefenseReply { public LiveAttack[] live; public Attack[] items; }

        [Serializable] private class SettingsBody { public bool enabled; public string pushToken; public string lang; }

        [Serializable] private class NameBody { public string name; }

        [Serializable]
        private class ArmyBody
        {
            public string army;
            public int earned, world, foiHp;
            public float foiHit, boldness;
            public string weapon;
            public int weaponLevel;
            public float armor;
            public int shield;
        }

        [Serializable] private class ResultBody { public bool won, draw; }

        // ---------- Calls ----------

        /// <summary>Makes this device's player on the server if it has none yet. Reports whether one is there.</summary>
        public static IEnumerator EnsurePlayer(Action<bool> done)
        {
            if (Registered) { done(true); yield break; }
            long code = 0; string body = null;
            yield return Send("POST", "/register", null, false, (c, b) => { code = c; body = b; });
            if (code != 200) { done(false); yield break; }
            var r = JsonUtility.FromJson<RegisterReply>(body);
            if (r == null || string.IsNullOrEmpty(r.id) || string.IsNullOrEmpty(r.key)) { done(false); yield break; }
            PlayerPrefs.SetString(IdKey, r.id);
            PlayerPrefs.SetString(SecretKey, r.key);
            PlayerPrefs.Save();
            done(true);
        }

        /// <summary>
        /// Claims a name. Reports null when it is taken, else why not: "shape", "word", "taken", "changes", or "offline"
        /// (no network: the name is kept on the phone and sent with the next challenge).
        /// </summary>
        public static IEnumerator SetName(string name, Action<string, Profile> done)
        {
            bool ok = false;
            yield return EnsurePlayer(r => ok = r);
            if (!ok) { done("offline", null); yield break; }
            long code = 0; string body = null;
            yield return Send("POST", "/name", JsonUtility.ToJson(new NameBody { name = name }), true, (c, b) => { code = c; body = b; });
            if (code == 200)
            {
                SyncedName = name;
                done(null, JsonUtility.FromJson<Profile>(body));
                yield break;
            }
            if (code == 400 || code == 409)
            {
                var e = TryParse<ErrorReply>(body);
                done(e != null && !string.IsNullOrEmpty(e.error) ? e.error : "taken", null);
                yield break;
            }
            done("offline", null);
        }

        /// <summary>Sends the army rivals will face (the groups' points, foi's strength in the duel).</summary>
        public static IEnumerator PutArmy(int[] points, int earned, int world, int foiHp, float foiHit, float boldness, string weapon, int weaponLevel, float armor, int shield, Action<bool> done)
        {
            var b = new ArmyBody { army = string.Join(",", points), earned = earned, world = world, foiHp = foiHp, foiHit = foiHit, boldness = boldness, weapon = weapon, weaponLevel = weaponLevel, armor = armor, shield = shield };
            long code = 0;
            yield return Send("PUT", "/army", JsonUtility.ToJson(b), true, (c, _) => code = c);
            done(code == 200);
        }

        /// <summary>Finds a rival (a real player's army, or a seed for a bot). Null: no network.</summary>
        public static IEnumerator Find(Action<Match> done)
        {
            long code = 0; string body = null;
            yield return Send("POST", "/match/find", "{}", true, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<Match>(body) : null);
        }

        /// <summary>Sends a challenge's result. Null: not taken (no network: the server counts it lost later).</summary>
        public static IEnumerator SendResult(long matchId, bool won, bool draw, Action<Result> done)
        {
            long code = 0; string body = null;
            yield return Send("POST", "/match/" + matchId + "/result", JsonUtility.ToJson(new ResultBody { won = won && !draw, draw = draw }), true, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<Result>(body) : null);
        }

        /// <summary>
        /// The player's army: who is fighting it right now (AI in command), and the attacks on it since it last looked.
        /// Both empty with no network.
        /// </summary>
        public static IEnumerator Defense(Action<string[], Attack[]> done)
        {
            if (!Registered) { done(Array.Empty<string>(), Array.Empty<Attack>()); yield break; }
            long code = 0; string body = null;
            yield return Send("GET", "/defense", null, true, (c, b) => { code = c; body = b; });
            var r = code == 200 ? TryParse<DefenseReply>(body) : null;
            var live = new System.Collections.Generic.List<string>();
            if (r?.live != null) foreach (var l in r.live) if (!string.IsNullOrEmpty(l.attacker)) live.Add(l.attacker);
            done(live.ToArray(), r?.items ?? Array.Empty<Attack>());
        }

        /// <summary>Challenges on or off, the phone's notification token (null: unchanged) and the game's language.</summary>
        public static IEnumerator PutSettings(bool enabled, string pushToken, string lang, Action<bool> done)
        {
            if (!Registered) { done(false); yield break; }
            long code = 0;
            yield return Send("PUT", "/settings", JsonUtility.ToJson(new SettingsBody { enabled = enabled, pushToken = pushToken, lang = lang }), true, (c, _) => code = c);
            done(code == 200);
        }

        /// <summary>The game's language as a short code for the server's notification texts.</summary>
        public static string LangCode => Loc.Current switch
        {
            Language.Turkish => "tr", Language.Arabic => "ar", Language.Spanish => "es", Language.German => "de",
            Language.Korean => "ko", Language.Italian => "it", Language.Azerbaijani => "az", _ => "en",
        };

        /// <summary>
        /// Deletes the online profile (name, army, every challenge) from the server and forgets it here, along with the
        /// challenge record. Reports whether it is gone (false: no network, nothing changed).
        /// </summary>
        public static IEnumerator DeleteMe(Action<bool> done)
        {
            if (!Registered) { Challenge.ResetLocal(); done(true); yield break; }
            long code = 0;
            yield return Send("DELETE", "/me", null, true, (c, _) => code = c);
            if (code != 200 && code != 401) { done(false); yield break; } // 401: already gone on the server
            PlayerPrefs.DeleteKey(IdKey);
            PlayerPrefs.DeleteKey(SecretKey);
            PlayerPrefs.DeleteKey(NameSyncedKey);
            Challenge.ResetLocal();
            done(true);
        }

        /// <summary>The player's record on the server. Null: no network.</summary>
        public static IEnumerator Me(Action<Profile> done)
        {
            long code = 0; string body = null;
            yield return Send("GET", "/me", null, true, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<Profile>(body) : null);
        }

        // ---------- Plumbing ----------

        private static T TryParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (Exception) { return null; }
        }

        private static IEnumerator Send(string method, string path, string json, bool auth, Action<long, string> done)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable) { done(0, null); yield break; }
            using var req = new UnityWebRequest(Base + path, method);
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Timeout;
            if (auth)
            {
                req.SetRequestHeader("X-Foi-Id", PlayerPrefs.GetString(IdKey, ""));
                req.SetRequestHeader("X-Foi-Key", PlayerPrefs.GetString(SecretKey, ""));
            }
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError)
            {
                done(0, null);
                yield break;
            }
            done(req.responseCode, req.downloadHandler.text);
        }
    }
}
