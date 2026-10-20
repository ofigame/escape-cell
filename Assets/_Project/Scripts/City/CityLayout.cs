using System.Collections.Generic;
using UnityEngine;

namespace BrickMuseum.City
{
    /// <summary>A place in the city where one model can stand.</summary>
    public sealed class Slot
    {
        public string id, kind, style;
        public Area area;
        public Vector2 local;     // centre relative to the city centre (x, z)
        public float w, d;        // size in the slot's own frame: w across, d front-to-back
        public float yaw;         // where the model's front looks: 0 = +z (south), 90 = +x, 180 = -z, 270 = -x
        public int level;         // player level that opens it
        public bool onRoad;       // parking spaces lie on the road, everything else on the raised blocks
        public string only;       // a slot for one model only (the bridge) or one decor (benches)

        public Vector2 WorldSize => Mathf.Abs(Mathf.Repeat(yaw, 180f) - 90f) < 1f ? new Vector2(d, w) : new Vector2(w, d);
    }

    /// <summary>A block of the city: a neighbourhood with one house style, a park, the square, ...</summary>
    public sealed class Area
    {
        public string name, style;
        public Vector2 center;
        public int level;
        public Color ground;
        public bool shore;        // on the lake: its south side is the waterfront promenade
        public bool creek;        // not a block: the creek valley (its slots stand over the water)
        public bool far;          // in the far district (closed to building until it is opened)
        public bool fields;       // the village's field blocks (four fields, no plots)
        public bool farmyard;     // the village's farmyard (the barn and the coop)
        public bool mega;         // in Gökkent (closed until it is opened)
        public readonly List<Vector2[]> paths = new List<Vector2[]>();   // winding footpaths (parks), plan points
        public readonly List<Slot> slots = new List<Slot>();
    }

    /// <summary>
    /// The city plan: a lakeside town of 4×4 blocks (64 studs, with a 4-stud pavement round each) between 18-stud roads.
    /// Row 0 lies on the lake shore (yalıs, the shore park, the funfair); the town climbs north towards the forest.
    /// Cars park in the kerbside lane of the road; trees stand on the pavements. Neighbourhoods open with the player's level.
    /// </summary>
    public static class CityLayout
    {
        public const float Block = 64f, Road = 18f, Pitch = Block + Road, Pavement = 4f, ParkingLane = 4.5f, Quay = 3f;
        public const int Cols = 4, Rows = 5;     // (row 4, by the forest: the sports grounds and the north park)
        /// <summary>The creek valley between the 2nd and 3rd columns, instead of a street.</summary>
        public const float Creek = 44f, CreekWater = 17f;    // (half the water's width: most of the valley is water)
        private static readonly float[] Gaps = { Road, Road, Creek, Road, Road };    // west edge, between columns, east edge
        private static float Width { get { float w = Cols * Block; foreach (var g in Gaps) w += g; return w; } }
        public static float MinX => -Width / 2f;
        public static float MaxX => Width / 2f;
        public static float CreekX => XOf(1) + Block / 2f + Creek / 2f;

        /// <summary>Centres of the north-south streets (the creek is not one).</summary>
        public static IEnumerable<float> StreetXs()
        {
            yield return MinX + Road / 2f;
            for (int c = 0; c < Cols - 1; c++) if (Gaps[c + 1] == Road) yield return XOf(c) + Block / 2f + Road / 2f;
            yield return MaxX - Road / 2f;
        }

        /// <summary>Centres of the east-west streets (north of every row).</summary>
        public static IEnumerable<float> StreetZs() { for (int r = 0; r < Rows; r++) yield return ZOf(r) - Block / 2f - Road / 2f; }

        /// <summary>The middle crossing of the creek, where the big bridge goes.</summary>
        public static float BridgeZ => ZOf(1) - Block / 2f - Road / 2f;

        public static bool InCreek(Vector2 p) => Mathf.Abs(p.x - CreekX) < Creek / 2f && p.y < ZOf(0) + Block / 2f && p.y > MinZ;

