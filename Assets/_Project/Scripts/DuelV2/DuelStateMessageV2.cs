namespace SquashBot.DuelV2
{
    /// <summary>The server's live state message ("v2"): the duel's state, read straight into the game's types.</summary>
    [System.Serializable]
    public class DuelStateMessageV2
    {
        public string t;
        public DuelSnapshot s;
    }
}
