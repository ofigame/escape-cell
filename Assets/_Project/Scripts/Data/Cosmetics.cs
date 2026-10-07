using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>What a cosmetic changes on the robot.</summary>
    public enum Slot
    {
        Color,
        Eyes,
        Hat,
        Arms,
        Legs,
        Back,
        /// <summary>A badge on the chest or shoulder (story rewards only).</summary>
        Badge,
        /// <summary>A short-lived mark on every tile the robot lands on.</summary>
        Step,
        /// <summary>The hop and road trail.</summary>
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

        public bool IsDefault => price == 0 && !reward && storyLevel == 0;

        /// <summary>The themed set this piece belongs to (null = none).</summary>
        public string set;
        /// <summary>The set's prize: not sold, it comes with the complete set.</summary>
        public bool reward;
        /// <summary>A story reward: not sold, it comes with beating this level (1-based; 0 = none).</summary>
        public int storyLevel;
    }

    /// <summary>
    /// Bip's paint workshop: body paint, eyes, head, arms, legs, back gear, badges, step marks, trails and dances,
    /// bought with coins and worn one per slot. The first entry of every slot is the free default. Looks only:
    /// nothing here makes the robot stronger. Each world set opens when its world is reached and is 20% cheaper as a
    /// whole; owning a set gives its prize. Story rewards cannot be bought.
    /// </summary>
    public static class Cosmetics
    {
        public static readonly List<Cosmetic> All = new List<Cosmetic>
        {
            C("color.world", Slot.Color, 0, 0, new Color(0.64f, 0.69f, 0.81f)),
            C("color.mint", Slot.Color, 100, 0, new Color(0.45f, 0.85f, 0.7f)),
            C("color.coral", Slot.Color, 100, 0, new Color(1f, 0.55f, 0.5f)),
            C("color.sun", Slot.Color, 100, 0, new Color(1f, 0.8f, 0.35f)),
            C("color.ocean", Slot.Color, 100, 1, new Color(0.35f, 0.6f, 1f)),
            C("color.sunset", Slot.Color, 400, 3, new Color(1f, 0.55f, 0.25f)),
            C("color.grape", Slot.Color, 400, 2, new Color(0.62f, 0.45f, 0.95f)),
            C("color.night", Slot.Color, 400, 4, new Color(0.25f, 0.24f, 0.36f)),
            C("color.gold", Slot.Color, 3000, 7, new Color(1f, 0.82f, 0.3f)),

            C("eyes.world", Slot.Eyes, 0, 0, new Color(0.4f, 1.6f, 2.2f)),
            C("eyes.lime", Slot.Eyes, 200, 0, new Color(0.8f, 2.2f, 0.4f)),
            C("eyes.ruby", Slot.Eyes, 200, 0, new Color(2.4f, 0.4f, 0.5f)),
            C("eyes.determined", Slot.Eyes, 300, 1, new Color(2.4f, 1.1f, 0.3f)),
            C("eyes.gold", Slot.Eyes, 300, 1, new Color(2.4f, 1.8f, 0.4f)),
            C("eyes.violet", Slot.Eyes, 300, 2, new Color(1.6f, 0.6f, 2.4f)),

            C("hat.none", Slot.Hat, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("hat.antenna", Slot.Hat, 200, 0, new Color(0.75f, 0.8f, 0.9f)),
            C("hat.party", Slot.Hat, 200, 0, new Color(1f, 0.45f, 0.6f)),
            C("hat.cap", Slot.Hat, 250, 0, new Color(0.35f, 0.6f, 1f)),
            C("hat.antenna2", Slot.Hat, 300, 1, new Color(1f, 0.45f, 0.35f)),
            C("hat.phones", Slot.Hat, 300, 1, new Color(0.3f, 0.3f, 0.4f)),
            C("hat.top", Slot.Hat, 400, 2, new Color(0.2f, 0.18f, 0.25f)),
            C("hat.pirate", Slot.Hat, 400, 3, new Color(0.15f, 0.13f, 0.2f)),
            C("hat.crown", Slot.Hat, 600, 5, new Color(1f, 0.8f, 0.3f)),

            C("arms.std", Slot.Arms, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("arms.brush", Slot.Arms, 300, 3, new Color(0.95f, 0.6f, 0.3f)),
            C("arms.spring", Slot.Arms, 400, 6, new Color(0.8f, 0.84f, 0.95f)),
            C("arms.crystal", Slot.Arms, 600, 14, new Color(0.6f, 0.9f, 1f)),

            C("legs.std", Slot.Legs, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("legs.spring", Slot.Legs, 400, 2, new Color(0.8f, 0.84f, 0.95f)),

            C("back.none", Slot.Back, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("back.cape", Slot.Back, 400, 0, new Color(0.9f, 0.3f, 0.4f)),
            C("back.jetpack", Slot.Back, 500, 1, new Color(0.7f, 0.75f, 0.85f)),
            C("back.wings", Slot.Back, 600, 4, new Color(0.85f, 0.95f, 1f)),
            C("back.shell", Slot.Back, 400, 9, new Color(1f, 0.75f, 0.7f)),
            C("back.rocket", Slot.Back, 600, 19, new Color(0.95f, 0.95f, 1f)),

            C("badge.none", Slot.Badge, 0, 0, new Color(0.5f, 0.5f, 0.6f)),

            C("step.none", Slot.Step, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("step.spark", Slot.Step, 300, 0, new Color(2.4f, 1.6f, 0.4f)),
            C("step.bubble", Slot.Step, 400, 9, new Color(0.7f, 1.6f, 2.2f)),
            C("step.stardust", Slot.Step, 600, 14, new Color(1.6f, 1f, 2.4f)),

            C("trail.none", Slot.Trail, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("trail.sparks", Slot.Trail, 300, 0, new Color(0.5f, 1.6f, 2.2f)),
            C("trail.paint", Slot.Trail, 400, 0, new Color(2.2f, 0.5f, 1.4f)),
            C("trail.hearts", Slot.Trail, 400, 1, new Color(2.2f, 0.6f, 1f)),
            C("trail.confetti", Slot.Trail, 400, 3, new Color(2.2f, 2f, 0.6f)),
            C("trail.stars", Slot.Trail, 500, 2, new Color(2.4f, 1.9f, 0.5f)),
            C("trail.fire", Slot.Trail, 600, 5, new Color(2.6f, 0.9f, 0.2f)),
            C("trail.bolt", Slot.Trail, 800, 19, new Color(1.8f, 2f, 2.6f)),

            C("dance.spin", Slot.Dance, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("dance.jump", Slot.Dance, 300, 0, new Color(0.55f, 0.9f, 0.75f)),
            C("dance.wobble", Slot.Dance, 400, 1, new Color(1f, 0.7f, 0.4f)),
            C("dance.flip", Slot.Dance, 500, 2, new Color(0.62f, 0.45f, 0.95f)),

            // World sets: three pieces each; owning all three gives the set's prize.
            S("hat.cable", Slot.Hat, 300, 0, new Color(0.3f, 0.32f, 0.4f), "canal"),
            S("back.rustbox", Slot.Back, 200, 0, new Color(0.72f, 0.45f, 0.3f), "canal"),
            S("legs.tracks", Slot.Legs, 300, 0, new Color(0.3f, 0.3f, 0.36f), "canal"),
            R("eyes.sleepy", Slot.Eyes, 0, new Color(1.4f, 1.2f, 2.2f), "canal"),
            S("hat.leaf", Slot.Hat, 300, 2, new Color(0.4f, 0.8f, 0.4f), "forest"),
            S("back.leafbag", Slot.Back, 300, 2, new Color(0.45f, 0.75f, 0.35f), "forest"),
            S("step.leaf", Slot.Step, 300, 2, new Color(0.6f, 1.8f, 0.5f), "forest"),
            R("eyes.happy", Slot.Eyes, 2, new Color(0.5f, 2.2f, 1.2f), "forest"),
            S("color.canyon", Slot.Color, 100, 5, new Color(0.85f, 0.38f, 0.28f), "canyon"),
            S("legs.wheel", Slot.Legs, 400, 5, new Color(0.9f, 0.6f, 0.35f), "canyon"),
            S("arms.claw", Slot.Arms, 500, 5, new Color(0.85f, 0.45f, 0.3f), "canyon"),
            R("trail.dust", Slot.Trail, 5, new Color(2.2f, 1.4f, 0.7f), "canyon"),
            S("hat.diver", Slot.Hat, 600, 8, new Color(0.85f, 0.95f, 1f), "ocean"),
            S("back.tank", Slot.Back, 500, 8, new Color(1f, 0.8f, 0.3f), "ocean"),
            S("trail.bubbles", Slot.Trail, 400, 8, new Color(0.7f, 1.6f, 2.2f), "ocean"),
            R("dance.swim", Slot.Dance, 8, new Color(0.4f, 0.75f, 1f), "ocean"),
            S("hat.icecrown", Slot.Hat, 500, 11, new Color(0.75f, 0.95f, 1f), "mountain"),
            S("legs.snow", Slot.Legs, 500, 11, new Color(0.55f, 0.7f, 0.95f), "mountain"),
            S("trail.snow", Slot.Trail, 500, 11, new Color(1.8f, 2.1f, 2.4f), "mountain"),
            R("eyes.star", Slot.Eyes, 11, new Color(2.4f, 2f, 0.5f), "mountain"),
            S("hat.visor", Slot.Hat, 500, 14, new Color(0.3f, 1f, 0.95f), "cyber"),
            S("back.cyberwings", Slot.Back, 600, 14, new Color(0.25f, 0.95f, 0.9f), "cyber"),
            S("trail.neon", Slot.Trail, 500, 14, new Color(0.4f, 2.4f, 1.2f), "cyber"),
            R("eyes.matrix", Slot.Eyes, 14, new Color(0.5f, 2.6f, 0.6f), "cyber"),
            S("hat.cloud", Slot.Hat, 500, 17, new Color(0.95f, 0.97f, 1f), "cloud"),
            S("back.balloon", Slot.Back, 500, 17, new Color(1f, 0.5f, 0.55f), "cloud"),
            S("legs.jet", Slot.Legs, 600, 17, new Color(0.8f, 0.82f, 0.9f), "cloud"),
            R("eyes.heart", Slot.Eyes, 17, new Color(2.4f, 0.6f, 1.2f), "cloud"),
            S("hat.lollipop", Slot.Hat, 400, 18, new Color(1f, 0.45f, 0.75f), "candy"),
            S("color.candy", Slot.Color, 400, 18, new Color(1f, 0.62f, 0.85f), "candy"),
            S("trail.sprinkles", Slot.Trail, 400, 18, new Color(2.2f, 1.6f, 0.6f), "candy"),
            R("dance.sugar", Slot.Dance, 18, new Color(1f, 0.7f, 0.9f), "candy"),
            S("hat.astro", Slot.Hat, 600, 23, new Color(0.95f, 0.95f, 1f), "galaxy"),
            S("color.cosmic", Slot.Color, 400, 23, new Color(0.45f, 0.3f, 0.85f), "galaxy"),
            S("trail.comet", Slot.Trail, 600, 23, new Color(1.6f, 0.9f, 2.4f), "galaxy"),
            R("back.halo", Slot.Back, 23, new Color(1f, 0.9f, 0.5f), "galaxy"),

            // Story rewards: earned, never sold.
            T("badge.press", Slot.Badge, 20, new Color(0.75f, 0.78f, 0.85f)),
            T("badge.cl1", Slot.Badge, 40, new Color(0.35f, 0.38f, 0.5f)),
            T("arms.lumi", Slot.Arms, 170, new Color(1f, 0.82f, 0.3f)),
            T("back.raven", Slot.Back, 211, new Color(0.16f, 0.15f, 0.24f)),
            T("badge.heart", Slot.Badge, 250, new Color(1f, 0.8f, 0.3f)),
        };

        private static Cosmetic C(string id, Slot slot, int price, int world, Color color) =>
            new Cosmetic { id = id, slot = slot, price = price, world = world, color = color };

        private static Cosmetic S(string id, Slot slot, int price, int world, Color color, string set) =>
            new Cosmetic { id = id, slot = slot, price = price, world = world, color = color, set = set };

        private static Cosmetic R(string id, Slot slot, int world, Color color, string set) =>
            new Cosmetic { id = id, slot = slot, price = 0, world = world, color = color, set = set, reward = true };

        private static Cosmetic T(string id, Slot slot, int level, Color color) =>
            new Cosmetic { id = id, slot = slot, price = 0, world = 0, color = color, storyLevel = level };

        /// <summary>The world sets in story order (the ids of the first four are older than the world names).</summary>
        public static readonly string[] Sets = { "canal", "forest", "canyon", "ocean", "mountain", "cyber", "cloud", "candy", "galaxy" };

        /// <summary>A whole set bought at once costs this share of its missing pieces.</summary>
        public const float SetDiscount = 0.8f;

        /// <summary>The free paint tube Bip hands over on the first workshop visit.</summary>
        public const string GiftId = "color.mint";

        private static bool Bought(Cosmetic c) => PlayerPrefs.GetInt("sb_cos_" + c.id, 0) == 1;

        /// <summary>How many of the set's three pieces are owned.</summary>
        public static int SetOwned(string set) => All.FindAll(c => c.set == set && !c.reward && Bought(c)).Count;

        public static int SetSize(string set) => All.FindAll(c => c.set == set && !c.reward).Count;

        public static bool SetComplete(string set) => SetOwned(set) >= SetSize(set);

        /// <summary>What the rest of the set costs bought together (20% off the pieces still missing).</summary>
        public static int SetPrice(string set)
        {
            int sum = 0;
            foreach (var c in All)
                if (c.set == set && !c.reward && !Bought(c)) sum += c.price;
            return Mathf.RoundToInt(sum * SetDiscount / 10f) * 10;
        }

        public static bool TryBuySet(string set, int reachedWorld)
        {
            var pieces = All.FindAll(c => c.set == set && !c.reward && !Bought(c));
            if (pieces.Count == 0 || reachedWorld < pieces[0].world || !Shop.Spend(SetPrice(set))) return false;
            foreach (var c in pieces) PlayerPrefs.SetInt("sb_cos_" + c.id, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static List<Cosmetic> InSlot(Slot slot) => All.FindAll(c => c.slot == slot);

        public static Cosmetic Find(string id) => All.Find(c => c.id == id);

        /// <summary>A story reward is owned once its level is beaten.</summary>
        public static bool StoryEarned(Cosmetic c) => c.storyLevel > 0 && Progress.Stars(c.storyLevel - 1) > 0;

        public static bool Owns(Cosmetic c) =>
            c.reward ? SetComplete(c.set) : c.storyLevel > 0 ? StoryEarned(c) : c.IsDefault || Bought(c);

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
            if (c.reward || c.storyLevel > 0 || Owns(c) || reachedWorld < c.world || !Shop.Spend(c.price)) return false;
            PlayerPrefs.SetInt("sb_cos_" + c.id, 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>A present: owned without paying.</summary>
        public static void Give(string id)
        {
            PlayerPrefs.SetInt("sb_cos_" + id, 1);
            PlayerPrefs.Save();
        }

        /// <summary>The full outfit: what is equipped, with an optional preview piece swapped in.</summary>
        public static Dictionary<Slot, Cosmetic> Outfit(Cosmetic preview = null)
        {
            var outfit = new Dictionary<Slot, Cosmetic>();
            foreach (Slot s in System.Enum.GetValues(typeof(Slot))) outfit[s] = Equipped(s);
            if (preview != null) outfit[preview.slot] = preview;
            return outfit;
        }

        /// <summary>Eye pieces that change the eyes' shape rather than only their colour.</summary>
        public static bool ShapedEyes(string id) =>
            id == "eyes.happy" || id == "eyes.heart" || id == "eyes.star" || id == "eyes.sleepy" || id == "eyes.determined";
    }
}
