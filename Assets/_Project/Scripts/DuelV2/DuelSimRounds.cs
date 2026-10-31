namespace SquashBot.DuelV2
{
    /// <summary>The v2 rules' rounds (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 3).</summary>
    public partial class DuelSim
    {
        /// <summary>The fight is on (no round's intro or end playing).</summary>
        public bool Fighting => F[0].Alive && F[1].Alive;

        private void AdvanceRound(float dt) { }

        private void KnockOut(Fighter winner, Fighter loser)
        {
            loser.hp = 0f;
            SetState(loser, FighterState.KO, 0f);
            Emit(DuelEventKind.KO, winner.index);
        }
    }
}
