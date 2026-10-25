using System.IO;
using SquashBot.Audio;
using SquashBot.Combat;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// Writes the duel experience's tuning assets under Resources/Duel the first time (the design's starting values: the
    /// controls, the touches, the defence, the camera, the faces, the loss's tips, the music, and the 25 training
    /// challenges under Duel/Challenges), so every number can be changed in the editor. Assets already there are never
    /// overwritten. Runs with the combat assets (every build's setup) and from the menu.
    /// </summary>
    public static class DuelAssetsCreator
    {
        private const string Folder = "Assets/_Project/Resources/Duel";

        [MenuItem("foi/Duel/Create default duel assets")]
        public static void EnsureAssets()
        {
            Make(Folder);
            Save(ScriptableObject.CreateInstance<DuelControlsSettings>(), "DuelControlsSettings");
            Save(ScriptableObject.CreateInstance<DuelTouchSettings>(), "DuelTouchSettings");
            Save(ScriptableObject.CreateInstance<DefenseSettings>(), "DefenseSettings");
            Save(ScriptableObject.CreateInstance<DuelCameraSettings>(), "DuelCameraSettings");
            Save(ScriptableObject.CreateInstance<FaceExpressionData>(), "FaceExpressionData");
            Save(ScriptableObject.CreateInstance<DefeatTipSettings>(), "DefeatTipSettings");
            Save(ScriptableObject.CreateInstance<DuelMusicSettings>(), "DuelMusicSettings");
            string challenges = Folder + "/Challenges";
            Make(challenges);
            foreach (var c in ComboChallenges.Defaults())
            {
                string path = challenges + "/" + c.id + ".asset";
                if (File.Exists(path)) continue;
                AssetDatabase.CreateAsset(c, path);
            }
            Make(FloorFolder);
            Save(ScriptableObject.CreateInstance<FloorTuning>(), "FloorTuning", FloorFolder);
            AssetDatabase.SaveAssets();
        }

        private const string FloorFolder = "Assets/_Project/Resources/Floors";

        private static void Make(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        private static void Save(ScriptableObject o, string name, string folder = Folder)
        {
            string path = folder + "/" + name + ".asset";
            if (File.Exists(path)) return;
            AssetDatabase.CreateAsset(o, path);
        }
    }
}
