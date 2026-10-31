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
