namespace SquashBot.DuelV2
{
    /// <summary>One tick's wishes of a v2 fighter (from the player's keys and buttons, or a bot).</summary>
    public struct FighterInput
    {
        /// <summary>A tap: strike (or go on with the chain).</summary>
        public bool attack;
        /// <summary>Walking toward the other (+1) or away (-1); 0 standing.</summary>
        public float walk;
        public bool jump, dodge, block;
        public bool special, ulti;
        /// <summary>The context button: BREAK, POWER HIT or KNOCKDOWN (whichever is offered).</summary>
        public bool context;
        /// <summary>A power slot used (0..2), or -1.</summary>
        public int power;
        public bool heart;

        public static FighterInput None => new FighterInput { power = -1 };
    }
}
