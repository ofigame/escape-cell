using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    public enum ChestRarity { Common, Rare, Epic }

    /// <summary>
    /// The surprise chest after a won floor. It doesn't come every time: the coins earned on won floors fill a meter, and
    /// when it is full the chest comes (the rest carries over). What is inside is a surprise: always some coins (more on
    /// later floors, more in the silver and golden chests), and one, two or three gifts from the workshop — a spare life,
    /// a boost from Bip's counter, defences (archers, cannons, towers once they are open) or a bomb. Its coins can be
    /// doubled by watching an ad.
    /// </summary>
    public static class LevelChest
    {
        public enum GiftKind { Life, Boost, Defense }

        public struct Gift
        {
            public GiftKind kind;
            public Boost boost;
            public DefenseKind defense;
            public int amount;
        }

        public struct Reward
        {
            public ChestRarity rarity;
            public int coins, lives;
            public List<Gift> gifts;
        }

        private const string MeterKey = "sb_chest_meter";

        /// <summary>Coins banked toward the next chest.</summary>
        public static int Meter => PlayerPrefs.GetInt(MeterKey, 0);

        /// <summary>How many coins fill the meter (a little more further in).</summary>
        public static int Cost(int levelIndex) => 120 + Mathf.RoundToInt(levelIndex * 0.8f);

        /// <summary>A floor was won with these coins: true when that filled the meter (the chest comes; the rest carries).</summary>
        public static bool AddCoins(int coins, int levelIndex)
        {
            int meter = Meter + Mathf.Max(0, coins);
            int cost = Cost(levelIndex);
            bool full = meter >= cost;
            PlayerPrefs.SetInt(MeterKey, full ? Mathf.Min(meter - cost, cost - 1) : meter);
            PlayerPrefs.Save();
            return full;
        }

        public static Reward Roll(int levelIndex)
        {
            float r = Random.value;
            var rarity = r < 0.12f ? ChestRarity.Epic : r < 0.4f ? ChestRarity.Rare : ChestRarity.Common;
            int baseCoins = 25 + Mathf.RoundToInt(levelIndex * 1.5f);
            float mult = rarity == ChestRarity.Epic ? 4f : rarity == ChestRarity.Rare ? 2f : 1f;
            var reward = new Reward
            {
                rarity = rarity,
                coins = Mathf.RoundToInt(baseCoins * mult * Random.Range(0.85f, 1.15f)),
                gifts = new List<Gift>(),
            };
            int gifts = rarity == ChestRarity.Epic ? 3 : rarity == ChestRarity.Rare ? 2 : 1;
            var counter = new List<Boost>(Shop.Counter());
            counter.RemoveAll(Shop.Full); // only boosts there is room for
            var defences = new List<DefenseKind>();
            // (no defences any more: the defences are gone from the game)
            for (int i = 0; i < gifts; i++)
            {
                float g = Random.value;
                if (g < 0.4f && defences.Count > 0)
                {
                    var k = defences[Random.Range(0, defences.Count)];
                    int n = k == DefenseKind.Archer ? Random.Range(1, 4) : 1;
                    int at = reward.gifts.FindIndex(o => o.kind == GiftKind.Defense && o.defense == k);
                    if (at >= 0) { var o = reward.gifts[at]; o.amount += n; reward.gifts[at] = o; }
                    else reward.gifts.Add(new Gift { kind = GiftKind.Defense, defense = k, amount = n });
                }
                else if (g < 0.8f && counter.Count > 0)
                {
                    var b = counter[Random.Range(0, counter.Count)];
                    counter.Remove(b); // one of each (only one can be carried)
                    reward.gifts.Add(new Gift { kind = GiftKind.Boost, boost = b, amount = 1 });
                }
                else
                {
                    reward.lives++;
                    int at = reward.gifts.FindIndex(o => o.kind == GiftKind.Life);
                    if (at >= 0) { var o = reward.gifts[at]; o.amount++; reward.gifts[at] = o; }
                    else reward.gifts.Add(new Gift { kind = GiftKind.Life, amount = 1 });
                }
            }
            return reward;
        }

        public static void Grant(Reward reward)
        {
            SaveData.Coins += reward.coins;
            if (reward.gifts == null) return;
            foreach (var g in reward.gifts)
            {
                switch (g.kind)
                {
                    case GiftKind.Life: Lives.Add(g.amount); break;
                    case GiftKind.Boost: Shop.Give(g.boost, g.amount); break;
                    case GiftKind.Defense: Defenses.AddStock(g.defense, g.amount); break;
                }
            }
        }

        /// <summary>The chest's contents as lines for its card.</summary>
        public static string Describe(Reward reward, int coins)
        {
            var lines = new List<string> { Loc.F("chest.coins", coins) };
            if (reward.gifts != null)
                foreach (var g in reward.gifts)
                {
                    string name = g.kind == GiftKind.Life ? Loc.T("chest.life")
                        : g.kind == GiftKind.Boost ? Loc.T("shop." + g.boost)
                        : Loc.T("def." + g.defense);
                    lines.Add(Loc.F("chest.gift", g.amount, name));
                }
            return string.Join("\n", lines);
        }
    }
}
