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
        Dance,
        /// <summary>The paint on foi's armour: camouflage or colours.</summary>
        Suit,
        /// <summary>Which head foi wears.</summary>
        Head,
        /// <summary>The colour of its hair.</summary>
        Hair,
        /// <summary>The colour of its skin (on the head: the body is armour).</summary>
        Skin,
        /// <summary>The colour of its eyes.</summary>
        Iris
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

        public bool IsDefault => price == 0 && !reward && storyLevel == 0 && rank == 0 && !chest;

        /// <summary>The themed set this piece belongs to (null = none).</summary>
        public string set;
        /// <summary>The set's prize: not sold, it comes with the complete set.</summary>
        public bool reward;
        /// <summary>A story reward: not sold, it comes with beating this level (1-based; 0 = none).</summary>
        public int storyLevel;
        /// <summary>A rank reward: not sold, worn from this challenge rank on (0 = none).</summary>
        public int rank;
        /// <summary>A duel chest's surprise: not sold, it only comes out of a chest.</summary>
        public bool chest;
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
            C("hat.hood", Slot.Hat, 500, 0, new Color(0.7f, 0.7f, 0.74f)),
            C("hat.hood.black", Slot.Hat, 600, 2, new Color(0.22f, 0.22f, 0.25f)),
            C("hat.hood.crimson", Slot.Hat, 700, 4, new Color(0.62f, 0.1f, 0.14f)),
            C("hat.hood.forest", Slot.Hat, 700, 5, new Color(0.3f, 0.42f, 0.26f)),

            C("arms.std", Slot.Arms, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("arms.brush", Slot.Arms, 300, 3, new Color(0.95f, 0.6f, 0.3f)),
            C("arms.spring", Slot.Arms, 400, 6, new Color(0.8f, 0.84f, 0.95f)),
            C("arms.crystal", Slot.Arms, 600, 14, new Color(0.6f, 0.9f, 1f)),
            C("arms.shoulder", Slot.Arms, 450, 4, new Color(0.35f, 0.38f, 0.5f)),

            C("legs.std", Slot.Legs, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("legs.knee", Slot.Legs, 250, 0, new Color(0.2f, 0.2f, 0.24f)),
            C("legs.spring", Slot.Legs, 400, 2, new Color(0.8f, 0.84f, 0.95f)),

            C("back.none", Slot.Back, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            X("back.cape", Slot.Back, new Color(0.9f, 0.3f, 0.4f)),
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
            // foi's own dances (made on its body)
            C("dance.d1", Slot.Dance, 300, 0, new Color(0.4f, 0.8f, 1f)),
            C("dance.d2", Slot.Dance, 400, 1, new Color(1f, 0.6f, 0.3f)),
            C("dance.d3", Slot.Dance, 500, 2, new Color(0.7f, 0.45f, 1f)),
            C("dance.d4", Slot.Dance, 600, 3, new Color(1f, 0.85f, 0.3f)),

            // The paint on foi's armour: camouflage and colours.
            C("suit.none", Slot.Suit, 0, 0, new Color(0.74f, 0.76f, 0.81f)),
            C("suit.camo", Slot.Suit, 400, 0, new Color(0.36f, 0.4f, 0.25f)),
            C("suit.tactical", Slot.Suit, 800, 1, new Color(0.18f, 0.18f, 0.2f)),
            C("suit.camo.desert", Slot.Suit, 500, 2, new Color(0.72f, 0.6f, 0.42f)),
            C("suit.camo.urban", Slot.Suit, 600, 3, new Color(0.45f, 0.47f, 0.52f)),
            C("suit.tactical.forest", Slot.Suit, 900, 3, new Color(0.3f, 0.36f, 0.22f)),
            C("suit.camo.snow", Slot.Suit, 600, 4, new Color(0.9f, 0.92f, 0.95f)),
            C("suit.tactical.desert", Slot.Suit, 900, 5, new Color(0.66f, 0.56f, 0.38f)),
            C("suit.tactical.urban", Slot.Suit, 900, 6, new Color(0.25f, 0.3f, 0.42f)),
            C("suit.shadow", Slot.Suit, 1500, 8, new Color(0.1f, 0.1f, 0.12f)),
            // ...and the duel chests' surprises: never sold.
            X("suit.elite", Slot.Suit, new Color(1f, 0.78f, 0.3f)),
            X("suit.ghost", Slot.Suit, new Color(0.93f, 0.94f, 0.96f)),
            X("suit.ranger", Slot.Suit, new Color(0.26f, 0.36f, 0.22f)),
            X("suit.crimson", Slot.Suit, new Color(0.6f, 0.08f, 0.12f)),
            X("suit.fox", Slot.Suit, new Color(0.78f, 0.62f, 0.4f)),

            // foi's heads (a man's and a woman's, three haircuts each), and the colours of its hair, skin and eyes.
            C("head.m1", Slot.Head, 0, 0, new Color(0.36f, 0.2f, 0.11f)),
            C("head.f1", Slot.Head, 0, 0, new Color(0.36f, 0.2f, 0.11f)),
            C("head.m2", Slot.Head, 400, 0, new Color(0.36f, 0.2f, 0.11f)),
            C("head.f2", Slot.Head, 400, 0, new Color(0.36f, 0.2f, 0.11f)),
            C("head.m3", Slot.Head, 400, 0, new Color(0.36f, 0.2f, 0.11f)),
            C("head.f3", Slot.Head, 400, 0, new Color(0.36f, 0.2f, 0.11f)),
            C("hair.own", Slot.Hair, 0, 0, new Color(0.35f, 0.18f, 0.11f)),
            C("hair.black", Slot.Hair, 150, 0, new Color(0.08f, 0.07f, 0.07f)),
            C("hair.blonde", Slot.Hair, 200, 0, new Color(0.95f, 0.78f, 0.42f)),
            C("hair.red", Slot.Hair, 200, 1, new Color(0.72f, 0.2f, 0.08f)),
            C("hair.white", Slot.Hair, 300, 2, new Color(0.92f, 0.92f, 0.95f)),
            C("hair.blue", Slot.Hair, 300, 3, new Color(0.18f, 0.4f, 0.95f)),
            C("hair.pink", Slot.Hair, 300, 3, new Color(1f, 0.45f, 0.7f)),
            C("hair.green", Slot.Hair, 300, 4, new Color(0.25f, 0.75f, 0.35f)),
            C("hair.purple", Slot.Hair, 400, 5, new Color(0.55f, 0.28f, 0.9f)),
            // the skin: people's own tones only, all free (wheat, fair, tan, dark)
            C("skin.wheat", Slot.Skin, 0, 0, new Color(0.82f, 0.66f, 0.55f)),
            C("skin.fair", Slot.Skin, 0, 0, new Color(0.95f, 0.79f, 0.69f)),
            C("skin.tan", Slot.Skin, 0, 0, new Color(0.68f, 0.49f, 0.36f)),
            C("skin.dark", Slot.Skin, 0, 0, new Color(0.42f, 0.27f, 0.18f)),
            // the eyes: people's own colours free, a few rarer ones to buy
            C("iris.brown", Slot.Iris, 0, 0, new Color(0.42f, 0.26f, 0.13f)),
            C("iris.hazel", Slot.Iris, 0, 0, new Color(0.56f, 0.42f, 0.2f)),
            C("iris.green", Slot.Iris, 0, 0, new Color(0.3f, 0.56f, 0.3f)),
            C("iris.blue", Slot.Iris, 0, 0, new Color(0.3f, 0.52f, 0.86f)),
            C("iris.grey", Slot.Iris, 0, 0, new Color(0.6f, 0.64f, 0.68f)),
            C("iris.amber", Slot.Iris, 200, 0, new Color(0.86f, 0.56f, 0.16f)),
            C("iris.violet", Slot.Iris, 300, 1, new Color(0.56f, 0.36f, 0.86f)),
            C("iris.red", Slot.Iris, 300, 2, new Color(0.8f, 0.18f, 0.15f)),

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

            // Capes that wave as foi runs (only from the duel chests). Plain ones tied at the neck (the old hero cape is the red one)...
            X("back.cape.sky", Slot.Back, new Color(0.3f, 0.55f, 0.95f)),
            X("back.cape.sun", Slot.Back, new Color(1f, 0.78f, 0.25f)),
            // ...middle ones tied at the neck with a light pattern...
            X("back.cape.night", Slot.Back, new Color(0.14f, 0.18f, 0.42f)),
            X("back.cape.teal", Slot.Back, new Color(0.15f, 0.6f, 0.58f)),
            X("back.cape.wine", Slot.Back, new Color(0.55f, 0.16f, 0.26f)),
            // ...high ones from the shoulders with gold patterns...
            X("back.cape.royal", Slot.Back, new Color(0.45f, 0.2f, 0.62f)),
            X("back.cape.flame", Slot.Back, new Color(0.95f, 0.38f, 0.12f)),
            X("back.cape.frost", Slot.Back, new Color(0.55f, 0.82f, 1f)),
            // ...two capes made as models, sold in the workshop: a plain blue one and a dark blue one with a torn hem...
            C("back.cape.basic", Slot.Back, 2500, 0, new Color(0.15f, 0.45f, 0.75f)),
            C("back.cape.mid", Slot.Back, 7500, 0, new Color(0.18f, 0.16f, 0.5f)),
            // ...and the ranks' own capes and hats: earned in the challenge, never sold.
            K("back.cape.rank3", Slot.Back, 3, new Color(0.18f, 0.42f, 0.22f)),
            K("back.cape.rank6", Slot.Back, 6, new Color(0.18f, 0.42f, 0.22f)),
            K("back.cape.rank9", Slot.Back, 9, new Color(0.18f, 0.42f, 0.22f)),
            K("back.cape.rank12", Slot.Back, 12, new Color(0.45f, 0.06f, 0.12f)),
            K("hat.rank2", Slot.Hat, 2, new Color(0.38f, 0.45f, 0.3f)),
            K("hat.rank5", Slot.Hat, 5, new Color(0.16f, 0.2f, 0.36f)),
            K("hat.rank8", Slot.Hat, 8, new Color(0.7f, 0.12f, 0.16f)),
            K("hat.rank11", Slot.Hat, 11, new Color(0.92f, 0.92f, 0.95f)),
            K("hat.rank12", Slot.Hat, 12, new Color(0.12f, 0.12f, 0.26f)),
        };

        private static Cosmetic C(string id, Slot slot, int price, int world, Color color) =>
            new Cosmetic { id = id, slot = slot, price = price, world = world, color = color };

        private static Cosmetic S(string id, Slot slot, int price, int world, Color color, string set) =>
            new Cosmetic { id = id, slot = slot, price = price, world = world, color = color, set = set };

        private static Cosmetic R(string id, Slot slot, int world, Color color, string set) =>
            new Cosmetic { id = id, slot = slot, price = 0, world = world, color = color, set = set, reward = true };

        private static Cosmetic T(string id, Slot slot, int level, Color color) =>
            new Cosmetic { id = id, slot = slot, price = 0, world = 0, color = color, storyLevel = level };

        private static Cosmetic K(string id, Slot slot, int rank, Color color) =>
            new Cosmetic { id = id, slot = slot, price = 0, world = 0, color = color, rank = rank };

        private static Cosmetic X(string id, Slot slot, Color color) =>
            new Cosmetic { id = id, slot = slot, price = 0, world = 0, color = color, chest = true };

        /// <summary>The duel chests' surprises not yet owned.</summary>
        public static List<Cosmetic> ChestOnly() => All.FindAll(c => c.chest && !Bought(c));

        /// <summary>The world sets in story order (the ids of the first four are older than the world names).</summary>
        public static readonly string[] Sets = { "canal", "forest", "canyon", "ocean", "mountain", "cyber", "cloud", "candy", "galaxy" };

        /// <summary>A whole set bought at once costs this share of its missing pieces.</summary>
        public const float SetDiscount = 0.8f;

        /// <summary>The free paint tube Bip hands over on the first workshop visit.</summary>
        public const string GiftId = "color.mint";

        private static bool Bought(Cosmetic c) => Prefs.GetInt("sb_cos_" + c.id, 0) == 1;

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
            foreach (var c in pieces) Prefs.SetInt("sb_cos_" + c.id, 1);
            Prefs.Save();
            return true;
        }

        public static List<Cosmetic> InSlot(Slot slot) => All.FindAll(c => c.slot == slot);

        public static Cosmetic Find(string id) => All.Find(c => c.id == id);

        /// <summary>A story reward is owned once its level is beaten.</summary>
        public static bool StoryEarned(Cosmetic c) => c.storyLevel > 0 && Progress.Stars(c.storyLevel - 1) > 0;

        /// <summary>A rank reward is worn once the challenge rank reached is that high.</summary>
        public static bool RankEarned(Cosmetic c) => c.rank > 0 && Challenge.BestRank >= c.rank;

        public static bool Owns(Cosmetic c) =>
            c.reward ? SetComplete(c.set) : c.storyLevel > 0 ? StoryEarned(c) : c.rank > 0 ? RankEarned(c) : c.chest ? Bought(c) : c.IsDefault || Bought(c);

        public static Cosmetic Equipped(Slot slot)
        {
            var c = Find(Prefs.GetString("sb_eq_" + slot, ""));
            return c != null && c.slot == slot && Owns(c) ? c : InSlot(slot)[0];
        }

        public static void Equip(Cosmetic c)
        {
            if (!Owns(c)) return;
            Prefs.SetString("sb_eq_" + c.slot, c.id);
            Prefs.Save();
        }

        public static bool TryBuy(Cosmetic c, int reachedWorld)
        {
            if (c.reward || c.storyLevel > 0 || c.rank > 0 || c.chest || Owns(c) || reachedWorld < c.world || !Shop.Spend(c.price)) return false;
            Prefs.SetInt("sb_cos_" + c.id, 1);
            Prefs.Save();
            return true;
        }

        /// <summary>
        /// Once, on the new heads' first start: the old heads are gone (their buyers get their gold back; whoever wore one
        /// wears the man's first head), the old skins too (the light and brown tones become the nearest new ones).
        /// </summary>
        public static void MigrateHeads()
        {
            if (Prefs.GetInt("sb_heads2", 0) == 1) return;
            int refund = 0;
            foreach (var (id, price) in new[] { ("head.2", 600), ("head.3", 600), ("head.4", 700) })
                if (Prefs.GetInt("sb_cos_" + id, 0) == 1) { refund += price; Prefs.DeleteKey("sb_cos_" + id); }
            if (refund > 0) SaveData.Coins += refund;
            string skin = Prefs.GetString("sb_eq_Skin", "");
            if (skin == "skin.light") Prefs.SetString("sb_eq_Skin", "skin.fair");
            else if (skin == "skin.brown" || skin == "skin.olive") Prefs.SetString("sb_eq_Skin", "skin.tan");
            Prefs.SetInt("sb_heads2", 1);
            Prefs.Save();
        }

        /// <summary>A present: owned without paying.</summary>
        public static void Give(string id)
        {
            Prefs.SetInt("sb_cos_" + id, 1);
            Prefs.Save();
        }

        /// <summary>The visible pieces worn (paint, eyes, hat, arms, legs, back, badge, suit) as one short text, for the rival's
        /// screen in a duel.</summary>
        public static string LookText()
        {
            var ids = new List<string>();
            foreach (var s in new[] { Slot.Color, Slot.Eyes, Slot.Hat, Slot.Arms, Slot.Legs, Slot.Back, Slot.Badge, Slot.Suit, Slot.Head, Slot.Hair, Slot.Skin, Slot.Iris })
            {
                var c = Equipped(s);
                if (!c.IsDefault) ids.Add(c.id);
            }
            return string.Join(",", ids);
        }

        /// <summary>An outfit from such a text (unknown pieces are skipped; the rest is the defaults).</summary>
        public static Dictionary<Slot, Cosmetic> OutfitFrom(string look)
        {
            var outfit = new Dictionary<Slot, Cosmetic>();
            foreach (Slot s in System.Enum.GetValues(typeof(Slot))) outfit[s] = InSlot(s)[0];
            if (string.IsNullOrEmpty(look)) return outfit;
            foreach (var id in look.Split(','))
            {
                var c = Find(id.Trim());
                if (c != null && (c.slot != Slot.Step && c.slot != Slot.Trail && c.slot != Slot.Dance)) outfit[c.slot] = c;
            }
            return outfit;
        }

        /// <summary>A made-up outfit for a computer rival (the same for the same name).</summary>
        public static Dictionary<Slot, Cosmetic> OutfitFor(string name)
        {
            var rng = new System.Random((name ?? "").GetHashCode());
            var outfit = OutfitFrom(null);
            foreach (var s in new[] { Slot.Hat, Slot.Back, Slot.Suit, Slot.Arms, Slot.Legs, Slot.Head, Slot.Hair, Slot.Skin, Slot.Iris })
            {
                if (rng.Next(3) == 0) continue;
                var pool = InSlot(s).FindAll(c => !c.reward && c.storyLevel == 0 && c.rank == 0 && !c.chest
                    && (s != Slot.Back || c.id.StartsWith("back.cape")) && !(c.id == "hat.diver" || c.id == "hat.astro" || c.id == "hat.visor" || c.id == "hat.cloud"));
                if (pool.Count == 0) continue;
                outfit[s] = pool[rng.Next(pool.Count)];
            }
            return outfit;
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
