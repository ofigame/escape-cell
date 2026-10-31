using System.Collections.Generic;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// One v2 fighter's whole state (the rules only, no pictures): its place on the line, its health against the
    /// reference health of its league tier, its shield, bar, hearts and powers, what it is doing and for how long, its
    /// attack (kind, chain step, contact made, taps), its series, what the context button offers it.
    /// </summary>
    public class Fighter
    {
        public int index;
        /// <summary>Weapon family (0 sword .. 3 heavy) and league tier (1..5).</summary>
        public int family, tier;
        public float x;
        /// <summary>+1 facing right (the other on its right), -1 left.</summary>
        public int facing = 1;
        public float hp, maxHp, refHp;
        public float shield;
        public float shieldBrokenUntil;
        public float meter;
        public int hearts;

        public FighterState state = FighterState.Idle;
        /// <summary>Time in the state, and when it ends (sim seconds; 0: it has no end of its own).</summary>
        public float stateTime, stateEnd;

        // ---------- the attack ----------
        public MoveKind move;
        /// <summary>The chain blow (0, 1, 2 within the set).</summary>
        public int chainStep;
        /// <summary>This attack's phases (sim time of each start; recovery's end).</summary>
        public float activeAt, recoveryAt, attackEnd;
        /// <summary>The contact was made (whichever result); the result.</summary>
        public bool contacted;
        public HitResult lastResult;
        /// <summary>Taps outside the chain window in this blow, a tap kept for the window, the chain ended by them.</summary>
        public int outsideTaps;
        public bool buffered, chainBroken;
        /// <summary>The chain window is open (after a hit, until the recovery's end), and from when.</summary>
        public bool windowOpen;
        public float windowAt;
        /// <summary>The next chain blow is queued (the window was taken).</summary>
        public bool chainQueued;
        public float hitStopUntil;
        /// <summary>Pushed back: the speed and until when.</summary>
        public float pushVel, pushUntil;

        // ---------- the series (hits in a row, never blocked) ----------
        public int series;
        public float lastHitAt = -99f;
        public bool snowThisSeries, fireThisSeries;

        // ---------- offers on the context button ----------
        public float powerHitUntil, knockdownUntil;

        // ---------- defence ----------
        public bool blockHeld;
        public float blockSince;
        public float invulnUntil;
        public float dodgeReadyAt;
        public int airHits;
        /// <summary>Struck by the other's chain right now (BREAK may answer it).</summary>
        public bool inChain;

        // ---------- moves on cooldowns, powers ----------
        public float specialReadyAt;
        public float powerReadyAt;
        public readonly List<ElementKind> powers = new List<ElementKind>();
        /// <summary>Slowed by snow until (walk and moves a little slower).</summary>
        public float slowUntil;

        // ---------- the match's numbers (the report, the bots) ----------
        public int roundsWon;
        public int hitsDealt, hitsTaken, blocks, perfects, whiffs, clashes, taps, wastedTaps;
        public float damageTaken;
        public float lastDamagedAt = -99f;

        public bool Alive => hp > 0.0001f && state != FighterState.KO;
        public float Health01 => maxHp > 0f ? hp / maxHp : 0f;
        public bool Attacking => state == FighterState.Windup || state == FighterState.Active || state == FighterState.Recovery;
        /// <summary>Free to start something (stand, walk, block).</summary>
        public bool Free => state == FighterState.Idle || state == FighterState.Walk || state == FighterState.Blocking;
        public bool ShieldUp => state == FighterState.Blocking || (blockHeld && state == FighterState.BlockStun);
    }
}
