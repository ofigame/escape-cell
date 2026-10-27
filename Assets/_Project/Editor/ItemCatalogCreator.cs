using SquashBot.Data;
using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>Writes the items' numbers (Resources/Items/ItemCatalog) the first time; never overwrites
    /// them. Runs with the combat assets at every build's setup and from the menu.</summary>
    public static class ItemCatalogCreator
    {
        private const string Path = "Assets/_Project/Resources/Items/ItemCatalog.asset";

        [MenuItem("foi/Items/Create item catalog")]
        public static void EnsureAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<ItemCatalog>(Path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ItemCatalog>(), Path);
            AssetDatabase.SaveAssets();
        }
    }
}
