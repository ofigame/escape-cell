namespace SquashBot.DuelV2
{
    /// <summary>The prototype menu's choices (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 9.2).</summary>
    public class PrototypeOptions
    {
        public enum Mode { PlayerVsBot, BotVsBot, PlayerVsDummy }

        public Mode mode = Mode.PlayerVsBot;
        /// <summary>Weapon family (0 sword, 1 spear, 2 axe, 3 heavy) and league tier (1..5), each side.</summary>
        public int[] family = { 0, 2 };
        public int[] tier = { 3, 3 };
        /// <summary>The bot's skill (0 Private .. 1 Marshal) and personality, each side (the player's when bot-vs-bot).</summary>
        public float[] skill = { 0.5f, 0.5f };
        public BotPersonality[] persona = { BotPersonality.Balanced, BotPersonality.Balanced };
        /// <summary>The stage (index into the game's duel stages) and the platform's width.</summary>
        public int stage = 3;
        public float width = 1.5f;
        public float roundSeconds = 60f;
        public int roundsToWin = 2;
    }
}
