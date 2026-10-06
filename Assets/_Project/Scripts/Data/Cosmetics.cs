using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>What a cosmetic changes on the robot.</summary>
    public enum Slot
    {
        Color,
        Hat,
        Eyes,
        Back,
        Trail,
        Dance
    }

    public class Cosmetic
    {
        public string id;
        public Slot slot;
        public int price;
        /// <summary>0-based world the player must have reached before it can be bought (0 = from the start).</summary>
        public int world;
        /// <summary>Body / eye / trail colour, or the swatch colour shown in the garage.</summary>
        public Color color;

        public bool IsDefault => price == 0;
    }

    /// <summary>
    /// The garage catalogue: robot paint, hats, eye colours, back gear, hop trails and victory dances, bought with
    /// coins and equipped one per slot. The first entry of every slot is the free default.
    /// </summary>
    public static class Cosmetics
    {
        public static readonly List<Cosmetic> All = new List<Cosmetic>
        {
            C("color.world", Slot.Color, 0, 0, new Color(0.64f, 0.69f, 0.81f)),
            C("color.mint", Slot.Color, 300, 0, new Color(0.45f, 0.85f, 0.7f)),
            C("color.coral", Slot.Color, 300, 0, new Color(1f, 0.55f, 0.5f)),
            C("color.sun", Slot.Color, 400, 0, new Color(1f, 0.8f, 0.35f)),
            C("color.ocean", Slot.Color, 400, 1, new Color(0.35f, 0.6f, 1f)),
            C("color.grape", Slot.Color, 500, 2, new Color(0.62f, 0.45f, 0.95f)),
            C("color.night", Slot.Color, 800, 4, new Color(0.25f, 0.24f, 0.36f)),
            C("color.gold", Slot.Color, 1500, 7, new Color(1f, 0.82f, 0.3f)),

            C("hat.none", Slot.Hat, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("hat.party", Slot.Hat, 350, 0, new Color(1f, 0.45f, 0.6f)),
            C("hat.cap", Slot.Hat, 450, 0, new Color(0.35f, 0.6f, 1f)),
            C("hat.phones", Slot.Hat, 600, 1, new Color(0.3f, 0.3f, 0.4f)),
            C("hat.top", Slot.Hat, 700, 2, new Color(0.2f, 0.18f, 0.25f)),
            C("hat.pirate", Slot.Hat, 800, 3, new Color(0.15f, 0.13f, 0.2f)),
            C("hat.crown", Slot.Hat, 1200, 5, new Color(1f, 0.8f, 0.3f)),

            C("eyes.world", Slot.Eyes, 0, 0, new Color(0.4f, 1.6f, 2.2f)),
            C("eyes.lime", Slot.Eyes, 250, 0, new Color(0.8f, 2.2f, 0.4f)),
            C("eyes.ruby", Slot.Eyes, 300, 0, new Color(2.4f, 0.4f, 0.5f)),
            C("eyes.gold", Slot.Eyes, 450, 1, new Color(2.4f, 1.8f, 0.4f)),
            C("eyes.violet", Slot.Eyes, 450, 2, new Color(1.6f, 0.6f, 2.4f)),

            C("back.none", Slot.Back, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("back.cape", Slot.Back, 700, 0, new Color(0.9f, 0.3f, 0.4f)),
            C("back.jetpack", Slot.Back, 900, 1, new Color(0.7f, 0.75f, 0.85f)),
            C("back.wings", Slot.Back, 1500, 4, new Color(0.85f, 0.95f, 1f)),

            C("trail.none", Slot.Trail, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("trail.sparks", Slot.Trail, 400, 0, new Color(0.5f, 1.6f, 2.2f)),
            C("trail.hearts", Slot.Trail, 500, 1, new Color(2.2f, 0.6f, 1f)),
            C("trail.stars", Slot.Trail, 600, 2, new Color(2.4f, 1.9f, 0.5f)),
            C("trail.fire", Slot.Trail, 900, 3, new Color(2.6f, 0.9f, 0.2f)),

            C("dance.spin", Slot.Dance, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("dance.jump", Slot.Dance, 300, 0, new Color(0.55f, 0.9f, 0.75f)),
            C("dance.wobble", Slot.Dance, 400, 1, new Color(1f, 0.7f, 0.4f)),
            C("dance.flip", Slot.Dance, 600, 2, new Color(0.62f, 0.45f, 0.95f)),
        };

        private static Cosmetic C(string id, Slot slot, int price, int world, Color color) =>
            new Cosmetic { id = id, slot = slot, price = price, world = world, color = color };

        public static List<Cosmetic> InSlot(Slot slot) => All.FindAll(c => c.slot == slot);

        public static Cosmetic Find(string id) => All.Find(c => c.id == id);

        public static bool Owns(Cosmetic c) => c.IsDefault || PlayerPrefs.GetInt("sb_cos_" + c.id, 0) == 1;

        public static Cosmetic Equipped(Slot slot)
        {
            var c = Find(PlayerPrefs.GetString("sb_eq_" + slot, ""));
            return c != null && c.slot == slot && Owns(c) ? c : InSlot(slot)[0];
        }

        public static void Equip(Cosmetic c)
        {
            if (!Owns(c)) return;
            PlayerPrefs.SetString("sb_eq_" + c.slot, c.id);
            PlayerPrefs.Save();
        }

        public static bool TryBuy(Cosmetic c, int reachedWorld)
        {
            if (Owns(c) || reachedWorld < c.world || !Shop.Spend(c.price)) return false;
            PlayerPrefs.SetInt("sb_cos_" + c.id, 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>The full outfit: what is equipped, with an optional preview piece swapped in.</summary>
        public static Dictionary<Slot, Cosmetic> Outfit(Cosmetic preview = null)
        {
            var outfit = new Dictionary<Slot, Cosmetic>();
            foreach (Slot s in System.Enum.GetValues(typeof(Slot))) outfit[s] = Equipped(s);
            if (preview != null) outfit[preview.slot] = preview;
            return outfit;
        }
    }
}
