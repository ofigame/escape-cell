using System.Collections.Generic;

namespace BrickMuseum.Data
{
    /// <summary>
    /// One piece of a model. Units: 1 = one stud across, heights in plates (a brick is 3 plates).
    /// x/z = footprint min corner, y = bottom. w/d = canonical size (slopes descend along w), f = facing:
    /// E = +x, N = -z, W = -x, S = +z (front). Discs and stickers stand on a wall and face outward.
    /// </summary>
    public sealed class Piece
    {
        public const float Plate = 0.4f;
        public const float Gap = 0.04f;
        private static readonly string[] Dirs = { "E", "N", "W", "S" };

        public string t, c, f, art;
        public int x, z, y, w, d, h, need = -1;
        public int steps, fw, fd;
        public string key;
        public int longSide;

        private static readonly HashSet<string> Square = new HashSet<string> { "round", "cone", "dome", "spire" };
        private static readonly HashSet<string> Symmetric = new HashSet<string> { "brick", "tile", "round", "cone", "dome", "spire", "wheel" };

        public static Piece Make(string t, string c, int x, int z, int y, int w = 0, int d = 0, int h = 0, string f = null, string art = null, int need = -1)
        {
            var p = new Piece { t = t, c = string.IsNullOrEmpty(c) ? "white" : c, x = x, z = z, y = y, art = art ?? "", need = need };
            Defaults(t, out int dw, out int dd, out int dh);
            p.w = w > 0 ? w : dw;
            p.d = d > 0 ? d : dd;
            p.h = h > 0 ? h : dh;
            if (Square.Contains(t)) p.d = p.w;
            if (t == "disc") p.h = (int)System.Math.Round(p.w / Plate);
            if (t == "sticker") p.h = p.art == "clock" ? (int)System.Math.Round(p.w / Plate) : 3;
            p.f = string.IsNullOrEmpty(f) ? "E" : f;
            string canon = t == "disc" || t == "sticker" ? "S" : "E";
            p.steps = (System.Array.IndexOf(Dirs, p.f) - System.Array.IndexOf(Dirs, canon) + 4) % 4;
            p.fw = p.steps % 2 == 1 ? p.d : p.w;
            p.fd = p.steps % 2 == 1 ? p.w : p.d;
            bool sym = Symmetric.Contains(t);
            int a = sym ? System.Math.Min(p.w, p.d) : p.w, b = sym ? System.Math.Max(p.w, p.d) : p.d;
            p.key = $"{t}:{a}x{b}x{p.h}:{p.c}:{p.art}";
            p.longSide = System.Math.Max(p.w, p.d);
            return p;
        }

        private static void Defaults(string t, out int w, out int d, out int h)
        {
            switch (t)
            {
                case "tile": w = 1; d = 1; h = 1; break;
                case "dome": w = 1; d = 1; h = 2; break;
                case "spire": w = 2; d = 2; h = 4; break;
                case "arch": w = 4; d = 1; h = 3; break;
                case "disc": case "sticker": w = 2; d = 1; h = 3; break;
                case "wheel": w = 2; d = 1; h = 3; break;
                default: w = 1; d = 1; h = 3; break;
            }
        }

        public bool IsSymmetric => Symmetric.Contains(t);
        public bool OnWall => t == "disc" || t == "sticker";
        public bool IsClock => t == "sticker" && art == "clock";

        /// <summary>The bag's copy of this piece: canonical size and orientation (long side along x for symmetric kinds).</summary>
        public Piece Kind()
        {
            int w2 = IsSymmetric ? System.Math.Max(w, d) : w, d2 = IsSymmetric ? System.Math.Min(w, d) : d;
            return Make(t, c, 0, 0, 0, w2, d2, h, null, art);
        }

        /// <summary>Height above the piece's bottom used to aim at it (middle of a brick, centre of a round plate).</summary>
        public float CenterY => t == "disc" || IsClock ? w / 2f : t == "sticker" ? 0.6f : h * Plate / 2f;

        /// <summary>Top of the piece in world units above its bottom.</summary>
        public float TopY => OnWall ? CenterY * 2f : h * Plate;

        public string Label()
        {
            if (t == "wheel") return "tekerlek";
            if (t == "sticker") return Stickers.Turkish(art) + " etiketi";
            string name = t == "brick" && h == 1 ? "plaka" : TypeName(t);
            string size = Square.Contains(t) || t == "disc" ? $"{w}x{w}" : $"{System.Math.Min(w, d)}x{System.Math.Max(w, d)}";
            return $"{Palette.Turkish(c)} {size} {name}";
        }

        private static string TypeName(string t)
        {
            switch (t)
            {
                case "brick": return "tuğla";
                case "tile": return "düz plaka";
                case "slope": return "eğimli parça";
                case "curve": return "kavisli parça";
                case "round": return "yuvarlak tuğla";
                case "cone": return "koni";
                case "dome": return "kubbe";
                case "spire": return "kule ucu";
                case "arch": return "kemer";
                case "disc": return "yuvarlak plaka";
                default: return t;
            }
        }
    }

    public static class Stickers
    {
        /// <summary>Height / width of each sticker picture.</summary>
        public static float Aspect(string art)
        {
            switch (art)
            {
                case "taxi": return 0.5f;
                case "plate": return 0.3f;
                case "bus": return 0.35f;
                default: return 1f;
            }
        }

        public static string Turkish(string art)
        {
            switch (art)
            {
                case "clock": return "saat";
                case "eye": return "göz";
                case "taxi": return "TAKSİ";
                case "plate": return "plaka";
                case "bus": return "otobüs tabelası";
                case "face": return "yüz";
                default: return art;
            }
        }
    }
}
