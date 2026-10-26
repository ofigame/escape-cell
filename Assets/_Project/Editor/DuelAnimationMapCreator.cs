using System.Collections.Generic;
using SquashBot.Combat;
using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// Writes the duel's animation map (Resources/Combat/DuelAnimationMap) the first time, with the rules' starting rows;
    /// a map already there is never overwritten, only a move it lacks is added (a new move's row). Runs with the combat
    /// assets at every build's setup and from the menu.
    /// </summary>
    public static class DuelAnimationMapCreator
    {
        private const string Path = "Assets/_Project/Resources/Combat/DuelAnimationMap.asset";

        [MenuItem("foi/Combat/Create duel animation map")]
        public static void EnsureAsset()
        {
            var have = AssetDatabase.LoadAssetAtPath<DuelAnimationMap>(Path);
            if (have == null)
            {
                var map = ScriptableObject.CreateInstance<DuelAnimationMap>();
                map.rows = DuelAnimationDefaults.Rows();
                AssetDatabase.CreateAsset(map, Path);
                AssetDatabase.SaveAssets();
                return;
            }
            var rows = new List<DuelAnimationRow>(have.rows ?? new DuelAnimationRow[0]);
            var known = new HashSet<DuelAction>();
            foreach (var r in rows) if (r != null) known.Add(r.action);
            bool added = false;
            foreach (var r in DuelAnimationDefaults.Rows()) if (!known.Contains(r.action)) { rows.Add(r); added = true; }
            if (!added) return;
            have.rows = rows.ToArray();
            EditorUtility.SetDirty(have);
            AssetDatabase.SaveAssets();
        }
    }
}
