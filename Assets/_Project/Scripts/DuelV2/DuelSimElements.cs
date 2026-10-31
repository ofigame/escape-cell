namespace SquashBot.DuelV2
{
    /// <summary>The v2 rules' elemental powers (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 5).</summary>
    public partial class DuelSim
    {
        private void AdvanceElements(float dt) { }
        /// <summary>A rain's circle is on this fighter.</summary>
        public bool RainOn(int who) => false;
        private void OnPerfectBlock(Fighter f) { }
        private void OnSeries(Fighter f) { }
        private void TryPower(Fighter f, Fighter o, int slot) { }
    }
}
