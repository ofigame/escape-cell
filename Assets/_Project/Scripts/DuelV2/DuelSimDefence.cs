using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The v2 rules' ways out (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 4): the dodge (untouchable a moment, a step back), the
    /// jump, a KNOCKDOWN's flight and the fall, lying and getting up (untouchable while rising), the wall.
    /// </summary>
    public partial class DuelSim
    {
        /// <summary>The states with timers of their own (the defence's, the casting's); true never (no strike starts here).</summary>
        private bool AdvanceDefence(Fighter f, Fighter o)
        {
            switch (f.state)
            {
                case FighterState.Dodging:
                case FighterState.Jumping:
                    SetState(f, f.blockHeld ? FighterState.Blocking : FighterState.Idle, 0f);
                    if (f.state == FighterState.Idle) Emit(DuelEventKind.Landed, f.index);
                    break;
                case FighterState.Airborne:
                    SetState(f, FighterState.Down, Time + S.downSeconds * S.tempo);
                    Emit(DuelEventKind.Landed, f.index);
                    break;
                case FighterState.Down:
                    SetState(f, FighterState.GettingUp, Time + S.getUpInvuln);
                    f.invulnUntil = Time + S.getUpInvuln;
                    break;
                case FighterState.GettingUp:
                    SetState(f, FighterState.Idle, 0f);
                    Emit(DuelEventKind.GotUp, f.index);
                    break;
                case FighterState.Casting:
                    SetState(f, FighterState.Idle, 0f);
                    if (f.move == MoveKind.Ulti) UltiLands(f, o);
                    break;
            }
            return false;
        }

        private void MoveDefence(Fighter f, Fighter o, float dt)
        {
            if (f.state == FighterState.Dodging)
            {
                float dir = f.x >= o.x ? 1f : -1f;
                float dur = Mathf.Max(0.05f, f.stateEnd - f.stateTime);
                f.x += dir * S.dodgeDistance / dur * dt;
            }
        }

        private void TryDodge(Fighter f, Fighter o)
        {
            if (!(f.Free) || Time < f.dodgeReadyAt) return;
            f.dodgeReadyAt = Time + S.dodgeCooldown;
            f.invulnUntil = Time + S.dodgeInvuln;
            SetState(f, FighterState.Dodging, Time + Mathf.Max(S.dodgeInvuln, 0.3f * S.tempo));
            Emit(DuelEventKind.Dodge, f.index);
        }

        private void TryJump(Fighter f)
        {
            if (!f.Free) return;
            float t = S.jumpSeconds * S.tempo;
            SetState(f, FighterState.Jumping, Time + t);
            f.invulnUntil = Time + t * 0.8f; // (over a blow along the ground for most of it)
            Emit(DuelEventKind.Jump, f.index);
        }

        /// <summary>Knocked down where it stands (an ultimate, a rain's last drop, a wall): lying, then up.</summary>
        private void Fall(Fighter f, Fighter from)
        {
            f.inChain = false;
            f.blockHeld = false;
            SetState(f, FighterState.Down, Time + S.downSeconds * S.tempo);
            Push(f, from, 0.6f);
        }

        /// <summary>Thrown hard into the line's end: it bounces off and falls forward (never pinned in the corner).</summary>
        private void OnWall(Fighter f)
        {
            if (Time >= f.pushUntil || Mathf.Abs(f.pushVel) < 8f) return;
            f.pushVel = -f.pushVel * 0.25f;
            var o = F[1 - f.index];
            if (f.state == FighterState.HitStun || f.state == FighterState.Staggered) { Fall(f, o); Emit(DuelEventKind.Result, o.index, MoveKind.PowerHit, HitResult.Hit, 0f, at: f.x, why: "wall"); }
        }

        /// <summary>
        /// BREAK (Docs 4.2): caught in the other's chain, a bar part cuts it: the attacker staggers back, the caught one is
        /// free at once (a moment untouchable, so the chain's next blow already on its way cannot land).
        /// </summary>
        private bool TryBreak(Fighter f, Fighter o)
        {
            if (!f.inChain || f.meter < S.breakCost - 0.001f || !(f.state == FighterState.HitStun)) return false;
            if (!o.Attacking || o.move != MoveKind.Chain) return false;
            f.meter -= S.breakCost;
            f.breaks++;
            f.inChain = false;
            SetState(f, f.blockHeld ? FighterState.Blocking : FighterState.Idle, 0f);
            f.invulnUntil = Time + S.S(S.minReactMs);
            o.series = 0; o.windowOpen = false; o.chainBroken = true;
            SetState(o, FighterState.Staggered, Time + S.S(S.breakStunMs));
            Push(o, f, 0.8f);
            Emit(DuelEventKind.BreakUsed, f.index, at: o.x);
            return true;
        }
    }
}