        public static bool OnStreetZ(float z) { foreach (var sz in StreetZs()) if (Mathf.Abs(z - sz) <= Road / 2f) return true; return false; }
        public static float MinZ => ZOf(Rows - 1) - Block / 2f - Road;
        /// <summary>South edge of the quay: the lake starts here.</summary>
        public static float MaxZ => ZOf(0) + Block / 2f + Quay;
        /// <summary>The player's own lot: a 24×24 baseplate with a pavement round it, west of the city on the west road.</summary>
        public const float HomeLot = 32f;
        public const int HomePlate = 24;
        public static Vector2 HomeLotCenter => LotCenter(0);

        /// <summary>The player's lots side by side along the west road: the home in the middle, the ones for sale north and south.</summary>
        public const int TownLots = 5, VillaLots = 4, LotCount = TownLots + VillaLots;
        private static readonly float[] LotOffsets = { 0f, -HomeLot, HomeLot, -2f * HomeLot, 2f * HomeLot };
        /// <summary>The town's lots along the west road; then the villa lots, a whole block each, down Gökkent's east column.</summary>
        public static Vector2 LotCenter(int i) => i < TownLots ? new Vector2(MinX - HomeLot / 2f, ZOf(1) + LotOffsets[i]) : new Vector2(MegaXOf(5), ZOf(i - TownLots));
        public static bool IsVilla(int i) => i >= TownLots;
        /// <summary>A lot's size (kerb to kerb) and its baseplate (studs).</summary>
        public static float LotSize(int i) => IsVilla(i) ? Block : HomeLot;
        public static int PlateOf(int i) => IsVilla(i) ? 52 : HomePlate;
        /// <summary>Which way the house on a lot faces: the town's lots look east at their road, the villas west at theirs.</summary>
        public static float LotFacing(int i) => IsVilla(i) ? 270f : 90f;
        /// <summary>Where the walk starts in front of a lot (on its road), and which way the walker looks.</summary>
        public static Vector2 LotFront(int i) => IsVilla(i) ? LotCenter(i) + new Vector2(-Block / 2f - Road / 2f, 3f) : new Vector2(MinX + 5f, LotCenter(i).y + 3f);
        public static float LotsNorth => ZOf(1) - 2.5f * HomeLot;
        public static float LotsSouth => ZOf(1) + 2.5f * HomeLot;

        /// <summary>Which lot a plan point is on (-1: none).</summary>
        public static int LotAt(Vector2 p, float margin = 0f)
        {
            for (int i = 0; i < LotCount; i++)
            {
                var c = LotCenter(i);
                float h = LotSize(i) / 2f + margin;
                if (Mathf.Abs(p.x - c.x) <= h && Mathf.Abs(p.y - c.y) <= h) return i;
            }
            return -1;
        }

        public static bool OnHomeLot(Vector2 local, float margin = 0f) => LotAt(local, margin) >= 0;
        // ---------- the village (Orman Köyü): 2×2 blocks far north in the forest, at the end of a country road ----------

        public const string FarName = "Orman Köyü";
        public const float CountryRoad = 12f, FarGap = 220f;
        public const int FarCols = 2;
        /// <summary>The country road runs north out of the town along the street between its 3rd and 4th columns.</summary>
        public static float CountryX => XOf(2) + Block / 2f + Road / 2f;
        public static float FarMinX => CountryX - (FarCols * Block + (FarCols + 1) * Road) / 2f;
        public static float FarMaxX => FarMinX + FarCols * Block + (FarCols + 1) * Road;
        public static float FarSouth => MinZ - FarGap;
        public static float FarNorth => FarSouth - (2 * Block + 3 * Road);
        public static Vector2 FarCenter => new Vector2((FarMinX + FarMaxX) / 2f, (FarNorth + FarSouth) / 2f);
        public static float FarXOf(int col) => FarMinX + Road + Block / 2f + col * Pitch;
        /// <summary>Row 0 is the village's south row (by the country road), row 1 the north one.</summary>
        public static float FarZOf(int row) => FarSouth - Road - Block / 2f - row * Pitch;
        public static IEnumerable<float> FarStreetXs() { for (int c = 0; c <= FarCols; c++) yield return FarMinX + Road / 2f + c * Pitch; }
        public static IEnumerable<float> FarStreetZs() { for (int r = 0; r <= 2; r++) yield return FarSouth - Road / 2f - r * Pitch; }
        public static bool InFar(Vector2 p, float m = 0f) => p.x >= FarMinX - m && p.x <= FarMaxX + m && p.y >= FarNorth - m && p.y <= FarSouth + m;
        public static bool OnCountryRoad(Vector2 p, float m = 0f) => Mathf.Abs(p.x - CountryX) <= CountryRoad / 2f + m && p.y <= MinZ + 2f && p.y >= FarSouth - 2f;

