using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// A v2 rival that thinks (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 8): it sees with a human's delay; it goes on with its
    /// chain when the first blow lands and stops when it is blocked; it punishes a miss or a blocked blow; in the gap
    /// after the other's set of three it blocks, dodges, breaks or strikes back (by its personality); it keeps to the
    /// chain's rhythm (and now and then taps too early, more so at low rank); it uses its powers (Faz 5) and runs from a
    /// rain's circle. The Spam bot (tests only) taps without end.
    /// </summary>
    public class DuelBotV2
    {
        public float skill;
        public BotPersonality persona;
        private readonly System.Random rng;
        private float nextThink, blockUntil, walkDir, walkUntil;
        private bool wantTap, wantContext;
        private float tapAt = -1f;
        private int lastStateSeen = -1;

        public DuelBotV2(float skill, BotPersonality persona, int seed)
        {
            this.skill = Mathf.Clamp01(skill); this.persona = persona;
            rng = new System.Random(seed);
        }

        private float R => (float)rng.NextDouble();
        /// <summary>The bot's reaction (s): a Private slow, a Marshal quick.</summary>
        private float React => Mathf.Lerp(0.36f, 0.17f, skill) * (0.85f + 0.3f * R);

        public FighterInput Think(DuelSim sim, Fighter me, Fighter o)
        {
            var s = sim.S;
            var inp = FighterInput.None;
            float t = sim.Time;
            if (!me.Alive) return inp;
            if (persona == BotPersonality.Spam)
            {
                // a finger on the screen without end: closer, and tap, tap, tap
                inp.walk = Mathf.Abs(o.x - me.x) > sim.ReachOf(me.family) * 0.85f ? 1f : 0f;
                inp.attack = R < 0.55f;
                return inp;
            }
            float dist = Mathf.Abs(o.x - me.x);
            float reach = sim.ReachOf(me.family);

            // within the chain: tap in the window (with a human's timing), sometimes too early
            if (me.Attacking && me.move == MoveKind.Chain)
            {
                if (me.lastResult == HitResult.Hit && me.state == FighterState.Recovery && !me.chainBroken && me.chainStep < s.chainLength - 1)
                {
                    float sloppy = (1f - skill) * 0.35f;
                    if (tapAt < 0f) tapAt = me.windowAt + (R < sloppy ? -s.S(s.bufferMs) * 2.2f : R * 0.06f);
                    if (t >= tapAt) { inp.attack = true; tapAt = -1f; }
                }
                else tapAt = -1f;
            }
            else tapAt = -1f;

            // the chain's finishers when offered
            if (t <= me.knockdownUntil && R < 0.4f + 0.5f * skill) inp.context = true;
            else if (t <= me.powerHitUntil && R < 0.25f + 0.5f * skill * (persona == BotPersonality.Aggressive ? 1.3f : 1f)) inp.context = true;

            if (t < nextThink)
            {
                inp.block = t < blockUntil;
                if (t < walkUntil) inp.walk = walkDir;
                return inp;
            }
            nextThink = t + React;

            // BREAK: chained, with a bar part to spend
            if (me.inChain && me.meter >= s.breakCost && R < 0.25f + 0.5f * skill * (persona == BotPersonality.Defensive ? 1.4f : 1f)) { inp.context = true; return inp; }
            // a heart when low
            if (me.hearts > 0 && me.Health01 < 0.3f && me.Free && R < 0.7f) { inp.heart = true; return inp; }
            // the ultimate on a full bar, in reach
            if (me.meter >= s.meterMax && me.Free && dist <= reach + 1f && R < 0.7f) { inp.ulti = true; return inp; }
            // a power when it has one (the defensive one keeps them a while)
            if (me.powers.Count > 0 && me.Free && t >= me.powerReadyAt && R < (persona == BotPersonality.Defensive ? 0.25f : persona == BotPersonality.Aggressive ? 0.9f : 0.55f)) { inp.power = 0; return inp; }
            // a rain's circle on it: out of it
            if (sim.RainOn(me.index) && R < Mathf.Lerp(0.1f, 0.6f, skill)) { walkDir = -1f; walkUntil = t + 0.5f; inp.walk = -1f; if (me.Free && R < 0.4f) inp.dodge = true; return inp; }

            if (!me.Free) return inp;
            bool otherOpen = (o.state == FighterState.Recovery && (o.lastResult == HitResult.Whiff || o.lastResult == HitResult.Blocked || o.lastResult == HitResult.Perfect)) || o.state == FighterState.Staggered || o.state == FighterState.Casting && o.move != MoveKind.Ulti;
            bool otherComing = o.state == FighterState.Windup && dist <= sim.ReachOf(o.family) + 0.4f;
            float defend = persona == BotPersonality.Defensive ? 0.75f : persona == BotPersonality.Aggressive ? 0.35f : 0.55f;

            // the other is winding up at it: shield, dodge or strike first
            if (otherComing && R < defend * (0.5f + 0.5f * skill))
            {
                if (R < 0.75f) { inp.block = true; blockUntil = t + 0.45f; }
                else inp.dodge = true;
                return inp;
            }
            // punish an opening
            if (otherOpen && dist <= reach + 0.3f) { inp.attack = true; return inp; }
            // the other's set of three just ended: its gap (shield, dodge or strike back)
            if (o.state == FighterState.Recovery && o.chainStep >= s.chainLength - 1 && o.lastResult == HitResult.Hit && me.state != FighterState.HitStun)
            {
                if (R < defend) { inp.block = true; blockUntil = t + 0.35f; }
                else if (dist <= reach) inp.attack = true;
                return inp;
            }
            // the special now and then
            if (t >= me.specialReadyAt && dist <= reach + 0.8f && R < 0.08f + 0.1f * skill) { inp.special = true; return inp; }
            // in reach: strike (or wait behind the shield)
            if (dist <= reach * 0.95f)
            {
                float aggr = persona == BotPersonality.Aggressive ? 0.85f : persona == BotPersonality.Defensive ? 0.45f : 0.65f;
                if (R < aggr) inp.attack = true;
                else { inp.block = true; blockUntil = t + 0.3f; }
                return inp;
            }
            // out of reach: closer
            walkDir = 1f; walkUntil = t + 0.25f; inp.walk = 1f;
            return inp;
        }
    }
}
