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
        public float size = 1f;
        public bool Special => rank >= 0;
        /// <summary>Only ever found in a duel chest (never sold): its own glowing metal and its trim's colour.</summary>
        public bool chest;
        public Color tint, trim, gem;
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
            new WeaponDef { id = "mallet", kind = WeaponKind.Hammer, tier = 1, damage = 5, reach = 2, cooldown = 0.24f, price = 0, unlockAt = 0 },
            new WeaponDef { id = "ironSword", kind = WeaponKind.Sword, tier = 2, damage = 8, reach = 2, cooldown = 0.13f, price = 400, unlockAt = 4 },
            new WeaponDef { id = "stoneAxe", kind = WeaponKind.Axe, tier = 1, damage = 12, reach = 2, cooldown = 0.3f, price = 900, unlockAt = 11 },
            new WeaponDef { id = "steelHammer", kind = WeaponKind.Hammer, tier = 2, damage = 12, reach = 2, cooldown = 0.22f, price = 1500, unlockAt = 22 },
            new WeaponDef { id = "spear", kind = WeaponKind.Spear, tier = 2, damage = 10, reach = 2, cooldown = 0.2f, price = 1800, unlockAt = 32 },
            // The whirl blades: every blow is a full turn that hits everything around foi at once (weaker, slower blows).
            new WeaponDef { id = "whirlBlade", kind = WeaponKind.Whirl, tier = 3, damage = 8, reach = 2, cooldown = 0.5f, price = 2600, unlockAt = 40 },
            new WeaponDef { id = "mace", kind = WeaponKind.Mace, tier = 3, damage = 16, reach = 2, cooldown = 0.34f, price = 3000, unlockAt = 50 },
            new WeaponDef { id = "crystalSword", kind = WeaponKind.Sword, tier = 4, damage = 18, reach = 2, cooldown = 0.12f, price = 4200, unlockAt = 70 },
            new WeaponDef { id = "battleAxe", kind = WeaponKind.Axe, tier = 4, damage = 22, reach = 2, cooldown = 0.27f, price = 6000, unlockAt = 95 },
            new WeaponDef { id = "stormSpear", kind = WeaponKind.Spear, tier = 4, damage = 20, reach = 2, cooldown = 0.18f, price = 8000, unlockAt = 125 },
            new WeaponDef { id = "stormWhirl", kind = WeaponKind.Whirl, tier = 5, damage = 16, reach = 2, cooldown = 0.45f, price = 11000, unlockAt = 150 },
            new WeaponDef { id = "starHammer", kind = WeaponKind.Hammer, tier = 5, damage = 30, reach = 2, cooldown = 0.23f, price = 12000, unlockAt = 165 },
            new WeaponDef { id = "flameSword", kind = WeaponKind.Sword, tier = 5, damage = 34, reach = 2, cooldown = 0.11f, price = 16000, unlockAt = 205 },
            // The utopian worlds' gear (the last floors' armour needs it): plasma blade and nova mace.
            new WeaponDef { id = "plasmaBlade", kind = WeaponKind.Sword, tier = 6, damage = 42, reach = 2, cooldown = 0.11f, price = 22000, unlockAt = 212 },
            new WeaponDef { id = "novaMace", kind = WeaponKind.Mace, tier = 6, damage = 50, reach = 2, cooldown = 0.24f, price = 30000, unlockAt = 232 },
            // The guns: fired over foi's shoulder at the crosshair in the arenas (aimed with the right thumb), at a tapped
            // robot on the floors. Reach is their range in tiles; they hit as hard as the blades of their time.
            new WeaponDef { id = "waterPistol", kind = WeaponKind.Gun, tier = 1, damage = 5, reach = 7, cooldown = 0.35f, price = 350, unlockAt = 9 },
            new WeaponDef { id = "laserPistol", kind = WeaponKind.Gun, tier = 2, damage = 8, reach = 8, cooldown = 0.3f, price = 1400, unlockAt = 29 },
            new WeaponDef { id = "plasmaRifle", kind = WeaponKind.Gun, tier = 3, damage = 12, reach = 10, cooldown = 0.42f, price = 3600, unlockAt = 59 },
            new WeaponDef { id = "twinBlaster", kind = WeaponKind.Gun, tier = 4, damage = 14, reach = 9, cooldown = 0.2f, price = 7200, unlockAt = 99 },
            new WeaponDef { id = "stormCannon", kind = WeaponKind.Gun, tier = 5, damage = 22, reach = 10, cooldown = 0.45f, price = 12500, unlockAt = 149 },
            new WeaponDef { id = "novaCannon", kind = WeaponKind.Gun, tier = 6, damage = 32, reach = 11, cooldown = 0.4f, price = 24000, unlockAt = 199 },
            // More of every kind (the workshop's bigger shelves): an early wooden sword and spiked club, a katana, a
            // double axe, a trident, a thunder maul, a halberd and a dragon axe.
            new WeaponDef { id = "woodSword", kind = WeaponKind.Sword, tier = 1, damage = 6, reach = 2, cooldown = 0.16f, price = 150, unlockAt = 2 },
            new WeaponDef { id = "spikedClub", kind = WeaponKind.Mace, tier = 1, damage = 9, reach = 2, cooldown = 0.3f, price = 600, unlockAt = 7 },
            new WeaponDef { id = "katana", kind = WeaponKind.Sword, tier = 3, damage = 14, reach = 2, cooldown = 0.11f, price = 3400, unlockAt = 60 },
            new WeaponDef { id = "doubleAxe", kind = WeaponKind.Axe, tier = 3, damage = 18, reach = 2, cooldown = 0.29f, price = 3800, unlockAt = 80 },
            new WeaponDef { id = "trident", kind = WeaponKind.Spear, tier = 3, damage = 15, reach = 2, cooldown = 0.19f, price = 3200, unlockAt = 85 },
            new WeaponDef { id = "thunderMaul", kind = WeaponKind.Hammer, tier = 4, damage = 28, reach = 2, cooldown = 0.26f, price = 9000, unlockAt = 140 },
            new WeaponDef { id = "halberd", kind = WeaponKind.Spear, tier = 5, damage = 32, reach = 2, cooldown = 0.2f, price = 14000, unlockAt = 185 },
            new WeaponDef { id = "dragonAxe", kind = WeaponKind.Axe, tier = 5, damage = 36, reach = 2, cooldown = 0.28f, price = 15000, unlockAt = 190 },
            // Automatic guns (a burst of energy balls a tap) and a long-range rail sniper.
            new WeaponDef { id = "bubbleSmg", kind = WeaponKind.Gun, tier = 2, damage = 3, reach = 7, cooldown = 0.5f, price = 1600, unlockAt = 24, auto = true, burst = 4 },
            new WeaponDef { id = "ionRifle", kind = WeaponKind.Gun, tier = 3, damage = 6, reach = 8, cooldown = 0.6f, price = 4400, unlockAt = 74, auto = true, burst = 4 },
            new WeaponDef { id = "railSniper", kind = WeaponKind.Gun, tier = 4, damage = 30, reach = 13, cooldown = 0.9f, price = 9500, unlockAt = 129 },
            new WeaponDef { id = "gatling", kind = WeaponKind.Gun, tier = 5, damage = 8, reach = 9, cooldown = 0.7f, price = 14500, unlockAt = 179, auto = true, burst = 6 },
            new WeaponDef { id = "photonSmg", kind = WeaponKind.Gun, tier = 6, damage = 12, reach = 10, cooldown = 0.55f, price = 26000, unlockAt = 225, auto = true, burst = 5 },

            // The rank weapons: three a shelf, opened by the trophy rank (Staff sergeant, Captain, Marshal), bought with gold
            // and lost when the rank falls below them. Big, and each with an effect of its own.
            new WeaponDef { id = "sunLance", kind = WeaponKind.Gun, tier = 6, damage = 22, reach = 11, cooldown = 0.5f, price = 18000, rank = 3, fx = SpecialFx.Pierce, size = 1.3f },
            new WeaponDef { id = "thunderRail", kind = WeaponKind.Gun, tier = 6, damage = 34, reach = 12, cooldown = 0.6f, price = 36000, rank = 7, fx = SpecialFx.Chain, size = 1.4f },
            new WeaponDef { id = "starBreaker", kind = WeaponKind.Gun, tier = 6, damage = 18, reach = 11, cooldown = 0.7f, price = 60000, rank = 12, fx = SpecialFx.Quake, size = 1.5f, auto = true, burst = 4 },
            new WeaponDef { id = "emberBlade", kind = WeaponKind.Sword, tier = 6, damage = 30, reach = 2, cooldown = 0.13f, price = 18000, rank = 3, fx = SpecialFx.Burn, size = 1.35f },
            new WeaponDef { id = "frostEdge", kind = WeaponKind.Sword, tier = 6, damage = 44, reach = 2, cooldown = 0.12f, price = 36000, rank = 7, fx = SpecialFx.Freeze, size = 1.5f },
            new WeaponDef { id = "kingsBlade", kind = WeaponKind.Sword, tier = 6, damage = 60, reach = 2, cooldown = 0.12f, price = 60000, rank = 12, fx = SpecialFx.Cleave, size = 1.7f },
            new WeaponDef { id = "lavaAxe", kind = WeaponKind.Axe, tier = 6, damage = 38, reach = 2, cooldown = 0.28f, price = 18000, rank = 3, fx = SpecialFx.Burn, size = 1.35f },
            new WeaponDef { id = "quakeMace", kind = WeaponKind.Mace, tier = 6, damage = 54, reach = 2, cooldown = 0.3f, price = 36000, rank = 7, fx = SpecialFx.Quake, size = 1.5f },
            new WeaponDef { id = "titanAxe", kind = WeaponKind.Axe, tier = 6, damage = 75, reach = 2, cooldown = 0.28f, price = 60000, rank = 12, fx = SpecialFx.Cleave, size = 1.7f },
            new WeaponDef { id = "lightningPike", kind = WeaponKind.Spear, tier = 6, damage = 30, reach = 2, cooldown = 0.18f, price = 18000, rank = 3, fx = SpecialFx.Chain, size = 1.35f },
            new WeaponDef { id = "frostLance", kind = WeaponKind.Spear, tier = 6, damage = 44, reach = 2, cooldown = 0.18f, price = 36000, rank = 7, fx = SpecialFx.Freeze, size = 1.5f },
            new WeaponDef { id = "dragonPike", kind = WeaponKind.Spear, tier = 6, damage = 62, reach = 2, cooldown = 0.17f, price = 60000, rank = 12, fx = SpecialFx.Pierce, size = 1.7f },
            new WeaponDef { id = "meteorHammer", kind = WeaponKind.Hammer, tier = 6, damage = 40, reach = 2, cooldown = 0.25f, price = 18000, rank = 3, fx = SpecialFx.Quake, size = 1.35f },
            new WeaponDef { id = "glacierMaul", kind = WeaponKind.Hammer, tier = 6, damage = 56, reach = 2, cooldown = 0.25f, price = 36000, rank = 7, fx = SpecialFx.Freeze, size = 1.5f },
            new WeaponDef { id = "worldbreaker", kind = WeaponKind.Hammer, tier = 6, damage = 80, reach = 2, cooldown = 0.26f, price = 60000, rank = 12, fx = SpecialFx.Quake, size = 1.7f },

            // The chest weapons: never in the shop, only in duel chests (the better ones, for winning streaks). Shining
            // metal with raised patterns and set gems; each with an effect of its own like the rank weapons.
            new WeaponDef { id = "dawnBlade", kind = WeaponKind.Sword, tier = 6, damage = 46, reach = 2, cooldown = 0.12f, price = 40000, rank = 0, fx = SpecialFx.Burn, size = 1.5f, chest = true, tint = new Color(1f, 0.85f, 0.5f), trim = new Color(1f, 0.78f, 0.3f), gem = new Color(1f, 0.42f, 0.18f) },
            new WeaponDef { id = "nightBlade", kind = WeaponKind.Sword, tier = 6, damage = 52, reach = 2, cooldown = 0.12f, price = 45000, rank = 0, fx = SpecialFx.Chain, size = 1.55f, chest = true, tint = new Color(0.45f, 0.35f, 0.95f), trim = new Color(0.85f, 0.9f, 1f), gem = new Color(0.45f, 0.95f, 1f) },
            new WeaponDef { id = "emeraldBlade", kind = WeaponKind.Sword, tier = 6, damage = 50, reach = 2, cooldown = 0.11f, price = 45000, rank = 0, fx = SpecialFx.Freeze, size = 1.5f, chest = true, tint = new Color(0.3f, 0.95f, 0.6f), trim = new Color(1f, 0.82f, 0.35f), gem = new Color(0.55f, 1f, 0.7f) },
            new WeaponDef { id = "sunMace", kind = WeaponKind.Mace, tier = 6, damage = 58, reach = 2, cooldown = 0.28f, price = 48000, rank = 0, fx = SpecialFx.Quake, size = 1.5f, chest = true, tint = new Color(1f, 0.72f, 0.25f), trim = new Color(1f, 0.86f, 0.45f), gem = new Color(1f, 0.25f, 0.3f) },
            new WeaponDef { id = "crownMaul", kind = WeaponKind.Hammer, tier = 6, damage = 64, reach = 2, cooldown = 0.25f, price = 50000, rank = 0, fx = SpecialFx.Quake, size = 1.5f, chest = true, tint = new Color(0.95f, 0.74f, 0.28f), trim = new Color(1f, 0.9f, 0.6f), gem = new Color(0.95f, 0.15f, 0.25f) },
        };

        /// <summary>The shop's shelves: 0 guns, 1 swords (and whirl blades), 2 axes and maces, 3 spears, 4 hammers.</summary>
        public static int Category(WeaponDef w)
        {
            switch (w.kind)
            {
                case WeaponKind.Gun: return 0;
                case WeaponKind.Sword: case WeaponKind.Whirl: return 1;
                case WeaponKind.Axe: case WeaponKind.Mace: return 2;
                case WeaponKind.Spear: return 3;
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

        /// <summary>Sold to the player: an ordinary weapon from its level on; a rank weapon while the trophy rank is high enough.</summary>
        public static bool Unlocked(WeaponDef w) => w.chest ? Owned(w) : w.Special ? Challenge.League >= w.rank : Owned(w) || SaveData.UnlockedLevel >= w.unlockAt;

        /// <summary>Why a weapon is not for sale yet: its level, or (a rank weapon) the rank it needs.</summary>
        public static string LockText(WeaponDef w) => w.chest ? Loc.T("chests.only") : w.Special ? Loc.F("special.needRank", Loc.T("rank." + w.rank)) : Loc.F("ws.lockedAt", w.unlockAt + 1);

        /// <summary>
        /// The rank weapons whose rank the player no longer holds are taken away (with their levels); the hand and the gun
        /// slot fall back to what is left. Returns their ids (empty: none lost).
        /// </summary>
        public static List<string> RevokeLostRanks()
        {
            var lost = new List<string>();
            int league = Challenge.League;
            foreach (var w in All)
            {
                if (!w.Special || w.chest || league >= w.rank || Prefs.GetInt("sb_weapon_own_" + w.id, 0) != 1) continue;
                Prefs.DeleteKey("sb_weapon_own_" + w.id);
                Prefs.DeleteKey("sb_weapon_lv_" + w.id);
                lost.Add(w.id);
            }
            if (lost.Count > 0) Prefs.Save();
            return lost;
        }

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
            if (Owned(w) || !Unlocked(w) || !Backpack.Fits(Backpack.Weight(w)) || !Shop.Spend(w.price)) return false;
            Prefs.SetInt("sb_weapon_own_" + w.id, 1);
            Equip(w);
            return true;
        }

        // ---------- Levels (1..3) ----------

        public const int MaxLevel = 3;

        /// <summary>A weapon's level in the workshop: every weapon starts at 1 (with its own strength) and grows to 3.</summary>
        public static int Level(WeaponDef w) => Owned(w) ? Mathf.Clamp(Prefs.GetInt("sb_weapon_lv_" + w.id, 1), 1, MaxLevel) : 1;

        /// <summary>
        /// Damage of one blow at a level, in health points: each level adds two fifths of the weapon's own. A first
        /// world's basic robot has 20, foi starts with 100.
        /// </summary>
        public static int Damage(WeaponDef w, int level) => Mathf.RoundToInt(w.damage * (1f + 0.4f * (Mathf.Clamp(level, 1, MaxLevel) - 1)));

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
            Prefs.SetInt("sb_weapon_lv_" + w.id, Level(w) + 1);
            Prefs.Save();
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
            Prefs.DeleteKey("sb_weapon_own_" + w.id);
            Prefs.DeleteKey("sb_weapon_lv_" + w.id);
            if (inHand) Prefs.SetString(EquipKey, All[0].id);
            SaveData.Coins += coins;
            Prefs.Save();
            return true;
        }

        /// <summary>A rough strength score (damage per second, reach counting extra).</summary>
        public static float Power(WeaponDef w) => Damage(w) / Mathf.Max(0.1f, Cooldown(w)) * (w.kind == WeaponKind.Whirl ? 1.6f : 1f);
    }
}
