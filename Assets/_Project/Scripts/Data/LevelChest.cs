using UnityEngine;

namespace SquashBot.Data
{
    public enum ChestRarity { Common, Rare, Epic }

    /// <summary>
    /// The chest at the end of every won floor: wooden (most of the time), silver or golden, opened with a tap. It
    /// holds coins (more on later floors, two and a half or five times as many in the silver and golden ones) and the
    /// better chests a spare life too. Its coins can be doubled by watching an ad.
    /// </summary>
    public static class LevelChest
    {
        public struct Reward
        {
            public ChestRarity rarity;
            public int coins, lives;
        }

        public static Reward Roll(int levelIndex)
        {
            float r = Random.value;
            var rarity = r < 0.08f ? ChestRarity.Epic : r < 0.35f ? ChestRarity.Rare : ChestRarity.Common;
            int baseCoins = 25 + Mathf.RoundToInt(levelIndex * 1.5f);
            float mult = rarity == ChestRarity.Epic ? 5f : rarity == ChestRarity.Rare ? 2.5f : 1f;
            return new Reward
            {
                rarity = rarity,
                coins = Mathf.RoundToInt(baseCoins * mult * Random.Range(0.85f, 1.15f)),
                lives = rarity == ChestRarity.Common ? 0 : 1,
            };
        }

        public static void Grant(Reward reward)
        {
            SaveData.Coins += reward.coins;
            if (reward.lives > 0) Lives.Add(reward.lives);
        }
    }
}
