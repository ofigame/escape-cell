using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The duel fight core v2's rules (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 2): two fighters on a line, each attack in three
    /// phases (windup, strike, recovery) that are rules, not pictures; a hit stuns, a block pushes back, a miss leaves the
    /// attacker open, a perfect block staggers it; the earlier strike wins a trade, the same frame clashes; the rhythmic
    /// chain (its window, the kept tap, the spam guard, the gap after every set of three); damage as a share of the
    /// defender's reference health, less with each hit of a series. No pictures here: the same rules run a watched fight
    /// and a thousand unwatched ones. Everything that happens goes out as <see cref="Events"/>.
    /// </summary>
    public partial class DuelSim
    {
        public readonly Fighter[] F = new Fighter[2];
        public readonly List<DuelEvent> Events = new List<DuelEvent>();
        public float Time { get; private set; }
        /// <summary>Half the line's length (world units from the middle to an end).</summary>
        public float HalfLength;
        public DuelCoreSettings S;
        /// <summary>A fighter's height (world units): the reach and the bodies' gap are shares of it.</summary>
        public float Height = 6.5f;
        public float ReachOf(int family) => S.Reach(family, Height);

        private const float OfferSeconds = 0.75f;

        public DuelSim(DuelCoreSettings settings, int[] family, int[] tier, float halfLength, float height = 6.5f)
        {
            S = settings;
            HalfLength = halfLength;
            Height = height;
            for (int i = 0; i < 2; i++)
            {
                var f = new Fighter { index = i, family = Mathf.Clamp(family[i], 0, 3), tier = Mathf.Clamp(tier[i], 1, 5) };
                f.refHp = S.referenceHp[f.tier - 1];
                f.maxHp = f.hp = f.refHp;
                f.shield = S.shieldDurability;
                f.hearts = S.heartsPerMatch;
                F[i] = f;
            }
            BeginMatch();
        }

        /// <summary>Both back on their marks, facing each other, standing.</summary>
        public void PlaceForRound()
        {
            float start = Mathf.Min(HalfLength * 0.7f, Mathf.Max(ReachOf(F[0].family), ReachOf(F[1].family)) * 0.5f + Height * 0.35f); // (just out of each other's reach)
            F[0].x = -start; F[1].x = start;
            foreach (var f in F)
            {
                f.facing = f.index == 0 ? 1 : -1;
                SetState(f, FighterState.Idle, 0f);
                f.series = 0; f.chainStep = 0; f.windowOpen = false; f.chainQueued = false; f.buffered = false;
                f.powerHitUntil = f.knockdownUntil = 0f; f.inChain = false; f.airHits = 0;
                f.invulnUntil = 0f; f.pushVel = 0f; f.pushUntil = 0f;
            }
        }

        // ---------- the tick ----------

        public void Tick(float dt, FighterInput a, FighterInput b)
        {
            if (dt <= 0f) return;
            Time += dt;
            if (!Fighting) { AdvanceRound(dt); return; }
            var inputs = new[] { a, b };
            for (int i = 0; i < 2; i++) Act(F[i], F[1 - i], inputs[i], dt);
            bool[] entered = new bool[2];
            for (int i = 0; i < 2; i++) entered[i] = Advance(F[i], F[1 - i]);
            ResolveContacts(entered);
            for (int i = 0; i < 2; i++) Move(F[i], F[1 - i], inputs[i], dt);
            Separate(dt);
            for (int i = 0; i < 2; i++)
            {
                var f = F[i];
                if (f.series > 0 && Time - f.lastHitAt > S.seriesGap) { f.series = 0; f.snowThisSeries = f.fireThisSeries = false; }
                if (f.Free || f.state == FighterState.Windup) f.facing = F[1 - i].x >= f.x ? 1 : -1;
            }
            AdvanceElements(dt);
            AdvanceRound(dt);
        }

        // ---------- what the fighter wants ----------

        private void Act(Fighter f, Fighter o, FighterInput inp, float dt)
        {
            if (!f.Alive) return;
            // the shield: held while free
            bool canBlock = Time >= f.shieldBrokenUntil && f.shield > 0f;
            if (inp.block && !f.blockHeld) f.blockSince = Time;
            f.blockHeld = inp.block && canBlock;
            if (f.blockHeld && (f.state == FighterState.Idle || f.state == FighterState.Walk)) SetState(f, FighterState.Blocking, 0f);
            if (!f.blockHeld && f.state == FighterState.Blocking) SetState(f, FighterState.Idle, 0f);

            if (inp.attack) Tap(f, o);
            else if (f.tapKeptUntil > 0f && f.Free) { f.tapKeptUntil = 0f; f.taps--; Tap(f, o); } // (the kept tap: the blow now)
            if (f.tapKeptUntil > 0f && Time > f.tapKeptUntil) f.tapKeptUntil = 0f;
            if (inp.context) Context(f, o);
            if (inp.special) TrySpecial(f, o);
            if (inp.ulti) TryUlti(f, o);
            if (inp.dodge) TryDodge(f, o);
            if (inp.jump) TryJump(f);
            if (inp.power >= 0) TryPower(f, o, inp.power);
            if (inp.heart) TryHeart(f);
        }

        /// <summary>A tap: a new blow when free; the chain's next when its window is open; else kept (once, just before the
        /// window) or counted as a wasted tap (two of them end the chain).</summary>
        private void Tap(Fighter f, Fighter o)
        {
            f.taps++;
            if (f.Free)
            {
                if (f.state == FighterState.Blocking) f.blockHeld = false;
                StartAttack(f, o, MoveKind.Chain, 0);
                return;
            }
            // free again in a moment (a stun, a push, the set's last blow): the tap kept for that moment, never lost
            bool chainGoesOn = f.Attacking && f.move == MoveKind.Chain && f.lastResult == HitResult.Hit && !f.chainBroken && f.chainStep < S.chainLength - 1;
            bool missedOwn = f.state == FighterState.Recovery && f.lastResult != HitResult.Hit; // (its own blow missed or blocked: no keeping, mashing gains nothing)
            if (!chainGoesOn && !missedOwn && f.stateEnd > Time && f.stateEnd - Time <= S.S(S.bufferMs)) { f.tapKeptUntil = f.stateEnd + 0.05f; return; }
            if (f.Attacking && f.move == MoveKind.Chain)
            {
                if (f.windowOpen && !f.chainBroken) { NextChain(f, o); return; }
                bool soon = f.state == FighterState.Recovery && f.lastResult == HitResult.Hit && !f.windowOpen && f.windowAt - Time <= S.S(S.bufferMs) && f.windowAt > Time;
                if (soon && !f.buffered) { f.buffered = true; return; }
                f.outsideTaps++;
                f.wastedTaps++;
                if (f.outsideTaps >= S.spamTaps && !f.chainBroken)
                {
                    f.chainBroken = true;
                    f.buffered = false;
                    Emit(DuelEventKind.ChainBroken, f.index, why: "spam");
                }
                return;
            }
            f.wastedTaps++;
        }

        /// <summary>An attack begins: its three phases from now (a slowed fighter a little slower).</summary>
        private void StartAttack(Fighter f, Fighter o, MoveKind kind, int step)
        {
            float slow = Time < f.slowUntil ? 1.15f : 1f;
            float w = S.Windup(f.family), a = S.Active(f.family), r = S.Recovery(f.family);
            switch (kind)
            {
                case MoveKind.PowerHit: w *= 1.4f; a *= 1.2f; r *= 1.5f; break;
                case MoveKind.Knockdown: w *= 1.3f; r *= 1.4f; break;
                case MoveKind.Special: w = S.S(220f); a = S.S(100f); r = S.S(450f); break;
            }
            w *= slow; r *= slow;
            f.move = kind;
            f.chainStep = step;
            f.contacted = false;
            f.outsideTaps = 0;
            f.buffered = false;
            f.windowOpen = false;
            f.chainQueued = false;
            if (step == 0) f.chainBroken = false;
            f.activeAt = Time + w;
            f.recoveryAt = f.activeAt + a;
            f.attackEnd = f.recoveryAt + r;
            f.facing = o.x >= f.x ? 1 : -1;
            SetState(f, FighterState.Windup, f.activeAt);
            Emit(DuelEventKind.Swing, f.index, kind, count: step, why: w.ToString("0.000"));
        }

        /// <summary>The chain's next blow (the window taken): the recovery cut, the other's stun held until it lands.</summary>
        private void NextChain(Fighter f, Fighter o)
        {
            StartAttack(f, o, MoveKind.Chain, f.chainStep + 1);
            if (o.state == FighterState.HitStun) o.stateEnd = Mathf.Max(o.stateEnd, f.activeAt + 0.01f); // (it cannot get out between)
        }

        // ---------- the states' timers ----------

        /// <summary>Moves the fighter's state on; true when it entered its strike this tick.</summary>
        private bool Advance(Fighter f, Fighter o)
        {
            if (f.stateEnd > 0f && Time < f.stateEnd) return false;
            switch (f.state)
            {
                case FighterState.Windup:
                    SetState(f, FighterState.Active, f.recoveryAt);
                    return true;
                case FighterState.Active:
                    if (!f.contacted)
                    {
                        // a miss: the recovery longer (the other's chance)
                        f.lastResult = HitResult.Whiff;
                        f.whiffs++;
                        f.attackEnd += S.S(S.whiffExtraMs);
                        f.series = 0;
                        Emit(DuelEventKind.Result, f.index, f.move, HitResult.Whiff, why: "out of reach");
                    }
                    SetState(f, FighterState.Recovery, f.attackEnd);
                    return false;
                case FighterState.Recovery:
                    if (f.windowOpen) { f.windowOpen = false; } // (the window closes with the recovery)
                    SetState(f, f.blockHeld ? FighterState.Blocking : FighterState.Idle, 0f);
                    return false;
                case FighterState.HitStun:
                case FighterState.BlockStun:
                case FighterState.Staggered:
                    f.inChain = false;
                    SetState(f, f.blockHeld ? FighterState.Blocking : FighterState.Idle, 0f);
                    return false;
                default:
                    return AdvanceDefence(f, o);
            }
        }

        // ---------- contact ----------

        private void ResolveContacts(bool[] entered)
        {
            var a = F[0]; var b = F[1];
            bool aIn = InStrike(a) && InReach(a, b), bIn = InStrike(b) && InReach(b, a);
            // the same frame, both striking into each other: a clash (each staggers a little)
            if (entered[0] && entered[1] && aIn && bIn && Strikeable(a) && Strikeable(b))
            {
                a.contacted = b.contacted = true;
                a.lastResult = b.lastResult = HitResult.Clash;
                a.clashes++; b.clashes++;
                foreach (var f in F) { f.series = 0; SetState(f, FighterState.Staggered, Time + S.S(S.clashStaggerMs)); Push(f, F[1 - f.index], 0.35f); }
                Emit(DuelEventKind.Result, 0, a.move, HitResult.Clash, at: (a.x + b.x) * 0.5f, why: "same frame");
                return;
            }
            // otherwise the earlier strike: the one already striking lands first and cuts the other's attack
            for (int k = 0; k < 2; k++)
            {
                var f = F[k]; var o = F[1 - k];
                if (!InStrike(f)) continue;
                if (!InReach(f, o)) continue;
                Contact(f, o);
            }
            // the chain window opens a moment after the strike's end
            foreach (var f in F)
            {
                if (f.state != FighterState.Recovery || f.windowOpen || f.lastResult != HitResult.Hit || f.move != MoveKind.Chain) continue;
                if (f.chainBroken || f.chainStep >= S.chainLength - 1) continue;
                if (Time < f.windowAt) continue;
                f.windowOpen = true;
                Emit(DuelEventKind.ChainOpen, f.index, count: f.chainStep);
                if (f.buffered) { f.buffered = false; NextChain(f, F[1 - f.index]); }
            }
        }

        private static bool InStrike(Fighter f) => f.state == FighterState.Active && !f.contacted;

        private bool InReach(Fighter f, Fighter o)
        {
            float reach = ReachOf(f.family) * (f.move == MoveKind.Special ? 1.25f : f.move == MoveKind.PowerHit ? 1.1f : 1f);
            float d = (o.x - f.x) * f.facing;
            return d > -0.2f && d <= reach;
        }

        /// <summary>Whether a blow can reach this fighter at all right now (not dodging, not lying, not getting up).</summary>
        private bool Strikeable(Fighter o) => o.Alive && Time >= o.invulnUntil && o.state != FighterState.Down && o.state != FighterState.GettingUp && o.state != FighterState.Dodging && o.state != FighterState.Casting;

        private void Contact(Fighter f, Fighter o)
        {
            f.contacted = true;
            // out of it: dodging, lying, getting up (their own words)
            if (!o.Alive || o.state == FighterState.KO) { f.lastResult = HitResult.Immune; return; }
            if (o.state == FighterState.Dodging || (o.state == FighterState.Jumping && Time < o.invulnUntil))
            {
                f.lastResult = HitResult.Dodged; f.series = 0;
                Emit(DuelEventKind.Result, f.index, f.move, HitResult.Dodged, at: o.x, why: o.state == FighterState.Dodging ? "dodge" : "jump");
                return;
            }
            if (!Strikeable(o) && o.state != FighterState.Airborne)
            {
                f.lastResult = HitResult.Immune;
                Emit(DuelEventKind.Result, f.index, f.move, HitResult.Immune, at: o.x, why: o.state.ToString());
                return;
            }
            // in the air after a KNOCKDOWN: a few blows more, each less
            if (o.state == FighterState.Airborne)
            {
                if (o.airHits >= S.airHitsMax) { f.lastResult = HitResult.Immune; Emit(DuelEventKind.Result, f.index, f.move, HitResult.Immune, at: o.x, why: "air hits used"); return; }
                o.airHits++;
                f.lastResult = HitResult.Hit;
                float dmg = Deal(f, o, S.airHitPct, true);
                Emit(DuelEventKind.Result, f.index, MoveKind.AirHit, HitResult.Hit, dmg, at: o.x, why: "air " + o.airHits);
                return;
            }
            // the shield: a block, or a perfect one (raised just in time)
            if (o.ShieldUp && f.move != MoveKind.Ulti)
            {
                bool perfect = Time - o.blockSince <= S.S(S.perfectWindowMs) && f.move != MoveKind.PowerHit;
                if (perfect)
                {
                    f.lastResult = HitResult.Perfect;
                    f.series = 0;
                    o.perfects++;
                    o.meter = Mathf.Min(S.meterMax, o.meter + S.meterPerPerfect);
                    SetState(f, FighterState.Staggered, Time + S.S(S.perfectStaggerMs));
                    Emit(DuelEventKind.Result, f.index, f.move, HitResult.Perfect, at: o.x, why: "shield raised " + Mathf.RoundToInt((Time - o.blockSince) * 1000f) + " ms before");
                    OnPerfectBlock(o);
                    return;
                }
                f.lastResult = HitResult.Blocked;
                f.series = 0;
                o.blocks++;
                float cost = f.move == MoveKind.PowerHit ? S.powerHitBlockCost : S.blockCost[f.family];
                o.shield = Mathf.Max(0f, o.shield - cost);
                float chip = Deal(f, o, PctOf(f) * S.chipShare, false);
                SetState(o, FighterState.BlockStun, Time + S.BlockPush(f.family));
                Push(o, f, f.move == MoveKind.PowerHit ? 1.0f : 0.3f);
                Emit(DuelEventKind.Result, f.index, f.move, HitResult.Blocked, chip, at: o.x, why: "shield " + Mathf.RoundToInt(o.shield));
                if (o.shield <= 0f)
                {
                    o.shieldBrokenUntil = Time + S.shieldBrokenSeconds;
                    o.blockHeld = false;
                    SetState(o, FighterState.Staggered, Time + S.S(S.perfectStaggerMs));
                    Emit(DuelEventKind.ShieldBroke, o.index);
                }
                return;
            }
            Hit(f, o);
        }

        /// <summary>A clean hit: damage, the other stunned (it loses whatever it was doing), the series and the offers.</summary>
        private void Hit(Fighter f, Fighter o)
        {
            f.lastResult = HitResult.Hit;
            f.hitsDealt++; o.hitsTaken++;
            bool lastOfSet = f.move == MoveKind.Chain && f.chainStep >= S.chainLength - 1;
            float dmg = Deal(f, o, PctOf(f), f.move == MoveKind.Chain);
            f.meter = Mathf.Min(S.meterMax, f.meter + S.meterPerHit);
            o.meter = Mathf.Min(S.meterMax, o.meter + S.meterPerTaken);
            f.series++;
            f.lastHitAt = Time;
            o.series = 0;
            o.chainQueued = false;
            switch (f.move)
            {
                case MoveKind.PowerHit:
                    SetState(o, FighterState.HitStun, Time + S.S(S.powerHitStunMs));
                    Push(o, f, S.powerHitPush);
                    o.inChain = false;
                    break;
                case MoveKind.Knockdown:
                    o.airHits = 0;
                    SetState(o, FighterState.Airborne, Time + S.airSeconds * S.tempo);
                    o.inChain = false;
                    break;
                default:
                    float stun = lastOfSet ? Mathf.Max(S.S(S.lastHitStunMs), S.S(S.minReactMs)) : S.HitStun(f.family);
                    SetState(o, FighterState.HitStun, Time + stun);
                    o.inChain = f.move == MoveKind.Chain && !lastOfSet;
                    Push(o, f, 0.12f);
                    break;
            }
            // the window for the chain's next, and the gap after a set (never shorter than a reaction)
            f.windowAt = f.recoveryAt + S.S(S.chainOpenMs);
            if (f.move == MoveKind.Chain && !lastOfSet && f.lag > 0f) f.attackEnd += f.lag; // (a live duel: the window open as much longer as this side's taps take to arrive; the struck one's stun runs on with it)
            if (lastOfSet)
            {
                int setsInRow = Mathf.Max(1, f.series / Mathf.Max(1, S.chainLength)); // (set upon set: the way out longer each time, never a lock)
                float gapFor = Mathf.Max(S.S(S.minReactMs), S.S(S.setGapMs) * (1f + 0.5f * (setsInRow - 1)));
                float rec = Mathf.Max(f.attackEnd - f.recoveryAt, (o.stateEnd - Time) + gapFor - S.Windup(f.family) - (f.recoveryAt - Time));
                f.attackEnd = f.recoveryAt + rec;
                // the gap the other has (measured: never under the reaction window)
                float gap = (f.attackEnd + S.Windup(f.family)) - o.stateEnd;
                f.minGap = Mathf.Min(f.minGap, gap);
                f.powerHitUntil = f.attackEnd + OfferSeconds;
                Emit(DuelEventKind.PowerHitOffered, f.index);
            }
            if (f.move == MoveKind.Chain && f.series == S.knockdownAt) { f.knockdownUntil = f.attackEnd + OfferSeconds; Emit(DuelEventKind.KnockdownOffered, f.index); }
            Emit(DuelEventKind.Result, f.index, f.move, HitResult.Hit, dmg, f.series, at: o.x, why: lastOfSet ? "set end" : "");
            if (f.series >= 2) Emit(DuelEventKind.Series, f.index, count: f.series);
            OnSeries(f);
            if (o.hp <= 0f) KnockOut(f, o);
            else NoteHit(f);
        }

        /// <summary>The blow's damage share (% of the reference health) by its kind.</summary>
        private float PctOf(Fighter f) => f.move switch
        {
            MoveKind.PowerHit => S.powerHitPct,
            MoveKind.Knockdown => S.knockdownPct,
            MoveKind.Special => S.specialPct,
            MoveKind.Ulti => S.ultiPct,
            MoveKind.AirHit => S.airHitPct,
            _ => S.blowPct[f.family],
        };

        /// <summary>Damage dealt: a share of the defender's reference health, less with each hit of the series.</summary>
        private float Deal(Fighter f, Fighter o, float pct, bool series)
        {
            float k = 1f;
            if (series && f.series > 0) k = Mathf.Max(S.seriesFloor, Mathf.Pow(S.seriesDecay, f.series));
            float dmg = o.refHp * pct * 0.01f * k;
            o.hp = Mathf.Max(0f, o.hp - dmg);
            o.damageTaken += dmg;
            o.lastDamagedAt = Time;
            o.roundDamage += dmg;
            return dmg;
        }

        // ---------- the context button ----------

        private void Context(Fighter f, Fighter o)
        {
            if (TryBreak(f, o)) return;
            if (Time <= f.knockdownUntil && f.series >= S.knockdownAt && (f.Free || f.state == FighterState.Recovery))
            {
                f.knockdownUntil = 0f; f.powerHitUntil = 0f;
                StartAttack(f, o, MoveKind.Knockdown, 0);
                return;
            }
            if (Time <= f.powerHitUntil && (f.Free || f.state == FighterState.Recovery))
            {
                f.powerHitUntil = 0f;
                StartAttack(f, o, MoveKind.PowerHit, 0);
            }
        }

        // ---------- moving ----------

        private void Move(Fighter f, Fighter o, FighterInput inp, float dt)
        {
            // pushed back
            if (Time < f.pushUntil) f.x += f.pushVel * dt;
            bool canWalk = f.state == FighterState.Idle || f.state == FighterState.Walk || f.state == FighterState.Blocking;
            if (canWalk)
            {
                float v = inp.walk * S.walkSpeed * (f.state == FighterState.Blocking ? S.blockWalkScale : 1f) * (Time < f.slowUntil ? 0.6f : 1f);
                int toward = o.x >= f.x ? 1 : -1;
                f.x += v * toward * dt;
                if (f.state != FighterState.Blocking) SetState(f, Mathf.Abs(inp.walk) > 0.05f ? FighterState.Walk : FighterState.Idle, 0f, keepTime: true);
            }
            MoveDefence(f, o, dt);
            float lim = HalfLength - 0.3f;
            if (f.x > lim || f.x < -lim) { f.x = Mathf.Clamp(f.x, -lim, lim); OnWall(f); }
        }

        /// <summary>
        /// The two bodies never in each other: their middles at least the body gap apart (a jump may pass over, a launch
        /// may not); round one lying on the ground more (its body along the floor), the lying one slid away (the standing
        /// one held back) at a rate, never a jump; what a wall keeps one from taking the other takes.
        /// </summary>
        private void Separate(float dt)
        {
            var a = F[0]; var b = F[1];
            if (a.state == FighterState.Jumping || b.state == FighterState.Jumping) return;
            bool aLying = Lying(a), bLying = Lying(b);
            float gap = (S.bodyGap + (aLying || bLying ? S.lyingGap : 0f)) * Height;
            float d = b.x - a.x;
            if (Mathf.Abs(d) >= gap) return;
            float sign = Mathf.Abs(d) > 1e-4f ? Mathf.Sign(d) : a.facing >= 0 ? 1f : -1f;
            float need = gap - Mathf.Abs(d);
            float step = aLying || bLying ? Mathf.Min(need, S.lyingPushRate * Height * dt) : need;
            float shareA = aLying && !bLying ? 0.8f : bLying && !aLying ? 0.2f : 0.5f;
            float lim = HalfLength - 0.3f;
            float na = Mathf.Clamp(a.x - sign * step * shareA, -lim, lim);
            float nb = Mathf.Clamp(b.x + sign * step * (1f - shareA), -lim, lim);
            float left = step - Mathf.Abs(na - a.x) - Mathf.Abs(nb - b.x);
            if (left > 1e-5f) { float nb2 = Mathf.Clamp(nb + sign * left, -lim, lim); left -= Mathf.Abs(nb2 - nb); nb = nb2; }
            if (left > 1e-5f) na = Mathf.Clamp(na - sign * left, -lim, lim);
            a.x = na; b.x = nb;
        }

        /// <summary>On the ground (knocked down or out): its body lies along the floor.</summary>
        private static bool Lying(Fighter f) => f.state == FighterState.Down || f.state == FighterState.KO;

        /// <summary>Pushed away from <paramref name="from"/> by <paramref name="dist"/> over a short time.</summary>
        private void Push(Fighter f, Fighter from, float dist)
        {
            float dir = f.x >= from.x ? 1f : -1f;
            const float T = 0.16f;
            f.pushVel = dir * dist / T;
            f.pushUntil = Time + T;
        }

        // ---------- helpers ----------

        public void SetState(Fighter f, FighterState s, float end, bool keepTime = false)
        {
            if (f.state != s || !keepTime) f.stateTime = Time;
            f.state = s;
            f.stateEnd = end;
        }

        public void Emit(DuelEventKind k, int who, MoveKind move = MoveKind.Chain, HitResult result = HitResult.Hit, float damage = 0f, int count = 0, float at = 0f, string why = "", ElementKind element = ElementKind.Snow)
        {
            Events.Add(new DuelEvent { kind = k, who = who, move = move, result = result, damage = damage, count = count, at = at, why = why, element = element, time = Time });
        }

        /// <summary>Who has the turn and by how much (ms): the one free to act sooner.</summary>
        public int Advantage(out float ms)
        {
            float a = FreeAt(F[0]), b = FreeAt(F[1]);
            ms = Mathf.Abs(a - b) * 1000f;
            return a < b ? 0 : b < a ? 1 : -1;
        }

        public float FreeAt(Fighter f)
        {
            if (f.Free) return Time;
            if (f.Attacking) return f.windowOpen ? Time : Mathf.Max(f.attackEnd, f.stateEnd);
            return f.stateEnd > 0f ? f.stateEnd : Time;
        }
    }
}
