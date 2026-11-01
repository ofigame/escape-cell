using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>The v2 rules' bigger moves (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 2.6): the special on its cooldown, the
    /// ultimate on a full bar (the other held through its picture, then struck hard), a heart.</summary>
    public partial class DuelSim
    {
        /// <summary>The ultimate's picture time (s, before the tempo).</summary>
        public const float UltiSeconds = 1.6f;

        private void TrySpecial(Fighter f, Fighter o)
        {
            if (!f.CanAct || Time < f.specialReadyAt) return;
            f.specialReadyAt = Time + S.specialCooldown;
            StartAttack(f, o, MoveKind.Special, 0);
            Emit(DuelEventKind.Special, f.index, MoveKind.Special);
        }

        private void TryUlti(Fighter f, Fighter o)
        {
            if (!f.CanAct || f.meter < S.meterMax - 0.001f || !o.Alive) return;
            f.meter = 0f;
            f.move = MoveKind.Ulti;
            float t = UltiSeconds * S.tempo;
            SetState(f, FighterState.Casting, Time + t);
            // far off: it dashes in first (a quarter of a second), to strike from beside the other
            float d = o.x - f.x, near = S.bodyGap * Height + ReachOf(f.family) * 0.4f;
            if (Mathf.Abs(d) > near) { const float Dash = 0.25f; f.pushVel = Mathf.Sign(d) * (Mathf.Abs(d) - near) / Dash; f.pushUntil = Time + Dash; }
            o.blockHeld = false;
            SetState(o, FighterState.HitStun, Time + t + 0.05f); // (held through it: the ultimate is the ultimate)
            o.inChain = false;
            Emit(DuelEventKind.UltiStart, f.index, MoveKind.Ulti);
        }

        /// <summary>The ultimate lands (its picture over): a big share of the other's health, the other down.</summary>
        private void UltiLands(Fighter f, Fighter o)
        {
            f.move = MoveKind.Ulti;
            float dmg = Deal(f, o, S.ultiPct, false);
            f.hitsDealt++; o.hitsTaken++;
            Emit(DuelEventKind.UltiHit, f.index, MoveKind.Ulti, HitResult.Hit, dmg, at: o.x);
            if (o.hp <= 0f) { KnockOut(f, o); return; }
            Fall(o, f);
        }
    }
}
