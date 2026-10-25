using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The floors' tools, boosts and difficulty help (the fixes design's job 2): what the EMP and the blast tool do to
    /// the robots, how full the start hammer leaves the ULTI bar, and how much the difficulty help eases a floor lost
    /// again and again. Resources/Floors/FloorTuning when there is one, else these values.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Floors/Floor Tuning", fileName = "FloorTuning")]
    public class FloorTuning : ScriptableObject
    {
        [Header("EMP tool")]
        /// <summary>The EMP's reach in tiles round foi (2: a 5x5 square).</summary>
        public int empRadius = 2;
        /// <summary>Seconds a repair drone stays knocked out, by tool level 1, 2, 3.</summary>
        public float[] empDroneSeconds = { 3f, 4f, 5f };

        [Header("Blast tool")]
        /// <summary>The blast's blow on every robot it covers, in foi's own blows.</summary>
        public float blastTimes = 3f;

        [Header("Start hammer boost")]
        /// <summary>How full the ULTI bar starts (0..1).</summary>
        public float startHammerUlti = 0.5f;

        [Header("Difficulty help")]
        /// <summary>Losses on the same floor before the help comes on (it stays until the floor is won).</summary>
        public int assistAfterFails = 5;
        /// <summary>The robots' blows and vanG's health while the help is on (shares of the usual).</summary>
        public float assistFoeHit = 0.85f;
        public float assistMonsterHp = 0.9f;

        private static FloorTuning loaded;

        public static FloorTuning Current
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<FloorTuning>("Floors/FloorTuning");
                if (loaded == null) loaded = CreateInstance<FloorTuning>();
                return loaded;
            }
        }

        /// <summary>The EMP's drone time at a tool level (1-3).</summary>
        public float EmpDroneSeconds(int level) => empDroneSeconds[Mathf.Clamp(level - 1, 0, empDroneSeconds.Length - 1)];
    }
}