        // ---------- Gökkent: 6×5 blocks west of the town, at the end of the highway past the lots ----------

        public const float HighwayGap = 160f, Highway = 18f;
        public const int MegaCols = 6, MegaRows = 5;
        /// <summary>The highway runs west out of the town along its street north of the lots.</summary>
        public static float HighwayZ => ZOf(2) - Block / 2f - Road / 2f;
        public static float MegaMaxX => MinX - HomeLot - HighwayGap;
        public static float MegaMinX => MegaMaxX - (MegaCols * Block + (MegaCols + 1) * Road);
        /// <summary>Gökkent's rows lie one row north of the town's (clear of the lake's west end).</summary>
        public static float MegaZOf(int row) => ZOf(row + 1);
        public static float MegaNorth => MegaZOf(MegaRows - 1) - Block / 2f - Road;
        public static float MegaSouth => MegaZOf(0) + Block / 2f + Road;
        public static float MegaXOf(int col) => MegaMinX + Road + Block / 2f + col * Pitch;
        public static Vector2 MegaCenter => new Vector2((MegaMinX + MegaMaxX) / 2f, (MegaNorth + MegaSouth) / 2f);
        public static IEnumerable<float> MegaStreetXs() { for (int c = 0; c <= MegaCols; c++) yield return MegaMinX + Road / 2f + c * Pitch; }
        public static IEnumerable<float> MegaStreetZs() { for (int r = 0; r <= MegaRows; r++) yield return MegaZOf(r) + Block / 2f + Road / 2f; }
        public static bool InMega(Vector2 p, float m = 0f) => p.x >= MegaMinX - m && p.x <= MegaMaxX + m && p.y >= MegaNorth - m && p.y <= MegaSouth + m;
        public static bool OnHighway(Vector2 p, float m = 0f) => p.x >= MegaMaxX - 2f && p.x <= MinX + 2f && Mathf.Abs(p.y - HighwayZ) <= Highway / 2f + m;

        public static float XOf(int col)
        {
            float x = MinX + Gaps[0] + Block / 2f;
            for (int c = 0; c < col; c++) x += Block + Gaps[c + 1];
            return x;
        }
        public static float ZOf(int row) => (1.5f - row) * Pitch;
        private static List<Area> areas;

        public static string KindName(string kind)
        {
            switch (kind)
            {
                case "house": return "Ev arsası";
                case "big": return "Büyük arsa";
                case "hotel": return "Otel arsası";
                case "golf": return "Golf sahası arsası";
                case "sport": return "Spor sahası arsası";
                case "civic": return "Kamu binası arsası";
                case "historic": return "Tarihi yapı arsası";
                case "vehicle": return "Park yeri";
                case "tree": return "Ağaç yeri";
                case "animal": return "Hayvan alanı";
                case "statue": return "Heykel yeri";
                case "landmark": return "Eser alanı";
                case "bridge": return "Köprü yeri";
                case "bench": return "Bank yeri";
                case "lamp": return "Lamba yeri";
                case "bin": return "Çöp konteyneri yeri";
                case "decor": return "Dekor yeri";
                default: return kind;
            }
        }

        /// <summary>Short label printed on the slot itself.</summary>
        public static string KindShort(string kind)
        {
            switch (kind)
            {
                case "house": return "EV";
                case "big": return "BİNA";
                case "hotel": return "OTEL";
                case "golf": return "GOLF";
                case "sport": return "SPOR";
                case "civic": return "KAMU";
                case "historic": return "TARİHİ";
                case "vehicle": return "ARAÇ";
                case "tree": return "AĞAÇ";
                case "animal": return "HAYVAN";
                case "statue": return "HEYKEL";
                case "landmark": return "ESER";
                case "bridge": return "KÖPRÜ";
                case "bench": return "BANK";
                case "lamp": return "LAMBA";
                case "bin": return "ÇÖP";
                case "decor": return "DEKOR";
                default: return kind;
            }
        }

