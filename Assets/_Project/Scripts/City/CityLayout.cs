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
        public static float MinX => -1.5f * Pitch - Block / 2f - Road;
        public static float MaxX => -MinX;
        public static float MinZ => ZOf(Rows - 1) - Block / 2f - Road;
        /// <summary>South edge of the quay: the lake starts here.</summary>
        public static float MaxZ => ZOf(0) + Block / 2f + Quay;
        /// <summary>The player's own lot: a 24×24 baseplate with a pavement round it, west of the city on the west road.</summary>
        public const float HomeLot = 32f;
        public const int HomePlate = 24;
        public static Vector2 HomeLotCenter => new Vector2(MinX - HomeLot / 2f, ZOf(1));
        public static bool OnHomeLot(Vector2 local, float margin = 0f) =>
            Mathf.Abs(local.x - HomeLotCenter.x) <= HomeLot / 2f + margin && Mathf.Abs(local.y - HomeLotCenter.y) <= HomeLot / 2f + margin;
        public static float XOf(int col) => (col - 1.5f) * Pitch;
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
                case "decor": return "DEKOR";
                default: return kind;
            }
        }

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
                Square(2, 1, 2),
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
            };
        }

        private static Area NewArea(string name, string style, int col, int row, int level, Color ground)
        {
            var a = new Area { name = name, style = style, center = new Vector2(XOf(col), ZOf(row)), level = level, ground = ground, shore = row == 0 };
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
        private static Area Residential(string name, string style, int col, int row, int level, Color ground)
        {
            var a = NewArea(name, style, col, row, level, ground);
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

        /// <summary>The square: a statue in the middle, decor round it, trees.</summary>
        private static Area Square(int col, int row, int level)
        {
            var a = NewArea("Meydan", "", col, row, level, new Color(0.84f, 0.8f, 0.72f));
            Add(a, "statue", 0, 0, 16, 16, 0, 4);
            foreach (var (x, z, l) in new[] { (-20f, 20f, 2), (20f, 20f, 2), (-20f, -20f, 3), (20f, -20f, 3) }) Add(a, "decor", x, z, 8, 8, 0, l);
            foreach (var (x, z) in new[] { (0f, 22f), (0f, -22f), (-22f, 0f), (22f, 0f) }) Add(a, "tree", x, z, 6, 6, 0, level);
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
        private static Area Plots(string name, string kind, int col, int row, int level, Color ground)
        {
            var a = NewArea(name, "", col, row, level, ground);
            Add(a, kind, -14, 14, 26, 26, 0, level);
            Add(a, kind, 14, 14, 26, 26, 0, level);
            Add(a, kind, -14, -14, 26, 26, 180, level + 1);
            Add(a, kind, 14, -14, 26, 26, 180, level + 2);
            return a;
        }
    }
}
