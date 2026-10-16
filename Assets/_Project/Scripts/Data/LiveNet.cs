using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The live challenge's connection (a WebSocket to the challenge server): queue for a rival, make or enter a friend's
    /// code, send this game's own state and blows. What the server says is queued and read on the main thread
    /// (<see cref="Poll"/>); a closed connection reads as a message "closed". Nothing is kept: each state read replaces
    /// the last one.
    /// </summary>
    public static class LiveNet
    {
        /// <summary>The live protocol this game speaks (the server turns older ones away).</summary>
        private const int Protocol = 4; // 4: the duel's moves (specials, combos, ultimate), foi-alone duels
        private static string Url => (FoiNet.DevServer != null ? FoiNet.DevServer.Replace("https://", "wss://").Replace("http://", "ws://") + "/v1/live" : "wss://apifoi.ofigame.com/v1/live") + "?v=" + Protocol;

        /// <summary>A message from the server (only the fields its kind uses are set; <see cref="raw"/> is the text itself).</summary>
        [Serializable]
        public class Msg
        {
            public string t, code, score;
            public long matchId;
            public int side, seed, min, outcome, change, by, u, d, f;
            /// <summary>Squads in the match (1) or foi against foi (0); -1: the server did not say.</summary>
            public int sq = -1;
            /// <summary>A move (the duel's specials: 1 special, 2 power hit, 3 knockdown, 4 ultimate, 5 breaker, 6 finish).</summary>
            public int k;
            /// <summary>An emote (0..5).</summary>
            public int e;
            /// <summary>The two power bars (0..3) with the health; -1: not sent.</summary>
            public float mm = -1f, tm = -1f;
            public float secs, mine, theirs;
            public bool won, ranked, left;
            public string from;
            public FoiNet.Rival rival;
            public FoiNet.Profile profile;
            [NonSerialized] public string raw;
        }

        private static ClientWebSocket ws;
        private static CancellationTokenSource cts;
        private static readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        private static float pingAt;

        /// <summary>Open (connected and not closed).</summary>
        public static bool Open => ws != null && ws.State == WebSocketState.Open;

        /// <summary>Connects (closing any connection before). Reports whether it is open.</summary>
        public static async Task<bool> Connect()
        {
            Close();
            while (inbox.TryDequeue(out _)) { }
            if (Application.internetReachability == NetworkReachability.NotReachable || !FoiNet.Registered) return false;
            var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("X-Foi-Id", FoiNet.AuthId);
            socket.Options.SetRequestHeader("X-Foi-Key", FoiNet.AuthKey);
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            var source = new CancellationTokenSource();
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(source.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(8));
                    await socket.ConnectAsync(new Uri(Url), timeout.Token);
                }
            }
            catch (Exception)
            {
                socket.Dispose();
                return false;
            }
            ws = socket;
            cts = source;
            // A fresh send lock for each connection: a send stuck on an old one never holds up the new one.
            sendLock = new SemaphoreSlim(1, 1);
            sending = 0;
            pingAt = Time.realtimeSinceStartup + 20f;
            _ = Receive(socket, source.Token);
            return true;
        }

        private static async Task Receive(ClientWebSocket socket, CancellationToken stop)
        {
            var buffer = new byte[16384];
            var text = new StringBuilder();
            try
            {
                while (socket.State == WebSocketState.Open && !stop.IsCancellationRequested)
                {
                    var r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stop);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    if (!r.EndOfMessage) continue;
                    inbox.Enqueue(text.ToString());
                    text.Clear();
                }
            }
            catch (Exception) { }
            if (socket == ws) inbox.Enqueue("{\"t\":\"closed\"}");
        }

        /// <summary>The next message from the server, or null. Also keeps the connection alive.</summary>
        public static Msg Poll()
        {
            if (Open && Time.realtimeSinceStartup > pingAt)
            {
                pingAt = Time.realtimeSinceStartup + 20f;
                Send("{\"t\":\"ping\"}");
            }
            while (inbox.TryDequeue(out var json))
            {
                Msg m;
                try { m = JsonUtility.FromJson<Msg>(json); }
                catch (Exception) { continue; }
                if (m == null || m.t == "pong") continue;
                m.raw = json;
                return m;
            }
            return null;
        }

        /// <summary>Sends a message as it is.</summary>
        public static void Send(string json) => Send(json, false);

        private static void Send(string json, bool state)
        {
            var socket = ws;
            if (socket == null || socket.State != WebSocketState.Open) return;
            _ = SendAsync(socket, sendLock, Encoding.UTF8.GetBytes(json), state);
        }

        // One send at a time on a socket (a WebSocket takes no overlapping sends).
        private static SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private static int sending;

        private static async Task SendAsync(ClientWebSocket socket, SemaphoreSlim gate, byte[] bytes, bool state)
        {
            // A state while another message is still on its way: dropped (the next state says it all again).
            if (state && Volatile.Read(ref sending) > 0) return;
            Interlocked.Increment(ref sending);
            await gate.WaitAsync();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // a stalled connection gives up the send
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token);
            }
            catch (Exception) { }
            finally { gate.Release(); Interlocked.Decrement(ref sending); }
        }

        private static string I(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        public static void Queue(int minutes) => Send("{\"t\":\"queue\",\"min\":" + minutes + ",\"sq\":" + (Challenge.DuelSquads ? 1 : 0) + "}");
        /// <summary>Makes a code for a friend (<paramref name="invite"/>: a friend's id, who is sent an invitation to it).</summary>
        public static void Host(int minutes, string invite = null) =>
            Send("{\"t\":\"host\",\"min\":" + minutes + ",\"sq\":" + (Challenge.DuelSquads ? 1 : 0) + (string.IsNullOrEmpty(invite) ? "" : ",\"to\":\"" + Clean(invite, 40, true) + "\"") + "}");
        public static void Join(string code) => Send("{\"t\":\"join\",\"code\":\"" + Clean(code) + "\"}");
        public static void Cancel() => Send("{\"t\":\"cancel\"}");
        /// <summary>Back for a match after the connection dropped.</summary>
        public static void Rejoin(long matchId) => Send("{\"t\":\"rejoin\",\"match\":" + matchId + "}");
        /// <summary>A rematch of a live match just over (both must ask).</summary>
        public static void Rematch(long matchId) => Send("{\"t\":\"rematch\",\"match\":" + matchId + "}");
        /// <summary>An emote for the rival.</summary>
        public static void Emote(int e) => Send("{\"t\":\"em\",\"e\":" + e + "}");
        public static void Ready() => Send("{\"t\":\"ready\"}");

        /// <summary>The duel: where this game's foi is, its facing, the blows it has swung, and its squad (see HuntSystem.SquadState).</summary>
        public static void DuelState(Vector3 pos, float yaw, int swings, string squad) =>
            Send("{\"t\":\"s\",\"x\":" + I(pos.x) + ",\"z\":" + I(pos.z) + ",\"r\":" + I(yaw) + ",\"w\":" + swings + ",\"q\":\"" + squad + "\"}", true);

        /// <summary>The duel: this game's foi landed a blow on the rival.</summary>
        public static void Hit() => Send("{\"t\":\"hit\"}");

        /// <summary>A blow on one of the rival's soldiers (its number in its squad, the damage; a foi's blow: it strikes back).</summary>
        public static void SquadHit(int id, int damage, bool byFoi) => Send("{\"t\":\"ah\",\"u\":" + id + ",\"d\":" + damage + ",\"f\":" + (byFoi ? 1 : 0) + "}");

        /// <summary>One of this game's soldiers landed a blow on the rival foi (health points).</summary>
        public static void FoeHit(int damage) => Send("{\"t\":\"sh\",\"d\":" + damage + "}");

        /// <summary>A heart (the server heals) and a shield (the server lets it take two blows for three seconds).</summary>
        public static void Heal() => Send("{\"t\":\"heal\"}");
        /// <summary>A duel move (the server checks the bar, the combo and the reach).</summary>
        public static void Special(int kind) => Send("{\"t\":\"sp\",\"k\":" + kind + "}");
        /// <summary>This game's special reached the rival.</summary>
        public static void SpecialHit() => Send("{\"t\":\"sphit\"}");
        public static void Shield() => Send("{\"t\":\"shield\"}");

        /// <summary>Leaves the live match (a loss) and closes the connection.</summary>
        public static void Leave()
        {
            if (Open) Send("{\"t\":\"leave\"}");
            Close();
        }

        public static void Close()
        {
            var socket = ws;
            if (socket == null) return;
            ws = null;
            try { cts?.Cancel(); } catch (Exception) { }
            cts = null;
            _ = CloseAsync(socket, sendLock);
        }

        private static async Task CloseAsync(ClientWebSocket socket, SemaphoreSlim gate)
        {
            // After whatever is still being sent (a leave) — but never stuck behind a stalled send: then it is cut off.
            bool got = await gate.WaitAsync(TimeSpan.FromSeconds(3));
            try
            {
                if (got && socket.State == WebSocketState.Open)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);
                }
                else socket.Abort();
            }
            catch (Exception) { }
            finally { if (got) gate.Release(); }
            socket.Dispose();
        }

        private static string Clean(string code, int max = 6, bool hex = false)
        {
            var sb = new StringBuilder();
            foreach (char ch in code ?? "") if ((ch >= '0' && ch <= '9' || hex && (ch == '-' || ch >= 'a' && ch <= 'f' || ch >= 'A' && ch <= 'F')) && sb.Length < max) sb.Append(ch);
            return sb.ToString();
        }
    }
}
