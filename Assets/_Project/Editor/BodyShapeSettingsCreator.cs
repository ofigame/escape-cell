using SquashBot.Data;
using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>Writes foi's body shapes by level (Resources/Items/BodyShapeSettings) the first time; never overwrites
    /// them. Runs with the combat assets at every build's setup and from the menu.</summary>
    public static class BodyShapeSettingsCreator
    {
        private const string Path = "Assets/_Project/Resources/Items/BodyShapeSettings.asset";

        [MenuItem("foi/Items/Create body shape settings")]
        public static void EnsureAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<BodyShapeSettings>(Path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BodyShapeSettings>(), Path);
            AssetDatabase.SaveAssets();
        }
    }
}
