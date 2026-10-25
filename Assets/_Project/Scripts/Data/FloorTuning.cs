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

        [Header("Weapons on the floors (the families even within a tier)")]
        /// <summary>A hand weapon's floor blow by family (sword, spear, axe, heavy) and tier 1..6, six a family: the same
        /// damage a second within a tier (50, 80, 120, 175, 240, 330) as the player's taps allow it — a floor blow is a tap,
        /// and five to seven taps a second keep a sword (8 blows a second) and a spear short of their pace, so theirs are
        /// half and a seventh again over the even table (E:CLAUDEscriptsloor_balance.js: every family within 15%).</summary>
        public int[] floorBlows = { 9, 15, 21, 32, 44, 60, 10, 16, 25, 37, 49, 68, 14, 22, 34, 49, 67, 92, 15, 24, 36, 53, 72, 99 };
        /// <summary>The starting mallet: under the first tier (30 a second).</summary>
        public int malletBlow = 9;
        /// <summary>The sword: every this-many-th blow in a row (each within this long of the last) hits this much harder.</summary>
        public int swordEvery = 4;
        public float swordTimes = 1.5f, swordChainGap = 1.2f;
        /// <summary>The spear reaches this far on the floors (the others 2).</summary>
        public int spearReach = 3;
        /// <summary>The axe: the tiles beside the target take this share of the blow.</summary>
        public float axeSideShare = 0.4f;
        /// <summary>The heavy ones: the tiles round the target take this share, and the target reels this long.</summary>
        public float heavySplashShare = 0.25f, heavyStagger = 0.15f;

        [Header("Bosses (the fixes design's job 8)")]
        /// <summary>vanG's guardian at the end of floors 1-9: this share of vanG's health, this size, a big attack this often.</summary>
        public float guardianHpShare = 0.6f, guardianScale = 0.7f, guardianBigEvery = 12f;
        /// <summary>A world form's first big attack comes this soon (it shows its move early).</summary>
        public float formFirstMove = 4f;

        [Header("Stars (the floor score 0..100)")]
        /// <summary>A won floor is one star; this score two, this score three.</summary>
        public int starTwo = 60, starThree = 85;
        /// <summary>Every star of a world's ten floors: this much gold (and the world's conqueror title), once.</summary>
        public int worldStarGold = 500;

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

        /// <summary>A hand weapon's floor blow by its family and tier (the mallet its own).</summary>
        public int FloorBlow(WeaponFamily family, int tier, bool mallet) =>
            mallet ? malletBlow : floorBlows[(int)family * 6 + Mathf.Clamp(tier, 1, 6) - 1];

        /// <summary>The EMP's drone time at a tool level (1-3).</summary>
        public float EmpDroneSeconds(int level) => empDroneSeconds[Mathf.Clamp(level - 1, 0, empDroneSeconds.Length - 1)];
    }
}
