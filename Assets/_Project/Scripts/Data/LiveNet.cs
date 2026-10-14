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
    /// code, send picks and results. What the server says is queued and read on the main thread (<see cref="Poll"/>);
    /// a closed connection reads as a message "closed".
    /// </summary>
    public static class LiveNet
    {
        private static string Url => FoiNet.DevServer != null ? FoiNet.DevServer.Replace("https://", "wss://").Replace("http://", "ws://") + "/v1/live" : "wss://apifoi.ofigame.com/v1/live";

        /// <summary>A message from the server (only the fields its kind uses are set).</summary>
        [Serializable]
        public class Msg
        {
            public string t, code, score;
            public long matchId;
            public int side, seed, n, mine, theirs, myLeft, theirLeft, outcome, change;
            public float secs;
            public FoiNet.Rival rival;
            public FoiNet.Profile profile;
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
            pingAt = Time.realtimeSinceStartup + 25f;
            _ = Receive(socket, source.Token);
            return true;
        }

        private static async Task Receive(ClientWebSocket socket, CancellationToken stop)
        {
            var buffer = new byte[8192];
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
                pingAt = Time.realtimeSinceStartup + 25f;
                Send("{\"t\":\"ping\"}");
            }
            while (inbox.TryDequeue(out var json))
            {
                Msg m;
                try { m = JsonUtility.FromJson<Msg>(json); }
                catch (Exception) { continue; }
                if (m == null || m.t == "pong") continue;
                return m;
            }
            return null;
        }

        private static void Send(string json)
        {
            var socket = ws;
            if (socket == null || socket.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            _ = SendAsync(socket, bytes);
        }

        // One send at a time on a socket (a WebSocket takes no overlapping sends).
        private static readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

        private static async Task SendAsync(ClientWebSocket socket, byte[] bytes)
        {
            await sendLock.WaitAsync();
            try { await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); }
            catch (Exception) { }
            finally { sendLock.Release(); }
        }

        public static void Queue() => Send("{\"t\":\"queue\"}");
        public static void Host() => Send("{\"t\":\"host\"}");
        public static void Join(string code) => Send("{\"t\":\"join\",\"code\":\"" + Clean(code) + "\"}");
        public static void Cancel() => Send("{\"t\":\"cancel\"}");
        public static void Ready() => Send("{\"t\":\"ready\"}");
        public static void Pick(int group) => Send("{\"t\":\"pick\",\"g\":" + group + "}");
        public static void Duel(int round, bool won, float seconds) =>
            Send("{\"t\":\"duel\",\"round\":" + round + ",\"won\":" + (won ? "true" : "false") + ",\"secs\":" + seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "}");
        public static void Final(int outcome) => Send("{\"t\":\"final\",\"outcome\":" + Mathf.Clamp(outcome, -1, 1) + "}");

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
            if (socket == null) return;
            _ = CloseAsync(socket);
        }

        private static async Task CloseAsync(ClientWebSocket socket)
        {
            // After whatever is still being sent (a last result, a leave).
            await sendLock.WaitAsync();
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch (Exception) { }
            finally { sendLock.Release(); }
            socket.Dispose();
        }

        private static string Clean(string code)
        {
            var sb = new StringBuilder();
            foreach (char ch in code ?? "") if (ch >= '0' && ch <= '9' && sb.Length < 6) sb.Append(ch);
            return sb.ToString();
        }
    }
}
