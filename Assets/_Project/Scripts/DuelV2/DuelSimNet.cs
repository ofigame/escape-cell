using UnityEngine;
using System.Collections.Generic;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// A live duel's rules across the wire: the server takes a snapshot of its run (with the events since the last), the
    /// game puts it on its own copy (which then only shows: it never ticks).
    /// </summary>
    public partial class DuelSim
    {
        public DuelSnapshot Snapshot(List<DuelEvent> events)
        {
            return new DuelSnapshot
            {
                time = Time, phase = Phase, round = Round, roundLeft = RoundLeft, phaseEnd = phaseEnd,
                matchWinner = MatchWinner, lastRoundWinner = LastRoundWinner,
                flawless = LastRoundFlawless, comeback = Comeback, lastGolden = LastRoundGolden,
                f0 = F[0], f1 = F[1], rains = Rains, events = events,
            };
        }

        /// <summary>The copy's clock between two of the server's states (the rains' drops, the countdown run smoothly).</summary>
        public void AdvanceClock(float dt) { Time += dt; if (Phase == RoundPhase.Fight || Phase == RoundPhase.Golden) RoundLeft = Mathf.Max(0f, RoundLeft - dt); }

        /// <summary>
        /// A state seen from the other side (this game is the server's side 1): left and right swapped, the line mirrored,
        /// so this game's foi is always the copy's fighter 0 on the left.
        /// </summary>
        public static DuelSnapshot Mirrored(DuelSnapshot s)
        {
            var a = s.f0; s.f0 = s.f1; s.f1 = a;
            foreach (var f in new[] { s.f0, s.f1 }) if (f != null) { f.x = -f.x; f.facing = -f.facing; }
            if (s.matchWinner >= 0) s.matchWinner = 1 - s.matchWinner;
            if (s.lastRoundWinner >= 0) s.lastRoundWinner = 1 - s.lastRoundWinner;
            if (s.rains != null) foreach (var r in s.rains) { r.caster = 1 - r.caster; r.target = 1 - r.target; r.centre = -r.centre; }
            if (s.events != null)
                for (int i = 0; i < s.events.Count; i++) { var e = s.events[i]; e.who = 1 - e.who; e.at = -e.at; s.events[i] = e; }
            return s;
        }

        /// <summary>The server's state on this copy (its events added to <see cref="Events"/>, to be shown).</summary>
        public void Apply(DuelSnapshot s)
        {
            Time = s.time; Phase = s.phase; Round = s.round; RoundLeft = s.roundLeft; phaseEnd = s.phaseEnd;
            MatchWinner = s.matchWinner; LastRoundWinner = s.lastRoundWinner;
            LastRoundFlawless = s.flawless; Comeback = s.comeback; LastRoundGolden = s.lastGolden;
            if (s.f0 != null) { s.f0.index = 0; F[0] = s.f0; }
            if (s.f1 != null) { s.f1.index = 1; F[1] = s.f1; }
            Rains.Clear();
            if (s.rains != null) Rains.AddRange(s.rains);
            if (s.events != null) Events.AddRange(s.events);
        }
    }
}
