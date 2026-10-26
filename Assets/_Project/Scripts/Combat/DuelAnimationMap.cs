using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The duel's animation map (Resources/Combat/DuelAnimationMap): each <see cref="DuelAction"/> to its own clip — a new
    /// clip is put to a move here, the code unchanged. Made with the rules' starting rows by the editor
    /// (<c>DuelAnimationMapCreator</c>); without the asset the same rows are used. A clip on two moves is warned of (but a
    /// stand-in's, the rules' section 6).
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Combat/Duel Animation Map")]
    public class DuelAnimationMap : ScriptableObject
    {
        public DuelAnimationRow[] rows = new DuelAnimationRow[0];

        [Header("The plain chain by weapon mastery (looks only; the rules' section 5)")]
        /// <summary>From this mastery the chain has a third blow: the family's body blow (a kick, a shoulder, a headbutt).</summary>
        public int bodyBlowMastery = 3;
        /// <summary>From this mastery the chain's last blow is the family's finishing blow instead.</summary>
        public int finishMastery = 5;
        /// <summary>From this mastery (the golden weapon's) the blows' trails glow gold.</summary>
        public int glowMastery = 10;

        private Dictionary<DuelAction, DuelAnimationRow> byAction;
        private static DuelAnimationMap current;

        /// <summary>The map in use (the asset, or the starting rows).</summary>
        public static DuelAnimationMap Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<DuelAnimationMap>("Combat/DuelAnimationMap");
                if (current == null) { current = CreateInstance<DuelAnimationMap>(); current.rows = DuelAnimationDefaults.Rows(); }
                current.Check();
                return current;
            }
        }

        /// <summary>The row of <paramref name="a"/> (null: none: the move keeps its coded pose).</summary>
        public DuelAnimationRow Get(DuelAction a)
        {
            if (byAction == null)
            {
                byAction = new Dictionary<DuelAction, DuelAnimationRow>();
                foreach (var r in rows) if (r != null && r.clips != null && r.clips.Length > 0) byAction[r.action] = r;
            }
            return byAction.TryGetValue(a, out var row) ? row : null;
        }

        /// <summary>The one-move-one-clip rule: the same clip's same section on two moves (neither a stand-in) is warned of.</summary>
        private void Check()
        {
            var seen = new Dictionary<string, DuelAction>();
            foreach (var r in rows)
            {
                if (r == null || r.temporary || r.clips == null) continue;
                foreach (var c in r.clips)
                {
                    string key = c + "@" + r.start.ToString("0.00") + "-" + r.end.ToString("0.00");
                    if (seen.TryGetValue(key, out var other)) Debug.LogWarning("[Anim] clip " + key + " on both " + other + " and " + r.action);
                    else seen[key] = r.action;
                }
            }
        }

        private void OnValidate() { byAction = null; }
    }
}
