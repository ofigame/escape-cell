using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// City mode rules and save: placing, moving and selling pieces (with undo), care stages and repairs,
    /// production buildings and their perks, and the residents' wishes.
    /// </summary>
    public static class City
    {
        public const int MinSize = 6, MaxSize = 12;

        /// <summary>
        /// The plot grows with the game: 6x6 when the city opens, one more cell every 3 worlds, 12x12 on the last floors.
        /// It never shrinks below what is already built. Updated by <see cref="RefreshSize"/> when the city opens.
        /// </summary>
        public static int Size { get; private set; } = MinSize;

        public static int SizeAtWorld(int world) => Mathf.Min(MaxSize, MinSize + world / 3);

        /// <summary>The next world (0-based) whose arrival makes the plot bigger, or -1 at full size.</summary>
        public static int NextGrowthWorld
        {
            get
            {
                for (int w = ReachedWorld + 1; w < LevelCatalog.WorldCount; w++)
                    if (SizeAtWorld(w) > SizeAtWorld(ReachedWorld)) return w;
                return -1;
            }
        }

        public static void RefreshSize()
        {
            int s = SizeAtWorld(ReachedWorld);
            foreach (var p in Data.pieces)
            {
                var f = p.Piece.Size(p.rot);
                s = Mathf.Max(s, Mathf.Max(p.x + f.x, p.y + f.y));
            }
            Size = Mathf.Min(s, MaxSize);
        }
        public const int MaxPieces = 150;
        public const int UndoDepth = 10;
        public const int MineRate = 5;      // coins per hour
        public const int MineCap = 60;
        public const int MaxRequests = 3;
        public const int WishesForBestFriend = 5;
        public const int BestFriendReward = 200;
        private const long Hour = 3600, Day = 86400;
        private const long RequestCooldown = 4 * Hour;
        private const long LongAbsence = 7 * Day;
        private const float WalkRefund = 0.2f;
        private const string Key = "sb_city";

        private static CityRecord data;
        private static readonly List<CityUndo> undo = new List<CityUndo>();

        public static CityRecord Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static IReadOnlyList<CityPlaced> Pieces => Data.pieces;

        /// <summary>The city opens with the first rescued resident (level 20's WARDEN).</summary>
        public static bool Open => Residents.RescuedCount > 0;

        /// <summary>The furthest world the player has reached (test builds: all of them).</summary>
        public static int ReachedWorld => SaveData.TestMode ? LevelCatalog.WorldCount - 1 : LevelCatalog.WorldOf(SaveData.UnlockedLevel);

        private static void Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            data = string.IsNullOrEmpty(json) ? new CityRecord() : JsonUtility.FromJson<CityRecord>(json) ?? new CityRecord();
            if (data.granted == null || data.granted.Length != Residents.Count) System.Array.Resize(ref data.granted, Residents.Count);
            if (data.bestPaid == null || data.bestPaid.Length != Residents.Count) System.Array.Resize(ref data.bestPaid, Residents.Count);
            data.pieces.RemoveAll(p => p.Piece == null);
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        // ---------- Unlocks ----------

        public static int WorldStars(int world)
        {
            int n = 0;
            for (int i = 0; i < LevelCatalog.LevelsPerWorld; i++) n += Progress.Stars(world * LevelCatalog.LevelsPerWorld + i);
            return n;
        }

        public static bool Unlocked(CityPiece p) => p.world <= ReachedWorld && (p.stars == 0 || SaveData.TestMode || WorldStars(p.world) >= p.stars);

        /// <summary>"Opens on floor 4" or "25 stars on Deep Ocean (12/25)"; null when unlocked.</summary>
        public static string LockText(CityPiece p)
        {
            if (p.world > ReachedWorld) return Loc.F("city.lockFloor", p.world + 1);
            if (p.stars > 0 && !Unlocked(p)) return Loc.F("city.lockStars", p.stars, Loc.T(LevelCatalog.WorldKey(p.world)), WorldStars(p.world));
            return null;
        }

        // ---------- Placement ----------

        public static CityPlaced Get(int uid) => Data.pieces.Find(p => p.uid == uid);

        /// <summary>The piece standing on cell (x, y), or null.</summary>
        public static CityPlaced At(int x, int y, int ignoreUid = 0)
        {
            foreach (var p in Data.pieces)
            {
                if (p.uid == ignoreUid) continue;
                var s = p.Piece.Size(p.rot);
                if (x >= p.x && x < p.x + s.x && y >= p.y && y < p.y + s.y) return p;
            }
            return null;
        }

        public static bool Fits(CityPiece piece, int x, int y, int rot, int ignoreUid = 0)
        {
            var s = piece.Size(rot);
            if (x < 0 || y < 0 || x + s.x > Size || y + s.y > Size) return false;
            for (int i = 0; i < s.x; i++)
                for (int j = 0; j < s.y; j++)
                    if (At(x + i, y + j, ignoreUid) != null) return false;
            return true;
        }

        /// <summary>A free spot for the piece, searching outward from (x, y); false when the plot is full for it.</summary>
        public static bool FindSpot(CityPiece piece, int rot, int x, int y, out Vector2Int spot)
        {
            for (int r = 0; r < Size * 2; r++)
                for (int i = -r; i <= r; i++)
                    for (int j = -r; j <= r; j++)
                    {
                        if (Mathf.Max(Mathf.Abs(i), Mathf.Abs(j)) != r) continue;
                        if (Fits(piece, x + i, y + j, rot))
                        {
                            spot = new Vector2Int(x + i, y + j);
                            return true;
                        }
                    }
            spot = default;
            return false;
        }

        public static bool Full => Data.pieces.Count >= MaxPieces;

        /// <summary>Buys and places a piece; null when it does not fit, is locked or cannot be paid.</summary>
        public static CityPlaced Place(CityPiece piece, int x, int y, int rot)
        {
            if (Full || !Unlocked(piece) || !Fits(piece, x, y, rot) || !Shop.Spend(piece.price)) return null;
            long now = CityClock.Now;
            var placed = new CityPlaced { uid = Data.nextUid++, id = piece.id, x = x, y = y, rot = rot, care = now, mined = now };
            Data.pieces.Add(placed);
            Remember(CityUndoKind.Place, placed, piece.price);
            Save();
            return placed;
        }

        public static int SellValue(CityPlaced p) => p.Piece.price / 2;

        public static int Sell(int uid)
        {
            var p = Get(uid);
            if (p == null) return 0;
            int refund = SellValue(p);
            Data.pieces.Remove(p);
            SaveData.Coins += refund;
            Remember(CityUndoKind.Sell, p, refund);
            Save();
            return refund;
        }

        public static bool Move(int uid, int x, int y, int rot)
        {
            var p = Get(uid);
            if (p == null || !Fits(p.Piece, x, y, rot, uid)) return false;
            if (p.x == x && p.y == y && p.rot == rot) return true;
            Remember(CityUndoKind.Move, p, 0);
            p.x = x;
            p.y = y;
            p.rot = rot;
            Save();
            return true;
        }

        private static void Remember(CityUndoKind kind, CityPlaced p, int coins)
        {
            undo.Add(new CityUndo { kind = kind, before = p.Copy(), coins = coins });
            if (undo.Count > UndoDepth) undo.RemoveAt(0);
        }

        public static int UndoCount => undo.Count;

        /// <summary>Takes back the last placing, selling or moving. Returns the uid it touched (0 = nothing undone).</summary>
        public static int Undo()
        {
            if (undo.Count == 0) return 0;
            var u = undo[undo.Count - 1];
            switch (u.kind)
            {
                case CityUndoKind.Place:
                    if (Get(u.before.uid) == null) break;
                    Data.pieces.RemoveAll(p => p.uid == u.before.uid);
                    SaveData.Coins += u.coins;
                    break;
                case CityUndoKind.Sell:
                    if (SaveData.Coins < u.coins || !Fits(u.before.Piece, u.before.x, u.before.y, u.before.rot)) return 0;
                    SaveData.Coins -= u.coins;
                    Data.pieces.Add(u.before.Copy());
                    break;
                case CityUndoKind.Move:
                    var p = Get(u.before.uid);
                    if (p == null || !Fits(p.Piece, u.before.x, u.before.y, u.before.rot, p.uid)) return 0;
                    p.x = u.before.x;
                    p.y = u.before.y;
                    p.rot = u.before.rot;
                    break;
            }
            undo.RemoveAt(undo.Count - 1);
            Save();
            return u.before.uid;
        }

        public static void ClearUndo() => undo.Clear();

        public static int Count(string id) => Data.pieces.FindAll(p => p.id == id).Count;

        // ---------- Care ----------

        public static CareStage Stage(CityPlaced p)
        {
            float days = p.Piece.CareDays;
            if (days <= 0f) return CareStage.Fresh;
            float age = (CityClock.Now - p.care) / (float)Day;
            return age >= days ? CareStage.NeedsCare : age >= days * 0.5f ? CareStage.Dusty : CareStage.Fresh;
        }

        /// <summary>0 (just cared for) to 1 (needs care): how faded it looks.</summary>
        public static float Wear(CityPlaced p)
        {
            float days = p.Piece.CareDays;
            if (days <= 0f) return 0f;
            return Mathf.Clamp01((CityClock.Now - p.care) / (float)Day / days);
        }

        public static bool NeedsAttention(CityPlaced p) => Stage(p) != CareStage.Fresh;

        public static int CareFee(CityPlaced p)
        {
            if (p.Piece.CareDays <= 0f || Data.freeRound) return 0;
            float k = PerkActive(CityPerk.RepairShop) ? 0.75f : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(p.Piece.BaseCareFee * k));
        }

        /// <summary>
        /// Cares for one building: false when it does not need it or cannot be paid. Done in person (walking up to it),
        /// a fifth of the fee comes back; <paramref name="refund"/> says how much.
        /// </summary>
        public static bool Repair(int uid, bool inPerson, out int paid, out int refund)
        {
            paid = refund = 0;
            var p = Get(uid);
            if (p == null || !NeedsAttention(p)) return false;
            int fee = CareFee(p);
            if (fee > 0 && !Shop.Spend(fee)) return false;
            paid = fee;
            if (inPerson && fee > 0)
            {
                refund = Mathf.Max(1, Mathf.RoundToInt(fee * WalkRefund));
                SaveData.Coins += refund;
            }
            // A mine that stood idle restarts its production now.
            if (Stage(p) == CareStage.NeedsCare && p.Piece.perk == CityPerk.Mine) p.mined = CityClock.Now - (long)(MineCoins(p) * Hour / (float)MineRate);
            p.care = CityClock.Now;
            EndFreeRoundIfDone();
            Save();
            return true;
        }

        public static int RepairAllCost
        {
            get
            {
                int sum = 0;
                foreach (var p in Data.pieces) if (NeedsAttention(p)) sum += CareFee(p);
                return sum;
            }
        }

        public static int NeedingCare
        {
            get
            {
                int n = 0;
                foreach (var p in Data.pieces) if (NeedsAttention(p)) n++;
                return n;
            }
        }

        /// <summary>"Repair all" from the building view: everything at once, no refund.</summary>
        public static bool RepairAll()
        {
            int cost = RepairAllCost;
            if (NeedingCare == 0 || (cost > 0 && !Shop.Spend(cost))) return false;
            foreach (var p in Data.pieces)
            {
                if (!NeedsAttention(p)) continue;
                if (Stage(p) == CareStage.NeedsCare && p.Piece.perk == CityPerk.Mine) p.mined = CityClock.Now - (long)(MineCoins(p) * Hour / (float)MineRate);
                p.care = CityClock.Now;
            }
            Data.freeRound = false;
            Save();
            return true;
        }

        private static void EndFreeRoundIfDone()
        {
            if (Data.freeRound && NeedingCare == 0) Data.freeRound = false;
        }

        /// <summary>
        /// Opening the city: back after a week or more, WARDEN "welcomes" the player and the first round of repairs is free.
        /// Returns true when that welcome should be shown.
        /// </summary>
        public static bool Visit()
        {
            long now = CityClock.Now;
            bool longAway = Data.lastVisit > 0 && now - Data.lastVisit >= LongAbsence;
            if (longAway && NeedingCare > 0) Data.freeRound = true;
            Data.lastVisit = now;
            Save();
            return longAway;
        }

        // ---------- Production buildings ----------

        /// <summary>A perk works while at least one of its buildings stands and is not waiting for care.</summary>
        public static bool PerkActive(CityPerk perk)
        {
            foreach (var p in Data.pieces)
                if (p.Piece.perk == perk && Stage(p) != CareStage.NeedsCare) return true;
            return false;
        }

        /// <summary>Coins waiting at a gold mine: 5 an hour up to 60; production pauses while the mine needs care.</summary>
        public static int MineCoins(CityPlaced p)
        {
            if (p.Piece.perk != CityPerk.Mine) return 0;
            long until = CityClock.Now;
            float days = p.Piece.CareDays;
            long broken = p.care + (long)(days * Day);
            if (until > broken) until = broken;
            float hours = Mathf.Max(0f, (until - p.mined) / (float)Hour);
            return Mathf.Min(MineCap, Mathf.FloorToInt(hours * MineRate));
        }

        public static int CollectMine(int uid)
        {
            var p = Get(uid);
            if (p == null) return 0;
            int coins = MineCoins(p);
            if (coins <= 0) return 0;
            p.mined = CityClock.Now;
            SaveData.Coins += coins;
            Save();
            return coins;
        }

        // ---------- Residents' wishes ----------

        public static IReadOnlyList<CityRequest> Requests => Data.requests;

        public static bool IsBestFriend(int resident) => Data.granted[resident] >= WishesForBestFriend;

        /// <summary>New wishes appear while there is room for one and the 4-hour wait since the last granted wish is over.</summary>
        public static void RefreshRequests(System.Random rng = null)
        {
            if (!Open) return;
            rng = rng ?? new System.Random();
            long now = CityClock.Now;
            // Wishes about buildings that are gone no longer make sense.
            Data.requests.RemoveAll(r => r.kind == CityRequestKind.Repair && Get(r.uid) == null);
            int guard = 0;
            while (Data.requests.Count < MaxRequests && now >= Data.nextRequest && guard++ < 10)
            {
                var r = MakeRequest(rng, now);
                if (r == null) break;
                Data.requests.Add(r);
                // A full board refills one wish at a time; the first ones come right away.
                if (Data.requests.Count >= MaxRequests) break;
            }
            Save();
        }

        private static CityRequest MakeRequest(System.Random rng, long now)
        {
            var free = new List<int>();
            for (int i = 0; i < Residents.Count; i++)
                if (Residents.Rescued(i) && !Data.requests.Exists(r => r.resident == i)) free.Add(i);
            if (free.Count == 0)
                for (int i = 0; i < Residents.Count; i++) if (Residents.Rescued(i)) free.Add(i);
            if (free.Count == 0) return null;
            int who = free[rng.Next(free.Count)];

            var kinds = new List<CityRequestKind> { CityRequestKind.Build, CityRequestKind.Build, CityRequestKind.MainMode };
            if (HasHome() && !Data.requests.Exists(r => r.kind == CityRequestKind.Near)) kinds.Add(CityRequestKind.Near);
            if (Data.pieces.Exists(p => NeedsAttention(p) && !Data.requests.Exists(r => r.uid == p.uid))) { kinds.Add(CityRequestKind.Repair); kinds.Add(CityRequestKind.Repair); }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                var kind = kinds[rng.Next(kinds.Count)];
                var r = new CityRequest { kind = kind, resident = who, created = now };
                switch (kind)
                {
                    case CityRequestKind.Build:
                    {
                        // Usually something in reach, sometimes a piece from a floor still ahead: a reason to go back and play.
                        var options = new List<CityPiece>();
                        bool ahead = rng.Next(100) < 35;
                        foreach (var p in CityCatalog.All)
                        {
                            if (p.role == CityRole.Producer || p.IsSpecial) continue;
                            if (Data.requests.Exists(q => q.kind == CityRequestKind.Build && q.piece == p.id)) continue;
                            bool reach = p.world <= ReachedWorld;
                            if (ahead ? !reach && p.world <= ReachedWorld + 2 : reach) options.Add(p);
                        }
                        if (options.Count == 0) continue;
                        var pick = options[rng.Next(options.Count)];
                        r.piece = pick.id;
                        r.baseline = Count(pick.id);
                        r.reward = Mathf.Clamp(60 + pick.price / 3, 60, 300);
                        return r;
                    }
                    case CityRequestKind.Near:
                    {
                        var options = new List<CityPiece>();
                        foreach (var p in CityCatalog.All)
                            if (p.category == CityCategory.Garden && p.w == 1 && p.h == 1 && Unlocked(p)) options.Add(p);
                        if (options.Count == 0) continue;
                        r.piece = options[rng.Next(options.Count)].id;
                        r.reward = 80;
                        return r;
                    }
                    case CityRequestKind.Repair:
                    {
                        var options = Data.pieces.FindAll(p => NeedsAttention(p) && !Data.requests.Exists(q => q.uid == p.uid));
                        if (options.Count == 0) continue;
                        var pick = options[rng.Next(options.Count)];
                        r.uid = pick.uid;
                        r.piece = pick.id;
                        r.reward = Mathf.Max(20, pick.Piece.BaseCareFee * 2);
                        return r;
                    }
                    default:
                    {
                        // Three-star a few more levels of a floor already reached.
                        int worlds = ReachedWorld + 1;
                        for (int k = 0; k < 4; k++)
                        {
                            int w = rng.Next(worlds);
                            int have = ThreeStars(w);
                            int open = Mathf.Min(LevelCatalog.LevelsPerWorld, LevelsOpenIn(w));
                            if (have >= open) continue;
                            r.world = w;
                            r.target = Mathf.Min(open, have + 1 + rng.Next(3));
                            r.reward = 50 * (r.target - have) + 10 * w;
                            return r;
                        }
                        continue;
                    }
                }
            }
            return null;
        }

        private static int LevelsOpenIn(int world)
        {
            if (SaveData.TestMode) return LevelCatalog.LevelsPerWorld;
            return Mathf.Clamp(SaveData.UnlockedLevel + 1 - world * LevelCatalog.LevelsPerWorld, 0, LevelCatalog.LevelsPerWorld);
        }

        public static int ThreeStars(int world)
        {
            int n = 0;
            for (int i = 0; i < LevelCatalog.LevelsPerWorld; i++)
                if (Progress.Stars(world * LevelCatalog.LevelsPerWorld + i) >= 3) n++;
            return n;
        }

        private static bool HasHome() => Data.pieces.Exists(p => p.Piece.role == CityRole.Home);

        /// <summary>How far along a wish is (0-1); 1 = ready to hand in.</summary>
        public static float RequestProgress(CityRequest r)
        {
            switch (r.kind)
            {
                case CityRequestKind.Build: return Count(r.piece) > r.baseline ? 1f : 0f;
                case CityRequestKind.Near: return HomeNextTo(r.piece) ? 1f : 0f;
                case CityRequestKind.Repair:
                    var p = Get(r.uid);
                    return p == null || (p.care >= r.created && !NeedsAttention(p)) ? 1f : 0f;
                default:
                    return Mathf.Clamp01(ThreeStars(r.world) / (float)Mathf.Max(1, r.target));
            }
        }

        public static bool RequestDone(CityRequest r) => RequestProgress(r) >= 1f;

        /// <summary>A home with this piece on a cell next to it (sides or corners).</summary>
        private static bool HomeNextTo(string id)
        {
            foreach (var home in Data.pieces)
            {
                if (home.Piece.role != CityRole.Home) continue;
                var s = home.Piece.Size(home.rot);
                foreach (var p in Data.pieces)
                {
                    if (p.id != id) continue;
                    var ps = p.Piece.Size(p.rot);
                    bool near = p.x <= home.x + s.x && p.x + ps.x >= home.x && p.y <= home.y + s.y && p.y + ps.y >= home.y;
                    if (near) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Hands in a finished wish: the coins, and the resident's count toward best friend.
        /// <paramref name="bestFriend"/> is true when this wish made it a best friend (extra reward paid).
        /// </summary>
        public static int Claim(CityRequest r, out bool bestFriend)
        {
            bestFriend = false;
            if (!Data.requests.Contains(r) || !RequestDone(r)) return 0;
            Data.requests.Remove(r);
            int coins = r.reward;
            Data.granted[r.resident]++;
            if (IsBestFriend(r.resident) && !Data.bestPaid[r.resident])
            {
                Data.bestPaid[r.resident] = true;
                bestFriend = true;
                coins += BestFriendReward;
            }
            SaveData.Coins += coins;
            Data.nextRequest = CityClock.Now + RequestCooldown;
            Save();
            return coins;
        }

        /// <summary>The words in the speech bubble.</summary>
        public static string RequestText(CityRequest r)
        {
            switch (r.kind)
            {
                case CityRequestKind.Build: return Loc.F("city.wish.build", Loc.T("city." + r.piece));
                case CityRequestKind.Near: return Loc.F("city.wish.near", Loc.T("city." + r.piece));
                case CityRequestKind.Repair: return Loc.F("city.wish.repair", Loc.T("city." + r.piece));
                default: return Loc.F("city.wish.stars", Loc.T(LevelCatalog.WorldKey(r.world)), r.target, ThreeStars(r.world));
            }
        }

        // ---------- Test helpers ----------

        /// <summary>Ages every piece by <paramref name="days"/> (test hooks only).</summary>
        public static void AgeAll(float days)
        {
            foreach (var p in Data.pieces)
            {
                p.care -= (long)(days * Day);
                p.mined -= (long)(days * Day);
            }
            Save();
        }

        public static void ResetAll()
        {
            data = new CityRecord { granted = new int[Residents.Count], bestPaid = new bool[Residents.Count] };
            undo.Clear();
            Save();
        }
    }
}
