using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    public enum WeaponKind { Hammer, Sword, Axe, Mace, Spear }

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
            new WeaponDef { id = "ironSword", kind = WeaponKind.Sword, tier = 2, damage = 1, reach = 2, cooldown = 0.14f, price = 400, unlockAt = 4 },
            new WeaponDef { id = "stoneAxe", kind = WeaponKind.Axe, tier = 1, damage = 2, reach = 2, cooldown = 0.34f, price = 900, unlockAt = 11 },
            new WeaponDef { id = "steelHammer", kind = WeaponKind.Hammer, tier = 2, damage = 2, reach = 2, cooldown = 0.25f, price = 1500, unlockAt = 22 },
            new WeaponDef { id = "spear", kind = WeaponKind.Spear, tier = 2, damage = 1, reach = 3, cooldown = 0.22f, price = 1800, unlockAt = 32 },
            new WeaponDef { id = "mace", kind = WeaponKind.Mace, tier = 3, damage = 3, reach = 2, cooldown = 0.38f, price = 3000, unlockAt = 50 },
            new WeaponDef { id = "crystalSword", kind = WeaponKind.Sword, tier = 4, damage = 2, reach = 2, cooldown = 0.13f, price = 4200, unlockAt = 70 },
            new WeaponDef { id = "battleAxe", kind = WeaponKind.Axe, tier = 4, damage = 3, reach = 2, cooldown = 0.3f, price = 6000, unlockAt = 95 },
            new WeaponDef { id = "stormSpear", kind = WeaponKind.Spear, tier = 4, damage = 2, reach = 3, cooldown = 0.2f, price = 8000, unlockAt = 125 },
            new WeaponDef { id = "starHammer", kind = WeaponKind.Hammer, tier = 5, damage = 4, reach = 2, cooldown = 0.26f, price = 12000, unlockAt = 165 },
            new WeaponDef { id = "flameSword", kind = WeaponKind.Sword, tier = 5, damage = 3, reach = 2, cooldown = 0.12f, price = 16000, unlockAt = 205 },
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

        /// <summary>A rough strength score (damage per second, reach counting extra).</summary>
        public static float Power(WeaponDef w) => w.damage / Mathf.Max(0.1f, w.cooldown) * (w.reach >= 3 ? 1.25f : 1f);
    }
}
