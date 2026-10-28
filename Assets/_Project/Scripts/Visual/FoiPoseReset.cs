using SquashBot.Combat;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The one place a fighter goes back to its clean state (Docs/ARAYUZ_HATA_VE_TEMIZLIK.md, section 2.3): its body
    /// (<see cref="SkinnedHero.CleanSlate"/>), its picture's push and lunge, its pace (hit-stop, slow motion), its status
    /// effects, a victory dance's dropped gear. Each reaction undoes itself when it ends; this is the second guarantee.
    /// </summary>
    public static class FoiPoseReset
    {
        /// <summary>The fighter under <paramref name="visual"/> (its picture's root) clean now.</summary>
        public static void Clean(Transform visual)
        {
            if (visual == null) return;
            var root = CombatFeel.RootOf(visual) ?? visual;
            var vr = root.GetComponent<VisualRoot>(); if (vr != null) vr.Clear();
            var vt = root.GetComponent<VisualTime>(); if (vt != null) vt.Clear();
            var st = root.GetComponent<StatusFx>(); if (st != null) st.Clear();
            foreach (var d in visual.GetComponentsInChildren<DanceDrop>(true)) d.Restore();
            foreach (var s in visual.GetComponentsInChildren<SkinnedHero>(true)) s.CleanSlate();
        }

        /// <summary>The whole game's slow motion off as well (a duel left during an ultimate).</summary>
        public static void CleanAll(params Transform[] visuals)
        {
            VisualTime.ClearAll();
            foreach (var v in visuals) Clean(v);
        }

        /// <summary>What is not clean on the fighter under <paramref name="visual"/> (null: all clean).</summary>
        public static string Violations(Transform visual)
        {
            if (visual == null) return null;
            string all = null;
            foreach (var s in visual.GetComponentsInChildren<SkinnedHero>(true))
            {
                var v = s.PoseViolations();
                if (v != null) all = (all == null ? "" : all + "; ") + v;
            }
            return all;
        }
    }
}
