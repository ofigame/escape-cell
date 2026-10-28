using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// One fighter's defence in the duel, by the clock it is given (the game's time; the same rules run on the server
    /// for live matches): the shield held or not, its durability (spent by blocked blows, filling again after the
    /// shield is let go, broken at nothing: dazed a moment, back later at half), the perfect guard's window round a
    /// press, the lock after a press let go too soon, the stagger after its own blow was guarded perfectly, the
    /// dodge's untouchable moment and its wait.
    /// </summary>
    public class DefenseState
    {
        public float Durability { get; private set; }
        public float Max { get; private set; } = 100f;
        public bool Holding { get; private set; }
        public float PressAt { get; private set; } = -9f;

        private float releaseAt = -9f, regenFrom, brokenUntil, dazedUntil, lockUntil, staggerUntil, invulnUntil, dodgeReadyAt;
        private float lastPerfectAt = -9f, lastHitAt = -9f;
        private BlowKind lastHitKind;

        /// <summary>The next blow's extra (a perfect guard's reward: +30%), taken by the next blow.</summary>
        public float NextBlowBonus;

        private static DefenseSettings S => DefenseSettings.Current;

        public void Reset(float max)
        {
            Max = Mathf.Max(1f, max);
            Durability = Max;
            Holding = false;
            PressAt = releaseAt = lastPerfectAt = lastHitAt = -9f;
            regenFrom = brokenUntil = dazedUntil = lockUntil = staggerUntil = invulnUntil = dodgeReadyAt = 0f;
            NextBlowBonus = 0f;
        }

        public bool Broken(float now) => brokenUntil > 0f && now < brokenUntil;
        public bool Dazed(float now) => now < dazedUntil;
        public bool Staggered(float now) => now < staggerUntil;
        public bool Locked(float now) => now < lockUntil;
        public bool Untouchable(float now) => now < invulnUntil;
        public float Share => Durability / Max;
        public bool Low => Share < S.lowShare;

        /// <summary>The lock's sweep (1 just locked .. 0 free) for the button, or the broken shield's way back.</summary>
        public float WaitShare(float now)
        {
            if (Broken(now)) return Mathf.Clamp01((brokenUntil - now) / Mathf.Max(0.01f, S.breakReturn));
            if (Locked(now)) return Mathf.Clamp01((lockUntil - now) / Mathf.Max(0.01f, S.tapLockout));
            return 0f;
        }

        /// <summary>Free to strike: not blocking, not staggered, not dazed.</summary>
        public bool CanStrike(float now) => !Holding && !Staggered(now) && !Dazed(now);

        /// <summary>The shield pressed: held from now (false: broken, locked, dazed or staggered — nothing happens).</summary>
        public bool Press(float now)
        {
            if (Holding || Broken(now) || Locked(now) || Dazed(now) || Staggered(now)) return false;
            Holding = true;
            PressAt = now;
            return true;
        }

        /// <summary>A press that came just after a blow landed (within the window's late side): that blow is forgiven
        /// as a perfect guard (the caller gives its damage back). Asked right after <see cref="Press"/>.</summary>
        public bool LatePerfect(float now)
        {
            if (lastHitKind == BlowKind.Ulti || lastHitKind == BlowKind.None || now - lastHitAt > S.perfectAfter) return false;
            lastHitAt = -9f;
            lastPerfectAt = now;
            return true;
        }

        /// <summary>The shield let go: it fills again after a moment; let go too soon without a perfect guard, it locks.</summary>
        public void Release(float now)
        {
            if (!Holding) return;
            Holding = false;
            releaseAt = now;
            regenFrom = now + S.regenDelay;
            if (now - PressAt <= S.tapRelease && lastPerfectAt < PressAt) lockUntil = now + S.tapLockout;
        }

        /// <summary>Each frame: a broken shield comes back in time; a shield let go fills again (<paramref name="perSecond"/>).</summary>
        public void Tick(float now, float dt, float perSecond)
        {
            if (brokenUntil > 0f && now >= brokenUntil)
            {
                brokenUntil = 0f;
                Durability = Max * S.breakReturnShare;
                regenFrom = now + S.regenDelay;
            }
            if (!Holding && brokenUntil <= 0f && now >= regenFrom && Durability < Max) Durability = Mathf.Min(Max, Durability + perSecond * dt);
        }

        /// <summary>
        /// A blow of <paramref name="kind"/> lands now, costing <paramref name="cost"/> durability if blocked: how it met
        /// the defence, and the share of its damage that gets through.
        /// </summary>
        public DefenseOutcome Judge(float now, BlowKind kind, float cost, out float through, float window = -1f)
        {
            through = 1f;
            if (kind == BlowKind.None) return DefenseOutcome.Hit;
            if (Untouchable(now)) { through = 0f; return DefenseOutcome.Dodged; }
            if (Broken(now) || Dazed(now) || !Holding)
            {
                lastHitAt = now;
                lastHitKind = kind;
                return DefenseOutcome.Hit;
            }
            float before = window > 0f ? window : S.perfectBefore;
            if (kind != BlowKind.Ulti && now - PressAt <= before)
            {
                through = 0f;
                lastPerfectAt = now;
                return DefenseOutcome.Perfect;
            }
            through = kind == BlowKind.Ulti ? S.ultiThrough : S.grazeShare;
            Durability -= cost;
            if (Durability > 0f) return DefenseOutcome.Blocked;
            Durability = 0f;
            Holding = false;
            brokenUntil = now + S.breakReturn;
            dazedUntil = now + S.breakDaze;
            return DefenseOutcome.Broke;
        }

        /// <summary>Its own blow was guarded perfectly: it staggers (no blows, no guard).</summary>
        public void Stagger(float now) { staggerUntil = Mathf.Max(staggerUntil, now + S.staggerSeconds); if (Holding) Release(now); }

        /// <summary>A dodge, if its wait is over: untouchable for <paramref name="untouchable"/> (true: it dodged).</summary>
        public bool Dodge(float now, float untouchable)
        {
            if (now < dodgeReadyAt || Dazed(now) || Staggered(now)) return false;
            if (Holding) Release(now);
            dodgeReadyAt = now + S.dodgeCooldown;
            invulnUntil = now + untouchable;
            return true;
        }

        /// <summary>The cost of blocking a blow (its family's for a plain blow).</summary>
        public static float Cost(BlowKind kind, WeaponFamily family)
        {
            var s = S;
            return kind switch
            {
                BlowKind.PowerHit => s.powerHitCost,
                BlowKind.Air => s.airCost,
                BlowKind.Special => s.airCost,
                BlowKind.Knockdown => s.knockdownCost,
                BlowKind.Ulti => s.ultiCost,
                _ => s.blowCost[Mathf.Clamp((int)family, 0, s.blowCost.Length - 1)],
            };
        }
    }
}
