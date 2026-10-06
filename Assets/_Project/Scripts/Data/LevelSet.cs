using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    [CreateAssetMenu(fileName = "LevelSet", menuName = "Squash Bot/Level Set")]
    public class LevelSet : ScriptableObject
    {
        public List<LevelData> levels = new List<LevelData>();

        public static LevelSet LoadOrDefault()
        {
            var set = Resources.Load<LevelSet>("LevelSet");
            if (set != null && set.levels.Count > 0) return set;

            set = CreateInstance<LevelSet>();
            set.levels = LevelCatalog.CreateDefault();
            return set;
        }
    }
}
