using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BrickMuseum.Data
{
    [Serializable] public class PartJson { public string t, c, f, art; public int x, z, y, w, d, h, need = -1; }
    [Serializable] public class LayerJson { public string[] rows; }
    [Serializable] public class ModelJson { public string id, name, cat, kind, style; public float scale; public LayerJson[] layers; public PartJson[] parts; }
    [Serializable] public class CatalogJson { public ModelJson[] models; }

    /// <summary>A buildable model: its pieces, size and how many mistakes the player may make on it.</summary>
    public sealed class ModelDef
    {
        public string id, name, cat;
        /// <summary>City slot kind: house, big, vehicle, tree, animal, statue, landmark, decor.</summary>
        public string kind;
        /// <summary>House style (tr, en, us, eu, as); empty for other kinds.</summary>
        public string style;
        /// <summary>How much bigger or smaller than built it stands in the city, so that it has its real size.</summary>
        public float scale = 1f;
        public int index;
        public List<Piece> pieces;
        public int sx, sz, ox, oz;
        public float height;

        /// <summary>Mistakes allowed: big, hard models give more room (user rule: easy few, hard many).</summary>
        public int Hearts => Mathf.Clamp(2 + pieces.Count / 15, 2, 8);
    }

    /// <summary>
    /// Loads Resources/models.json. Each model is a stack of layer maps (one brick tall each; rows back → front,
    /// characters left → right) that are split into standard bricks, plus hand-placed shaped parts and stickers.
    /// The list order is the difficulty order.
    /// </summary>
    public static class ModelCatalog
    {
        private static List<ModelDef> all;

        public static List<ModelDef> All
        {
            get
            {
                if (all != null) return all;
                var json = Resources.Load<TextAsset>("models");
                var cat = JsonUtility.FromJson<CatalogJson>(json.text);
                all = new List<ModelDef>();
                foreach (var m in cat.models) all.Add(Build(m, all.Count));
                return all;
            }
        }

        public static ModelDef Find(string id) => All.FirstOrDefault(m => m.id == id);

        /// <summary>A model made in code (furniture) — not part of the catalogue, the roulette or the collection.</summary>
        public static ModelDef FromPieces(string id, string name, string kind, List<Piece> pieces) =>
            Measure(new ModelDef { id = id, name = name, cat = "", kind = kind, style = "", index = -1 }, pieces);

        private static ModelDef Measure(ModelDef m, List<Piece> pieces)
        {
            int minX = pieces.Min(p => p.x), maxX = pieces.Max(p => p.x + p.fw);
            int minZ = pieces.Min(p => p.z), maxZ = pieces.Max(p => p.z + p.fd);
            m.pieces = pieces;
            m.sx = maxX - minX; m.sz = maxZ - minZ;
            m.ox = -Mathf.RoundToInt((minX + maxX) / 2f); m.oz = -Mathf.RoundToInt((minZ + maxZ) / 2f);
            m.height = pieces.Max(p => p.y * Piece.Plate + p.TopY);
            return m;
        }

        private static ModelDef Build(ModelJson j, int index)
        {
            var pieces = Decompose(j.layers);
            if (j.parts != null)
                foreach (var p in j.parts)
                    pieces.Add(Piece.Make(p.t, p.c, p.x, p.z, p.y, p.w, p.d, p.h, p.f, p.art, p.need));
            int minX = pieces.Min(p => p.x), maxX = pieces.Max(p => p.x + p.fw);
            int minZ = pieces.Min(p => p.z), maxZ = pieces.Max(p => p.z + p.fd);
            return new ModelDef
            {
                id = j.id, name = j.name, cat = j.cat, kind = j.kind ?? "", style = j.style ?? "", scale = j.scale > 0 ? j.scale : 1f, index = index, pieces = pieces,
                sx = maxX - minX, sz = maxZ - minZ,
                ox = -Mathf.RoundToInt((minX + maxX) / 2f), oz = -Mathf.RoundToInt((minZ + maxZ) / 2f),
                height = pieces.Max(p => p.y * Piece.Plate + p.TopY),
            };
        }

        private static readonly int[,] SizesX = { { 2, 8 }, { 8, 2 }, { 2, 6 }, { 6, 2 }, { 1, 8 }, { 8, 1 }, { 2, 4 }, { 4, 2 }, { 1, 6 }, { 6, 1 }, { 2, 3 }, { 3, 2 }, { 1, 4 }, { 4, 1 }, { 2, 2 }, { 1, 3 }, { 3, 1 }, { 1, 2 }, { 2, 1 }, { 1, 1 } };

        /// <summary>Greedy split: the biggest standard brick that fits first, orientation preference alternating per layer.</summary>
        private static List<Piece> Decompose(LayerJson[] layers)
        {
            var output = new List<Piece>();
            if (layers == null) return output;
            for (int li = 0; li < layers.Length; li++)
            {
                var rows = layers[li].rows;
                int depth = rows.Length, width = rows.Max(r => r.Length);
                var used = new bool[depth, width];
                char At(int x, int z) => z < depth && x < width && x < rows[z].Length ? rows[z][x] : '.';
                for (int z = 0; z < depth; z++)
                    for (int x = 0; x < width; x++)
                    {
                        char c = At(x, z);
                        if (c == '.' || used[z, x]) continue;
                        if (c == 'W')
                        {
                            if (At(x + 1, z) == 'W' && !used[z, x + 1]) { used[z, x] = used[z, x + 1] = true; output.Add(Piece.Make("wheel", "black", x, z, li * 3)); }
                            else { used[z, x] = true; output.Add(Piece.Make("brick", "black", x, z, li * 3)); }
                            continue;
                        }
                        for (int s = 0; s < SizesX.GetLength(0); s++)
                        {
                            int w = li % 2 == 1 ? SizesX[s, 1] : SizesX[s, 0], d = li % 2 == 1 ? SizesX[s, 0] : SizesX[s, 1];
                            bool ok = true;
                            for (int i = 0; i < w && ok; i++)
                                for (int k = 0; k < d && ok; k++)
                                    if (At(x + i, z + k) != c || used[z + k, x + i]) ok = false;
                            if (!ok) continue;
                            for (int i = 0; i < w; i++) for (int k = 0; k < d; k++) used[z + k, x + i] = true;
                            output.Add(Piece.Make("brick", Palette.Chars.TryGetValue(c, out var col) ? col : "white", x, z, li * 3, w, d));
                            break;
                        }
                    }
            }
            return output;
        }
    }
}
