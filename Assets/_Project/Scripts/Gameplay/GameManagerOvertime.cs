using System.Collections;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The golden knock-down (the fixes design's job 12): a level score at time up plays on (the duel table's 30 s) with a
    /// big "GOLDEN KNOCK-DOWN!" and a 3-2-1; health and hearts carry on as they were. The first knock-down wins; if none
    /// comes, the larger share of health wins ("More health wins!"), and only health within a hair of each other is a draw.
    /// Not in the sudden-death week (one knock-down ends it anyway), the tutorial, the training room or the lab. A live
    /// match's overtime is the server's ("ot"), shown here the same way.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The duel is in its golden knock-down overtime.</summary>
        private bool overtime;
        /// <summary>The last duel was won in the overtime (its band on the rewards).</summary>
        private bool wonInOvertime;

        private bool OvertimeAllowed => !SuddenDeath && !tutorialOn && !trainingOn && !LabOn;

        /// <summary>A new duel: no overtime yet.</summary>
        private void ResetOvertime()
        {
            overtime = false;
            wonInOvertime = false;
        }

        /// <summary>Time is up: with the score level the overtime starts (true); else the match is decided now (false).</summary>
        private bool TryStartOvertime()
        {
            if (overtime || chDuelMe != chDuelThem || !OvertimeAllowed || LiveActive) return false;
            OvertimeShown(DuelBalanceTable.Current.overtimeSeconds);
            cameraRig.StartCoroutine(OvertimeGo());
            return true;
        }

        /// <summary>The overtime begins (here, or as the server says): the clock set, the board held, the big title.</summary>
        private void OvertimeShown(float seconds)
        {
            overtime = true;
            chDuelLeft = seconds;
            chPhase = ChPhase.Break;
            hunt.Freeze();
            lastBeat = -1;
            RefreshDuelClock();
            chUi.SetStatus(Loc.T("duel.goldenKo"));
            ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.62f), Loc.T("duel.goldenKo"), Palette.UiGold, 130f);
            chUi.PulseScore(1.5f);
            AudioManager.SetTension(1f);
            AudioManager.PlaySfx(Sfx.Win, 1f, 0.7f);
            if (colosseum != null) { colosseum.Cheer(0, 1f); colosseum.Cheer(1, 1f); }
        }

        private IEnumerator OvertimeGo()
        {
            yield return new WaitForSeconds(1.4f);
            while (challengeRun && State == GameState.Paused) yield return null;
            if (!challengeRun || chPhase != ChPhase.Break || State != GameState.Playing) yield break;
            yield return CountdownThenGo(); // (both stay where they stand, health as it is)
        }

        /// <summary>The overtime ran out with no knock-down: the larger share of health wins.</summary>
        private void OvertimeEnd()
        {
            chPhase = ChPhase.Break;
            hunt.Freeze();
            float mine = health, theirs = hunt.ChallengeFoeHealth;
            int outcome = Mathf.Abs(mine - theirs) <= DuelBalanceTable.Current.overtimeHealthTie ? 0 : mine > theirs ? 1 : -1;
            if (outcome != 0) chUi.SetStatus(Loc.T("duel.byHealth"));
            wonInOvertime = outcome > 0;
            ChallengeFinish(outcome, ScoreText());
        }

        /// <summary>A knock-down in the overtime: it decides the match at once.</summary>
        private void OvertimeKnockdown(bool iWon)
        {
            wonInOvertime = iWon;
            RefreshDuelClock();
            chPhase = ChPhase.Break;
            hunt.Freeze();
            ChallengeEnd();
        }
    }
}
