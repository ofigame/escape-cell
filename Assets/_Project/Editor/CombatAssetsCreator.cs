using System.IO;
using SquashBot.Combat;
using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// Writes the duel's feel assets under Resources/Combat the first time (the design's starting values: the settings,
    /// the reactions, each weapon family with its blows), so they can be tuned in the editor. Assets already there are
    /// never overwritten. Runs with every build's setup and from the menu.
    /// </summary>
    public static class CombatAssetsCreator
    {
        private const string Folder = "Assets/_Project/Resources/Combat";

        [MenuItem("foi/Combat/Create default feel assets")]
        public static void EnsureAssets()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }
            Save(CombatDefaults.Settings(), "CombatFeelSettings");
            Save(CombatDefaults.Reactions(), "HitReactions");
            Save(CombatDefaults.UltiFinal(), "UltiFinal");
            foreach (WeaponFamily f in System.Enum.GetValues(typeof(WeaponFamily)))
            {
                string path = Folder + "/Family_" + f + ".asset";
                if (File.Exists(path)) continue;
                var p = CombatDefaults.Family(f);
                AssetDatabase.CreateAsset(p, path);
                // its blows inside the family's file (one file a family, tidy in the project)
                foreach (var b in p.chain) AssetDatabase.AddObjectToAsset(b, p);
                AssetDatabase.AddObjectToAsset(p.powerHit, p);
                AssetDatabase.AddObjectToAsset(p.knockdown, p);
                AssetDatabase.AddObjectToAsset(p.flyStrike, p);
                AssetDatabase.AddObjectToAsset(p.airStrike, p);
                AssetDatabase.AddObjectToAsset(p.skyStrike, p);
            }
            AssetDatabase.SaveAssets();
            DuelAssetsCreator.EnsureAssets(); // (the duel experience's tuning too)
        }

        private static void Save(ScriptableObject so, string name)
        {
            string path = Folder + "/" + name + ".asset";
            if (File.Exists(path)) return;
            AssetDatabase.CreateAsset(so, path);
        }
    }
}
