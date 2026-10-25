using System;
using System.Collections;
using SquashBot.Audio;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Live duels: the waiting room first looks for a live rival who picked the same length for a while (or waits for a
    /// friend's code or invitation), and only then takes a virtual rival. In a live match each game moves its own foi
    /// and squad and sends their state some times a second (the other game shows them; nothing is kept); the
    /// server checks the blows and keeps the score and the clock. A random live match moves trophies; a friend's does
    /// not. A player who leaves loses at once.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Seconds the waiting room looks for a live rival before it takes a virtual one.</summary>
        private const float LiveWaitSeconds = 20f;

        private bool chLive;
        /// <summary>This game's side in the live match (0: the south end, left on screen; 1: the north, right).</summary>
        private int liveSide;
        private bool liveRanked, liveLeft, liveLost;
        /// <summary>0: a random rival; 1: making a code for a friend; 2: entering a friend's code.</summary>
        private int chFriend;
        private string chFriendCode;
        private int friendMinutes = 3;
        /// <summary>The friend invited to the code being made (their id), or null.</summary>
        private string friendInvite;

        private enum LiveWait { None, Start }
        private LiveWait liveWait;
        private int liveChange;
        private FoiNet.Profile liveProfile;
        private float liveStateAt;
        /// <summary>The live match's id (to come back to it if the connection drops), the last one over (for a rematch).</summary>
        private long liveMatchId, rematchMatch;
        private bool liveRejoining;

        /// <summary>A live match being played (the other game is there).</summary>
        private bool LiveActive => chLive && challengeRun && chPhase != ChPhase.Done;

        // ---------- Finding a live rival ----------

        /// <summary>Connects and queues (or makes / enters a friend's code); reports the match, or null (none found).</summary>
        private IEnumerator LiveSearch(int token, Action<LiveNet.Msg> done)
        {
            var connect = LiveNet.Connect();
            while (!connect.IsCompleted) yield return null;
            if (!connect.Result)
            {
                if (chFriend != 0) { chUi.SetFriendStatus(Loc.T("live.offline")); chUi.SetFriendBusy(false); }
                done(null);
                yield break;
            }
            if (chFriend == 1) LiveNet.Host(friendMinutes, friendInvite);
            else if (chFriend == 2) LiveNet.Join(chFriendCode);
            else if (chFriend == 3) LiveNet.Rematch(rematchMatch);
            else LiveNet.Queue(chDuelMinutes);
            float until = Time.realtimeSinceStartup + (chFriend == 0 ? LiveWaitSeconds : chFriend == 3 ? 32f : 600f);
            while (!LobbyOver(token))
            {
                float left = until - Time.realtimeSinceStartup;
                if (left <= 0f) break;
                if (chFriend == 0) chUi.SetLobbyStatus(Loc.F("live.searching", Mathf.CeilToInt(left)));
                else if (chFriend == 3) chUi.SetLobbyStatus(Loc.F("live.rematchWait", Mathf.CeilToInt(left)));
                var m = LiveNet.Poll();
                if (m != null)
                {
                    switch (m.t)
                    {
                        case "matched":
                            if (!string.IsNullOrEmpty(m.rules)) DuelBalanceTable.Offer(m.rules); // (the server's newer duel table: kept from now on)
                            done(m);
                            yield break;
                        case "code":
                            chUi.SetFriendCode(m.code);
                            chUi.SetFriendStatus(string.IsNullOrEmpty(friendInvite) ? Loc.T("live.codeWait") : Loc.F("friends.inviteWait", invitedName));
                            break;
                        case "declined":
                        case "nofriend":
                            chUi.SetFriendStatus(m.t == "declined" ? Loc.F("friends.declined", invitedName) : Loc.T("friends.err.notfound"));
                            chUi.SetFriendBusy(false);
                            LiveNet.Close();
                            done(null);
                            yield break;
                        case "norematch":
                            LiveNet.Close();
                            done(null);
                            yield break;
                        case "nocode":
                            chUi.SetFriendStatus(Loc.T("live.noCode"));
                            chUi.SetFriendBusy(false);
                            LiveNet.Close();
                            done(null);
                            yield break;
                        case "closed":
                        case "error":
                            if (chFriend == 1 || chFriend == 2) { chUi.SetFriendStatus(Loc.T("live.offline")); chUi.SetFriendBusy(false); }
                            LiveNet.Close();
                            done(null);
                            yield break;
                    }
                }
                yield return null;
            }
            LiveNet.Cancel();
            LiveNet.Close();
            if ((chFriend == 1 || chFriend == 2) && !LobbyOver(token)) { chUi.SetFriendStatus(Loc.T("live.codeExpired")); chUi.SetFriendBusy(false); }
            done(null);
        }

        /// <summary>A live rival was found: its card in the waiting room, then the arena of the kind it was made for.</summary>
        private IEnumerator StartLive(LiveNet.Msg m, int world, int token)
        {
            chLive = true;
            liveSide = m.side == 1 ? 1 : 0;
            liveMatchId = m.matchId;
            rematchMatch = 0;
            liveRanked = m.ranked;
            liveLeft = false;
            liveLost = false;
            liveWait = LiveWait.None;
            liveProfile = null;
            liveChange = 0;
            chOnline = true;
            chMatchId = 0;
            chDuelMinutes = 3;
            if (m.sq >= 0) chDuelSquads = m.sq == 1; // a friend's code: the maker's choice
            rival = Challenge.FromRival(m.rival);
            if (chFriend == 1 || chFriend == 2)
            {
                chUi.HideFriend();
                chUi.ShowLobby(MyCard(), Challenge.Rank, ModeLabel);
            }
            chUi.LockLobby();
            int rivalPower = 0;
            for (int g = 0; g < Army.Groups; g++) rivalPower += Mathf.RoundToInt(Army.Soldiers * rival.Power(g) * 10f);
            chUi.ShowRival(RivalCard(rival), rival.Rank);
            chUi.SetLobbyStatus(Loc.T(liveRanked ? "live.found" : "live.foundFriend"));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f);
            yield return new WaitForSecondsRealtime(1.2f);
            if (token != chLobbyToken) { LiveNet.Leave(); chLive = false; yield break; }
            int stage = Mathf.Abs(m.seed) % DuelStageCount; // the same stage on both screens
            yield return SpinToStage(stage);
            if (token != chLobbyToken) { LiveNet.Leave(); chLive = false; yield break; }
            chUi.HideLobby();
            BuildDuelBoard(stage);
        }

        // ---------- The match ----------

        /// <summary>This side is ready (the briefing read): the server starts the match when both are.</summary>
        private void LiveWaitFor(LiveWait kind)
        {
            if (chPhase != ChPhase.Lobby) return; // already started (or over)
            liveWait = kind;
            chPhase = ChPhase.Wait;
            LiveNet.Ready();
            chUi.SetStatus(Loc.T("live.waiting"));
        }

        [Serializable] private class DuelStateMsg { public float x, z, r; public int w; public string q; }

        /// <summary>Each frame of a live match: this game's state goes out, the server's words and the rival's state come in.</summary>
        private void UpdateLive()
        {
            if (!chLive || !challengeRun) return;
            if (liveRejoining) return; // coming back after a drop: its own loop reads the server
            // This game's own state (foi and its squad), some times a second.
            if (chPhase == ChPhase.Duel && Time.unscaledTime >= liveStateAt)
            {
                liveStateAt = Time.unscaledTime + 1f / 12f;
                LiveNet.DuelState(robot.transform.position, robot.Visual.eulerAngles.y, robot.Swings, hunt.SquadState());
            }
            LiveNet.Msg m;
            while ((m = LiveNet.Poll()) != null)
            {
                switch (m.t)
                {
                    case "start":
                        liveWait = LiveWait.None;
                        chDuelLeft = m.secs;
                        BeginDuel();
                        break;
                    case "resume":
                        LiveDuelGo(m.secs);
                        break;
                    case "ot":
                        // the server's golden knock-down: the overtime shown, its 3-2-1 (the server holds the blows as long)
                        if (!challengeRun) break;
                        OvertimeShown(m.secs);
                        cameraRig.StartCoroutine(CountdownThenGo());
                        break;
                    case "s":
                    {
                        DuelStateMsg s;
                        try { s = JsonUtility.FromJson<DuelStateMsg>(m.raw); } catch (Exception) { break; }
                        if (s == null) break;
                        hunt.RemoteFoeState(new Vector3(s.x, GridView.ToWorld(new Core.GridPos(Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.z))).y, s.z), s.r, s.w);
                        var near = new Core.GridPos(Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.z));
                        hunt.ApplySquadState(s.q, g => Army.StatsFromTotal(g, rival.points[g]), near);
                        break;
                    }
                    case "hp":
                        // The duel's health as the server keeps it (100 each); held back while a big moment plays.
                        if (DuelCinematic) pendingHp = m;
                        else ApplyLiveHp(m);
                        break;
                    case "shield":
                        // The rival raised its shield (the server counts its blows): its bubble here for the time.
                        if (m.by == 0) { hunt.ShieldFoe(DuelShieldHits); rivalShieldUntil = Time.time + DuelShieldSeconds; }
                        break;
                    case "blk":
                        // A carried shield took a plain blow (the server decides): mine raised, or sparks on the rival's.
                        if (m.by == 1) ShieldShow(hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.position : robot.transform.position + robot.Visual.forward);
                        else if (hunt.ChallengeFoeModel != null)
                        {
                            fx.Burst(hunt.ChallengeFoeModel.position + Vector3.up * 0.7f, new Color(1f, 0.9f, 0.6f), new Color(3f, 2.6f, 1.6f), 18, 4f);
                            AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.9f);
                        }
                        break;
                    case "def":
                        // The server judged a blow against a defence: blocked, broke, perfect, dodged (by 1: mine).
                        LiveGuard(m.by == 1, m.k, m.d);
                        break;
                    case "gr":
                        // The rival raised or lowered its shield: shown on it.
                        LiveRivalGuard(m.f == 1);
                        break;
                    case "hold":
                        chUi.SetStatus(Loc.T("live.rivalDropped"));
                        break;
                    case "back":
                        chUi.SetStatus(Loc.T("ch.duel"));
                        break;
                    case "em":
                        ShowEmote(false, m.e);
                        break;
                    case "sp":
                        LiveRivalMove(m.k, m.m);
                        break;
                    case "ko":
                        LiveKnockdown(m.won, Mathf.RoundToInt(m.mine), Mathf.RoundToInt(m.theirs));
                        break;
                    case "ah":
                        // The rival's blow on one of my soldiers (its foi's: he strikes back).
                        hunt.SquadStruckRemote(m.u, m.d, m.f == 1);
                        break;
                    case "result":
                        FinishLive(m);
                        break;
                    case "closed":
                    case "error":
                        // This game lost the server: it tries to come back to the match (the server waits a moment);
                        // if it can't, the match is over here (the server counts it as left).
                        if (chPhase == ChPhase.Done) { chLive = false; break; }
                        if (liveMatchId != 0) { cameraRig.StartCoroutine(LiveRejoin()); return; }
                        liveRanked = false;
                        liveLost = true;
                        liveChange = 0;
                        chUi.SetStatus(Loc.T("live.lost"));
                        ChallengeFinish(-1, ScoreText());
                        chLive = false;
                        break;
                }
            }
        }

        /// <summary>The live duel's health from the server, held back during a big moment (see <see cref="DuelCinematic"/>).</summary>
        private LiveNet.Msg pendingHp;

        private void ApplyLiveHp(LiveNet.Msg m)
        {
            float before = health;
            health = Mathf.Clamp01(m.mine / 100f);
            RefreshHealthBar();
            if (health < before - 0.0001f)
            {
                // a heavy weapon's blow from the rival staggers foi a moment (the server holds foi's blows as long)
                if (m.by == 0 && rival != null) strikeCooldown = Mathf.Max(strikeCooldown, DuelPower.Stagger(Armory.Get(rival.weapon)));
                ui.Flash(new Color(1f, 0.15f, 0.15f), 0.3f);
                cameraRig.Shake(0.6f);
                fx.Burst(robot.transform.position + Vector3.up * 0.4f, new Color(1f, 0.35f, 0.35f), new Color(2.4f, 0.5f, 0.4f), 30, 6f);
                Haptics.Light();
            }
            else if (health > before + 0.0001f) FloatAt(robot.transform.position, "+" + Mathf.RoundToInt((health - before) * 100f) + "%", new Color(1f, 0.5f, 0.6f));
            hunt.SetFoeHealth(m.theirs / 100f);
            LiveMeters(m.mm, m.tm);
        }

        /// <summary>The server's word on the match: the result card with its score and trophies.</summary>
        private void FinishLive(LiveNet.Msg m)
        {
            if (m.profile != null && !string.IsNullOrEmpty(m.profile.name)) liveProfile = m.profile;
            liveChange = m.change;
            if (overtime && m.outcome > 0) wonInOvertime = true; // (the server's golden knock-down won)
            liveRanked = m.ranked;
            liveLeft = m.left;
            rematchMatch = liveLost || m.left ? 0 : liveMatchId; // a rematch can be asked for (not after a walk-out)
            liveMatchId = 0;
            if (chPhase != ChPhase.Done) ChallengeFinish(m.outcome, string.IsNullOrEmpty(m.score) ? ScoreText() : m.score);
            LiveNet.Close();
        }

        /// <summary>
        /// The connection dropped mid-match: back for it (the server waits 15 s), a few tries; the match carries on where
        /// it was. Not back: the match is lost here.
        /// </summary>
        private IEnumerator LiveRejoin()
        {
            liveRejoining = true;
            long id = liveMatchId;
            chUi.SetStatus(Loc.T("live.reconnecting"));
            hunt.Freeze();
            float until = Time.realtimeSinceStartup + 13f;
            bool back = false;
            while (!back && Time.realtimeSinceStartup < until && challengeRun)
            {
                var connect = LiveNet.Connect();
                while (!connect.IsCompleted) yield return null;
                if (connect.Result)
                {
                    LiveNet.Rejoin(id);
                    float wait = Time.realtimeSinceStartup + 3f;
                    while (Time.realtimeSinceStartup < wait && !back)
                    {
                        var m = LiveNet.Poll();
                        if (m != null && m.t == "rejoined")
                        {
                            back = true;
                            liveSide = m.side == 1 ? 1 : 0;
                            chDuelMe = Mathf.RoundToInt(m.mine);
                            chDuelThem = Mathf.RoundToInt(m.theirs);
                            chDuelLeft = m.secs;
                        }
                        else if (m != null && m.t == "norejoin") { until = 0f; break; }
                        yield return null;
                    }
                }
                if (!back) yield return new WaitForSecondsRealtime(1f);
            }
            liveRejoining = false;
            if (back && challengeRun)
            {
                hunt.Resume();
                if (chPhase == ChPhase.Break) chPhase = ChPhase.Duel;
                RefreshDuelClock();
                chUi.SetStatus(Loc.T("live.back"));
                yield break;
            }
            if (!challengeRun || chPhase == ChPhase.Done) yield break;
            liveRanked = false;
            liveLost = true;
            liveChange = 0;
            liveMatchId = 0;
            chUi.SetStatus(Loc.T("live.lost"));
            ChallengeFinish(-1, ScoreText());
            chLive = false;
        }

        /// <summary>The result card's REMATCH: the same rival asked again (both must ask within half a minute).</summary>
        private void StartRematch(long matchId)
        {
            rematchMatch = matchId;
            chFriend = 3;
            chCancelled = false;
            chPhase = ChPhase.Lobby;
            cameraRig.StartCoroutine(Lobby());
        }

        // ---------- Playing a friend ----------

        /// <summary>The friend panel's MAKE A CODE (1, with the minutes picked; an invited friend's id) or ENTER CODE (2).</summary>
        private void StartFriend(int kind, string code, int minutes, string invite = null)
        {
            if (!Challenge.Open) { chUi.SetFriendStatus(Loc.T("ch.locked")); return; }
            if (!Challenge.Enabled) { chUi.SetFriendStatus(Loc.T("ch.disabled")); return; }
            if (!Challenge.NameChosen) { chUi.HideFriend(); chUi.ShowName(Challenge.PlayerName); return; }
            chFriend = kind;
            chFriendCode = code;
            friendInvite = invite;
            friendMinutes = minutes is 3 or 5 or 10 ? minutes : 3;
            chDuelMinutes = 3;
            chCancelled = false;
            chPhase = ChPhase.Lobby;
            chUi.SetFriendBusy(true);
            chUi.SetFriendStatus(Loc.T("live.preparing"));
            cameraRig.StartCoroutine(Lobby());
        }

        private void CloseFriend()
        {
            chCancelled = true;
            chLobbyToken++;
            LiveNet.Close();
            chFriend = 0;
            if (chPhase == ChPhase.Lobby) chPhase = ChPhase.Off;
            chUi.HideFriend();
        }

        /// <summary>Leaving a live match (or its search): a loss on the server; the connection closes.</summary>
        private void EndLive()
        {
            if (chLive && chPhase != ChPhase.Done) LiveNet.Leave();
            else LiveNet.Close();
            chLive = false;
            liveWait = LiveWait.None;
            hunt.FoeRemote = false;
        }
    }
}
