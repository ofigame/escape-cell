using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The v2 rules' elemental powers (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 5): earned, never drawn (a series of 4 hits:
    /// snowball; of 6: fireball; a perfect block: lightning), three at most (one more is lost), used from a slot with a
    /// short wait between, raining on the other from the sky in a row of drops after a target circle's warning. Out of the
    /// circle, no harm; behind the shield, a fifth of it and no fall, until the shield breaks; the last drop knocks down.
    /// A chain blow and a drop on the same frame count once.
    /// </summary>
    public partial class DuelSim
    {
        public readonly List<ElementRain> Rains = new List<ElementRain>();

        private void OnSeries(Fighter f)
        {
            if (f.series >= S.snowAtSeries && !f.snowThisSeries) { f.snowThisSeries = true; Earn(f, ElementKind.Snow); }
            if (f.series >= S.fireAtSeries && !f.fireThisSeries) { f.fireThisSeries = true; Earn(f, ElementKind.Fire); }
        }

        private void OnPerfectBlock(Fighter f) => Earn(f, ElementKind.Lightning);

        private void Earn(Fighter f, ElementKind k)
        {
            if (f.powers.Count >= S.maxPowers) { Emit(DuelEventKind.PowerLost, f.index, element: k); return; }
            f.powers.Add(k);
            f.powersEarned++;
            Emit(DuelEventKind.PowerEarned, f.index, element: k, count: f.powers.Count);
        }

        /// <summary>A power used from slot <paramref name="slot"/>: the hand raised to the sky, the circle on the other.</summary>
        private void TryPower(Fighter f, Fighter o, int slot)
        {
            if (slot < 0 || slot >= f.powers.Count || !f.Free || Time < f.powerReadyAt || !o.Alive) return;
            var k = f.powers[slot];
            f.powers.RemoveAt(slot);
            f.powerReadyAt = Time + S.powerCooldown;
            f.powersUsed++;
            f.move = MoveKind.Special;
            SetState(f, FighterState.Casting, Time + S.summonSeconds * S.tempo);
            int e = (int)k;
            var rain = new ElementRain
            {
                kind = k, caster = f.index, target = o.index, centre = o.x, radius = S.rainRadius,
                warnEnd = Time + S.summonSeconds * S.tempo + S.rainWarn[e] * S.tempo,
                follows = k == ElementKind.Lightning,
            };
            int n = Mathf.Max(1, S.rainDrops[e]);
            rain.dropPct = S.rainPct / (n + 1); // (the last drop twice a plain one: n + 1 shares)
            float span = S.rainSeconds[e] * S.tempo;
            for (int i = 0; i < n; i++) rain.dropAt.Add(rain.warnEnd + (n == 1 ? 0f : span * i / (n - 1)) + (i % 2 == 1 ? 0.04f : 0f)); // (small gaps a master can time a dodge into)
            Rains.Add(rain);
            Emit(DuelEventKind.RainStart, f.index, element: k, at: rain.centre, count: n);
        }

        private void AdvanceElements(float dt)
        {
            for (int r = Rains.Count - 1; r >= 0; r--)
            {
                if (r >= Rains.Count) continue; // (a knock-out cleared the rains meanwhile)
                var rain = Rains[r];
                var target = F[rain.target];
                // lightning's circle follows its target (slowly: the hardest to get out of)
                if (rain.follows && !rain.Done) rain.centre = Mathf.MoveTowards(rain.centre, target.x, S.lightningFollow * dt);
                while (!rain.Done && Time >= rain.dropAt[rain.fallen] && Rains.Contains(rain)) Drop(rain, rain.fallen++);
                if (!Rains.Contains(rain)) continue;
                if (rain.Done) { Emit(DuelEventKind.RainEnd, rain.caster, element: rain.kind); Rains.Remove(rain); }
            }
        }

        private void Drop(ElementRain rain, int i)
        {
            var o = F[rain.target]; var by = F[rain.caster];
            bool last = rain.Last(i);
            bool inside = Mathf.Abs(o.x - rain.centre) <= rain.radius;
            if (!o.Alive || !inside || o.state == FighterState.Dodging || o.state == FighterState.Down || o.state == FighterState.GettingUp || Time < o.invulnUntil)
            {
                Emit(DuelEventKind.RainDrop, by.index, result: HitResult.Dodged, element: rain.kind, count: i, at: rain.centre, why: last ? "last" : "");
                return;
            }
            if (o.lastDamagedAt >= Time - 0.0001f) { Emit(DuelEventKind.RainDrop, by.index, result: HitResult.Immune, element: rain.kind, count: i, at: rain.centre, why: "same frame"); return; } // (a chain blow this frame: counted once)
            float pct = rain.dropPct * (last ? 2f : 1f);
            if (o.ShieldUp && Time >= o.shieldBrokenUntil && o.shield > 0f)
            {
                o.shield = Mathf.Max(0f, o.shield - S.rainBlockCost[(int)rain.kind]);
                float chip = Deal(by, o, pct * S.rainBlockShare, false);
                if (o.shield <= 0f) { o.shieldBrokenUntil = Time + S.shieldBrokenSeconds; o.blockHeld = false; SetState(o, FighterState.Idle, 0f); Emit(DuelEventKind.ShieldBroke, o.index); }
                Emit(DuelEventKind.RainDrop, by.index, result: HitResult.Blocked, damage: chip, element: rain.kind, count: i, at: o.x, why: last ? "last" : "");
                if (o.hp <= 0f) KnockOut(by, o);
                return;
            }
            float dmg = Deal(by, o, pct, false);
            by.rainHits++;
            switch (rain.kind)
            {
                case ElementKind.Snow: o.slowUntil = Time + 0.7f; break;
                case ElementKind.Lightning: if (!last && (o.Free || o.state == FighterState.Windup)) SetState(o, FighterState.HitStun, Time + 0.12f); break;
            }
            Emit(DuelEventKind.RainDrop, by.index, result: HitResult.Hit, damage: dmg, element: rain.kind, count: i, at: o.x, why: last ? "last" : "");
            if (o.hp <= 0f) { KnockOut(by, o); return; }
            if (last) { o.series = 0; o.inChain = false; Fall(o, by); NoteHit(by); }
        }

        /// <summary>A rain's circle is on this fighter (warned or falling).</summary>
        public bool RainOn(int who)
        {
            foreach (var r in Rains) if (r.target == who && !r.Done && Mathf.Abs(F[who].x - r.centre) <= r.radius + 0.2f) return true;
            return false;
        }

        private void ClearRains() => Rains.Clear();
    }
}
