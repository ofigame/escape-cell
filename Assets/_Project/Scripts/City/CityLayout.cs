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
        public const int Cols = 4, Rows = 4;
        /// <summary>The creek valley between the 2nd and 3rd columns, instead of a street.</summary>
        public const float Creek = 44f, CreekWater = 10.5f;
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
        public const int LotCount = 5;
        private static readonly float[] LotOffsets = { 0f, -HomeLot, HomeLot, -2f * HomeLot, 2f * HomeLot };
        public static Vector2 LotCenter(int i) => new Vector2(MinX - HomeLot / 2f, ZOf(1) + LotOffsets[i]);
        public static float LotsNorth => ZOf(1) - 2.5f * HomeLot;
        public static float LotsSouth => ZOf(1) + 2.5f * HomeLot;

        /// <summary>Which lot a plan point is on (-1: none).</summary>
        public static int LotAt(Vector2 p, float margin = 0f)
        {
            for (int i = 0; i < LotCount; i++)
            {
                var c = LotCenter(i);
                if (Mathf.Abs(p.x - c.x) <= HomeLot / 2f + margin && Mathf.Abs(p.y - c.y) <= HomeLot / 2f + margin) return i;
            }
            return -1;
        }

        public static bool OnHomeLot(Vector2 local, float margin = 0f) => LotAt(local, margin) >= 0;
        // ---------- the far district (Doğu Yakası): 2×2 blocks east of the town, at the end of a country road ----------

        public const string FarName = "Doğu Yakası";
        public const float CountryRoad = 12f, FarGap = 150f;
        public const int FarCols = 2;
        public static float FarMinX => MaxX + FarGap;
        public static float FarMaxX => FarMinX + FarCols * Block + (FarCols + 1) * Road;
        public static float FarNorth => ZOf(2) - Block / 2f - Road;
        public static float FarSouth => ZOf(1) + Block / 2f + Road;
        /// <summary>The country road runs along the town's middle east-west street.</summary>
        public static float CountryZ => ZOf(1) - Block / 2f - Road / 2f;
        public static Vector2 FarCenter => new Vector2((FarMinX + FarMaxX) / 2f, (FarNorth + FarSouth) / 2f);
        public static float FarXOf(int col) => FarMinX + Road + Block / 2f + col * Pitch;
        public static IEnumerable<float> FarStreetXs() { for (int c = 0; c <= FarCols; c++) yield return FarMinX + Road / 2f + c * Pitch; }
        public static IEnumerable<float> FarStreetZs() { yield return FarNorth + Road / 2f; yield return CountryZ; yield return FarSouth - Road / 2f; }
        public static bool InFar(Vector2 p, float m = 0f) => p.x >= FarMinX - m && p.x <= FarMaxX + m && p.y >= FarNorth - m && p.y <= FarSouth + m;
        public static bool OnCountryRoad(Vector2 p, float m = 0f) => p.x >= MaxX - 2f && p.x <= FarMinX + 2f && Mathf.Abs(p.y - CountryZ) <= CountryRoad / 2f + m;

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
                case "civic": return "Kamu binası arsası";
                case "historic": return "Tarihi yapı arsası";
                case "vehicle": return "Park yeri";
                case "tree": return "Ağaç yeri";
                case "animal": return "Hayvan alanı";
                case "statue": return "Heykel yeri";
                case "landmark": return "Eser alanı";
                case "bridge": return "Köprü yeri";
                case "bench": return "Bank yeri";
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
                case "civic": return "KAMU";
                case "historic": return "TARİHİ";
                case "vehicle": return "ARAÇ";
                case "tree": return "AĞAÇ";
                case "animal": return "HAYVAN";
                case "statue": return "HEYKEL";
                case "landmark": return "ESER";
                case "bridge": return "KÖPRÜ";
                case "bench": return "BANK";
                case "decor": return "DEKOR";
                default: return kind;
            }
        }

        /// <summary>A slot's name and short label: by its kind, or by the one thing it takes (bridge, bench).</summary>
        public static string SlotName(Slot s) => KindName(s.only == "bridge" ? "bridge" : s.only == "bank" ? "bench" : s.kind);
        public static string SlotShort(Slot s) => KindShort(s.only == "bridge" ? "bridge" : s.only == "bank" ? "bench" : s.kind);

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
            return new List<Area>
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
                // the far district: houses of any style, towers and public buildings (copies may be built there)
                Residential("Doğu Mahallesi", "", 0, 1, 1, new Color(0.6f, 0.8f, 0.52f), true),
                Plots("Doğu Merkezi", "big", 1, 1, 1, new Color(0.74f, 0.76f, 0.8f), true),
                Plots("Doğu Kamu Binaları", "civic", 0, 2, 1, new Color(0.74f, 0.8f, 0.7f), true),
                Residential("Doğu Bahçeleri", "", 1, 2, 1, new Color(0.58f, 0.78f, 0.56f), true),
            };
        }

        private static Area NewArea(string name, string style, int col, int row, int level, Color ground, bool far = false)
        {
            var a = new Area { name = name, style = style, center = new Vector2(far ? FarXOf(col) : XOf(col), ZOf(row)), level = level, ground = ground, shore = row == 0 && !far, far = far };
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
        }

        /// <summary>5 house plots on two streets and decor spots between them.</summary>
        private static Area Residential(string name, string style, int col, int row, int level, Color ground, bool far = false)
        {
            var a = NewArea(name, style, col, row, level, ground, far);
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
        private static Area Park(int col, int row, int level)
        {
            var a = NewArea("Şehir Parkı", "", col, row, level, new Color(0.5f, 0.76f, 0.44f));
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

        /// <summary>Four big plots (apartments, civic buildings, historic buildings or landmarks).</summary>
        private static Area Plots(string name, string kind, int col, int row, int level, Color ground, bool far = false)
        {
            var a = NewArea(name, "", col, row, level, ground, far);
            Add(a, kind, -14, 14, 26, 26, 0, level);
            Add(a, kind, 14, 14, 26, 26, 0, level);
            Add(a, kind, -14, -14, 26, 26, 180, level + 1);
            Add(a, kind, 14, -14, 26, 26, 180, level + 2);
            return a;
        }
    }
}
