using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// A training challenge (the design's §10.2): its family (0 sword, 1 spear, 2 axe, 3 heavy; -1 the defence's), its
    /// number in the family, the dummy it is tried against, its steps in order, and its reward (given once): gold, core
    /// parts, a title. New challenges are new assets under Resources/Duel/Challenges (no code).
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Combo Challenge", fileName = "Challenge")]
    public class ComboChallengeData : ScriptableObject
    {
        public string id;
        public int family = -1;
        public int number = 1;
        public TrainingDummy dummy = TrainingDummy.Standing;
        public List<ComboStep> steps = new List<ComboStep>();
        public int gold, cores;
        /// <summary>A title won with it (a localisation key), or empty.</summary>
        public string title;

        public static ComboChallengeData Make(string id, int family, int number, TrainingDummy dummy, int gold, int cores, string title, params ComboStep[] steps)
        {
            var c = CreateInstance<ComboChallengeData>();
            c.id = id; c.family = family; c.number = number; c.dummy = dummy; c.gold = gold; c.cores = cores; c.title = title;
            c.steps = new List<ComboStep>(steps);
            c.name = id;
            return c;
        }
    }
}
