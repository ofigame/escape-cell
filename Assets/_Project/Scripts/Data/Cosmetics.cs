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

        public bool IsDefault => price == 0 && storyLevel == 0;

        /// <summary>A story reward: not sold, it comes with beating this level (1-based; 0 = none).</summary>
        public int storyLevel;
    }

    /// <summary>
    /// Bip's workshop: foi's head, hair, skin and eye colours, badges and dances (and the old robot's eyes, step marks
    /// and trails), bought with coins and worn one per slot. The first entry of every slot is the free default. Looks
    /// only: nothing here makes the robot stronger. Story rewards (the badges) cannot be bought. The hats, armour
    /// paints, capes, arm and leg gear, paints, back pieces and world sets are gone (<see cref="MigrateGarage"/>).
    /// </summary>
    public static class Cosmetics
    {
        public static readonly List<Cosmetic> All = new List<Cosmetic>
        {
            // The slots that are gone (paint, hats, arm and leg gear, back pieces, armour paints: Docs/RutbeKusami_Kaldirilacaklar.md)
            // keep only their plain default, so every slot still has its first entry; none is shown or sold.
            C("color.world", Slot.Color, 0, 0, new Color(0.64f, 0.69f, 0.81f)),
            C("hat.none", Slot.Hat, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("arms.std", Slot.Arms, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("legs.std", Slot.Legs, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("back.none", Slot.Back, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("suit.none", Slot.Suit, 0, 0, new Color(0.74f, 0.76f, 0.81f)),

            C("eyes.world", Slot.Eyes, 0, 0, new Color(0.4f, 1.6f, 2.2f)),
            C("eyes.lime", Slot.Eyes, 200, 0, new Color(0.8f, 2.2f, 0.4f)),
            C("eyes.ruby", Slot.Eyes, 200, 0, new Color(2.4f, 0.4f, 0.5f)),
            C("eyes.determined", Slot.Eyes, 300, 1, new Color(2.4f, 1.1f, 0.3f)),
            C("eyes.gold", Slot.Eyes, 300, 1, new Color(2.4f, 1.8f, 0.4f)),
            C("eyes.violet", Slot.Eyes, 300, 2, new Color(1.6f, 0.6f, 2.4f)),

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

            // foi's own dances (made on its body: Resources/Heroes/Foi/hero_dance0..5); the first is everyone's
            C("dance.spin", Slot.Dance, 0, 0, new Color(0.5f, 0.5f, 0.6f)),
            C("dance.d1", Slot.Dance, 300, 0, new Color(0.4f, 0.8f, 1f)),
            C("dance.d2", Slot.Dance, 400, 1, new Color(1f, 0.6f, 0.3f)),
            C("dance.d3", Slot.Dance, 500, 2, new Color(0.7f, 0.45f, 1f)),
            C("dance.d4", Slot.Dance, 600, 3, new Color(1f, 0.85f, 0.3f)),
            C("dance.d5", Slot.Dance, 700, 4, new Color(0.45f, 1f, 0.6f)),

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

            // What is left of the old world sets (the sets are gone): plain pieces now.
            C("step.leaf", Slot.Step, 300, 2, new Color(0.6f, 1.8f, 0.5f)),
            C("trail.bubbles", Slot.Trail, 400, 8, new Color(0.7f, 1.6f, 2.2f)),
            C("trail.snow", Slot.Trail, 500, 11, new Color(1.8f, 2.1f, 2.4f)),
            C("trail.neon", Slot.Trail, 500, 14, new Color(0.4f, 2.4f, 1.2f)),
            C("trail.sprinkles", Slot.Trail, 400, 18, new Color(2.2f, 1.6f, 0.6f)),
            C("trail.comet", Slot.Trail, 600, 23, new Color(1.6f, 0.9f, 2.4f)),
            C("trail.dust", Slot.Trail, 400, 5, new Color(2.2f, 1.4f, 0.7f)),
            C("eyes.sleepy", Slot.Eyes, 400, 0, new Color(1.4f, 1.2f, 2.2f)),
            C("eyes.happy", Slot.Eyes, 400, 2, new Color(0.5f, 2.2f, 1.2f)),
            C("eyes.star", Slot.Eyes, 400, 11, new Color(2.4f, 2f, 0.5f)),
            C("eyes.matrix", Slot.Eyes, 400, 14, new Color(0.5f, 2.6f, 0.6f)),
            C("eyes.heart", Slot.Eyes, 400, 17, new Color(2.4f, 0.6f, 1.2f)),
            C("dance.swim", Slot.Dance, 500, 8, new Color(0.4f, 0.75f, 1f)),
            C("dance.sugar", Slot.Dance, 500, 18, new Color(1f, 0.7f, 0.9f)),

            // Story rewards: earned, never sold.
            T("badge.press", Slot.Badge, 20, new Color(0.75f, 0.78f, 0.85f)),
            T("badge.cl1", Slot.Badge, 40, new Color(0.35f, 0.38f, 0.5f)),
            T("badge.lumi", Slot.Badge, 170, new Color(1f, 0.82f, 0.3f)),
            T("badge.raven", Slot.Badge, 211, new Color(0.16f, 0.15f, 0.24f)),
            T("badge.heart", Slot.Badge, 250, new Color(1f, 0.8f, 0.3f)),
        };

        private static Cosmetic C(string id, Slot slot, int price, int world, Color color) =>
            new Cosmetic { id = id, slot = slot, price = price, world = world, color = color };

        private static Cosmetic T(string id, Slot slot, int level, Color color) =>
            new Cosmetic { id = id, slot = slot, price = 0, world = 0, color = color, storyLevel = level };

        private static bool Bought(Cosmetic c) => Prefs.GetInt("sb_cos_" + c.id, 0) == 1;

        public static List<Cosmetic> InSlot(Slot slot) => All.FindAll(c => c.slot == slot);

        public static Cosmetic Find(string id) => All.Find(c => c.id == id);

        /// <summary>A story reward is owned once its level is beaten.</summary>
        public static bool StoryEarned(Cosmetic c) => c.storyLevel > 0 && Progress.Stars(c.storyLevel - 1) > 0;

        public static bool Owns(Cosmetic c) => c.storyLevel > 0 ? StoryEarned(c) : c.IsDefault || Bought(c);

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
            if (c.storyLevel > 0 || Owns(c) || reachedWorld < c.world || !Shop.Spend(c.price)) return false;
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

        /// <summary>
        /// Once, on the new dances' first start: the old made-up dances (jump, wobble, backflip) are gone, their buyers get
        /// their gold back; whoever wore a dance that is no more dances the first one.
        /// </summary>
        public static void MigrateDances()
        {
            if (Prefs.GetInt("sb_dances2", 0) == 1) return;
            int refund = 0;
            foreach (var (id, price) in new[] { ("dance.jump", 300), ("dance.wobble", 400), ("dance.flip", 500) })
                if (Prefs.GetInt("sb_cos_" + id, 0) == 1) { refund += price; Prefs.DeleteKey("sb_cos_" + id); }
            if (refund > 0) SaveData.Coins += refund;
            string worn = Prefs.GetString("sb_eq_Dance", "");
            if (worn != "" && !worn.StartsWith("dance.d")) Prefs.SetString("sb_eq_Dance", "dance.spin");
            Prefs.SetInt("sb_dances2", 1);
            Prefs.Save();
        }

        private const string RefundDoneKey = "sb_cosmetic_refund_done", RefundGoldKey = "sb_cosmetic_refund_gold", RefundCoresKey = "sb_cosmetic_refund_cores";

        /// <summary>
        /// Once, on the renewed garage's first start (Docs/RutbeKusami_Kaldirilacaklar.md, section 3): the hats, armour
        /// paints, capes, arm and leg gear, paints and the old robot's back pieces are gone. Whatever was bought is paid back
        /// in full (the prices kept here: the rows are gone from the catalog), a chest's own piece with 1,000 gold and 5
        /// core parts, and so is a chest's outfit still waiting to be given. The old keys stay (ignored); the flag goes to
        /// the cloud copy with the rest, so a copy brought back never pays twice. What was paid is kept for the menu's card
        /// (<see cref="TakeRefundNote"/>).
        /// </summary>
        public static void MigrateGarage()
        {
            if (Prefs.GetInt(RefundDoneKey, 0) == 1) return;
            // bought with gold: the price paid (bought, or given by a chest, the season road, the general or a pack: all the same key)
            var bought = new (string id, int price)[]
            {
                // hats (sold, and the world sets' ones)
                ("hat.antenna", 200), ("hat.party", 200), ("hat.cap", 250), ("hat.antenna2", 300), ("hat.phones", 300),
                ("hat.top", 400), ("hat.pirate", 400), ("hat.crown", 600), ("hat.hood", 500), ("hat.hood.black", 600),
                ("hat.hood.crimson", 700), ("hat.hood.forest", 700),
                ("hat.cable", 300), ("hat.leaf", 300), ("hat.diver", 600), ("hat.icecrown", 500), ("hat.visor", 500),
                ("hat.cloud", 500), ("hat.lollipop", 400), ("hat.astro", 600),
                // armour paints
                ("suit.camo", 400), ("suit.tactical", 800), ("suit.camo.desert", 500), ("suit.camo.urban", 600),
                ("suit.tactical.forest", 900), ("suit.camo.snow", 600), ("suit.tactical.desert", 900), ("suit.tactical.urban", 900),
                ("suit.shadow", 1500),
                // the two capes made as models
                ("back.cape.basic", 2500), ("back.cape.mid", 7500),
                // arm gear
                ("arms.brush", 300), ("arms.spring", 400), ("arms.crystal", 600), ("arms.shoulder", 450), ("arms.claw", 500),
                // paints (color.mint was Bip's present: not paid back)
                ("color.coral", 100), ("color.sun", 100), ("color.ocean", 100), ("color.sunset", 400), ("color.grape", 400),
                ("color.night", 400), ("color.gold", 3000), ("color.canyon", 100), ("color.candy", 400), ("color.cosmic", 400),
                // leg gear
                ("legs.knee", 250), ("legs.spring", 400), ("legs.tracks", 300), ("legs.wheel", 400), ("legs.snow", 500), ("legs.jet", 600),
                // the old robot's back pieces
                ("back.jetpack", 500), ("back.wings", 600), ("back.shell", 400), ("back.rocket", 600), ("back.rustbox", 200),
                ("back.leafbag", 300), ("back.tank", 500), ("back.cyberwings", 600), ("back.balloon", 500),
            };
            // only from the chests: 1,000 gold and 5 core parts each
            var chestOnly = new[]
            {
                "back.cape", "back.cape.sky", "back.cape.sun", "back.cape.night", "back.cape.teal", "back.cape.wine",
                "back.cape.royal", "back.cape.flame", "back.cape.frost",
                "suit.elite", "suit.ghost", "suit.ranger", "suit.crimson", "suit.fox",
            };
            const int ChestGold = 1000, ChestCores = 5;
            int gold = 0, cores = 0;
            foreach (var (id, price) in bought)
                if (Prefs.GetInt("sb_cos_" + id, 0) == 1) gold += price;
            foreach (var id in chestOnly)
                if (Prefs.GetInt("sb_cos_" + id, 0) == 1) { gold += ChestGold; cores += ChestCores; }
            // a chest's outfit drawn but not given yet (the game closed while it was shown): paid as a chest's own piece
            string pending = Prefs.GetString(DuelChests.PendingKey, "");
            if (pending.StartsWith("o:"))
            {
                gold += ChestGold; cores += ChestCores;
                Prefs.SetString(DuelChests.PendingKey, "");
            }
            if (gold > 0) SaveData.Coins += gold;
            if (cores > 0) CoreParts.Add(cores);
            if (gold > 0 || cores > 0)
            {
                Prefs.SetInt(RefundGoldKey, gold);
                Prefs.SetInt(RefundCoresKey, cores);
            }
            Prefs.SetInt(RefundDoneKey, 1);
            Prefs.Save();
        }

        /// <summary>What <see cref="MigrateGarage"/> paid back and has not been told yet (then forgotten): gold and core parts.</summary>
        public static (int gold, int cores) TakeRefundNote()
        {
            int gold = Prefs.GetInt(RefundGoldKey, 0), cores = Prefs.GetInt(RefundCoresKey, 0);
            if (gold <= 0 && cores <= 0) return (0, 0);
            Prefs.SetInt(RefundGoldKey, 0);
            Prefs.SetInt(RefundCoresKey, 0);
            Prefs.Save();
            return (gold, cores);
        }

        /// <summary>A present: owned without paying.</summary>
        public static void Give(string id)
        {
            Prefs.SetInt("sb_cos_" + id, 1);
            Prefs.Save();
        }

        /// <summary>The visible pieces worn (eyes, badge, head, hair, skin, eye colour) as one short text, for the rival's
        /// screen in a duel.</summary>
        public static string LookText()
        {
            var ids = new List<string>();
            foreach (var s in new[] { Slot.Eyes, Slot.Badge, Slot.Head, Slot.Hair, Slot.Skin, Slot.Iris })
            {
                var c = Equipped(s);
                if (!c.IsDefault) ids.Add(c.id);
            }
            if (BattleWear.Shown > 0) ids.Add("wear" + BattleWear.Shown); // (the battle wear the rival sees: rust as wear; older games skip it)
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
            foreach (var s in new[] { Slot.Head, Slot.Hair, Slot.Skin, Slot.Iris })
            {
                if (rng.Next(3) == 0) continue;
                var pool = InSlot(s).FindAll(c => c.storyLevel == 0);
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