        /// <summary>A slot's name and short label: by its kind, or by the one thing it takes (bridge, bench).</summary>
        private static string SlotKind(Slot s) => s.only == "bridge" ? "bridge" : s.only == "bank" ? "bench" : s.only == "lamba" ? "lamp" : s.only == "konteyner" ? "bin" : s.kind;
        public static string SlotName(Slot s) => KindName(SlotKind(s));
        public static string SlotShort(Slot s) => KindShort(SlotKind(s));

        public static string StyleName(string style)
        {
            switch (style)
            {
                case "tr": return "Türk";
                case "en": return "İngiliz";
                case "us": return "Amerikan";
                case "eu": return "Avrupa";
                case "as": return "Asya";
                case "yali": return "Yalı";
                default: return "";
            }
        }

        public static List<Area> Areas => areas ??= Build();

        public static IEnumerable<Slot> Slots { get { foreach (var a in Areas) foreach (var s in a.slots) yield return s; } }

        public static Slot Find(string id) { foreach (var s in Slots) if (s.id == id) return s; return null; }

        public static Area AreaNamed(string name) { foreach (var a in Areas) if (a.name == name) return a; return null; }

        private static List<Area> Build()
        {
            var green = new Color(0.6f, 0.78f, 0.5f);
            var list = new List<Area>
            {
                Residential("Türk Mahallesi", "tr", 1, 1, 1, green),
                ShorePark(1, 0, 2),
                Park(2, 1, 2),
                CreekValley(5),
                Yalis("Yalı Boyu", 2, 0, 3),
                Residential("İngiliz Mahallesi", "en", 0, 1, 3, new Color(0.56f, 0.74f, 0.52f)),
                Plots("Merkez", "big", 1, 2, 4, new Color(0.78f, 0.78f, 0.76f)),
                Plots("Kamu Binaları", "civic", 2, 2, 4, new Color(0.74f, 0.8f, 0.7f)),
                Residential("Amerikan Mahallesi", "us", 3, 1, 5, new Color(0.66f, 0.8f, 0.48f)),
                Funfair(3, 0, 5),
                Plots("Tarihi Çarşı", "historic", 0, 3, 6, new Color(0.82f, 0.76f, 0.62f)),
                Plots("Eserler Bahçesi", "landmark", 1, 3, 6, new Color(0.56f, 0.74f, 0.5f)),
                Residential("Avrupa Mahallesi", "eu", 0, 2, 7, new Color(0.6f, 0.76f, 0.56f)),
                Yalis("Yalı Boyu II", 0, 0, 7),
                Plots("Eserler Bahçesi II", "landmark", 2, 3, 8, new Color(0.56f, 0.74f, 0.5f)),
                Plots("Gökdelenler", "big", 3, 3, 8, new Color(0.74f, 0.76f, 0.8f)),
                Residential("Asya Mahallesi", "as", 3, 2, 9, new Color(0.58f, 0.78f, 0.6f)),
                // the northern row by the forest: sports grounds and a second park
                SportsGround("Spor Alanı", 0, 4, 4),
                Courts("Spor Kompleksi", 1, 4, 5),
                Park(2, 4, 6, "Kuzey Parkı"),
                SportsGround("Stadyum Alanı", 3, 4, 7),
                // the far district: houses of any style, towers and public buildings (copies may be built there)
                Residential("Köy Evleri", "", 0, 0, 1, new Color(0.6f, 0.8f, 0.52f), true),
                Farm("Tarlalar", 1, 0, false),
                Farm("Çiftlik", 0, 1, true),
                Farm("Tarlalar II", 1, 1, false),
            };
            list.AddRange(MegaAreas());
            return list;
        }

