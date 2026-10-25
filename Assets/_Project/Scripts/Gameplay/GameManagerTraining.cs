using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The training room (the design's §10): a duel with nothing at stake (no energy, no reward, no rank, nothing
    /// counted) against a dummy that stands, attacks, defends or casts its ultimate, both kept at full health; INFO shows
    /// the damage numbers and the perfect guard's cue (a ring closing on the SHIELD, green at the right moment); the
    /// challenges (five per weapon family, five for the defence) are followed step by step and pay their reward once.
    /// Opened from the duel menu, and from a loss's HOW? straight on its challenge.
    /// </summary>
    public partial class GameManager
    {
        private bool trainingOn, trainingInfo = true;
        private TrainingDummy trainingDummy;
        private TrainingPanel trainingPanel;
        private readonly ComboChallengeRunner trainingRun = new ComboChallengeRunner();
        private WeaponDef weaponBeforeTraining;
        private float dummyUltiAt;

        /// <summary>The dummy defends (the lab's, or the training room's in that mode).</summary>
        private bool DummyDefends => LabDefends || (trainingOn && trainingDummy == TrainingDummy.Defending);

        /// <summary>The training room, on <paramref name="challengeId"/>'s challenge (null: none picked yet).</summary>
        private void StartTraining(string challengeId)
        {
            if (challengeRun) { EndChallenge(); }
            ShowMenu();
            if (chUi == null) InitChallenge();
            chDuelMinutes = 3; chOnline = false; chLive = false; chMode = 1; chFriend = 0; copyMatch = false; duelRule = null; chMatchId = 0;
            chDuelSquads = false;
            rival = Challenge.MakeBot(5151, Mathf.Max(0, Challenge.Rank - 1));
            for (int g = 0; g < rival.squad.Length; g++) rival.squad[g] = 0;
            trainingOn = true;
            weaponBeforeTraining = weapon;
            BuildDuelBoard(5);
            trainingPanel = TrainingPanel.Create(skillBar.Root);
            trainingPanel.DummyPicked += d => { trainingDummy = d; RefreshTraining(); };
            trainingPanel.InfoToggled += () => { trainingInfo = !trainingInfo; RefreshTraining(); };
            trainingPanel.ChallengePicked += PickChallenge;
            trainingPanel.ExitPressed += () => { EndTraining(); ShowMenu(); };
            trainingDummy = TrainingDummy.Standing;
            if (challengeId != null) PickChallenge(challengeId);
            else { trainingRun.Stop(); RefreshTraining(); }
        }

        private void PickChallenge(string id)
        {
            var c = ComboChallenges.Get(id);
            if (c == null) return;
            trainingRun.Begin(c);
            trainingDummy = c.dummy;
            // a family's challenge: a weapon of that family in hand for it (the room's own, the player's back after)
            if (c.family >= 0)
            {
                var kind = c.family == 0 ? WeaponKind.Sword : c.family == 1 ? WeaponKind.Spear : c.family == 2 ? WeaponKind.Axe : WeaponKind.Hammer;
                var mine = weapon ?? Armory.Equipped;
                if (Armory.Family(mine) != Armory.Family(new WeaponDef { kind = kind }))
                {
                    var w = Armory.All.Find(x => x.kind == kind && !x.chest);
                    if (w != null) { weapon = w; ShowHeld(w); }
                }
            }
            myMeter = 0f;
            RefreshTraining();
        }

        private void RefreshTraining()
        {
            if (trainingPanel == null) return;
            trainingPanel.SetModes(trainingDummy, trainingInfo);
            trainingPanel.SetChallenge(trainingRun.Challenge, trainingRun.Step, trainingRun.Progress);
        }

        /// <summary>Something happened in the training room: the challenge's step may be done (a chime), the whole of it
        /// (its reward, once).</summary>
        private void TrainingNote(TrainingEvent e, int value = 0)
        {
            if (!trainingOn || trainingRun.Challenge == null) return;
            if (!trainingRun.Note(e, value)) { if (e != TrainingEvent.Hurt) RefreshTraining(); return; }
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.5f);
            if (trainingRun.Finished)
            {
                var c = trainingRun.Challenge;
                bool paid = ComboChallenges.Grant(c);
                skillBar?.Shout(Loc.T("tr.done"), Palette.UiGold);
                if (paid) FloatAt(robot.transform.position + Vector3.up * 2f, (c.gold > 0 ? "+" + c.gold + " " : "") + (c.cores > 0 ? Loc.F("login.cores", "+" + c.cores) : ""), Palette.UiGold);
                ui.RefreshMenuCoins();
            }
            RefreshTraining();
        }

        /// <summary>
        /// Each frame of the training room: both at full health and the clock held; the dummy as its mode says (standing:
        /// still; attacking: its plain blows; defending: still behind its defence; ultimate: its ultimate every few seconds);
        /// the perfect guard's cue with INFO on.
        /// </summary>
        private void UpdateTraining()
        {
            if (!trainingOn) return;
            if (!challengeRun) { EndTraining(); return; }
            if (chPhase != ChPhase.Duel) return;
            chDuelLeft = Mathf.Max(chDuelLeft, 170f);
            if (health < 1f) { health = 1f; RefreshHealthBar(); }
            if (hunt.ChallengeFoeHealth < 0.98f) hunt.HealFoe(1f);
            bool attacks = trainingDummy == TrainingDummy.Attacking;
            if (!attacks) hunt.StunFoe(0.4f);
            if (trainingDummy == TrainingDummy.Ulti && Time.time > dummyUltiAt && !moveBusy && !DuelCinematic)
            {
                dummyUltiAt = Time.time + 7f;
                rivalMeter = 0f;
                UseUlti(false, UltiKind.Cinematic);
            }
            skillBar?.SetGuardCue(trainingInfo ? hunt.FoeWindLeft : -1f, Def.perfectBefore);
        }

        /// <summary>The training room left: the player's own weapon back, the panel gone.</summary>
        private void EndTraining()
        {
            if (!trainingOn) return;
            trainingOn = false;
            trainingRun.Stop();
            if (weaponBeforeTraining != weapon) { weapon = weaponBeforeTraining; if (weapon != null) ShowHeld(weapon); }
            weaponBeforeTraining = null;
            if (trainingPanel != null) Destroy(trainingPanel.gameObject);
            trainingPanel = null;
        }

        /// <summary>A screen point over the training room's controls.</summary>
        private bool TrainingOver(Vector2 p) => trainingPanel != null && trainingPanel.IsOver(p);
    }
}
