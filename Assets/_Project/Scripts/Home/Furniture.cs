using System.Collections.Generic;
using BrickMuseum.Data;

namespace BrickMuseum.Home
{
    /// <summary>One piece of furniture for the player's home, built from our bricks; new ones open with the player's level.</summary>
    public sealed class FurnitureDef
    {
        public string id, name;
        public int level;
        public ModelDef model;
    }

    /// <summary>
    /// The furniture catalogue. Each item is a little brick model (front = +z, units = studs and plates as in the
    /// models); in the house it stands at half size (<c>Game.FurnitureScale</c>) so a sofa seats a person.
    /// </summary>
    public static class Furniture
    {
        private static List<FurnitureDef> all;

        public static List<FurnitureDef> All => all ??= Build();

        public static FurnitureDef Find(string id) { foreach (var f in All) if (f.id == id) return f; return null; }

        private static List<Piece> ps;
        private static void P(string t, string c, int x, int z, int y, int w = 0, int d = 0, int h = 0, string f = null) => ps.Add(Piece.Make(t, c, x, z, y, w, d, h, f));

        private static void Add(List<FurnitureDef> list, string id, string name, int level, System.Action build)
        {
            ps = new List<Piece>();
            build();
            list.Add(new FurnitureDef { id = id, name = name, level = level, model = ModelCatalog.FromPieces("f_" + id, name, "furniture", ps) });
        }

