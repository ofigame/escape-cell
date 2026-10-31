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
        private DuelCameraV2 v2Cam;

        /// <summary>This duel is fought by the v2 core (a duel against the computer; a live one too once the server speaks v2).</summary>
        private bool V2Duel => DuelCoreV2.Enabled && !DuelCoreV2.PrototypeScene && challengeRun && !LiveActive;

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
            if (GameDuelHostV2.BotPlaysFoi) { o.mode = PrototypeOptions.Mode.BotVsBot; o.skill[0] = 0.6f; }
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
            if (cameraRig != null && cameraRig.Cam != null) cameraRig.Cam.enabled = false;
            v2Host = new GameDuelHostV2(this, o, s, V2GameDone);
            v2Host.Fight = DuelFightV2.Begin(v2Host, v2Cam);
            FirstSession.DuelStarted(); // (time_to_first_duel)
        }

        /// <summary>The week's rule and the tournament on the match's numbers (Docs 3.4).</summary>
        private void V2ApplyRule(DuelCoreSettings s) { }

        /// <summary>Each frame while the v2 core fights a game duel: the back button, the pause (the fight stands still).</summary>
        private void V2GameTick()
        {
            if (v2Host != null) v2Host.Paused = State == GameState.Paused;
        }

        /// <summary>The match decided (its last moments shown): the rounds become the duel's score, then the game's own ending.</summary>
        private void V2GameDone(DuelSim sim)
        {
            if (!DuelCoreV2.InGame) return;
            chDuelMe = sim.F[0].roundsWon;
            chDuelThem = sim.F[1].roundsWon;
            if (sim.MatchWinner == 0 && sim.Comeback) matchWorst = 2; // (0–1, then won: the comeback's reward)
            wonInOvertime = sim.MatchWinner == 0 && sim.LastRoundGolden;
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
            DuelCoreV2.Running = DuelCoreV2.InGame = false;
            Time.timeScale = 1f;
            if (robot != null) robot.enabled = true;
        }
    }
}
