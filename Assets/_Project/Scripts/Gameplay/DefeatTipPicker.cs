using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The loss's one-line tip (the design's §6.3): the rules in order, the first that applies wins — caught by
    /// breakers, knocked down with hearts unused, the shield broken, ultimates taken with no dodge, combo moves let run
    /// out, never blocking, the ultimate left waiting, a much stronger weapon — else one from the general pool. The same
    /// tip never twice in a row (the next one that applies instead). Each comes with the training challenge its HOW?
    /// opens (or none).
    /// </summary>
    public static class DefeatTipPicker
    {
        private const string LastKey = "sb_tip_last";

        public struct Tip
        {
            public string id, text, challenge;
        }

        /// <param name="weaponTierGap">The rival's weapon tier over the player's (under the ceilings).</param>
        /// <param name="shopWeapon">A stronger weapon of the player's family in the shop (for the weapon tip), or null.</param>
        public static Tip Pick(MatchStats me, int weaponTierGap, string shopWeapon, int heartsLeft)
        {
            var s = DefeatTipSettings.Current;
            var all = new List<Tip>();
            void Add(bool when, string id, string text, string challenge) { if (when) all.Add(new Tip { id = id, text = text, challenge = challenge }); }
            Add(me.brokenByRival >= s.breakersCaught, "breaker", Loc.F("tip.breaker", me.brokenByRival), "sword2");
            Add(me.downWithHeartsUnused > 0 && me.hearts == 0, "hearts", Loc.F("tip.hearts", heartsLeft), null);
            Add(me.shieldBreaks >= s.shieldsBroken, "shieldBreak", Loc.F("tip.shieldBreak", me.shieldBreaks), "defense2");
            Add(me.ultisTaken >= s.ultisTaken && me.dodges == 0, "ultiDodge", Loc.T("tip.ultiDodge"), "defense4");
            Add(me.comboMissed >= s.comboMissed, "comboMissed", Loc.F("tip.comboMissed", me.comboMissed), "sword3");
            Add(me.blocked == 0 && me.perfects == 0, "block", Loc.T("tip.block"), "defense1");
            Add(me.ultiIdleLongest >= s.ultiIdleSeconds, "ultiIdle", Loc.F("tip.ultiIdle", Mathf.RoundToInt(me.ultiIdleLongest)), null);
            Add(weaponTierGap >= s.weaponTiersAhead && shopWeapon != null, "weapon", Loc.F("tip.weapon", shopWeapon ?? ""), null);
            string last = Prefs.GetString(LastKey, "");
            Tip pick = default;
            foreach (var t in all) if (t.id != last) { pick = t; break; }
            if (pick.id == null)
            {
                // the general pool (never the last one again)
                int n = Mathf.Max(1, s.generalTips), i = Random.Range(0, n);
                if ("gen" + i == last) i = (i + 1) % n;
                string[] challenges = { "defense3", "defense1", "defense4", "sword1", "defense5", "heavy4" };
                pick = new Tip { id = "gen" + i, text = Loc.T("tip.gen." + i), challenge = challenges[i % challenges.Length] };
            }
            Prefs.SetString(LastKey, pick.id);
            Prefs.Save();
            return pick;
        }
    }
}