        /// <summary>Gökkent: hotel boulevards, golf clubs, towers (narrow and wide plots), parks, homes (the east column is left for villa lots).</summary>
        private static IEnumerable<Area> MegaAreas()
        {
            // rows 0 (south) → 4 (north), columns 0 → 4 (column 5 is for the villas)
            string[,] plan =
            {
                { "hotel", "golf", "hotel", "golf", "hotel" },
                { "tower", "wide", "park", "tower", "wide" },
                { "tower", "hotel", "tower", "hotel", "tower" },
                { "golf", "wide", "homes", "tower", "golf" },
                { "tower", "park", "tower", "wide", "hotel" },
            };
            var count = new Dictionary<string, int>();
            for (int r = 0; r < MegaRows; r++)
                for (int c = 0; c < 5; c++)
                {
                    string k = plan[r, c];
                    count[k] = count.TryGetValue(k, out int n) ? n + 1 : 1;
                    string num = count[k] == 1 ? "" : " " + new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[count[k]];
                    switch (k)
                    {
                        case "hotel": yield return MegaPair("Otel Bulvarı" + num, "hotel", c, r, new Color(0.78f, 0.82f, 0.86f)); break;
                        case "golf": yield return Golf("Golf Kulübü" + num, c, r); break;
                        case "tower": yield return Plots("Kuleler" + num, "big", c, r, 1, new Color(0.74f, 0.76f, 0.82f), false, true); break;
                        case "wide": yield return MegaPair("Kule Meydanı" + num, "big", c, r, new Color(0.72f, 0.74f, 0.8f)); break;
                        case "park": yield return Park(c, r, 1, "Gökkent Parkı" + num, true); break;
                        default: yield return Residential("Gökkent Evleri" + num, "", c, r, 1, new Color(0.6f, 0.78f, 0.56f), false, true); break;
                    }
                }
        }

        /// <summary>Two wide plots (hotels, twin towers) facing the streets north and south.</summary>
        private static Area MegaPair(string name, string kind, int col, int row, Color ground)
        {
            var a = NewArea(name, "", col, row, 1, ground, false, true);
            Add(a, kind, 0, 14, 56, 27, 0, 1);
            Add(a, kind, 0, -14, 56, 27, 180, 1);
            return a;
        }

        /// <summary>A golf course filling the block.</summary>
        private static Area Golf(string name, int col, int row)
        {
            var a = NewArea(name, "", col, row, 1, new Color(0.46f, 0.72f, 0.4f), false, true);
            Add(a, "golf", 0, 0, 58, 58, 0, 1);
            a.slots.RemoveAll(s => s.kind == "tree");   // (the course has its own trees)
            return a;
        }

        private static Area NewArea(string name, string style, int col, int row, int level, Color ground, bool far = false, bool mega = false)
        {
            var a = new Area { name = name, style = style, center = new Vector2(mega ? MegaXOf(col) : far ? FarXOf(col) : XOf(col), mega ? MegaZOf(row) : far ? FarZOf(row) : ZOf(row)), level = level, ground = ground, shore = row == 0 && !far && !mega, far = far, mega = mega };
            Street(a);
            return a;
        }

        private static Slot Add(Area a, string kind, float x, float z, float w, float d, float yaw, int level, string style = null)
        {
            var s = new Slot
            {
                id = $"{a.name}/{kind}/{a.slots.Count}", kind = kind, style = style ?? (kind == "house" ? a.style : ""), area = a,
                local = a.center + new Vector2(x, z), w = w, d = d, yaw = yaw, level = Mathf.Max(level, a.level),
            };
            a.slots.Add(s);
            return s;
        }

        /// <summary>Every block: one kerbside parking space in its street and two trees on its front pavement.</summary>
        private static void Street(Area a)
        {
            float half = Block / 2f;
            // shore blocks park on their north street, the others on their south street
            float lane = a.shore ? -(half + ParkingLane / 2f) : half + ParkingLane / 2f;
            Add(a, "vehicle", 0, lane, 12, ParkingLane, a.shore ? 180 : 0, a.level).onRoad = true;
            float walk = a.shore ? half - Pavement / 2f : half - Pavement / 2f;
            Add(a, "tree", -16, walk, 3, 3, 0, a.level);
            Add(a, "tree", 16, walk, 3, 3, 0, a.level + 1);
            // street furniture on the pavement: two street lamps and a rubbish bin
            foreach (float x in new[] { -27f, 27f }) Add(a, "decor", x, walk, 3, 3, 0, a.level).only = "lamba";
            Add(a, "decor", -6f, walk, 3.6f, 3f, 0, a.level).only = "konteyner";
        }

