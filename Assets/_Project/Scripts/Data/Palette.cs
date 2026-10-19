using System.Collections.Generic;
using UnityEngine;

namespace BrickMuseum.Data
{
    /// <summary>
    /// Brick colours by the keys used in the model files, with their Turkish names. Our own palette, deliberately not
    /// any toy brand's exact colours.
    /// </summary>
    public static class Palette
    {
        private static readonly Dictionary<string, (Color color, string tr)> table = new Dictionary<string, (Color, string)>
        {
            { "red", (Hex(0xe0483a), "kırmızı") },
            { "white", (Hex(0xf4f1ea), "beyaz") },
            { "gray", (Hex(0xa7aeb4), "gri") },
            { "dgray", (Hex(0x5b6470), "koyu gri") },
            { "black", (Hex(0x2a2d33), "siyah") },
            { "blue", (Hex(0x3a78d8), "mavi") },
            { "tblue", (Hex(0x9fd8f5), "cam") },
            { "yellow", (Hex(0xf6c445), "sarı") },
            { "green", (Hex(0x4caf5f), "yeşil") },
            { "orange", (Hex(0xf58d3c), "turuncu") },
            { "brown", (Hex(0x8a5a36), "kahverengi") },
            { "bronze", (Hex(0x8c6a4a), "bronz") },
            { "sand", (Hex(0xdcc694), "kum rengi") },
            { "dgreen", (Hex(0x2f6a4a), "koyu yeşil") },
            { "skin", (Hex(0xf2c89a), "ten rengi") },
            { "skin2", (Hex(0xd79e6c), "koyu ten") },
            { "dblue", (Hex(0x27426e), "lacivert") },
            { "gold", (Hex(0xd9a92e), "altın") },
            { "cream", (Hex(0xf1e3c2), "krem") },
            { "rose", (Hex(0xe8a3ab), "gül pembesi") },
            { "mint", (Hex(0x9fd6b9), "nane yeşili") },
            { "lblue", (Hex(0xa8cdee), "açık mavi") },
            { "terra", (Hex(0xc4643d), "kiremit") },
            { "slate", (Hex(0x4b5562), "arduvaz") },
            { "dred", (Hex(0x9a3a2c), "tuğla kırmızısı") },
            { "ochre", (Hex(0xdaa645), "hardal") },
            { "lilac", (Hex(0xb9a8d9), "lila") },
        };

        /// <summary>Characters used in the layer maps.</summary>
        public static readonly Dictionary<char, string> Chars = new Dictionary<char, string>
        {
            { 'r', "red" }, { 'w', "white" }, { 'g', "gray" }, { 'd', "dgray" }, { 'k', "black" }, { 'b', "blue" }, { 't', "tblue" },
            { 'y', "yellow" }, { 'G', "green" }, { 'o', "orange" }, { 'n', "brown" }, { 'N', "bronze" }, { 's', "sand" }, { 'E', "dgreen" },
            { 'f', "skin" }, { 'F', "skin2" }, { 'B', "dblue" }, { 'Y', "gold" },
            { 'c', "cream" }, { 'p', "rose" }, { 'm', "mint" }, { 'l', "lblue" }, { 'a', "terra" }, { 'S', "slate" },
            { 'R', "dred" }, { 'O', "ochre" }, { 'L', "lilac" },
        };

        public static IEnumerable<string> Keys => table.Keys;
        public static Color Of(string key) => table.TryGetValue(key, out var v) ? v.color : Color.magenta;
        public static string Turkish(string key) => table.TryGetValue(key, out var v) ? v.tr : key;
        public static bool IsGlass(string key) => key == "tblue";

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
