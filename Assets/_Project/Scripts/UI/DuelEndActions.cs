using System;

namespace SquashBot.UI
{
    /// <summary>
    /// What the duel's end screen offers besides the rewards: PLAY AGAIN (with the background search's state), the
    /// REMATCH (a live rival's, or a computer rival asking for one), MENU, the doubled gold for an ad, the loss's tip
    /// (with its HOW? to the training), the score and the result's lines.
    /// </summary>
    public class DuelEndActions
    {
        public Action replay, menu;
        /// <summary>The rematch (null: none offered) and whether a computer rival asks for it.</summary>
        public Action rematch;
        public bool botAsks;
        /// <summary>The next rival found in the background: 0 still looking, 1 ready.</summary>
        public Func<int> searchState;
        /// <summary>Doubles the gold for a rewarded ad (null: not offered); its callback says whether it was watched.</summary>
        public Action<Action<bool>> doubleGold;
        public string score;
        /// <summary>The loss's tip (null: none) and its HOW? (null: no button).</summary>
        public string tip;
        public Action tipHow;
    }
}