        /// <summary>5 house plots on two streets and decor spots between them.</summary>
        private static Area Residential(string name, string style, int col, int row, int level, Color ground, bool far = false, bool mega = false)
        {
            var a = NewArea(name, style, col, row, level, ground, far, mega);
            foreach (float x in new[] { -19f, 0f, 19f }) Add(a, "house", x, 18, 17, 17, 0, level);
            foreach (float x in new[] { -10f, 10f }) Add(a, "house", x, -18, 17, 17, 180, level + 1);
            Add(a, "decor", 0, 0, 8, 8, 0, level + 1);
            Add(a, "decor", -24, -18, 6, 6, 0, level + 2);
            Add(a, "decor", 24, -18, 6, 6, 0, level + 2);
            return a;
        }

        /// <summary>Yalıs on the water: three plots facing the lake, a garden behind.</summary>
        private static Area Yalis(string name, int col, int row, int level)
        {
            var a = NewArea(name, "yali", col, row, level, new Color(0.56f, 0.76f, 0.5f));
            foreach (float x in new[] { -19f, 0f, 19f }) Add(a, "house", x, 17, 18, 19, 0, level, "yali");
            Add(a, "decor", -12, -16, 8, 8, 0, level + 1);
            Add(a, "decor", 12, -16, 8, 8, 0, level + 1);
            Add(a, "tree", -24, -6, 6, 6, 0, level);
            Add(a, "tree", 24, -6, 6, 6, 0, level);
            return a;
        }

        /// <summary>The shore park: animal enclosures, a fountain spot, trees.</summary>
        private static Area ShorePark(int col, int row, int level)
        {
            var a = NewArea("Sahil Parkı", "", col, row, level, new Color(0.5f, 0.76f, 0.44f));
            Add(a, "animal", -14, -13, 18, 16, 0, 3);
            Add(a, "animal", 14, -13, 18, 16, 0, 3);
            Add(a, "animal", 14, 14, 18, 16, 0, 4);
            Add(a, "decor", -14, 14, 12, 12, 0, level);
            foreach (var (x, z) in new[] { (0f, -2f), (-26f, 2f), (26f, 2f) }) Add(a, "tree", x, z, 6, 6, 0, level);
            return a;
        }

        /// <summary>
        /// The city park by the creek: winding footpaths round a statue, benches along them, trees, a fountain spot.
        /// </summary>
        private static Area Park(int col, int row, int level, string name = "Şehir Parkı", bool mega = false)
        {
            var a = NewArea(name, "", col, row, level, new Color(0.5f, 0.76f, 0.44f), false, mega);
            // the paths: a wobbly ring round the middle and four wandering branches out to the pavement
            var ring = new List<Vector2>();
            for (int k = 0; k <= 48; k++)
            {
                float t = k / 48f * Mathf.PI * 2f, r = 15f + 2.2f * Mathf.Sin(3f * t + 0.6f) + 1.2f * Mathf.Sin(5f * t);
                ring.Add(a.center + new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r));
            }
            a.paths.Add(ring.ToArray());
            foreach (var (ang, bend) in new[] { (0.35f, 5f), (1.9f, -6f), (3.5f, 4f), (5.0f, -5f) })
            {
                var arm = new List<Vector2>();
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var side = new Vector2(-dir.y, dir.x);
                for (int k = 0; k <= 12; k++)
                {
                    float t = k / 12f, along = Mathf.Lerp(15f, 31f, t);
                    arm.Add(a.center + dir * along + side * (bend * Mathf.Sin(t * Mathf.PI)));
                }
                a.paths.Add(arm.ToArray());
            }
            Add(a, "statue", 0, 0, 14, 14, 0, 4);
            // benches beside the ring, facing the middle
            for (int k = 0; k < 6; k++)
            {
                float t = (k + 0.5f) / 6f * Mathf.PI * 2f, r = 15f + 2.2f * Mathf.Sin(3f * t + 0.6f) + 1.2f * Mathf.Sin(5f * t) + 2.6f;
                var bench = Add(a, "decor", Mathf.Cos(t) * r, Mathf.Sin(t) * r, 4, 3, 90f - t * Mathf.Rad2Deg + 180f, level + (k < 3 ? 0 : 1));
                bench.only = "bank";
            }
            foreach (var (x, z) in new[] { (-23f, -21f), (22f, 21f), (-24f, 15f), (23f, -14f), (6f, -25f), (-6f, 25f) }) Add(a, "tree", x, z, 6, 6, 0, level);
            Add(a, "decor", 22f, 2f, 8, 8, 0, level + 1);
            Add(a, "decor", -22f, -2f, 8, 8, 0, level + 1);
            return a;
        }

