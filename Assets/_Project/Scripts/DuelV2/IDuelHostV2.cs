using SquashBot.Gameplay;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// What runs a v2 fight: the prototype scene (its menu, its tools) or the game itself (a challenge duel). It gives the
    /// match's choices and numbers, the tools' switches (all off in the game) and hears when the match is over.
    /// </summary>
    public interface IDuelHostV2
    {
        GameManager Game { get; }
        PrototypeOptions Options { get; }
        /// <summary>The match's numbers (the prototype: the live-tuned ones; the game: a copy with the week's rule).</summary>
        DuelCoreSettings Settings { get; }
        /// <summary>The tools' speed (slow motion, pause): 1 in the game.</summary>
        float ToolScale { get; }
        bool ShowLog { get; }
        bool ShowAdvantage { get; }
        bool ShowBoxes { get; }
        /// <summary>The prototype (its match panel, its self-test log); false in the game.</summary>
        bool IsPrototype { get; }
        /// <summary>The fight being fought (null before it and after).</summary>
        DuelFightV2 Fight { get; }
        /// <summary>The match is decided and its last moments shown (the knock-out's slow motion, the replay).</summary>
        void MatchDone(DuelSim sim);
        void BackToMenu();
        /// <summary>The screen's pause button (the game: its pause screen; never in a live duel).</summary>
        void PauseAsked();
        /// <summary>Something happened in the fight (the game counts its deeds: the quests, the match's challenge).</summary>
        void Event(DuelEvent e);
        /// <summary>A live duel: the server's next state (mirrored to this game's side), or false when none came.</summary>
        bool NextState(out DuelSnapshot state);
        /// <summary>A live duel: this game's wishes go to the server (only when they change, and every press).</summary>
        void SendInput(FighterInput input);
    }
}
