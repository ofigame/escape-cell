using System;
using System.Collections;
using SquashBot.Audio;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Live challenges: the waiting room first looks for a live rival who picked the same kind (and duel length) for a
    /// while, or waits for a friend's code, and only then takes a rival played by the AI. In a live match each game moves
    /// its own fighters and sends their state some times a second (the other game shows them; nothing is kept); the
    /// server checks the blows and keeps the score and the clock. A random live match moves trophies; a friend's does
    /// not. A player who leaves loses at once.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Seconds the waiting room looks for a live rival before it takes an AI-played one.</summary>
        private const float LiveWaitSeconds = 20f;

        private bool chLive;
        /// <summary>This game's side in the live match (0: the south end, left on screen; 1: the north, right).</summary>
        private int liveSide;
        private bool liveRanked, liveLeft, liveLost;
        /// <summary>0: a random rival; 1: making a code for a friend; 2: entering a friend's code.</summary>
        private int chFriend;
        private string chFriendCode;
        private int friendMode, friendMinutes = 3;

        private enum LiveWait { None, Start }
        private LiveWait liveWait;
        private int liveChange;
        private FoiNet.Profile liveProfile;
        private float liveStateAt;

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
            int mode = chMode == ChMode.Duel ? 1 : 0;
            if (chFriend == 1) LiveNet.Host(friendMode, friendMinutes);
            else if (chFriend == 2) LiveNet.Join(chFriendCode);
            else LiveNet.Queue(mode, chDuelMinutes);
            float until = Time.realtimeSinceStartup + (chFriend == 0 ? LiveWaitSeconds : 600f);
            while (!LobbyOver(token))
            {
                float left = until - Time.realtimeSinceStartup;
                if (left <= 0f) break;
                if (chFriend == 0) chUi.SetLobbyStatus(Loc.F("live.searching", Mathf.CeilToInt(left)));
                var m = LiveNet.Poll();
                if (m != null)
                {
                    switch (m.t)
                    {
                        case "matched":
                            done(m);
                            yield break;
                        case "code":
                            chUi.SetFriendCode(m.code);
                            chUi.SetFriendStatus(Loc.T("live.codeWait"));
                            break;
                        case "nocode":
                            chUi.SetFriendStatus(Loc.T("live.noCode"));
                            chUi.SetFriendBusy(false);
                            LiveNet.Close();
                            done(null);
                            yield break;
                        case "closed":
                        case "error":
                            if (chFriend != 0) { chUi.SetFriendStatus(Loc.T("live.offline")); chUi.SetFriendBusy(false); }
                            LiveNet.Close();
                            done(null);
                            yield break;
                    }
                }
                yield return null;
            }
            LiveNet.Cancel();
            LiveNet.Close();
            if (chFriend != 0 && !LobbyOver(token)) { chUi.SetFriendStatus(Loc.T("live.codeExpired")); chUi.SetFriendBusy(false); }
            done(null);
        }

        /// <summary>A live rival was found: its card in the waiting room, then the arena of the kind it was made for.</summary>
        private IEnumerator StartLive(LiveNet.Msg m, int world, int token)
        {
            chLive = true;
            liveSide = m.side == 1 ? 1 : 0;
            liveRanked = m.ranked;
            liveLeft = false;
            liveLost = false;
            liveWait = LiveWait.None;
            liveProfile = null;
            liveChange = 0;
            chOnline = true;
            chMatchId = 0;
            chMode = m.mode == 1 ? ChMode.Duel : ChMode.Army;
            chDuelMinutes = m.min is 3 or 5 or 10 ? m.min : 3;
            rival = Challenge.FromRival(m.rival);
            if (chFriend != 0)
            {
                chUi.HideFriend();
                chUi.ShowLobby(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore), Challenge.Rank, ModeLabel);
            }
            chUi.LockLobby();
            int rivalPower = 0;
            for (int g = 0; g < Army.Groups; g++) rivalPower += Mathf.RoundToInt(Army.Soldiers * rival.Power(g) * 10f);
            chUi.ShowRival(SideText(rival.name, rival.Rank, rival.trophies, rivalPower), rival.Rank);
            chUi.SetLobbyStatus(Loc.T(liveRanked ? "live.found" : "live.foundFriend"));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f);
            yield return new WaitForSecondsRealtime(1.8f);
            if (token != chLobbyToken) { LiveNet.Leave(); chLive = false; yield break; }
            chUi.HideLobby();
            if (chMode == ChMode.Duel) BuildDuelBoard(world);
            else BuildArmyBoard(world);
        }

        // ---------- The match ----------

        /// <summary>This side is ready (the briefing read): the server starts the match when both are.</summary>
        private void LiveWaitFor(LiveWait kind)
        {
            liveWait = kind;
            chPhase = ChPhase.Wait;
            LiveNet.Ready();
            chUi.SetStatus(Loc.T("live.waiting"));
        }

        [Serializable] private class ArmyHitMsg { public int u; public float d; }
        [Serializable] private class DuelStateMsg { public float x, z, r; public int w; }

        /// <summary>Each frame of a live match: this game's state goes out, the server's words and the rival's state come in.</summary>
        private void UpdateLive()
        {
            if (!chLive || !challengeRun) return;
            // This game's own state, some times a second (a duel's foi often, an army's soldiers less).
            if (chPhase == ChPhase.Duel || chPhase == ChPhase.Army)
            {
                float every = chPhase == ChPhase.Duel ? 1f / 15f : 1f / 6f;
                if (Time.unscaledTime >= liveStateAt)
                {
                    liveStateAt = Time.unscaledTime + every;
                    if (chPhase == ChPhase.Duel) LiveNet.DuelState(robot.transform.position, robot.Visual.eulerAngles.y, robot.Swings);
                    else if (army != null) LiveNet.ArmyState(army.StateBody());
                }
            }
            LiveNet.Msg m;
            while ((m = LiveNet.Poll()) != null)
            {
                switch (m.t)
                {
                    case "start":
                        liveWait = LiveWait.None;
                        if (chMode == ChMode.Duel) { chDuelLeft = m.secs; BeginDuel(); }
                        break;
                    case "resume":
                        if (chMode == ChMode.Duel) LiveDuelGo(m.secs);
                        break;
                    case "s":
                        if (chMode == ChMode.Duel)
                        {
                            DuelStateMsg s;
                            try { s = JsonUtility.FromJson<DuelStateMsg>(m.raw); } catch (Exception) { break; }
                            if (s != null) hunt.RemoteFoeState(new Vector3(s.x, GridView.ToWorld(new Core.GridPos(Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.z))).y, s.z), s.r, s.w);
                        }
                        else if (army != null) army.ApplyState(m.raw);
                        break;
                    case "hp":
                    {
                        // The duel's health as the server keeps it (100 each).
                        float before = health;
                        health = Mathf.Clamp01(m.mine / 100f);
                        RefreshHealthBar();
                        if (health < before - 0.0001f)
                        {
                            ui.Flash(new Color(1f, 0.15f, 0.15f), 0.3f);
                            cameraRig.Shake(0.6f);
                            fx.Burst(robot.transform.position + Vector3.up * 0.4f, new Color(1f, 0.35f, 0.35f), new Color(2.4f, 0.5f, 0.4f), 30, 6f);
                            Haptics.Light();
                        }
                        hunt.SetFoeHealth(m.theirs / 100f);
                        break;
                    }
                    case "ko":
                        LiveKnockdown(m.won, Mathf.RoundToInt(m.mine), Mathf.RoundToInt(m.theirs));
                        break;
                    case "round":
                        chArmyMe = Mathf.RoundToInt(m.mine);
                        chArmyThem = Mathf.RoundToInt(m.theirs);
                        liveWait = LiveWait.None;
                        chArmyLeft = m.secs;
                        StartArmyRound(m.n);
                        chArmyLeft = m.secs;
                        break;
                    case "ah":
                    {
                        if (army == null) break;
                        ArmyHitMsg h;
                        try { h = JsonUtility.FromJson<ArmyHitMsg>(m.raw); } catch (Exception) { break; }
                        if (h != null) army.ApplyHit(h.u, h.d);
                        break;
                    }
                    case "roundEnd":
                        chArmyMe = Mathf.RoundToInt(m.mine);
                        chArmyThem = Mathf.RoundToInt(m.theirs);
                        if (chPhase == ChPhase.Army) ShowArmyRoundEnd(m.won ? 1 : m.even ? 0 : -1);
                        break;
                    case "result":
                        FinishLive(m);
                        break;
                    case "closed":
                    case "error":
                        // This game lost the server: the match is over here (the server counts it as left).
                        if (chPhase == ChPhase.Done) { chLive = false; break; }
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

        /// <summary>The server's word on the match: the result card with its score and trophies.</summary>
        private void FinishLive(LiveNet.Msg m)
        {
            if (m.profile != null && !string.IsNullOrEmpty(m.profile.name)) liveProfile = m.profile;
            liveChange = m.change;
            liveRanked = m.ranked;
            liveLeft = m.left;
            if (chPhase != ChPhase.Done) ChallengeFinish(m.outcome, string.IsNullOrEmpty(m.score) ? ScoreText() : m.score);
            LiveNet.Close();
        }

        // ---------- Playing a friend ----------

        /// <summary>The friend panel's MAKE A CODE (1, with the kind and minutes picked) or ENTER CODE (2).</summary>
        private void StartFriend(int kind, string code, int mode, int minutes)
        {
            if (!Challenge.Open) { chUi.SetFriendStatus(Loc.T("ch.locked")); return; }
            if (!Challenge.Enabled) { chUi.SetFriendStatus(Loc.T("ch.disabled")); return; }
            if (!Challenge.NameChosen) { chUi.HideFriend(); chUi.ShowName(Challenge.PlayerName); return; }
            chFriend = kind;
            chFriendCode = code;
            friendMode = mode;
            friendMinutes = minutes is 3 or 5 or 10 ? minutes : 3;
            chMode = mode == 1 ? ChMode.Duel : ChMode.Army;
            chDuelMinutes = friendMinutes;
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
