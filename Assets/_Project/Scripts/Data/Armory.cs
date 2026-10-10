using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    public enum WeaponKind { Hammer, Sword, Axe, Mace, Spear, Whirl }

    /// <summary>One weapon of the armory: what it is, how it hits, what it costs and from which level it is sold.</summary>
    public class WeaponDef
    {
        public string id;
        public WeaponKind kind;
        /// <summary>Its look within its kind: 1 plain … 5 legendary.</summary>
        public int tier;
        /// <summary>Hammer blows per strike on robots and the monster.</summary>
        public int damage;
        /// <summary>How many tiles away it strikes.</summary>
        public int reach;
        /// <summary>Seconds between swings.</summary>
        public float cooldown;
        public int price;
        /// <summary>The level (0-based index) from which it can be bought.</summary>
        public int unlockAt;
    }

    /// <summary>
    /// The armory of Bip's workshop: hammers, swords, axes, maces and spears, each opening at a level of the campaign.
    /// Swords swing fast, axes and maces hit hard but slowly, spears reach a tile farther. The robot carries one
    /// (the equipped one); the wooden mallet is owned from the start.
    /// </summary>
    public static class Armory
    {
        public static readonly List<WeaponDef> All = new List<WeaponDef>
        {
            new WeaponDef { id = "mallet", kind = WeaponKind.Hammer, tier = 1, damage = 1, reach = 2, cooldown = 0.24f, price = 0, unlockAt = 0 },
            new WeaponDef { id = "ironSword", kind = WeaponKind.Sword, tier = 2, damage = 2, reach = 2, cooldown = 0.13f, price = 400, unlockAt = 4 },
            new WeaponDef { id = "stoneAxe", kind = WeaponKind.Axe, tier = 1, damage = 3, reach = 2, cooldown = 0.3f, price = 900, unlockAt = 11 },
            new WeaponDef { id = "steelHammer", kind = WeaponKind.Hammer, tier = 2, damage = 3, reach = 2, cooldown = 0.22f, price = 1500, unlockAt = 22 },
            new WeaponDef { id = "spear", kind = WeaponKind.Spear, tier = 2, damage = 2, reach = 2, cooldown = 0.2f, price = 1800, unlockAt = 32 },
            // The whirl blades: every blow is a full turn that hits everything around foi at once (weaker, slower blows).
            new WeaponDef { id = "whirlBlade", kind = WeaponKind.Whirl, tier = 3, damage = 2, reach = 2, cooldown = 0.5f, price = 2600, unlockAt = 40 },
            new WeaponDef { id = "mace", kind = WeaponKind.Mace, tier = 3, damage = 4, reach = 2, cooldown = 0.34f, price = 3000, unlockAt = 50 },
            new WeaponDef { id = "crystalSword", kind = WeaponKind.Sword, tier = 4, damage = 3, reach = 2, cooldown = 0.12f, price = 4200, unlockAt = 70 },
            new WeaponDef { id = "battleAxe", kind = WeaponKind.Axe, tier = 4, damage = 5, reach = 2, cooldown = 0.27f, price = 6000, unlockAt = 95 },
            new WeaponDef { id = "stormSpear", kind = WeaponKind.Spear, tier = 4, damage = 4, reach = 2, cooldown = 0.18f, price = 8000, unlockAt = 125 },
            new WeaponDef { id = "stormWhirl", kind = WeaponKind.Whirl, tier = 5, damage = 4, reach = 2, cooldown = 0.45f, price = 11000, unlockAt = 150 },
            new WeaponDef { id = "starHammer", kind = WeaponKind.Hammer, tier = 5, damage = 6, reach = 2, cooldown = 0.23f, price = 12000, unlockAt = 165 },
            new WeaponDef { id = "flameSword", kind = WeaponKind.Sword, tier = 5, damage = 5, reach = 2, cooldown = 0.11f, price = 16000, unlockAt = 205 },
            // The utopian worlds' gear (the last floors' armour needs it): plasma blade and nova mace.
            new WeaponDef { id = "plasmaBlade", kind = WeaponKind.Sword, tier = 6, damage = 7, reach = 2, cooldown = 0.11f, price = 22000, unlockAt = 212 },
            new WeaponDef { id = "novaMace", kind = WeaponKind.Mace, tier = 6, damage = 9, reach = 2, cooldown = 0.24f, price = 30000, unlockAt = 232 },
        };

        private const string EquipKey = "sb_weapon";

        public static WeaponDef Get(string id) => All.Find(w => w.id == id) ?? All[0];

        public static bool Owned(WeaponDef w) => w.price == 0 || PlayerPrefs.GetInt("sb_weapon_own_" + w.id, 0) == 1;

        public static bool Unlocked(WeaponDef w) => Owned(w) || SaveData.UnlockedLevel >= w.unlockAt;

        public static WeaponDef Equipped
        {
            get
            {
                var w = Get(PlayerPrefs.GetString(EquipKey, All[0].id));
                return Owned(w) ? w : All[0];
            }
        }

        public static void Equip(WeaponDef w)
        {
            if (!Owned(w)) return;
            PlayerPrefs.SetString(EquipKey, w.id);
            PlayerPrefs.Save();
        }

        /// <summary>Buys the weapon (and puts it in the robot's hand).</summary>
        public static bool TryBuy(WeaponDef w)
        {
            if (Owned(w) || !Unlocked(w) || !Backpack.Fits(Backpack.Weight(w)) || !Shop.Spend(w.price)) return false;
            PlayerPrefs.SetInt("sb_weapon_own_" + w.id, 1);
            Equip(w);
            return true;
        }

        // ---------- Levels (1..3) ----------

        public const int MaxLevel = 3;

        /// <summary>A weapon's level in the workshop: every weapon starts at 1 (with its own strength) and grows to 3.</summary>
        public static int Level(WeaponDef w) => Owned(w) ? Mathf.Clamp(PlayerPrefs.GetInt("sb_weapon_lv_" + w.id, 1), 1, MaxLevel) : 1;

        /// <summary>Damage of one blow at a level: each level adds about two fifths of the weapon's own damage.</summary>
        public static int Damage(WeaponDef w, int level) => w.damage + (level - 1) * Mathf.Max(1, Mathf.RoundToInt(w.damage * 0.4f));

        /// <summary>Seconds between blows at a level: a tenth quicker each level.</summary>
        public static float Cooldown(WeaponDef w, int level) => w.cooldown * (1f - 0.1f * (level - 1));

        public static int Damage(WeaponDef w) => Damage(w, Level(w));
        public static float Cooldown(WeaponDef w) => Cooldown(w, Level(w));

        /// <summary>The price of the next level (0 at the top).</summary>
        public static int UpgradePrice(WeaponDef w)
        {
            int l = Level(w);
            if (l >= MaxLevel) return 0;
            return l == 1 ? Mathf.Max(150, w.price / 2) : Mathf.Max(400, w.price);
        }

        public static bool TryUpgrade(WeaponDef w)
        {
            if (!Owned(w) || Level(w) >= MaxLevel || !Shop.Spend(UpgradePrice(w))) return false;
            PlayerPrefs.SetInt("sb_weapon_lv_" + w.id, Level(w) + 1);
            PlayerPrefs.Save();
            return true;
        }

        // ---------- Selling ----------

        /// <summary>What the workshop pays for a weapon: two fifths of what it and its levels cost.</summary>
        public static int SellPrice(WeaponDef w)
        {
            int spent = w.price;
            int l = Level(w);
            if (l >= 2) spent += Mathf.Max(150, w.price / 2);
            if (l >= 3) spent += Mathf.Max(400, w.price);
            return Mathf.RoundToInt(spent * 0.4f);
        }

        /// <summary>Any owned weapon but the wooden mallet can be sold (to make room in the backpack).</summary>
        public static bool CanSell(WeaponDef w) => w.price > 0 && Owned(w);

        public static bool Sell(WeaponDef w)
        {
            if (!CanSell(w)) return false;
            int coins = SellPrice(w);
            bool inHand = Equipped == w;
            PlayerPrefs.DeleteKey("sb_weapon_own_" + w.id);
            PlayerPrefs.DeleteKey("sb_weapon_lv_" + w.id);
            if (inHand) PlayerPrefs.SetString(EquipKey, All[0].id);
            SaveData.Coins += coins;
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>A rough strength score (damage per second, reach counting extra).</summary>
        public static float Power(WeaponDef w) => Damage(w) / Mathf.Max(0.1f, Cooldown(w)) * (w.kind == WeaponKind.Whirl ? 1.6f : 1f);
    }
}
