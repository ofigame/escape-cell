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
        /// <summary>The challenge server (a development build may be pointed elsewhere: -foiApi http://host:port).</summary>
        private static string Base => (DevServer ?? "https://apifoi.ofigame.com") + "/v1";

        /// <summary>A test server given on the command line of a development build (null: the real one).</summary>
        internal static string DevServer
        {
            get
            {
                if (devServerRead) return devServer;
                devServerRead = true;
                if (!Debug.isDebugBuild) return null;
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-foiApi") devServer = args[i + 1].TrimEnd('/');
                return devServer;
            }
        }

        private static string devServer;
        private static bool devServerRead;
        // A test server keeps its own player (the real one's keys are never sent to it, nor lost to its refusals).
        private static string Dev => DevServer != null ? "_dev" : "";
        private static string IdKey => "sb_net_id" + Dev;
        private static string SecretKey => "sb_net_key" + Dev;
        private static string NameSyncedKey => "sb_net_named" + Dev;
        private const int Timeout = 8;

        public static bool Registered => Prefs.GetString(IdKey, "").Length > 0;

        /// <summary>The player's id and key, for the live connection's headers.</summary>
        internal static string AuthId => Prefs.GetString(IdKey, "");
        internal static string AuthKey => Prefs.GetString(SecretKey, "");

        /// <summary>The name the server holds for this player (empty: none sent yet).</summary>
        public static string SyncedName
        {
            get => Prefs.GetString(NameSyncedKey, "");
            private set { Prefs.SetString(NameSyncedKey, value); Prefs.Save(); }
        }

        // ---------- Replies ----------

        [Serializable] private class RegisterReply { public string id; public string key; }

        [Serializable]
        public class Profile
        {
            public string name;
            public int trophies, rank, wins, played, defWins, defLosses;
            /// <summary>Matches for the record only: against the AI, against friends.</summary>
            public int aiWins, aiPlayed, friendWins, friendPlayed;
            /// <summary>Days until the name can be changed again (0: now).</summary>
            public int renameDays;
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
            /// <summary>What its foi wears (null from an older game).</summary>
            public string look;
            /// <summary>Its move set ("special,ulti,passive") and duel level.</summary>
            public string moves;
            public int duelLevel;
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

        [Serializable] private class SettingsBody { public bool enabled; public string pushToken; public string lang; public string country; }

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
            /// <summary>What foi wears (see Cosmetics.LookText): the rival sees it in a duel.</summary>
            public string look;
            /// <summary>The move set ("special,ulti,passive") and the duel level.</summary>
            public string moves;
            public int duelLevel;
        }

        [Serializable] private class ResultBody { public bool won, draw, left; }

        // ---------- Calls ----------

        /// <summary>A register call is on its way: others wait for it instead of making a second player.</summary>
        private static bool registering;
        private static float registeringSince;

        /// <summary>Makes this device's player on the server if it has none yet (only once, however many ask at the same
        /// time: the cloud save and the name both ask at the first start). Reports whether one is there.</summary>
        public static IEnumerator EnsurePlayer(Action<bool> done)
        {
            if (Registered) { done(true); yield break; }
            if (registering && Time.realtimeSinceStartup - registeringSince < 30f) // (a stopped call frees it after a while)
            {
                while (registering && Time.realtimeSinceStartup - registeringSince < 30f) yield return null;
                done(Registered);
                yield break;
            }
            registering = true;
            registeringSince = Time.realtimeSinceStartup;
            try
            {
                long code = 0; string body = null;
                yield return Send("POST", "/register", null, false, (c, b) => { code = c; body = b; });
                if (code != 200) { done(false); yield break; }
                var r = JsonUtility.FromJson<RegisterReply>(body);
                if (r == null || string.IsNullOrEmpty(r.id) || string.IsNullOrEmpty(r.key)) { done(false); yield break; }
                Prefs.SetString(IdKey, r.id);
                Prefs.SetString(SecretKey, r.key);
                Prefs.Save();
            }
            finally { registering = false; }
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
            var b = new ArmyBody { army = string.Join(",", points), earned = earned, world = world, foiHp = foiHp, foiHit = foiHit, boldness = boldness, weapon = weapon, weaponLevel = weaponLevel, armor = armor, shield = shield, look = Cosmetics.LookText(), moves = MoveSet.Code, duelLevel = DuelLevel.Level };
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
        /// <param name="left">The player walked out of the duel: the leaver's penalty (rank points), not a plain loss.</param>
        public static IEnumerator SendResult(long matchId, bool won, bool draw, Action<Result> done, bool left = false)
        {
            // Kept on the phone until the server has it: a lost connection sends it again before the next search.
            Prefs.SetString(PendingKey, matchId + "," + (won ? 1 : 0) + "," + (draw ? 1 : 0) + "," + (left ? 1 : 0));
            Prefs.Save();
            long code = 0; string body = null;
            yield return Send("POST", "/match/" + matchId + "/result", JsonUtility.ToJson(new ResultBody { won = won && !draw && !left, draw = draw && !left, left = left }), true, (c, b) => { code = c; body = b; });
            // 200 taken, 4xx settled or unknown already: either way nothing more to send.
            if (code >= 200 && code < 500 && Prefs.GetString(PendingKey, "").StartsWith(matchId + ","))
            {
                Prefs.DeleteKey(PendingKey);
                Prefs.Save();
            }
            done(code == 200 ? TryParse<Result>(body) : null);
        }

        private static string PendingKey => "sb_net_pending" + Dev;

        /// <summary>A result that never reached the server (no network then) goes up now. Reports the server's answer or null.</summary>
        public static IEnumerator SendPending(Action<Result> done)
        {
            var parts = Prefs.GetString(PendingKey, "").Split(',');
            if ((parts.Length != 3 && parts.Length != 4) || !long.TryParse(parts[0], out long id) || id <= 0) { Prefs.DeleteKey(PendingKey); done(null); yield break; }
            yield return SendResult(id, parts[1] == "1", parts[2] == "1", done, parts.Length == 4 && parts[3] == "1");
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
            yield return Send("PUT", "/settings", JsonUtility.ToJson(new SettingsBody { enabled = enabled, pushToken = pushToken, lang = lang, country = Country }), true, (c, _) => code = c);
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
        // ---------- The whole game's copy and its recovery code ----------

        [Serializable] private class SavePut { public long version; public string json; }
        [Serializable] public class SaveBody { public long version; public string json; }
        [Serializable] public class RecoverBody { public string id; public string key; public string name; public long version; public string json; }
        [Serializable] private class RecoveryCode { public string code; }
        [Serializable] private class ConflictBody { public long version; }

        /// <summary>The game's copy on the server (null: none reachable).</summary>
        public static IEnumerator GetSave(Action<SaveBody> done)
        {
            bool ok = Registered;
            if (!ok) yield return EnsurePlayer(r => ok = r);
            if (!ok) { done(null); yield break; }
            long code = 0; string body = null;
            yield return Send("GET", "/save", null, true, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<SaveBody>(body) : null);
        }

        /// <summary>Sends the game's copy. Done with 200 (kept), 409 and the server's newer version, or 0 (no network).</summary>
        public static IEnumerator PutSave(long version, string json, Action<long, long> done)
        {
            bool ok = Registered;
            if (!ok) yield return EnsurePlayer(r => ok = r);
            if (!ok) { done(0, 0); yield break; }
            long code = 0; string body = null;
            yield return Send("PUT", "/save", JsonUtility.ToJson(new SavePut { version = version, json = json }), true, (c, b) => { code = c; body = b; });
            long theirs = code == 409 ? (TryParse<ConflictBody>(body)?.version ?? 0) : 0;
            done(code, theirs);
        }

        /// <summary>A new recovery code for this player (the old one stops working); null with no network.</summary>
        public static IEnumerator NewRecoveryCode(Action<string> done)
        {
            bool ok = Registered;
            if (!ok) yield return EnsurePlayer(r => ok = r);
            if (!ok) { done(null); yield break; }
            long code = 0; string body = null;
            yield return Send("POST", "/recovery", "{}", true, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<RecoveryCode>(body)?.code : null);
        }

        /// <summary>A recovery code typed in: the player it belongs to becomes this phone's (its id and a new key), and its
        /// game's copy comes back. Done with the copy, or null and why ("code": no such code, "net": no network).</summary>
        public static IEnumerator Recover(string typed, Action<RecoverBody, string> done)
        {
            long code = 0; string body = null;
            yield return Send("POST", "/recover", JsonUtility.ToJson(new CodeBody { code = typed }), false, (c, b) => { code = c; body = b; });
            if (code == 400 || code == 404) { done(null, "code"); yield break; }
            if (code == 429) { done(null, "wait"); yield break; }
            var r = code == 200 ? TryParse<RecoverBody>(body) : null;
            if (r == null || string.IsNullOrEmpty(r.id) || string.IsNullOrEmpty(r.key)) { done(null, "net"); yield break; }
            Prefs.SetString(IdKey, r.id);
            Prefs.SetString(SecretKey, r.key);
            if (!string.IsNullOrEmpty(r.name)) Prefs.SetString(NameSyncedKey, r.name);
            else Prefs.DeleteKey(NameSyncedKey);
            Prefs.DeleteKey(PendingKey);
            Prefs.Save();
            done(r, null);
        }

        /// <summary>The phone's country (two letters) for the country leaderboard; "" when unknown.</summary>
        private static string Country
        {
            get
            {
                try { var c = System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName; return c != null && c.Length == 2 ? c : ""; }
                catch (Exception) { return ""; }
            }
        }

        // ---------- Seasons, leaderboards, the weekend cup, clans ----------

        [Serializable] public class SeasonReward { public int season, rank, coins; }
        [Serializable] public class SeasonInfo { public int season; public long endsAt; public int best, bestRank, trophies; public SeasonReward reward; }
        [Serializable] public class TopEntry { public string name; public int trophies, rank, wins, score, size, badge; public bool me; public string id; }
        [Serializable] public class TopList { public TopEntry[] list; public int place; }
        [Serializable] public class CupInfo { public bool open; public long endsAt; public int wins, claimed; public int[] steps, coins; public TopEntry[] list; }
        [Serializable] public class ClanMember { public string name; public int trophies, rank; public bool leader, me; }
        [Serializable] public class ClanInfo { public string id, name; public int badge, score, size; public bool mine; public ClanMember[] members; }
        [Serializable] private class ClanBody { public ClanInfo clan; }
        [Serializable] private class CoinsBody { public int coins; }
        [Serializable] private class ErrorBody { public string error; }
        [Serializable] private class StepBody { public int step; }
        [Serializable] private class ClanCreate { public string name; public int badge; }
        [Serializable] private class ClanJoin { public string id; }

        private static IEnumerator Authed(string method, string path, string json, Action<long, string> done)
        {
            bool ok = Registered;
            if (!ok) yield return EnsurePlayer(r => ok = r);
            if (!ok) { done(0, null); yield break; }
            yield return Send(method, path, json, true, done);
        }

        public static IEnumerator Season(Action<SeasonInfo> done)
        {
            long code = 0; string body = null;
            yield return Authed("GET", "/season", null, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<SeasonInfo>(body) : null);
        }

        /// <summary>Takes the last season's reward: its coins (0: none, or no network).</summary>
        public static IEnumerator ClaimSeason(Action<int> done)
        {
            long code = 0; string body = null;
            yield return Authed("POST", "/season/claim", "{}", (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<CoinsBody>(body)?.coins ?? 0 : 0);
        }

        /// <summary>A leaderboard: "world", "country", "friends" or "clans".</summary>
        public static IEnumerator Top(string scope, Action<TopList> done)
        {
            long code = 0; string body = null;
            yield return Authed("GET", "/top?scope=" + scope, null, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<TopList>(body) : null);
        }

        public static IEnumerator Cup(Action<CupInfo> done)
        {
            long code = 0; string body = null;
            yield return Authed("GET", "/cup", null, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<CupInfo>(body) : null);
        }

        public static IEnumerator ClaimCup(int step, Action<int> done)
        {
            long code = 0; string body = null;
            yield return Authed("POST", "/cup/claim", JsonUtility.ToJson(new StepBody { step = step }), (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<CoinsBody>(body)?.coins ?? 0 : 0);
        }

        /// <summary>The clans (the best, or the ones whose name holds a search).</summary>
        public static IEnumerator Clans(string search, Action<TopList> done)
        {
            long code = 0; string body = null;
            yield return Send("GET", "/clans" + (string.IsNullOrWhiteSpace(search) ? "" : "?q=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(search.Trim())), null, false, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<TopList>(body) : null);
        }

        /// <summary>A clan (null id: the player's own). Done with null for no clan (or no network: ok false).</summary>
        public static IEnumerator Clan(string id, Action<ClanInfo, bool> done)
        {
            long code = 0; string body = null;
            yield return Authed("GET", "/clan" + (string.IsNullOrEmpty(id) ? "" : "?id=" + id), null, (c, b) => { code = c; body = b; });
            var info = code == 200 ? TryParse<ClanBody>(body)?.clan : null;
            if (info != null && string.IsNullOrEmpty(info.id)) info = null;
            done(info, code == 200);
        }

        /// <summary>Founds a clan; done with null, or why not ("taken", "inclan", a name problem, "net").</summary>
        public static IEnumerator CreateClan(string name, int badge, Action<string> done)
        {
            long code = 0; string body = null;
            yield return Authed("POST", "/clans/create", JsonUtility.ToJson(new ClanCreate { name = name, badge = badge }), (c, b) => { code = c; body = b; });
            done(code == 200 ? null : code == 0 ? "net" : TryParse<ErrorBody>(body)?.error ?? "net");
        }

        public static IEnumerator JoinClan(string id, Action<string> done)
        {
            long code = 0; string body = null;
            yield return Authed("POST", "/clans/join", JsonUtility.ToJson(new ClanJoin { id = id }), (c, b) => { code = c; body = b; });
            done(code == 200 ? null : code == 0 ? "net" : TryParse<ErrorBody>(body)?.error ?? "net");
        }

        public static IEnumerator LeaveClan(Action<bool> done)
        {
            long code = 0;
            yield return Authed("POST", "/clans/leave", "{}", (c, _) => code = c);
            done(code == 200);
        }

        public static IEnumerator DeleteMe(Action<bool> done)
        {
            if (!Registered) { Challenge.ResetLocal(); CloudSave.ProfileDeleted(); done(true); yield break; }
            long code = 0;
            yield return Send("DELETE", "/me", null, true, (c, _) => code = c);
            if (code != 200 && code != 401) { done(false); yield break; } // 401: already gone on the server
            Prefs.DeleteKey(IdKey);
            Prefs.DeleteKey(SecretKey);
            Prefs.DeleteKey(NameSyncedKey);
            Challenge.ResetLocal();
            CloudSave.ProfileDeleted(); // no copy goes up again until the player asks for the online side
            done(true);
        }

        /// <summary>The player's record on the server. Null: no network.</summary>
        public static IEnumerator Me(Action<Profile> done)
        {
            long code = 0; string body = null;
            yield return Send("GET", "/me", null, true, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<Profile>(body) : null);
        }

        // ---------- Friends ----------

        [Serializable] public class Friend { public string id, name; public int rank, trophies, power; public bool online; }
        [Serializable] public class FriendList { public Friend[] friends, incoming, outgoing; public Friend me; }
        [Serializable] public class Invitation { public string code, from; public int min; }
        [Serializable] private class FriendBody { public string id; }
        [Serializable] private class CodeBody { public string code; }
        [Serializable] private class AddReply { public bool friends; }

        /// <summary>The friends list (friends, requests to answer, requests sent). Null: no network.</summary>
        public static IEnumerator Friends(Action<FriendList> done)
        {
            if (!Registered) { done(null); yield break; }
            long code = 0; string body = null;
            yield return Send("GET", "/friends", null, true, (c, b) => { code = c; body = b; });
            done(code == 200 ? TryParse<FriendList>(body) : null);
        }

        /// <summary>A friend request by name. Reports null (sent; true: friends at once) or why not: notfound, self, already, limit, offline.</summary>
        public static IEnumerator AddFriend(string name, Action<string, bool> done)
        {
            long code = 0; string body = null;
            yield return Send("POST", "/friends/add", JsonUtility.ToJson(new NameBody { name = name }), true, (c, b) => { code = c; body = b; });
            if (code == 200) { done(null, TryParse<AddReply>(body)?.friends ?? false); yield break; }
            var e = TryParse<ErrorReply>(body);
            done(code == 0 ? "offline" : e != null && !string.IsNullOrEmpty(e.error) ? e.error : "notfound", false);
        }

        public static IEnumerator AcceptFriend(string id, Action<bool> done)
        {
            long code = 0;
            yield return Send("POST", "/friends/accept", JsonUtility.ToJson(new FriendBody { id = id }), true, (c, _) => code = c);
            done(code == 200);
        }

        public static IEnumerator RemoveFriend(string id, Action<bool> done)
        {
            long code = 0;
            yield return Send("POST", "/friends/remove", JsonUtility.ToJson(new FriendBody { id = id }), true, (c, _) => code = c);
            done(code == 200);
        }

        /// <summary>A friend's duel invitation waiting for this player (null: none, or no network).</summary>
        public static IEnumerator PollInvite(Action<Invitation> done)
        {
            if (!Registered) { done(null); yield break; }
            long code = 0; string body = null;
            yield return Send("GET", "/friends/invite", null, true, (c, b) => { code = c; body = b; });
            var inv = code == 200 ? TryParse<Invitation>(body) : null;
            done(inv != null && !string.IsNullOrEmpty(inv.code) ? inv : null);
        }

        public static IEnumerator DeclineInvite(string code)
        {
            yield return Send("POST", "/friends/decline", JsonUtility.ToJson(new CodeBody { code = code }), true, (c, _) => { });
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
                req.SetRequestHeader("X-Foi-Id", Prefs.GetString(IdKey, ""));
                req.SetRequestHeader("X-Foi-Key", Prefs.GetString(SecretKey, ""));
            }
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError)
            {
                done(0, null);
                yield break;
            }
            if (auth && req.responseCode == 401 && path != "/me")
            {
                // The server no longer knows this player (deleted, or its key lost): register afresh next time.
                Prefs.DeleteKey(IdKey);
                Prefs.DeleteKey(SecretKey);
                Prefs.DeleteKey(NameSyncedKey);
                Prefs.DeleteKey(PendingKey);
                Prefs.Save();
            }
            done(req.responseCode, req.downloadHandler.text);
        }
    }
}
