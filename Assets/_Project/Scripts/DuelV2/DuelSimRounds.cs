using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The v2 rules' rounds (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 3): an intro ("ROUND 1", "FINAL ROUND" at 1–1), 3-2-1, a
    /// timed fight; a knock-out ends the round; time up: the larger share of health wins, equal (±1%) a golden round
    /// where the first hit wins (then the larger health, then the last to have hit). Between rounds health and shield
    /// fill up, the bar and the hearts stay, the powers, the special's cooldown and the series start over. Two rounds
    /// win the match.
    /// </summary>
    public partial class DuelSim
    {
        public RoundPhase Phase { get; private set; } = RoundPhase.Intro;
        public int Round { get; private set; } = 1;
        public float RoundLeft { get; private set; }
        public float PhaseLeft => Mathf.Max(0f, phaseEnd - Time);
        public int MatchWinner { get; private set; } = -1;
        public int LastRoundWinner { get; private set; } = -1;
        /// <summary>The round just won was flawless (no damage taken); the match was a comeback (0–1, then won).</summary>
        public bool LastRoundFlawless { get; private set; }
        public bool Comeback { get; private set; }
        private float phaseEnd;
        private int lastHitter = -1;
        private bool behind0, behind1;
        private const float CountdownSeconds = 3f;

        /// <summary>The fight is on (no intro, count or end playing).</summary>
        public bool Fighting => Phase == RoundPhase.Fight || Phase == RoundPhase.Golden;
        public bool Golden => Phase == RoundPhase.Golden;
        public bool FinalRound => F[0].roundsWon == S.roundsToWin - 1 && F[1].roundsWon == S.roundsToWin - 1;

        /// <summary>The match from its first round's intro.</summary>
        public void BeginMatch()
        {
            Round = 1;
            MatchWinner = -1;
            StartIntro();
        }

        private void StartIntro()
        {
            Phase = RoundPhase.Intro;
            phaseEnd = Time + S.roundIntro;
            RoundLeft = S.roundSeconds;
            lastHitter = -1;
            foreach (var f in F)
            {
                f.hp = f.maxHp; f.shield = S.shieldDurability; f.shieldBrokenUntil = 0f;
                f.powers.Clear(); f.specialReadyAt = 0f; f.powerReadyAt = 0f;
                f.series = 0; f.snowThisSeries = f.fireThisSeries = false;
                f.roundDamage = 0f;
            }
            PlaceForRound();
            ClearRains();
            Emit(DuelEventKind.RoundIntro, 0, count: Round, why: FinalRound ? "final" : "");
        }

        private void AdvanceRound(float dt)
        {
            switch (Phase)
            {
                case RoundPhase.Intro:
                    if (Time >= phaseEnd) { Phase = RoundPhase.Countdown; phaseEnd = Time + CountdownSeconds; }
                    break;
                case RoundPhase.Countdown:
                    if (Time >= phaseEnd) { Phase = RoundPhase.Fight; Emit(DuelEventKind.RoundFight, 0, count: Round); }
                    break;
                case RoundPhase.Fight:
                    RoundLeft -= dt;
                    if (RoundLeft <= 0f) TimeUp();
                    break;
                case RoundPhase.Golden:
                    RoundLeft -= dt;
                    if (RoundLeft <= 0f) GoldenOver();
                    break;
                case RoundPhase.Over:
                    if (Time >= phaseEnd)
                    {
                        if (F[0].roundsWon >= S.roundsToWin || F[1].roundsWon >= S.roundsToWin) EndMatch();
                        else { Round++; StartIntro(); }
                    }
                    break;
            }
        }

        /// <summary>A clean hit in the golden round wins it at once.</summary>
        private void NoteHit(Fighter by)
        {
            lastHitter = by.index;
            if (Phase == RoundPhase.Golden) WinRound(by.index, "golden");
        }

        private void TimeUp()
        {
            float a = F[0].Health01, b = F[1].Health01;
            Emit(DuelEventKind.TimeUp, 0);
            if (Mathf.Abs(a - b) <= S.tieShare)
            {
                Phase = RoundPhase.Golden;
                RoundLeft = S.goldenRoundSeconds;
                Emit(DuelEventKind.GoldenRound, 0);
                return;
            }
            WinRound(a > b ? 0 : 1, "time");
        }

        private void GoldenOver()
        {
            float a = F[0].Health01, b = F[1].Health01;
            if (Mathf.Abs(a - b) > 0.0001f) { WinRound(a > b ? 0 : 1, "golden health"); return; }
            WinRound(lastHitter >= 0 ? lastHitter : (F[0].hitsDealt >= F[1].hitsDealt ? 0 : 1), "golden last hit");
        }

        private void KnockOut(Fighter winner, Fighter loser)
        {
            loser.hp = 0f;
            SetState(loser, FighterState.KO, 0f);
            Emit(DuelEventKind.KO, winner.index, why: winner.roundsWon + 1 >= S.roundsToWin ? "match" : "round");
            WinRound(winner.index, "ko");
        }

        private void WinRound(int who, string why)
        {
            if (Phase == RoundPhase.Over || Phase == RoundPhase.MatchOver) return;
            var w = F[who]; var l = F[1 - who];
            if (w.roundsWon == 0 && l.roundsWon == 1) { if (who == 0) behind0 = true; else behind1 = true; }
            w.roundsWon++;
            LastRoundWinner = who;
            LastRoundFlawless = w.roundDamage <= 0.01f;
            Phase = RoundPhase.Over;
            phaseEnd = Time + S.roundWinPose + 1.5f;
            ClearRains();
            foreach (var f in F) if (f.Alive) SetState(f, FighterState.Idle, 0f);
            Emit(DuelEventKind.RoundOver, who, count: Round, why: why + (LastRoundFlawless ? " flawless" : ""));
        }

        private void EndMatch()
        {
            MatchWinner = F[0].roundsWon > F[1].roundsWon ? 0 : 1;
            Comeback = MatchWinner == 0 ? behind0 : behind1;
            Phase = RoundPhase.MatchOver;
            Emit(DuelEventKind.MatchOver, MatchWinner, count: F[MatchWinner].roundsWon, why: Comeback ? "comeback" : "");
        }

        private void TryHeart(Fighter f)
        {
            if (f.hearts <= 0 || !f.Free || f.hp >= f.maxHp - 0.5f) return;
            f.hearts--;
            f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * S.heartShare);
            Emit(DuelEventKind.Heart, f.index, count: f.hearts);
        }
    }
}
