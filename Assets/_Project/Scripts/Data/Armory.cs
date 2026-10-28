using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    public enum WeaponKind { Hammer, Sword, Axe, Mace, Spear, Whirl, Gun }

    /// <summary>What a rank weapon does besides its blow: burns, freezes, jumps to the next enemies, shakes the ground,
    /// cuts wide or goes through.</summary>
    public enum SpecialFx { None, Burn, Freeze, Chain, Quake, Cleave, Pierce }

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
        /// <summary>A gun that fires a burst of energy balls a tap (automatic), and how many; else one ball a tap.</summary>
        public bool auto;
        public int burst = 1;
        /// <summary>A rank weapon: the trophy rank (league) it needs (-1: an ordinary weapon), its effect and its size.</summary>
        public int rank = -1;
        public SpecialFx fx;
        /// <summary>How many times its burn ticks (0: the usual three).</summary>
        public int fxTicks;
        public float size = 1f;
        public bool Special => rank >= 0;
        /// <summary>It carries an effect on the floors (rank and chest weapons, and the shop's lava axe and frost lance).</summary>
        public bool HasFx => fx != SpecialFx.None;
        /// <summary>Only ever found in a duel chest (never sold): its own glowing metal and its trim's colour.</summary>
        public bool chest;
        public Color tint, trim, gem;
    }

    /// <summary>
    /// The armory of Bip's workshop: hammers, swords, axes, maces and spears, each opening at a level of the campaign.
    /// Swords swing fast, axes and maces hit hard but slowly, spears in between (every blade reaches the same). The robot carries one
    /// (the equipped one); the wooden mallet is owned from the start.
    /// </summary>
    public static class Armory
    {
        // The scenario's 30 hand weapons in four families (§3.5): swords 0.12 s, spears 0.18 s, axes 0.28 s and the heavy
        // ones (hammers, maces) 0.30 s between blows. Tier 1..6 sets the duel blow (DuelPower.Blow); damage is the
        // floors' blow. Shop weapons open at a level, rank weapons at the highest rank reached (kept for good), chest
        // weapons only come out of the gold chest. And the floors' six guns.
        public static readonly List<WeaponDef> All = new List<WeaponDef>
        {
            new WeaponDef { id = "mallet", kind = WeaponKind.Hammer, tier = 1, damage = 6, reach = 2, cooldown = 0.30f, price = 0, unlockAt = 0 },
            // swords
            new WeaponDef { id = "ironSword", kind = WeaponKind.Sword, tier = 1, damage = 8, reach = 2, cooldown = 0.12f, price = 300, unlockAt = 4 },
            new WeaponDef { id = "katana", kind = WeaponKind.Sword, tier = 2, damage = 12, reach = 2, cooldown = 0.12f, price = 1500, unlockAt = 34 },
            new WeaponDef { id = "crystalSword", kind = WeaponKind.Sword, tier = 3, damage = 18, reach = 2, cooldown = 0.12f, price = 4000, unlockAt = 74 },
            new WeaponDef { id = "flameSword", kind = WeaponKind.Sword, tier = 4, damage = 26, reach = 2, cooldown = 0.12f, price = 9000, unlockAt = 129 },
            new WeaponDef { id = "plasmaBlade", kind = WeaponKind.Sword, tier = 5, damage = 36, reach = 2, cooldown = 0.12f, price = 18000, unlockAt = 189 },
            // heavy
            new WeaponDef { id = "steelHammer", kind = WeaponKind.Hammer, tier = 2, damage = 14, reach = 2, cooldown = 0.30f, price = 1500, unlockAt = 24 },
            new WeaponDef { id = "mace", kind = WeaponKind.Mace, tier = 3, damage = 22, reach = 2, cooldown = 0.30f, price = 4000, unlockAt = 59 },
            new WeaponDef { id = "thunderMaul", kind = WeaponKind.Hammer, tier = 4, damage = 32, reach = 2, cooldown = 0.30f, price = 9000, unlockAt = 139 },
            new WeaponDef { id = "novaMace", kind = WeaponKind.Mace, tier = 5, damage = 46, reach = 2, cooldown = 0.30f, price = 18000, unlockAt = 219 },
            // axes
            new WeaponDef { id = "stoneAxe", kind = WeaponKind.Axe, tier = 1, damage = 11, reach = 2, cooldown = 0.28f, price = 300, unlockAt = 11 },
            new WeaponDef { id = "doubleAxe", kind = WeaponKind.Axe, tier = 2, damage = 16, reach = 2, cooldown = 0.28f, price = 1500, unlockAt = 44 },
            new WeaponDef { id = "battleAxe", kind = WeaponKind.Axe, tier = 3, damage = 22, reach = 2, cooldown = 0.28f, price = 4000, unlockAt = 94 },
            new WeaponDef { id = "lavaAxe", kind = WeaponKind.Axe, tier = 4, damage = 32, reach = 2, cooldown = 0.28f, price = 9000, unlockAt = 149, fx = SpecialFx.Burn, size = 1.2f },
            new WeaponDef { id = "dragonAxe", kind = WeaponKind.Axe, tier = 5, damage = 44, reach = 2, cooldown = 0.28f, price = 18000, unlockAt = 199 },
            // spears
            new WeaponDef { id = "spear", kind = WeaponKind.Spear, tier = 1, damage = 9, reach = 2, cooldown = 0.18f, price = 300, unlockAt = 32 },
            new WeaponDef { id = "trident", kind = WeaponKind.Spear, tier = 2, damage = 13, reach = 2, cooldown = 0.18f, price = 1500, unlockAt = 54 },
            new WeaponDef { id = "stormSpear", kind = WeaponKind.Spear, tier = 3, damage = 19, reach = 2, cooldown = 0.18f, price = 4000, unlockAt = 119 },
            new WeaponDef { id = "halberd", kind = WeaponKind.Spear, tier = 4, damage = 27, reach = 2, cooldown = 0.18f, price = 9000, unlockAt = 179 },
            new WeaponDef { id = "frostLance", kind = WeaponKind.Spear, tier = 5, damage = 38, reach = 2, cooldown = 0.18f, price = 18000, unlockAt = 229, fx = SpecialFx.Freeze, size = 1.2f },

            // The floors' guns: fired at a tapped robot (reach: their range in tiles).
            new WeaponDef { id = "waterPistol", kind = WeaponKind.Gun, tier = 1, damage = 5, reach = 7, cooldown = 0.35f, price = 350, unlockAt = 9 },
            new WeaponDef { id = "laserPistol", kind = WeaponKind.Gun, tier = 2, damage = 8, reach = 8, cooldown = 0.3f, price = 1400, unlockAt = 29 },
            new WeaponDef { id = "plasmaRifle", kind = WeaponKind.Gun, tier = 3, damage = 12, reach = 10, cooldown = 0.42f, price = 3600, unlockAt = 59 },
            new WeaponDef { id = "twinBlaster", kind = WeaponKind.Gun, tier = 4, damage = 14, reach = 9, cooldown = 0.2f, price = 7200, unlockAt = 99 },
            new WeaponDef { id = "railSniper", kind = WeaponKind.Gun, tier = 5, damage = 30, reach = 13, cooldown = 0.9f, price = 9500, unlockAt = 129 },
            new WeaponDef { id = "novaCannon", kind = WeaponKind.Gun, tier = 6, damage = 32, reach = 11, cooldown = 0.4f, price = 24000, unlockAt = 199 },

            // The rank weapons: opened by the highest rank reached (never taken back), bought with gold; big, each with its effect.
            new WeaponDef { id = "emberBlade", kind = WeaponKind.Sword, tier = 3, damage = 22, reach = 2, cooldown = 0.12f, price = 12000, rank = 3, fx = SpecialFx.Burn, size = 1.35f },
            new WeaponDef { id = "frostEdge", kind = WeaponKind.Sword, tier = 4, damage = 30, reach = 2, cooldown = 0.12f, price = 20000, rank = 6, fx = SpecialFx.Freeze, size = 1.5f }, // the first lieutenant's
            new WeaponDef { id = "colonelBlade", kind = WeaponKind.Sword, tier = 5, damage = 40, reach = 2, cooldown = 0.12f, price = 32000, rank = 9, fx = SpecialFx.Freeze, size = 1.6f }, // the lieutenant colonel's
            new WeaponDef { id = "kingsBlade", kind = WeaponKind.Sword, tier = 6, damage = 52, reach = 2, cooldown = 0.12f, price = 50000, rank = 12, fx = SpecialFx.Cleave, size = 1.7f },
            new WeaponDef { id = "worldbreaker", kind = WeaponKind.Hammer, tier = 6, damage = 62, reach = 2, cooldown = 0.30f, price = 50000, rank = 12, fx = SpecialFx.Quake, size = 1.7f },
            new WeaponDef { id = "titanAxe", kind = WeaponKind.Axe, tier = 6, damage = 58, reach = 2, cooldown = 0.28f, price = 50000, rank = 12, fx = SpecialFx.Cleave, size = 1.7f },
            new WeaponDef { id = "dragonPike", kind = WeaponKind.Spear, tier = 6, damage = 50, reach = 2, cooldown = 0.18f, price = 50000, rank = 12, fx = SpecialFx.Pierce, size = 1.7f },

            // The chest swords: only in the gold chest. Shining metal with raised patterns and set gems.
            new WeaponDef { id = "dawnBlade", kind = WeaponKind.Sword, tier = 4, damage = 30, reach = 2, cooldown = 0.12f, price = 40000, rank = 0, fx = SpecialFx.Burn, size = 1.5f, chest = true, tint = new Color(1f, 0.85f, 0.5f), trim = new Color(1f, 0.78f, 0.3f), gem = new Color(1f, 0.42f, 0.18f) },
            new WeaponDef { id = "nightBlade", kind = WeaponKind.Sword, tier = 5, damage = 40, reach = 2, cooldown = 0.12f, price = 45000, rank = 0, fx = SpecialFx.Chain, size = 1.55f, chest = true, tint = new Color(0.45f, 0.35f, 0.95f), trim = new Color(0.85f, 0.9f, 1f), gem = new Color(0.45f, 0.95f, 1f) },
            new WeaponDef { id = "emeraldBlade", kind = WeaponKind.Sword, tier = 5, damage = 40, reach = 2, cooldown = 0.12f, price = 45000, rank = 0, fx = SpecialFx.Burn, fxTicks = 5, size = 1.5f, chest = true, tint = new Color(1f, 0.3f, 0.16f), trim = new Color(1f, 0.82f, 0.35f), gem = new Color(1f, 0.38f, 0.2f) }, // the ember thorn: a red, long-burning blade
        };

        /// <summary>A weapon's family (hammers and maces are both heavy; guns count as heavy, unused in the duel).</summary>
        public static WeaponFamily Family(WeaponDef w) =>
            w.kind == WeaponKind.Sword ? WeaponFamily.Sword : w.kind == WeaponKind.Spear ? WeaponFamily.Spear : w.kind == WeaponKind.Axe ? WeaponFamily.Axe : WeaponFamily.Heavy;

        /// <summary>The shop's shelves: 0 guns, 1 swords, 2 axes, 3 spears, 4 the heavy ones (hammers and maces).</summary>
        public static int Category(WeaponDef w)
        {
            if (w.kind == WeaponKind.Gun) return 0;
            switch (Family(w))
            {
                case WeaponFamily.Sword: return 1;
                case WeaponFamily.Axe: return 2;
                case WeaponFamily.Spear: return 3;
                default: return 4;
            }
        }

        public const int Categories = 5;

        /// <summary>Energy balls a gun carries into a floor (a single-shot gun fewer, an automatic many).</summary>
        public static int Ammo(WeaponDef w) => w.auto ? 80 + w.tier * 20 : 30 + w.tier * 5;

        private const string EquipKey = "sb_weapon";

        /// <summary>A chest weapon goes to the player (no gold, no backpack check: it was won).</summary>
        public static void Grant(WeaponDef w)
        {
            Prefs.SetInt("sb_weapon_own_" + w.id, 1);
            Prefs.Save();
        }

        public static WeaponDef Get(string id) => All.Find(w => w.id == id) ?? All[0];

        public static bool Owned(WeaponDef w) => w.price == 0 || Prefs.GetInt("sb_weapon_own_" + w.id, 0) == 1;

        /// <summary>Sold to the player: an ordinary weapon from its level on; a rank weapon once its rank was ever reached.</summary>
        public static bool Unlocked(WeaponDef w) => w.chest ? Owned(w) : w.Special ? Challenge.BestRank >= w.rank : Owned(w) || SaveData.UnlockedLevel >= w.unlockAt;

        /// <summary>Why a weapon is not for sale yet: its level, or (a rank weapon) the rank it needs.</summary>
        public static string LockText(WeaponDef w) => w.chest ? Loc.T("chests.only") : w.Special ? Loc.F("special.needRank", Loc.T("rank." + w.rank)) : Loc.F("ws.lockedAt", w.unlockAt + 1);

        /// <summary>Rank weapons are never taken back (they follow the highest rank reached): nothing is lost.</summary>
        public static List<string> RevokeLostRanks() => new List<string>();

        private const string GunKey = "sb_gun";

        /// <summary>The blade (or other hand weapon) in foi's hand: the floors' only weapon, the arenas' close one.</summary>
        public static WeaponDef Equipped
        {
            get
            {
                var w = Get(Prefs.GetString(EquipKey, All[0].id));
                return Owned(w) && w.kind != WeaponKind.Gun ? w : All[0];
            }
        }

        /// <summary>The gun foi carries into the battle arenas (null: none). Guns are no use on the floors.</summary>
        public static WeaponDef EquippedGun
        {
            get
            {
                string id = Prefs.GetString(GunKey, "");
                if (id == "") id = Prefs.GetString(EquipKey, ""); // a gun picked before guns had their own slot
                var w = All.Find(x => x.id == id);
                return w != null && w.kind == WeaponKind.Gun && Owned(w) ? w : null;
            }
        }

        /// <summary>Takes a weapon: a gun into the gun slot, anything else into the hand.</summary>
        public static void Equip(WeaponDef w)
        {
            if (!Owned(w)) return;
            Prefs.SetString(w.kind == WeaponKind.Gun ? GunKey : EquipKey, w.id);
            Prefs.Save();
        }

        /// <summary>Puts the gun down: foi goes on with its blade alone (until a gun is taken again).</summary>
        public static void DropGun()
        {
            Prefs.SetString(GunKey, "-"); // (not empty: an empty slot would fall back on an old save's hand weapon)
            Prefs.Save();
        }

        public static bool IsEquipped(WeaponDef w) => w == Equipped || w == EquippedGun;

        /// <summary>Buys the weapon (and puts it in the robot's hand).</summary>
        public static bool TryBuy(WeaponDef w)
        {
            if (Owned(w) || !Unlocked(w) || !Shop.Spend(w.price)) return false;
            Prefs.SetInt("sb_weapon_own_" + w.id, 1);
            Equip(w);
            return true;
        }

        // ---------- No levels any more (mastery takes their place) ----------

        public const int MaxLevel = 1;

        public static int Level(WeaponDef w) => 1;

        /// <summary>The floors' blow of a weapon, in health points (the arms add their share).</summary>
        public static int Damage(WeaponDef w, int level) => Damage(w);

        public static float Cooldown(WeaponDef w, int level) => w.cooldown;

        /// <summary>The floors' blow: a gun's own; a hand weapon's by its family and tier (FloorTuning: the four families
        /// even within a tier, the difference in how each fights).</summary>
        public static int Damage(WeaponDef w) => w.kind == WeaponKind.Gun ? w.damage : FloorTuning.Current.FloorBlow(Family(w), w.tier, w.id == "mallet");

        /// <summary>How far a weapon strikes on the floors (the spear farther; a gun its own range).</summary>
        public static int Reach(WeaponDef w) => w.kind != WeaponKind.Gun && Family(w) == WeaponFamily.Spear ? FloorTuning.Current.spearReach : w.reach;
        public static float Cooldown(WeaponDef w) => w.cooldown;

        public static bool TryUpgrade(WeaponDef w) => false;

        /// <summary>Weapons are no longer sold back (there is no backpack to make room in).</summary>
        public static bool CanSell(WeaponDef w) => false;

        public static int SellPrice(WeaponDef w) => 0;

        public static bool Sell(WeaponDef w) => false;

        /// <summary>A rough strength score (damage per second, reach counting extra).</summary>
        public static float Power(WeaponDef w) => Damage(w) / Mathf.Max(0.1f, Cooldown(w)) * (w.kind == WeaponKind.Whirl ? 1.6f : 1f);
    }
}
