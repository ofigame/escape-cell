using System;
using SquashBot.Data;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The loss's "why you lost" tip (the design's §6): both sides' numbers are kept through the duel
    /// (<see cref="MatchStatsRecorder"/>); after a loss the first rule that applies gives one concrete line, with a HOW?
    /// that opens its training challenge.
    /// </summary>
    public partial class GameManager
    {
        private readonly MatchStatsRecorder stats = new MatchStatsRecorder();

        /// <summary>The tip for the duel just lost (and its HOW?: the training's challenge on it, or null).</summary>
        private string DefeatTip(out Action how)
        {
            var w = weapon ?? Armory.Equipped;
            int gap = duelTheirs.weaponTier - duelMine.weaponTier;
            var family = Armory.Family(w);
            var better = Armory.All.Find(x => Armory.Family(x) == family && x.tier == duelMine.weaponTier + 1 && !x.chest && x.kind != WeaponKind.Gun);
            var tip = DefeatTipPicker.Pick(stats.mine, gap, better != null ? Loc.T("weapon." + better.id) : null, duelHearts);
            string challenge = tip.challenge;
            how = challenge != null ? () => StartTraining(challenge) : (Action)null;
            return tip.text;
        }
    }
}
