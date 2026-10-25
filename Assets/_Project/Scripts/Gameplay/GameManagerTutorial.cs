using System.Collections;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.UI;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The first ten minutes (the design's §9): a new player's first duel is a guided one, Bip talking it through step by
    /// step, each shown by a hand, the next only once the last was done — tap to strike, drag to walk, swipe up to jump,
    /// hold the shield, a perfect guard (the rival's blow slowed, the window wide: 300 ms), swipe down to dodge, five
    /// blows and the POWER HIT, the ultimate (the bar given full) — and the rival loses by a hair at the end. SKIP is
    /// always there. Offered once by itself, again from the duel controls card.
    /// </summary>
    public partial class GameManager
    {
        private bool tutorialOn;
        private int tutStep;
        private bool tutJumped;
        private Vector3 tutFrom;
        private TutorialHand tutHand;
        private GameObject tutSkip;
        private const string TutorialKey = "sb_tutorial_done";

        /// <summary>The guided duel is due: never played (nor skipped) and no duel fought yet.</summary>
        private static bool TutorialDue => Prefs.GetInt(TutorialKey, 0) == 0 && BotDifficulty.Played == 0;

        /// <summary>The guided duel on the Cappadocia stage against a rival that waits for each step.</summary>
        private void StartTutorial()
        {
            if (challengeRun) return;
            ShowMenu();
            if (chUi == null) InitChallenge();
            chDuelMinutes = 3; chOnline = false; chLive = false; chMode = 1; chFriend = 0; copyMatch = false; duelRule = null; chMatchId = 0;
            chDuelSquads = false;
            rival = Challenge.MakeBot(7071, 0);
            for (int g = 0; g < rival.squad.Length; g++) rival.squad[g] = 0;
            tutorialOn = true;
            tutStep = 0;
            tutJumped = false;
            BuildDuelBoard(3);
            tutHand = TutorialHand.Create(skillBar.Root);
            var skip = UI.Flat.Button(skillBar.Root, Loc.T("tut.skip"), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(240f, 84f), new Color(0.15f, 0.18f, 0.3f, 0.9f), UI.Flat.Text, () => EndTutorial(true), 32f);
            ((RectTransform)skip.transform).pivot = new Vector2(0.5f, 0.5f);
            tutSkip = skip.gameObject;
            cameraRig.StartCoroutine(TutorialRun());
        }

        private IEnumerator TutorialRun()
        {
            while (tutorialOn && challengeRun && chPhase != ChPhase.Duel) yield return null;
            var bar = skillBar;
            for (tutStep = 1; tutStep <= 8 && tutorialOn && challengeRun; tutStep++)
            {
                Vector2 mid = new Vector2(120f, -200f);
                var (gesture, at) = tutStep switch
                {
                    1 => (TutorialHand.Gesture.Tap, mid),
                    2 => (TutorialHand.Gesture.Drag, mid),
                    3 => (TutorialHand.Gesture.SwipeUp, mid),
                    4 => (TutorialHand.Gesture.Hold, bar.PointOf(0)),
                    5 => (TutorialHand.Gesture.Press, bar.PointOf(0)),
                    6 => (TutorialHand.Gesture.SwipeDown, mid),
                    7 => (TutorialHand.Gesture.Tap, mid),
                    _ => (TutorialHand.Gesture.Press, bar.PointOf(1)),
                };
                if (tutHand != null) tutHand.Show(gesture, at);
                skillBar?.SetTip(Loc.T("story.bip"), Loc.T("tut." + tutStep));
                tutFrom = robot.transform.position;
                if (tutStep == 8) myMeter = 3f; // (the bar given full for it)
                while (tutorialOn && challengeRun && !TutorialStepDone(tutStep))
                {
                    if (tutStep == 7 && myComboMove > 0 && tutHand != null) tutHand.Show(TutorialHand.Gesture.Press, bar.PointOf(3)); // (the orange button is up: press it)
                    yield return null;
                }
                if (!tutorialOn || !challengeRun) yield break;
                AudioManager.PlaySfx(Sfx.Win, 0.6f, 1.6f);
                if (tutHand != null) tutHand.Show(TutorialHand.Gesture.None, Vector2.zero);
                yield return new WaitForSeconds(0.9f);
            }
            if (!tutorialOn || !challengeRun) yield break;
            // the end: the rival loses by a hair (one more blow puts it down), the duel is over
            skillBar?.SetTip(Loc.T("story.bip"), Loc.T("tut.9"));
            tutStep = 9;
            chDuelLeft = 0.5f; // (the duel ends at the knock-down)
            while (tutorialOn && challengeRun && chPhase == ChPhase.Duel && hunt.ChallengeFoeHealth > 0.12f) { HitFoe(0.4f, Combat.BlowKind.None); yield return new WaitForSeconds(0.25f); }
            Prefs.SetInt(TutorialKey, 1);
            Prefs.Save();
            if (tutSkip != null) Destroy(tutSkip);
        }

        private bool TutorialStepDone(int step) => step switch
        {
            1 => stats.mine.hits >= 1,
            2 => Vector3.Distance(robot.transform.position, tutFrom) >= 1.5f,
            3 => tutJumped,
            4 => stats.mine.blocked >= 1,
            5 => stats.mine.perfects >= 1,
            6 => stats.mine.dodges >= 1,
            7 => stats.mine.comboUsed >= 1,
            _ => stats.mine.ultis >= 1 && !ultimateOn && !moveBusy,
        };

        /// <summary>
        /// Each frame of the guided duel: nobody is in danger (foi kept above half health, the rival above a third), the
        /// clock never runs out, the rival only strikes in the defence steps (slowly; the perfect guard's step slowed
        /// down too — the AI game only).
        /// </summary>
        private void UpdateTutorial()
        {
            if (!tutorialOn) return;
            if (!challengeRun) { EndTutorial(false); return; }
            if (chPhase != ChPhase.Duel) return;
            if (tutStep < 9) chDuelLeft = Mathf.Max(chDuelLeft, 170f);
            if (health < 0.5f) { health = 0.5f; RefreshHealthBar(); }
            if (tutStep < 9 && hunt.ChallengeFoeHealth < 0.35f) hunt.HealFoe(0.4f);
            bool attacks = tutStep >= 4 && tutStep <= 6;
            if (!attacks) hunt.StunFoe(0.5f);
            hunt.FoeReactScale = attacks ? 2.4f : 1f; // (slow blows to learn on)
            Time.timeScale = tutStep == 5 && State == GameState.Playing ? 0.6f : (State == GameState.Playing ? 1f : Time.timeScale); // (the perfect guard: slowed)
        }

        /// <summary>The perfect guard's window (the tutorial's wider one in its step).</summary>
        private float GuardWindow => tutorialOn && tutStep == 5 ? Def.tutorialWindow : -1f;

        /// <summary>The guided duel left: SKIP (marked done, to the menu) or the duel gone another way.</summary>
        private void EndTutorial(bool skipped)
        {
            if (!tutorialOn) return;
            tutorialOn = false;
            Time.timeScale = 1f;
            hunt.FoeReactScale = 1f;
            if (tutHand != null) Destroy(tutHand.gameObject);
            if (tutSkip != null) Destroy(tutSkip);
            tutHand = null;
            tutSkip = null;
            Prefs.SetInt(TutorialKey, 1);
            Prefs.Save();
            skillBar?.SetTip(null, null);
            if (skipped) ShowMenu();
        }
    }
}
