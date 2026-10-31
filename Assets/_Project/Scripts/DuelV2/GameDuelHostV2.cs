using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The game running a v2 fight (a challenge duel): the match's choices from the duel being set up, its own copy of
    /// the numbers (the week's rule, the tournament's round length on it), no tools, and the result handed back to the
    /// game when the match is over.
    /// </summary>
    public class GameDuelHostV2 : IDuelHostV2
    {
        private readonly System.Action<DuelSim> done;

        /// <summary>The self-test: a bot fights for foi too.</summary>
        public static bool BotPlaysFoi;

        public GameDuelHostV2(GameManager game, PrototypeOptions options, DuelCoreSettings settings, System.Action<DuelSim> done)
        {
            Game = game; Options = options; Settings = settings; this.done = done;
        }

        public GameManager Game { get; }
        public PrototypeOptions Options { get; }
        public DuelCoreSettings Settings { get; }
        /// <summary>Paused (the game's pause screen): the fight stands still.</summary>
        public bool Paused { get; set; }
        public float ToolScale => Paused ? 0f : 1f;
        public bool ShowLog => false;
        public bool ShowAdvantage => false;
        public bool ShowBoxes => false;
        public bool IsPrototype => false;
        public DuelFightV2 Fight { get; set; }

        public void MatchDone(DuelSim sim) => done?.Invoke(sim);
        public void BackToMenu() { }

        /// <summary>
        /// A bot's skill (0 Private .. 1 Marshal) from the game's tuning of it: its reaction (quicker = better) and its
        /// shield (more = better), the two the old bot's level showed most.
        /// </summary>
        public static float SkillOf(Data.BotTuning t)
        {
            if (t == null) return 0.5f;
            float react = (t.reactMin + t.reactMax) * 0.5f;
            float quick = Mathf.InverseLerp(0.45f, 0.15f, react);
            return Mathf.Clamp01(0.6f * quick + 0.4f * Mathf.Clamp01(t.shield));
        }

        /// <summary>A bot's personality from the game's tuning: bold → aggressive, much shield → defensive.</summary>
        public static BotPersonality PersonaOf(Data.BotTuning t)
        {
            if (t == null) return BotPersonality.Balanced;
            if (t.aggression >= 0.7f) return BotPersonality.Aggressive;
            if (t.shield >= 0.6f && t.aggression < 0.5f) return BotPersonality.Defensive;
            return BotPersonality.Balanced;
        }
    }
}
