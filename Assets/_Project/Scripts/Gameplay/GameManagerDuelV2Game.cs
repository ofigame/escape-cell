using SquashBot.Data;
using SquashBot.DuelV2;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge duels fought by the v2 core (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md, Faz 10): the game sets the duel up
    /// as it always did (the stage 1.5 times as wide, its crowd, the two foi, their weapons and crowns, the rival and its
    /// bot), then the v2 core takes the two over: rounds, the rhythmic chain, the elemental rains, its own camera. When
    /// the match is decided its result goes back into the game's own ending (rewards, rank, chests, goals).
    /// </summary>
    public partial class GameManager
    {
        private GameDuelHostV2 v2Host;
        /// <summary>The last v2 match's flawless rounds won (their XP in the rewards).</summary>
        private int v2Flawless;
        private DuelCameraV2 v2Cam;

        /// <summary>This duel is fought by the v2 core (against the computer: here; live: the server runs it, protocol 10).</summary>
        private bool V2Duel => DuelCoreV2.Enabled && !DuelCoreV2.PrototypeScene && challengeRun;

        /// <summary>The platform's width along the fighters' line for the duel about to be built (1: the old duel's).</summary>
        private float V2BoardWidth => V2Duel ? DuelCoreSettings.Fresh().platformWidth : 1f;

        /// <summary>The v2 core takes the duel set up on the board (instead of the old 5-4-3-2-1 and fight).</summary>
        private void V2GameBegin()
        {
            var s = DuelCoreSettings.Fresh();
            var o = new PrototypeOptions { mode = PrototypeOptions.Mode.PlayerVsBot, stage = duelStage, width = s.platformWidth };
            o.family[0] = (int)Armory.Family(Armory.Equipped);
            o.family[1] = rival != null ? (int)Armory.Family(Armory.Get(rival.weapon)) : 0;
            o.tier[0] = DuelPower.TierOf(Challenge.Rank);
            o.tier[1] = rival != null ? DuelPower.TierOf(rival.Rank) : o.tier[0];
            o.skill[1] = GameDuelHostV2.SkillOf(botTuning);
            o.persona[1] = GameDuelHostV2.PersonaOf(botTuning);
            if (LiveActive) o.mode = PrototypeOptions.Mode.Live; // (the server runs the rules: this game shows its states)
            else if (GameDuelHostV2.BotPlaysFoi) { o.mode = PrototypeOptions.Mode.BotVsBot; o.skill[0] = 0.6f; }
            o.names[0] = Challenge.PlayerName;
            o.names[1] = rival != null ? rival.name : Loc.T("v2.side.rival");
            V2ApplyRule(s);
            o.roundSeconds = s.roundSeconds; o.roundsToWin = s.roundsToWin;

            hunt.Freeze();
            robot.enabled = false;
            var foe = hunt.ChallengeFoeModel;
            var foeSkin = foe != null ? foe.GetComponentInChildren<SkinnedHero>() : null;
            if (foeSkin != null) ShieldHold.Attach(foeSkin.transform.parent != null ? foeSkin.transform.parent : foeSkin.transform); // (a shield on its arm as foi has)

            DuelCoreV2.Running = DuelCoreV2.InGame = true;
            chPhase = ChPhase.Duel;
            v2Cam = DuelCameraV2.Create(sideDir, V2Line, V2FighterScale);
            if (cameraRig != null) { cameraRig.SetMenuFocus(false); if (cameraRig.Cam != null) cameraRig.Cam.enabled = false; } // (the menu's blur off: it is a global volume, on the v2 camera too)
            v2Host = new GameDuelHostV2(this, o, s, V2GameDone) { Pause = Pause, Happened = V2GameEvent };
            V2HideBattleHud(true);
            if (LiveActive) { v2Host.Mirror = liveSide == 1; v2Host.Send = LiveNet.InputV2; }
            v2Host.Fight = DuelFightV2.Begin(v2Host, v2Cam);
            if (LiveActive) { v2LiveResult = null; v2LiveDone = false; LiveNet.ReadyV2(v2Host.Fight.Sim.Height); }
            FirstSession.DuelStarted(); // (time_to_first_duel)
        }

        /// <summary>The old duel's screen (its bars, clock, skill bar, the stick) out of sight while the v2 one shows (and back).</summary>
        private void V2HideBattleHud(bool hide)
        {
            if (chUi != null && chUi.BattleHud != null) chUi.BattleHud.gameObject.SetActive(!hide); // (its bars, clock, squad bar, the skill bar)
            if (ui != null) { ui.SetHudHidden(hide); ui.ShowStick(false, default, default); ui.SetFire(false, false); }
        }

        /// <summary>
        /// The week's rule and the tournament on the match's numbers (Docs 3.4): sudden death one round of 30 s, the quick
        /// ultimate a bar of two parts, no hearts, shields twice as strong, the combo frenzy's series lasting longer and its
        /// knockdown sooner; the tournament's rounds 45 s. (One weapon, the giant and the random set: the game's own, as before.)
        /// </summary>
        private void V2ApplyRule(DuelCoreSettings s)
        {
            if (chMode == 3) s.roundSeconds = 45f;
            if (duelRule == null) return;
            switch (duelRule.Value)
            {
                case WeeklyMode.Rule.SuddenDeath: s.roundsToWin = 1; s.roundSeconds = 30f; break;
                case WeeklyMode.Rule.QuickUlti: s.meterMax = 2f; break;
                case WeeklyMode.Rule.Heartless: s.heartsPerMatch = 0; break;
                case WeeklyMode.Rule.ShieldWar: s.shieldDurability *= 2f; break;
                case WeeklyMode.Rule.ComboFrenzy: s.seriesGap *= 2f; s.knockdownAt = Mathf.Max(3, s.knockdownAt - 2); break;
            }
        }

        /// <summary>
        /// A fight's deed counted as the old duel counted it (the quests, the match's challenge): the chain's set of three
        /// for the old combo, a knockdown landed for the finish, a perfect block, an ultimate dodged, a special, an ultimate,
        /// a heart used.
        /// </summary>
        private void V2GameEvent(DuelEvent e)
        {
            switch (e.kind)
            {
                case DuelEventKind.Result:
                    if (e.who == 0 && e.result == HitResult.Hit && e.why == "set end") NoteDuel(DuelGoal.Combo5);
                    if (e.who == 0 && e.result == HitResult.Hit && e.move == MoveKind.Knockdown) ChallengeQuests.Report(DuelGoal.Finish);
                    if (e.who == 1 && e.result == HitResult.Perfect) { mcPerfects++; NoteDuel(DuelGoal.Perfect); }
                    if (e.who == 1 && e.result == HitResult.Dodged && e.move == MoveKind.Ulti) { mcUltiDodges++; NoteDuel(DuelGoal.DodgeUlti); }
                    break;
                case DuelEventKind.Special: if (e.who == 0) NoteDuel(DuelGoal.Special); break;
                case DuelEventKind.UltiStart: if (e.who == 0) NoteDuel(DuelGoal.Ultimate); break;
                case DuelEventKind.Heart: if (e.who == 0) mcHearts++; break;
            }
        }

        /// <summary>Each frame while the v2 core fights a game duel: the back button, the pause (the fight stands still).</summary>
        private void V2GameTick()
        {
            if (v2Host != null) v2Host.Paused = State == GameState.Paused && !LiveActive; // (a live duel never stands still)
            if (LiveActive) UpdateLive();
        }

        /// <summary>A live duel's state from the server: into the fight's queue.</summary>
        private void V2LiveState(LiveNet.Msg m)
        {
            if (v2Host == null) return;
            DuelStateMessageV2 s;
            try { s = JsonUtility.FromJson<DuelStateMessageV2>(m.raw); } catch (System.Exception) { return; }
            if (s != null && s.s != null) v2Host.States.Enqueue(s.s);
        }

        /// <summary>A live duel's result: kept until the fight has shown its end (the knock-out, the replay), then the game's ending.</summary>
        private LiveNet.Msg v2LiveResult;
        private bool v2LiveDone;

        private void V2LiveResult(LiveNet.Msg m)
        {
            v2LiveResult = m;
            if (v2LiveDone || m.left) { V2GameRelease(); FinishLive(m); } // (the rival walked out: at once)
        }

        /// <summary>The match decided (its last moments shown): the rounds become the duel's score, then the game's own ending.</summary>
        private void V2GameDone(DuelSim sim)
        {
            if (!DuelCoreV2.InGame) return;
            if (LiveActive)
            {
                // the server's word decides (and brings the rank points): its result now, or when it comes
                chDuelMe = sim.F[0].roundsWon; chDuelThem = sim.F[1].roundsWon;
                if (sim.MatchWinner == 0 && sim.Comeback) matchWorst = 2;
                wonInOvertime = sim.MatchWinner == 0 && sim.LastRoundGolden;
                v2Flawless = sim.F[0].flawlessRounds;
                v2LiveDone = true;
                if (v2LiveResult != null) { var r = v2LiveResult; V2GameRelease(); FinishLive(r); }
                return;
            }
            chDuelMe = sim.F[0].roundsWon;
            chDuelThem = sim.F[1].roundsWon;
            if (sim.MatchWinner == 0 && sim.Comeback) matchWorst = 2; // (0–1, then won: the comeback's reward)
            wonInOvertime = sim.MatchWinner == 0 && sim.LastRoundGolden;
            v2Flawless = sim.F[0].flawlessRounds;
            V2GameRelease();
            ChallengeEnd();
        }

        /// <summary>The v2 core lets the duel go (its end, or the duel left): its camera and pictures away, foi the game's again.</summary>
        private void V2GameRelease()
        {
            if (v2Host != null && v2Host.Fight != null) Destroy(v2Host.Fight.gameObject);
            v2Host = null;
            if (v2Cam != null) Destroy(v2Cam.gameObject);
            v2Cam = null;
            if (cameraRig != null && cameraRig.Cam != null) cameraRig.Cam.enabled = true;
            V2HideBattleHud(false);
            DuelCoreV2.Running = DuelCoreV2.InGame = false;
            Time.timeScale = 1f;
            if (robot != null) robot.enabled = true;
        }
    }
}
