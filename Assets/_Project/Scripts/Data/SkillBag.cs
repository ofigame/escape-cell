using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The skill bag: skill orbs picked up on the floor are not used on the spot any more, they go into the bag
    /// (kept between levels, up to <see cref="Max"/> of each) and the player fires them from the skill bar when it
    /// suits. The kinds are the <see cref="Gameplay.PowerUpType"/> values that <see cref="Bagged"/> accepts.
    /// </summary>
    public static class SkillBag
    {
        public const int Max = 9;

        /// <summary>How many of one kind fit now: the backpack's level decides (3 at first, up to <see cref="Max"/>).</summary>
        public static int Room => Backpack.OrbCapacity;

        /// <summary>Kinds that go into the bag (rescues still work by themselves).</summary>
        public static bool Bagged(Gameplay.PowerUpType t) => t != Gameplay.PowerUpType.Rescue;

        public static readonly Gameplay.PowerUpType[] Order =
        {
            Gameplay.PowerUpType.Super, Gameplay.PowerUpType.Shield, Gameplay.PowerUpType.Heart,
            Gameplay.PowerUpType.Freeze, Gameplay.PowerUpType.Blast, Gameplay.PowerUpType.Magnet
        };

        private static string Key(Gameplay.PowerUpType t) => "sb_bag_" + t;

        public static int Count(Gameplay.PowerUpType t) => PlayerPrefs.GetInt(Key(t), 0);

        public static bool Full(Gameplay.PowerUpType t) => Count(t) >= Room;

        public static void Add(Gameplay.PowerUpType t, int n = 1)
        {
            PlayerPrefs.SetInt(Key(t), Mathf.Clamp(Count(t) + n, 0, Mathf.Max(Room, Count(t))));
            PlayerPrefs.Save();
        }

        public static bool TryUse(Gameplay.PowerUpType t)
        {
            if (Count(t) <= 0) return false;
            PlayerPrefs.SetInt(Key(t), Count(t) - 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