        /// <summary>The creek valley: no block, only the big bridge's place over the middle crossing.</summary>
        private static Area CreekValley(int level)
        {
            var a = new Area { name = "Dere", style = "", center = new Vector2(CreekX, 0f), level = level, ground = Color.clear, creek = true };
            var s = Add(a, "landmark", 0f, BridgeZ, 84f, 21f, 0f, level);
            s.only = "bridge";
            return a;
        }

        /// <summary>The funfair on the shore: room for a ferris wheel and a windmill, small stalls.</summary>
        private static Area Funfair(int col, int row, int level)
        {
            var a = NewArea("Lunapark", "", col, row, level, new Color(0.62f, 0.8f, 0.6f));
            Add(a, "decor", -12, -12, 28, 28, 0, level);
            Add(a, "decor", 16, -14, 16, 16, 0, level + 1);
            foreach (float x in new[] { -18f, 0f, 18f }) Add(a, "decor", x, 18, 8, 8, 0, level + 1);
            return a;
        }

        /// <summary>A football pitch (or any sports ground) filling the block, benches for the crowd behind it.</summary>
        private static Area SportsGround(string name, int col, int row, int level)
        {
            var a = NewArea(name, "", col, row, level, new Color(0.46f, 0.72f, 0.4f));
            Add(a, "sport", 0, 6, 56, 38, 0, level);
            foreach (float x in new[] { -18f, 0f, 18f }) Add(a, "decor", x, -21, 4, 3, 0, level + 1).only = "bank";
            Add(a, "tree", -25, -21, 4, 4, 0, level);
            Add(a, "tree", 25, -21, 4, 4, 0, level);
            return a;
        }

        /// <summary>Four courts (basketball, tennis, volleyball...).</summary>
        private static Area Courts(string name, int col, int row, int level)
        {
            var a = NewArea(name, "", col, row, level, new Color(0.5f, 0.74f, 0.46f));
            Add(a, "sport", -14, 14, 26, 26, 0, level);
            Add(a, "sport", 14, 14, 26, 26, 0, level);
            Add(a, "sport", -14, -14, 26, 26, 180, level + 1);
            Add(a, "sport", 14, -14, 26, 26, 180, level + 1);
            return a;
        }

        /// <summary>A block of the village: four fields, or the farmyard with the barn and the coop (no plots).</summary>
        private static Area Farm(string name, int col, int row, bool yard)
        {
            var a = NewArea(name, "", col, row, 1, yard ? new Color(0.6f, 0.76f, 0.44f) : new Color(0.62f, 0.72f, 0.42f), true);
            a.fields = !yard;
            a.farmyard = yard;
            a.slots.RemoveAll(s => s.kind == "vehicle");   // (a tractor lane would be nice; no parking here)
            return a;
        }

        /// <summary>Four big plots (apartments, civic buildings, historic buildings or landmarks).</summary>
        private static Area Plots(string name, string kind, int col, int row, int level, Color ground, bool far = false, bool mega = false)
        {
            var a = NewArea(name, "", col, row, level, ground, far, mega);
            Add(a, kind, -14, 14, 26, 26, 0, level);
            Add(a, kind, 14, 14, 26, 26, 0, level);
            Add(a, kind, -14, -14, 26, 26, 180, level + 1);
            Add(a, kind, 14, -14, 26, 26, 180, level + 2);
            return a;
        }
    }
}
