using System;
using System.Collections;
using SquashBot.Audio;
using SquashBot.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Live challenges (the arena kind): the waiting room first looks for a live rival for a while (or waits for a
    /// friend's code), and only then falls back to a rival's army played by the AI. In a live match each turn's picks
    /// meet on the server, which decides every clash and the all-out battle; both games play out that same result. The
    /// duel's rounds are fought against the rival's foi (its AI) on each side and the server compares them. If the rival
    /// leaves (or the connection drops) the match goes on against the AI.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Seconds the waiting room looks for a live rival before it takes an AI-played one.</summary>
        private const float LiveWaitSeconds = 20f;

        private bool chLive, chLiveSolo;
        /// <summary>0: a random rival; 1: making a code for a friend; 2: entering a friend's code.</summary>
        private int chFriend;
        private string chFriendCode;

        private enum LiveWait { None, Start, AfterClash, Result }
        private LiveWait liveWait;
        /// <summary>The clash about to be played: what is left of each group in it (from the server), -1 when none.</summary>
        private int liveMyLeft = -1, liveTheirLeft = -1, liveClashMine = -1, liveClashTheirs = -1;
        private float liveDuelStart, liveResultBy;
        private bool livePickSent;
        private int liveChange;
        private FoiNet.Profile liveProfile;

        /// <summary>A live match with the rival still there (the server decides).</summary>
        private bool LiveActive => chLive && !chLiveSolo;

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
            if (chFriend == 1) LiveNet.Host();
            else if (chFriend == 2) LiveNet.Join(chFriendCode);
            else LiveNet.Queue();
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

        /// <summary>A live rival was found: its card in the waiting room, then the arena.</summary>
        private IEnumerator StartLive(LiveNet.Msg m, int world, int token)
        {
            chLive = true;
            chLiveSolo = false;
            liveWait = LiveWait.None;
            liveMyLeft = liveTheirLeft = liveClashMine = liveClashTheirs = -1;
            liveProfile = null;
            chOnline = true;
            chMatchId = 0;
            chMode = ChMode.Arena;
            rival = Challenge.FromRival(m.rival);
            if (chFriend != 0)
            {
                chUi.HideFriend();
                chUi.ShowLobby(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore), Challenge.Rank, Loc.T("ch.mode.arena"));
            }
            chUi.LockLobby();
            int rivalPower = 0;
            for (int g = 0; g < Army.Groups; g++) rivalPower += Mathf.RoundToInt(Army.Soldiers * rival.Power(g) * 10f);
            chUi.ShowRival(SideText(rival.name, rival.Rank, rival.trophies, rivalPower), rival.Rank);
            chUi.SetLobbyStatus(Loc.T("live.found"));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f);
            yield return new WaitForSecondsRealtime(1.8f);
            if (token != chLobbyToken) { LiveNet.Leave(); chLive = false; yield break; }
            chUi.HideLobby();
            BuildBoard(world);
        }

        // ---------- The match ----------

        /// <summary>This side is ready for what comes next (the briefing read, a clash played out): the server goes on.</summary>
        private void LiveWaitFor(LiveWait kind)
        {
            liveWait = kind;
            chPhase = ChPhase.Wait;
            LiveNet.Ready();
            chUi.SetStatus(Loc.T("live.waiting"));
        }

        /// <summary>The last round is in: the server compares both sides' duels and settles the match.</summary>
        private void LiveAwaitResult()
        {
            liveWait = LiveWait.Result;
            chPhase = ChPhase.Wait;
            liveResultBy = Time.realtimeSinceStartup + 330f;
            chUi.SetStatus(Loc.T("live.waitDuel"));
        }

        /// <summary>Each frame of a live match: what the server says.</summary>
        private void UpdateLive()
        {
            if (!chLive || !challengeRun) return;
            if (LiveActive && liveWait == LiveWait.Result && Time.realtimeSinceStartup > liveResultBy) { chOnline = false; LiveGoSolo(null); return; }
            LiveNet.Msg m;
            while ((m = LiveNet.Poll()) != null)
            {
                switch (m.t)
                {
                    case "turn":
                        if (!LiveActive) break;
                        liveWait = LiveWait.None;
                        livePickSent = false;
                        BeginTurn();
                        break;
                    case "rivalPicked":
                        if (LiveActive && chPhase == ChPhase.Pick && chMyPick < 0) chUi.SetStatus(Loc.T("live.rivalPicked"));
                        break;
                    case "reveal":
                        if (!LiveActive) break;
                        chMyPick = m.mine;
                        chTheirPick = m.theirs;
                        liveMyLeft = m.myLeft;
                        liveTheirLeft = m.theirLeft;
                        chUi.ClosePick();
                        clash.SetPickable(null, -1);
                        chPhase = ChPhase.Reveal;
                        chTimer = 1.2f;
                        chUi.SetClock("");
                        chUi.SetStatus(Loc.T("army.kind." + Army.KindOf(chMyPick)) + "  VS  " + Loc.T("army.kind." + Army.KindOf(chTheirPick)));
                        AudioManager.PlaySfx(Sfx.Warning, 0.7f, 1.1f);
                        break;
                    case "battle":
                        if (!LiveActive) break;
                        liveWait = LiveWait.None;
                        clash.Script(Mathf.Max(0, Total(myLeft) - m.myLeft), Mathf.Max(0, Total(theirLeft) - m.theirLeft));
                        StartBattle();
                        break;
                    case "result":
                        FinishLive(m);
                        break;
                    case "rivalLeft":
                        LiveGoSolo("live.rivalLeft");
                        break;
                    case "closed":
                    case "error":
                        if (chPhase == ChPhase.Done) { chLive = false; break; }
                        chOnline = false; // nothing more reaches the server: practice from here
                        LiveGoSolo("live.lost");
                        break;
                }
            }
        }

        /// <summary>The live clash about to start: it ends as the server decided.</summary>
        private void ScriptLiveClash()
        {
            if (liveMyLeft < 0) return;
            clash.Script(Mathf.Max(0, myLeft[chMyPick] - liveMyLeft), Mathf.Max(0, theirLeft[chTheirPick] - liveTheirLeft));
            liveClashMine = liveMyLeft;
            liveClashTheirs = liveTheirLeft;
            liveMyLeft = liveTheirLeft = -1;
        }

        /// <summary>A live pick turn: the clock runs out with no pick, any group left goes (the rival's comes from the server).</summary>
        private void LivePickTick()
        {
            if (chTimer > 0f || chMyPick >= 0 || livePickSent) return;
            int start = Random.Range(0, Army.Groups);
            for (int k = 0; k < Army.Groups; k++)
                if (myLeft[(start + k) % Army.Groups] > 0) { chMyPick = (start + k) % Army.Groups; break; }
            if (chMyPick >= 0) { LiveNet.Pick(chMyPick); livePickSent = true; }
            chUi.SetStatus(Loc.T("ch.waitRival"));
        }

        /// <summary>The rival is gone (or the connection): the match goes on against the AI from where it stands.</summary>
        private void LiveGoSolo(string why)
        {
            if (!chLive || chLiveSolo) return;
            chLiveSolo = true;
            if (why != null) { chUi.SetStatus(Loc.T(why)); ui.ShowIntro(Loc.T(why), ""); }
            var waited = liveWait;
            liveWait = LiveWait.None;
            switch (waited)
            {
                case LiveWait.Start:
                    BeginTurn();
                    break;
                case LiveWait.AfterClash:
                {
                    int half = Army.Groups * Army.Soldiers / 2;
                    if (Total(myLeft) > half && Total(theirLeft) > half) BeginTurn();
                    else StartBattle();
                    break;
                }
                case LiveWait.Result:
                    ChallengeEnd();
                    break;
            }
            // A turn being picked: the AI picks for the rival (as offline). A clash or battle playing: it plays on.
            if (chPhase == ChPhase.Pick && chTheirPick < 0) chBotPickAt = Mathf.Min(chBotPickAt, chTimer - 1f);
        }

        /// <summary>The server's word on the match: the result card with its score and trophies.</summary>
        private void FinishLive(LiveNet.Msg m)
        {
            if (m.profile != null && !string.IsNullOrEmpty(m.profile.name)) liveProfile = m.profile;
            if (chPhase == ChPhase.Done)
            {
                // Settled after this side's own end (it played on alone): only the record is brought up to date.
                if (liveProfile != null) Challenge.Sync(liveProfile);
                LiveNet.Close();
                chLive = false;
                return;
            }
            liveChange = m.change;
            chPhase = ChPhase.Wait; // (from the waiting phase to the result)
            ChallengeFinish(m.outcome, string.IsNullOrEmpty(m.score) ? ScoreText() : m.score);
            LiveNet.Close();
        }

        // ---------- Playing a friend ----------

        /// <summary>The friend panel's MAKE A CODE (1) or ENTER CODE (2): the online steps, then the code.</summary>
        private void StartFriend(int kind, string code)
        {
            if (!Challenge.Open) { chUi.SetFriendStatus(Loc.T("ch.locked")); return; }
            if (!Challenge.Enabled) { chUi.SetFriendStatus(Loc.T("ch.disabled")); return; }
            if (!Challenge.NameChosen) { chUi.HideFriend(); chUi.ShowName(Challenge.PlayerName); return; }
            chFriend = kind;
            chFriendCode = code;
            chMode = ChMode.Arena;
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
            if (chLive || LiveNet.Open) LiveNet.Leave();
            chLive = chLiveSolo = false;
            liveWait = LiveWait.None;
            chFriend = 0;
        }
    }
}
