using System;
using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Character;
using BrickMuseum.City;
using UnityEngine;

namespace BrickMuseum.Data
{
    [Serializable]
    public class ModelState
    {
        public string id;
        public int stars;          // best result, 0 = never finished
        public long lockedUntil;   // UTC ticks; after running out of hearts the model stays away for 3 days
    }

    [Serializable]
    public class Placement
    {
        public string slot;
        public string id;
    }

    /// <summary>One piece of furniture in the player's house: position on the lot (units; y = the floor it stands on) and quarter turns.</summary>
    [Serializable]
    public class HomeItem
    {
        public string id;
        public float x, y, z;
        public int rot;
    }

    /// <summary>The inside of the player's house: wall and floor colours and the furniture.</summary>
    [Serializable]
    public class HomeData
    {
        public int version;                  // 2 = furniture stands in the free-built house
        public List<HomeItem> items = new List<HomeItem>();
    }

    /// <summary>How many pieces of one kind the player has for free building (kind = BuildKind.key).</summary>
    [Serializable]
    public class StockItem
    {
        public string kind;
        public int count;
    }

    /// <summary>One piece of the player's own build: kind, colour, min corner on the lot (studs; y in plates), quarter turns.</summary>
    [Serializable]
    public class BuildPieceData
    {
        public string kind, c;
        public int x, y, z, r;
    }

    /// <summary>A workshop build left half-way: which model, for which slot, the pieces already on (target indices), hearts.</summary>
    [Serializable]
    public class PendingBuild
    {
        public string model = "", slot = "";
        public List<int> placed = new List<int>();
        public int heartsTotal, heartsLeft, mistakes;
        public bool adUsed;
        public bool Has => !string.IsNullOrEmpty(model);

        public PendingBuild Copy() => new PendingBuild
        {
            model = model, slot = slot, placed = new List<int>(placed), heartsTotal = heartsTotal, heartsLeft = heartsLeft,
            mistakes = mistakes, adUsed = adUsed,
        };
    }

    /// <summary>One of the player's own cars: a name and its pieces on the chassis.</summary>
    [Serializable]
    public class CarSave
    {
        public string name = "";
        public List<BuildPieceData> pieces = new List<BuildPieceData>();
    }

    /// <summary>A lot bought along the west road: its house's pieces and furniture.</summary>
    [Serializable]
    public class LotSave
    {
        public bool owned;
        public List<BuildPieceData> pieces = new List<BuildPieceData>();
        public List<HomeItem> items = new List<HomeItem>();
        public long rentLast;    // when its rent was last collected (UTC ticks)
    }

    /// <summary>A city house's garden: the pieces laid round it on its plot.</summary>
    [Serializable]
    public class GardenSave
    {
        public string slot;
        public List<BuildPieceData> pieces = new List<BuildPieceData>();
    }

    /// <summary>A kind of model that can't be built again until this time (UTC ticks).</summary>
    [Serializable]
    public class KindLock
    {
        public string kind;
        public long until;
    }

    [Serializable]
    public class SaveFile
    {
        public List<ModelState> models = new List<ModelState>();
        public List<Placement> placements = new List<Placement>();
        public List<string> inventory = new List<string>();
        public Outfit outfit = new Outfit();
        public string myCar = "";            // model id of the player's own car ("" = none chosen yet)
        public string myHouse = "";          // model id of the player's own house
        public HomeData home = new HomeData();
        public PendingBuild pending = new PendingBuild();
        public List<PendingBuild> sites = new List<PendingBuild>();   // half-finished builds left on their plots
        public int walkLot;                                          // the home the walk starts from
        public bool moneyInit;
        public int money;                     // lira: earned with the taxi (and fishing), spent on special materials, furniture, lots
        public List<CarSave> cars = new List<CarSave>();   // the player's own cars (the garage)
        public List<FishItem> fish = new List<FishItem>();  // the cooler
        public List<LotSave> lots = new List<LotSave>();
        public List<GardenSave> gardens = new List<GardenSave>();
        public List<KindLock> cooldowns = new List<KindLock>();
        public int bonusPending;                      // level-up city bonuses waiting to be collected (with an ad)
        public int bonusPolice, bonusWorkers, bonusStaff;   // people the bonuses brought
        public bool farOpen;                          // the far district has been opened for building
        public bool farmOwned;                        // the fish farm has been bought
        public List<string> tips = new List<string>();   // tutorial tips already seen
        public List<FieldSave> fields = new List<FieldSave>();   // the village's fields
        public List<BarnSave> barns = new List<BarnSave>();      // the barn and the coop
        public bool megaOpen;                                    // Gökkent has been opened
        public List<City.HotelSave> hotels = new List<City.HotelSave>();   // the hotels' tills
        public long farmReady;                        // when its batch is ready (UTC ticks)
        public int farmLevel;                         // its upgrades (1..3)    // lots 1.. along the west road (lot 0 is build/home)
        public int activeCar;
        public bool stockInit;               // the starter kit (and materials for models built before) has been given
        public List<StockItem> stock = new List<StockItem>();
        public List<BuildPieceData> build = new List<BuildPieceData>();   // the player's own house, piece by piece
        public List<BuildPieceData> carBuild = new List<BuildPieceData>(); // the player's own car
    }

