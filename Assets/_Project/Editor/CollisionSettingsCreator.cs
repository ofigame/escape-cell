using SquashBot.Combat;
using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>Writes the duel's collision settings (Resources/Combat/CollisionSettings) the first time; never overwrites
    /// them. Runs with the combat assets at every build's setup and from the menu.</summary>
    public static class CollisionSettingsCreator
    {
        private const string Path = "Assets/_Project/Resources/Combat/CollisionSettings.asset";

        [MenuItem("foi/Combat/Create collision settings")]
        public static void EnsureAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<CollisionSettings>(Path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CollisionSettings>(), Path);
            AssetDatabase.SaveAssets();
        }
    }
}