        private static List<FurnitureDef> Build()
        {
            var l = new List<FurnitureDef>();
            Add(l, "kanepe", "Kanepe", 1, () =>
            {
                P("brick", "blue", 0, 0, 0, 6, 3, 2);
                P("brick", "blue", 0, 0, 2, 6, 1, 4);                      // back
                P("brick", "blue", 0, 1, 2, 1, 2, 2); P("brick", "blue", 5, 1, 2, 1, 2, 2);   // arms
                P("tile", "lblue", 1, 1, 2, 2, 2, 1); P("tile", "lblue", 3, 1, 2, 2, 2, 1);   // cushions
            });
            Add(l, "masa", "Yemek Masası", 1, () =>
            {
                foreach (var (x, z) in new[] { (0, 0), (5, 0), (0, 3), (5, 3) }) P("round", "brown", x, z, 0, 1, 1, 5);
                P("brick", "brown", 0, 0, 5, 6, 4, 1);
            });
            Add(l, "sandalye", "Sandalye", 1, () =>
            {
                foreach (var (x, z) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) }) P("round", "brown", x, z, 0, 1, 1, 3);
                P("brick", "brown", 0, 0, 3, 2, 2, 1);
                P("brick", "brown", 0, 0, 4, 2, 1, 5);
            });
            Add(l, "yatak", "Yatak", 1, () =>
            {
                P("brick", "brown", 0, 0, 0, 4, 7, 2);
                P("brick", "brown", 0, 0, 2, 4, 1, 5);                     // headboard
                P("tile", "white", 0, 1, 2, 4, 2, 1);                       // pillows
                P("tile", "red", 0, 3, 2, 4, 4, 1);                         // blanket
            });
            Add(l, "lamba", "Ayaklı Lamba", 1, () =>
            {
                P("round", "dgray", 0, 0, 0, 1, 1, 1);
                P("round", "gray", 0, 0, 1, 1, 1, 9);
                P("dome", "yellow", 0, 0, 10, 1, 1, 2);
            });
            Add(l, "saksi", "Saksı Çiçeği", 1, () =>
            {
                P("brick", "terra", 0, 0, 0, 2, 2, 3);
                P("dome", "dgreen", 0, 0, 3, 2, 2, 2);
                P("cone", "green", 0, 0, 5, 2, 2, 3);
            });
            Add(l, "hali", "Halı", 1, () =>
            {
                P("tile", "dred", 0, 0, 0, 8, 6, 1);
                P("tile", "ochre", 1, 1, 1, 6, 4, 1);
                P("tile", "dred", 3, 2, 2, 2, 2, 1);
            });
            Add(l, "tv", "Televizyon", 2, () =>
            {
                P("brick", "brown", 0, 0, 0, 6, 2, 3);
                P("brick", "black", 1, 0, 3, 4, 1, 6);
                P("tile", "dgray", 2, 1, 3, 2, 1, 1);
            });
            Add(l, "koltuk", "Koltuk", 2, () =>
            {
                P("brick", "rose", 0, 0, 0, 3, 3, 2);
                P("brick", "rose", 0, 0, 2, 3, 1, 4);
                P("brick", "rose", 0, 1, 2, 1, 2, 2); P("brick", "rose", 2, 1, 2, 1, 2, 2);
            });
            Add(l, "calisma", "Çalışma Masası", 2, () =>
            {
                foreach (var (x, z) in new[] { (0, 0), (4, 0), (0, 1), (4, 1) }) P("round", "white", x, z, 0, 1, 1, 5);
                P("brick", "white", 0, 0, 5, 5, 2, 1);
                P("brick", "black", 1, 0, 6, 3, 1, 4);                     // screen
                P("tile", "gray", 1, 1, 6, 3, 1, 1);                        // keyboard
            });
            Add(l, "mutfak", "Mutfak Tezgâhı", 3, () =>
            {
                P("brick", "cream", 0, 0, 0, 6, 2, 5);
                P("tile", "gray", 0, 0, 5, 4, 2, 1);
                P("tile", "black", 4, 0, 5, 2, 2, 1);                       // the hob
                P("tile", "lblue", 1, 0, 6, 2, 1, 1);                       // the sink
            });
            Add(l, "buzdolabi", "Buzdolabı", 3, () =>
            {
                P("brick", "white", 0, 0, 0, 3, 2, 8);
                P("brick", "white", 0, 0, 8, 3, 2, 4);
                P("tile", "gray", 2, 2, 5, 1, 1, 2);                        // handle
            });
            Add(l, "dolap", "Gardırop", 3, () =>
            {
                P("brick", "brown", 0, 0, 0, 2, 2, 12);
                P("brick", "brown", 2, 0, 0, 2, 2, 12);
                P("tile", "gold", 1, 2, 6, 1, 1, 1); P("tile", "gold", 2, 2, 6, 1, 1, 1);
            });
            Add(l, "kuvet", "Küvet", 4, () =>
            {
                P("brick", "white", 0, 0, 0, 6, 3, 4);
                P("tile", "lblue", 1, 1, 4, 4, 1, 1);
                P("round", "gray", 5, 1, 4, 1, 1, 3);                       // tap
            });
            Add(l, "kitaplik", "Kitaplık", 4, () =>
            {
                P("brick", "brown", 0, 0, 0, 1, 2, 12); P("brick", "brown", 4, 0, 0, 1, 2, 12);
                foreach (int y in new[] { 0, 4, 8 }) P("brick", "brown", 1, 0, y, 3, 2, 1);
                P("brick", "brown", 0, 0, 12, 5, 2, 1);
                string[] books = { "red", "blue", "green", "yellow", "dred", "dblue" };
                int k = 0;
                foreach (int y in new[] { 1, 5, 9 }) for (int x = 1; x <= 3; x++) P("brick", books[k++ % books.Length], x, 0, y, 1, 2, 3);
            });
            Add(l, "somine", "Şömine", 5, () =>
            {
                P("brick", "slate", 0, 0, 0, 1, 2, 9); P("brick", "slate", 5, 0, 0, 1, 2, 9);
                P("brick", "black", 1, 0, 0, 4, 1, 9);
                P("brick", "slate", 0, 0, 9, 6, 2, 3);
                P("cone", "orange", 2, 1, 0, 1, 1, 3); P("cone", "yellow", 3, 1, 0, 1, 1, 2);
            });
            Add(l, "piyano", "Piyano", 6, () =>
            {
                P("brick", "black", 0, 0, 0, 6, 2, 12);
                P("brick", "black", 0, 2, 3, 6, 1, 2);
                P("tile", "white", 0, 2, 5, 6, 1, 1);                       // the keys
            });
            Add(l, "akvaryum", "Akvaryum", 7, () =>
            {
                P("brick", "brown", 0, 0, 0, 5, 2, 4);
                P("brick", "tblue", 0, 0, 4, 5, 2, 6);
                P("tile", "dgreen", 0, 0, 10, 5, 2, 1);
            });
            return l;
        }
    }
}