    /// <summary>
    /// The player's progress (PlayerPrefs JSON) and the rules that read it: what stands where in the city, the inventory,
    /// stars and the level they give, and which models the roulette may offer for a slot.
    /// </summary>
    public static class Progress
    {
        private const string Key = "bm_city";
        public static readonly TimeSpan LockTime = TimeSpan.FromDays(3);
        /// <summary>Total stars needed for each level (level 1 = index 0).</summary>
        public static readonly int[] LevelStars = { 0, 3, 7, 12, 18, 25, 33, 42, 52, 63, 75, 88 };
        private static SaveFile data;

        public static SaveFile Data
        {
            get
            {
                if (data != null) return data;
                try { data = JsonUtility.FromJson<SaveFile>(PlayerPrefs.GetString(Key, "")); } catch { data = null; }
                return data ??= new SaveFile();
            }
        }

        public static void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data)); PlayerPrefs.Save(); }

        public static void Reset() { data = new SaveFile(); Save(); }

        public static ModelState Of(string id)
        {
            var s = Data.models.FirstOrDefault(m => m.id == id);
            if (s == null) { s = new ModelState { id = id }; Data.models.Add(s); }
            return s;
        }

        /// <summary>What the player's brick person wears.</summary>
        public static Outfit Outfit
        {
            get => Data.outfit ??= new Outfit();
            set { Data.outfit = value; Save(); }
        }

        // ---------- stars and level ----------

        public static int Stars => Data.models.Sum(m => m.stars);

        public static int Level
        {
            get
            {
                int stars = Stars, level = 1;
                while (level < LevelStars.Length && stars >= LevelStars[level]) level++;
                return level;
            }
        }

        /// <summary>Stars of the current level's start and of the next level (equal at the top level).</summary>
        public static (int from, int to) LevelSpan
        {
            get
            {
                int l = Level;
                return (LevelStars[l - 1], l < LevelStars.Length ? LevelStars[l] : LevelStars[l - 1]);
            }
        }

        // ---------- locks ----------

        public static bool IsLocked(string id, out TimeSpan left)
        {
            var s = Data.models.FirstOrDefault(m => m.id == id);
            left = s == null ? TimeSpan.Zero : new DateTime(s.lockedUntil, DateTimeKind.Utc) - DateTime.UtcNow;
            return s != null && s.lockedUntil > DateTime.UtcNow.Ticks;
        }

        public static void Finished(string id, int stars) { var s = Of(id); s.stars = Math.Max(s.stars, stars); Save(); }

        public static void Failed(string id) { Of(id).lockedUntil = (DateTime.UtcNow + LockTime).Ticks; Save(); }

        public static string LockText(TimeSpan left)
        {
            if (left.TotalHours >= 24) return $"{(int)left.TotalDays}g {left.Hours}s";
            if (left.TotalMinutes >= 60) return $"{(int)left.TotalHours}s {left.Minutes}dk";
            return $"{Math.Max(1, (int)left.TotalMinutes)}dk";
        }

        // ---------- the city ----------

        /// <summary>Built and kept: standing somewhere in the city or waiting in the inventory. Never offered again.</summary>
        public static bool Owned(string id) => Data.placements.Any(p => p.id == id) || Data.inventory.Contains(id);

        public static string At(string slot) => Data.placements.FirstOrDefault(p => p.slot == slot)?.id;

        public static void Place(string slot, string id)
        {
            Data.placements.RemoveAll(p => p.slot == slot);
            Data.inventory.Remove(id);
            Data.placements.Add(new Placement { slot = slot, id = id });
            Save();
        }

        /// <summary>Takes what stands on a slot into the inventory.</summary>
        public static string Remove(string slot)
        {
            string id = At(slot);
            if (id == null) return null;
            Data.placements.RemoveAll(p => p.slot == slot);
            Data.inventory.Add(id);
            Save();
            return id;
        }

        /// <summary>Into the inventory (street decor and trees may be there more than once: bonuses give several).</summary>
        /// <summary>The half-finished build on a plot (null: none).</summary>
        public static PendingBuild Site(string slot) => Data.sites.FirstOrDefault(x => x.slot == slot);

        public static void ToInventory(string id)
        {
            var m = ModelCatalog.Find(id);
            if (!Data.inventory.Contains(id) || (m != null && Repeatable(m.kind))) Data.inventory.Add(id);
            Save();
        }

        /// <summary>Whether a model may stand on a slot: same kind, same house style, and small enough.</summary>
        public static bool Fits(ModelDef m, Slot s)
        {
            if (m.kind != s.kind) return false;
            if (s.area != null && (s.area.far && !Data.farOpen || s.area.mega && !Data.megaOpen)) return false;   // closed to building
            // the bridge only spans the creek; a slot for one model (the bridge, a bench) takes only that one
            if (!string.IsNullOrEmpty(s.only) ? m.id != s.only : m.id == "bridge") return false;
            if (s.kind == "house" && !string.IsNullOrEmpty(s.style) && m.style != s.style) return false;
            // it must stand at (nearly) its real size: trees may spread over the pavement
            return s.kind == "tree" || CityMap.SlotFit(m, s) >= 0.85f * m.scale;
        }

        /// <summary>
        /// The roulette's choices for a slot: the next 3 models of its kind (difficulty order) that aren't owned, aren't
        /// waiting out a lock and fit the slot.
        /// </summary>
        public static List<ModelDef> PoolFor(Slot s) =>
            ModelCatalog.All.Where(m => Fits(m, s) && (Repeatable(m.kind) || !Owned(m.id) || s.area.far || s.area.mega) && !IsLocked(m.id, out _))
                .OrderBy(m => Owned(m.id) ? 1 : 0).ThenBy(m => m.index).Take(3).ToList();

        /// <summary>Every model this slot can take now (not built yet, or one that may be built again), easiest first.</summary>
        public static List<ModelDef> ChoicesFor(Slot s) =>
            ModelCatalog.All.Where(m => Fits(m, s) && (Repeatable(m.kind) || !Owned(m.id) || s.area.far || s.area.mega) && !IsLocked(m.id, out _))
                .OrderBy(m => Owned(m.id) ? 1 : 0).ThenBy(m => m.index).Take(12).ToList();

        /// <summary>After a model is finished its kind rests for an hour (an ad wakes it at once).</summary>
        public static readonly TimeSpan KindRest = TimeSpan.FromHours(1);

        public static void RestKind(string kind)
        {
            var c = Data.cooldowns.FirstOrDefault(x => x.kind == kind);
            if (c == null) Data.cooldowns.Add(c = new KindLock { kind = kind });
            c.until = (DateTime.UtcNow + KindRest).Ticks;
            Save();
        }

        public static void WakeKind(string kind) { Data.cooldowns.RemoveAll(x => x.kind == kind); Save(); }

        /// <summary>How long a kind still rests (null: it can be built).</summary>
        public static TimeSpan? KindResting(string kind)
        {
            var c = Data.cooldowns.FirstOrDefault(x => x.kind == kind);
            if (c == null) return null;
            var left = new DateTime(c.until, DateTimeKind.Utc) - DateTime.UtcNow;
            return left > TimeSpan.Zero ? left : (TimeSpan?)null;
        }

        /// <summary>Trees and street decor can be built again and again (a street needs many of them).</summary>
        public static bool Repeatable(string kind) => kind == "tree" || kind == "decor" || kind == "sport";   // (courts may be built again)

        /// <summary>The soonest a locked model of this slot's kind becomes available again (null if none is locked).</summary>
        public static TimeSpan? NextUnlock(Slot s)
        {
            TimeSpan? best = null;
            foreach (var m in ModelCatalog.All)
                if (Fits(m, s) && !Owned(m.id) && IsLocked(m.id, out var left) && (best == null || left < best)) best = left;
            return best;
        }

        public static List<ModelDef> InventoryFor(Slot s) =>
            Data.inventory.Select(ModelCatalog.Find).Where(m => m != null && Fits(m, s)).ToList();
    }
}
