using System;
using System.Collections.Generic;

namespace BrickMuseum.Character
{
    /// <summary>What the brick person wears. Colours are palette keys; styles are the ids in <see cref="Wardrobe"/>.</summary>
    [Serializable]
    public class Outfit
    {
        public string hairStyle = "short", hair = "brown", skin = "skin", hat = "none";
        public string shirt = "blue", pattern = "plain", pants = "dblue", shoes = "black";
        /// <summary>Uniforms: the hat's colour and the stripe / tie colour ("" = the usual one).</summary>
        public string hatColor = "", trim = "";

        public static Outfit Default => new Outfit();
        public Outfit Copy() => (Outfit)MemberwiseClone();
        public string Key => $"{hairStyle}|{hair}|{skin}|{hat}|{shirt}|{pattern}|{pants}|{shoes}|{hatColor}|{trim}";
    }

    /// <summary>One choice in the character workshop: a style id or a colour, opened at a player level.</summary>
    public sealed class WardrobeItem
    {
        public string id, label;
        public int level;
        public bool IsColour;
    }

    /// <summary>A tab of the character workshop.</summary>
    public sealed class WardrobeSlot
    {
        public string field, title;
        public List<WardrobeItem> items = new List<WardrobeItem>();
    }

    /// <summary>Everything the character workshop offers; new items open as the player levels up.</summary>
    public static class Wardrobe
    {
        private static List<WardrobeSlot> slots;

        private static WardrobeSlot Styles(string field, string title, params (string id, string label, int level)[] items)
        {
            var s = new WardrobeSlot { field = field, title = title };
            foreach (var (id, label, level) in items) s.items.Add(new WardrobeItem { id = id, label = label, level = level });
            return s;
        }

        private static WardrobeSlot Colours(string field, string title, params (string id, int level)[] items)
        {
            var s = new WardrobeSlot { field = field, title = title };
            foreach (var (id, level) in items) s.items.Add(new WardrobeItem { id = id, label = Data.Palette.Turkish(id), level = level, IsColour = true });
            return s;
        }

        public static List<WardrobeSlot> Slots => slots ??= new List<WardrobeSlot>
        {
            Styles("hairStyle", "Saç", ("short", "Kısa", 1), ("long", "Uzun", 1), ("bangs", "Kâküllü", 2), ("bald", "Kel", 1)),
            Colours("hair", "Saç rengi", ("brown", 1), ("black", 1), ("yellow", 1), ("dred", 2), ("orange", 3), ("gray", 4), ("lilac", 6)),
            Colours("skin", "Ten", ("cream", 1), ("skin", 1), ("skin2", 1), ("brown", 1)),
            Styles("hat", "Şapka", ("none", "Yok", 1), ("cap", "Kep", 1), ("helmet", "Baret", 2), ("top", "Silindir", 3), ("crown", "Taç", 5)),
            Colours("shirt", "Gömlek", ("blue", 1), ("red", 1), ("white", 1), ("green", 1), ("yellow", 2), ("orange", 2), ("mint", 3), ("lilac", 3), ("black", 4), ("gold", 6)),
            Styles("pattern", "Desen", ("plain", "Düz", 1), ("stripe", "Çizgili", 2), ("tie", "Kravatlı", 3)),
            Colours("pants", "Pantolon", ("dblue", 1), ("black", 1), ("sand", 1), ("brown", 2), ("gray", 2), ("dgreen", 3), ("red", 4)),
            Colours("shoes", "Ayakkabı", ("black", 1), ("brown", 1), ("white", 2), ("red", 3), ("blue", 3), ("yellow", 5)),
        };

        public static string Get(Outfit o, string field) => (string)typeof(Outfit).GetField(field).GetValue(o);
        public static void Set(Outfit o, string field, string value) => typeof(Outfit).GetField(field).SetValue(o, value);
    }
}
