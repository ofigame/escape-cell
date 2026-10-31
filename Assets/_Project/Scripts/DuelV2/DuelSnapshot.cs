using System.Collections.Generic;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// A live duel's state as the server sends it (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md, Faz 10): the server runs the rules
    /// and the games show this. The round's phase and clock, both fighters whole, the rains falling, and what happened
    /// since the last one (the events: the pictures, sounds and words come from them). Field names are the wire's.
    /// </summary>
    [System.Serializable]
    public class DuelSnapshot
    {
        public float time;
        public RoundPhase phase;
        public int round;
        public float roundLeft, phaseEnd;
        public int matchWinner = -1, lastRoundWinner = -1;
        public bool flawless, comeback, lastGolden;
        public Fighter f0, f1;
        public List<ElementRain> rains = new List<ElementRain>();
        public List<DuelEvent> events = new List<DuelEvent>();
    }
}
