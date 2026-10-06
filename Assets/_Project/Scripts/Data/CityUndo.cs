namespace SquashBot.Data
{
    /// <summary>One building move remembered for "undo": the piece as it was before, and the coins that changed hands.</summary>
    public class CityUndo
    {
        public CityUndoKind kind;
        public CityPlaced before;
        public int coins;
    }
}
