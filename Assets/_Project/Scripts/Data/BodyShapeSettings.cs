using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// foi's body by its parts (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 6): each part's level gives a thickness
    /// (0 thin … 1 big), a small step a level and a clear jump at 5, 10, 15 and 20; the body's chest, belly and shoulders
    /// and the arms (the fists most) go from thin to big. Today's body, as made, is thickness <see cref="today"/>
    /// (about level 15). Resources/Items/BodyShapeSettings (made by the editor; without it, these).
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Items/Body Shape Settings")]
    public class BodyShapeSettings : ScriptableObject
    {
        [Header("Thickness (0..1) at each level 0..20")]
        public float[] bodyByLevel = Steps();
        public float[] armsByLevel = Steps();

        [Header("What thickness 0 and 1 mean (today's body = 1)")]
        /// <summary>Today's body as made: this thickness.</summary>
        public float today = 0.75f;
        /// <summary>The chest, belly and shoulders across: thin and big.</summary>
        public float torsoThin = 0.8f, torsoThick = 1.2f;
        /// <summary>The upper arms and forearms across.</summary>
        public float armThin = 0.78f, armThick = 1.25f;
        /// <summary>The fists (and the gloves on them), every way.</summary>
        public float fistThin = 0.9f, fistThick = 1.3f;

        /// <summary>A small step a level and a jump at every fifth: 0 at level 0, <see cref="today"/> at 15, 1 at 20.</summary>
        private static float[] Steps()
        {
            var t = new float[RobotParts.MaxLevel + 1];
            for (int l = 0; l <= RobotParts.MaxLevel; l++) t[l] = 0.0375f * l + 0.0625f * (l / 5);
            return t;
        }

        private static BodyShapeSettings current;

        public static BodyShapeSettings Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<BodyShapeSettings>("Items/BodyShapeSettings");
                if (current == null) current = CreateInstance<BodyShapeSettings>();
                return current;
            }
        }

        public float Body(int level) => bodyByLevel[Mathf.Clamp(level, 0, bodyByLevel.Length - 1)];
        public float Arms(int level) => armsByLevel[Mathf.Clamp(level, 0, armsByLevel.Length - 1)];

        /// <summary>A thickness as a scale: <paramref name="thin"/> at 0, 1 at <see cref="today"/>, <paramref name="thick"/> at 1.</summary>
        public float Scale(float thickness, float thin, float thick) =>
            thickness <= today ? Mathf.Lerp(thin, 1f, thickness / Mathf.Max(0.01f, today)) : Mathf.Lerp(1f, thick, (thickness - today) / Mathf.Max(0.01f, 1f - today));
    }
}
