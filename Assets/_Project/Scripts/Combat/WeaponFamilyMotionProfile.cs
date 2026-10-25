using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>A weapon family's whole way of fighting: its four-blow chain, its POWER HIT and KNOCKDOWN.</summary>
    [CreateAssetMenu(menuName = "foi/Combat/Weapon Family Motion", fileName = "Family")]
    public class WeaponFamilyMotionProfile : ScriptableObject
    {
        public WeaponFamily family;
        /// <summary>The blows in turn (1, 2, 3, 4, 1 ...).</summary>
        public AttackMotionData[] chain = new AttackMotionData[0];
        public AttackMotionData powerHit;
        public AttackMotionData knockdown;
        /// <summary>FLY's blow as it lands, a blow struck in a jump, SKY STRIKE's crash (the design's 100 / 70 / 120 ms freezes).</summary>
        public AttackMotionData flyStrike, airStrike, skyStrike;
        /// <summary>The weapon's trail colour (a rank weapon uses its own effect colour instead).</summary>
        public Color trail = new Color(0.85f, 0.95f, 1f);

        public AttackMotionData Blow(int index) => chain == null || chain.Length == 0 ? null : chain[((index % chain.Length) + chain.Length) % chain.Length];
    }
}
