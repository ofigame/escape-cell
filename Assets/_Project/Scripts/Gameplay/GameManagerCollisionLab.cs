using System.Collections;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The duel lab's collision tools (Docs/DUELLO_CARPISMA_VE_TEMAS.md, section 6): the boxes on screen, frame by frame,
    /// a simulated server delay (a landed blow's confirmation arriving again that much later: one blow must still show one
    /// effect) and the twelve test scenarios of section 7, each on one button.
    /// </summary>
    public partial class GameManager
    {
        private static readonly Color LabCollide = new Color(0.2f, 0.55f, 0.75f);
        private DuelBoxesView labBoxes;
        private bool labStepping;

        /// <summary>The lab's simulated server delay (0, 100, 200 ms).</summary>
        public static int LabLatencyMs;

        private void LabBoxes()
        {
            if (labBoxes != null) { Destroy(labBoxes.gameObject); labBoxes = null; return; }
            labBoxes = DuelBoxesView.Create();
        }

        /// <summary>One frame on, then still again.</summary>
        private IEnumerator LabStep()
        {
            labStepping = true;
            Time.timeScale = 1f;
            yield return null;
            if (labStepping) Time.timeScale = 0f;
        }

        private void AddCollisionScenarios(DuelLabPanel lab)
        {
            for (int i = 1; i <= 12; i++)
            {
                int n = i;
                lab.Add("S" + n, () => cameraRig.StartCoroutine(CollisionScenario(n)), LabCollide);
            }
        }

        /// <summary>Both back on the line, <paramref name="tiles"/> apart (the rival at the board's edge if
        /// <paramref name="atEdge"/>).</summary>
        private IEnumerator LabPlace(int tiles, bool atEdge = false)
        {
            PlaceDuelists(false);
            chPhase = ChPhase.Duel; moveBusy = false; ultimateOn = false;
            for (int i = 0; i < 4; i++) { myPhysReady[i] = 0f; rivalPhysReady[i] = 0f; }
            yield return null;
            if (!hunt.ChallengeFoeTile.HasValue) yield break;
            var foe = hunt.ChallengeFoeTile.Value;
            var me = robot.Position;
            var step = new GridPos(System.Math.Sign(me.x - foe.x), System.Math.Sign(me.y - foe.y));
            if (step.x == 0 && step.y == 0) step = new GridPos(0, -1);
            if (atEdge)
            {
                // the rival at the edge behind it, foi the given tiles in front
                var edge = foe;
                var back = new GridPos(-step.x, -step.y);
                while (grid.InBounds(edge + back) && grid.IsStandable(edge + back)) edge = edge + back;
                hunt.PlaceFoe(edge);
                foe = edge;
            }
            var to = new GridPos(foe.x + step.x * tiles, foe.y + step.y * tiles);
            if (grid.InBounds(to) && grid.IsStandable(to)) robot.Spawn(grid, to);
            SkinnedHero.ResetOverlapCount();
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The section 7 scenarios (1 .. 12) in the lab.</summary>
        private IEnumerator CollisionScenario(int n)
        {
            switch (n)
            {
                case 1: yield return LabPlace(7); yield return DoSpecial(true, SpecialKind.Fly); break; // FLY from the farthest
                case 2: yield return LabPlace(HuntSystem.DuelGap); yield return DoSpecial(true, SpecialKind.Fly); break; // FLY from contact
                case 3: yield return LabPlace(HuntSystem.DuelGap); myJumpUntil = Time.time + 0.6f; LabPhys(PhysKind.Headbutt); break; // the flying headbutt, the rival standing
                case 4: yield return LabPlace(HuntSystem.DuelGap); rivalCrouchUntil = Time.time + 3f; yield return new WaitForSeconds(0.2f); myJumpUntil = Time.time + 0.6f; LabPhys(PhysKind.Headbutt); break; // the rival crouched
                case 5: yield return LabPlace(7); UseUlti(true, UltiKind.Whirlwind); break; // WHIRLWIND from far
                case 6: yield return LabPlace(HuntSystem.DuelGap); UseUlti(true, UltiKind.Cinematic); break; // the cinematic strike
                case 7: yield return LabPlace(7); cameraRig.StartCoroutine(DoSpecial(false, SpecialKind.Fly)); yield return DoSpecial(true, SpecialKind.Fly); break; // both FLY at once
                case 8: yield return LabPlace(HuntSystem.DuelGap, true); myComboMove = HeavyCombo; comboMoveUntil = Time.time + 3f; OnSkill(DuelSkillBar.Move.Heavy); break; // POWER HIT at the edge
                case 9: yield return LabPlace(5); yield return DoSpecial(true, SpecialKind.Hook); break; // HOOK
                case 10: yield return LabPlace(5); yield return DoSpecial(true, SpecialKind.Teleport); break; // TELEPORT
                case 11: { yield return LabPlace(7); bool d = LabDefends; LabDefends = true; rivalGuard.Press(Time.time - 0.3f); yield return DoSpecial(true, SpecialKind.Fly); LabDefends = d; } break; // FLY into a raised shield
                case 12: LabLatencyMs = 200; yield return CollisionScenario(1); yield return new WaitForSeconds(1.5f); yield return CollisionScenario(5); yield return new WaitForSeconds(4f); yield return CollisionScenario(6); LabLatencyMs = 0; break; // 1, 5, 6 with 200 ms
            }
        }
    }
}
