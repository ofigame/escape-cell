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
                if (File.Exists(path)) { UpgradeChain(path, f); continue; }
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

        /// <summary>
        /// A family made before the five-blow chain (the choreography's 4.2): its chain's blows replaced by the design's
        /// five (the third the body's), in the same file; its other blows (POWER HIT, KNOCKDOWN, the flying ones) kept.
        /// </summary>
        private static void UpgradeChain(string path, WeaponFamily f)
        {
            var have = AssetDatabase.LoadAssetAtPath<WeaponFamilyMotionProfile>(path);
            var made = CombatDefaults.Family(f);
            if (have == null || have.chain == null || have.chain.Length >= made.chain.Length) return;
            foreach (var old in have.chain) if (old != null) Object.DestroyImmediate(old, true);
            foreach (var b in made.chain) AssetDatabase.AddObjectToAsset(b, have);
            have.chain = made.chain;
            EditorUtility.SetDirty(have);
            Debug.Log("[Combat] " + path + ": chain of " + made.chain.Length + " blows");
        }

        private static void Save(ScriptableObject so, string name)
        {
            string path = Folder + "/" + name + ".asset";
            if (File.Exists(path)) return;
            AssetDatabase.CreateAsset(so, path);
        }
    }
}
