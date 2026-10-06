using System.Collections.Generic;

namespace SquashBot.Data
{
    /// <summary>
    /// Every building piece, world by world. Each world adds a themed set to the tray: normal pieces for coins when
    /// the world opens, and one special piece that also needs stars from that world. Production buildings come
    /// with the worlds that open them. Names are Loc keys "city.&lt;id&gt;".
    /// </summary>
    public static class CityCatalog
    {
        public static readonly CityPiece[] All = Build();

        private static readonly Dictionary<string, CityPiece> byId = Index();

        public static CityPiece Find(string id) => id != null && byId.TryGetValue(id, out var p) ? p : null;

        /// <summary>Stars a world's special piece asks for: a little more on later floors (out of 30 per world).</summary>
        public static int SpecialStars(int world) => 15 + world / 2;

        private static CityPiece[] Build()
        {
            var list = new List<CityPiece>();
            const CityCategory B = CityCategory.Build, D = CityCategory.Decor, G = CityCategory.Garden;

            void Add(string id, CityCategory cat, CityRole role, int w, int h, int price, int world, bool glow = false, CityPerk perk = CityPerk.None)
                => list.Add(new CityPiece { id = id, category = cat, role = role, w = w, h = h, price = price, world = world, glow = glow, perk = perk });
            void Special(string id, int w, int h, int price, int world, bool glow = false)
                => list.Add(new CityPiece { id = id, category = B, role = CityRole.Special, w = w, h = h, price = price, world = world, stars = SpecialStars(world), glow = glow });

            // 1 Lavender space: the basics.
            Add("house", B, CityRole.Home, 2, 2, 150, 0);
            Add("wall", D, CityRole.Decor, 1, 1, 10, 0);
            Add("floor", D, CityRole.Decor, 1, 1, 10, 0);
            Add("door", D, CityRole.Decor, 1, 1, 20, 0);
            Add("window", D, CityRole.Decor, 1, 1, 25, 0);
            Add("bush", G, CityRole.Decor, 1, 1, 20, 0);
            Add("flowers", G, CityRole.Decor, 1, 1, 20, 0);
            Special("antenna", 1, 1, 600, 0, glow: true);
            // 2 Sunset: the gold mine opens.
            Add("mine", B, CityRole.Producer, 2, 2, 500, 1, perk: CityPerk.Mine);
            Add("porch", D, CityRole.Decor, 2, 1, 40, 1);
            Add("hammock", G, CityRole.Decor, 2, 1, 45, 1);
            Add("umbrella", D, CityRole.Decor, 1, 1, 30, 1);
            Special("sunsetTower", 1, 1, 700, 1, glow: true);
            // 3 Ice: the workshop.
            Add("workshop", B, CityRole.Producer, 2, 2, 600, 2, perk: CityPerk.Workshop);
            Add("igloo", B, CityRole.Home, 2, 2, 200, 2);
            Add("iceBlock", D, CityRole.Decor, 1, 1, 15, 2);
            Special("snowman", 1, 1, 700, 2);
            // 4 Neon night: the training ground.
            Add("training", B, CityRole.Producer, 2, 2, 700, 3, perk: CityPerk.Training);
            Add("neonSign", D, CityRole.Decor, 1, 1, 50, 3, glow: true);
            Add("lightPath", D, CityRole.Decor, 1, 1, 20, 3, glow: true);
            Special("lightArch", 2, 1, 800, 3, glow: true);
            // 5 Lava core.
            Add("magmaLamp", D, CityRole.Decor, 1, 1, 50, 4, glow: true);
            Add("stoneOven", D, CityRole.Decor, 1, 1, 60, 4, glow: true);
            Special("lavaFall", 2, 2, 900, 4, glow: true);
            // 6 Mint forest: the town square.
            Add("square", B, CityRole.Producer, 2, 2, 900, 5, perk: CityPerk.Square);
            Add("tree", G, CityRole.Decor, 1, 1, 30, 5);
            Add("fence", D, CityRole.Decor, 1, 1, 15, 5);
            Add("flowerBed", G, CityRole.Decor, 1, 1, 25, 5);
            Special("treeHouse", 2, 2, 900, 5);
            // 7 Golden desert.
            Add("sandHouse", B, CityRole.Home, 2, 2, 250, 6);
            Add("palm", G, CityRole.Decor, 1, 1, 35, 6);
            Special("desertFountain", 2, 2, 1000, 6);
            // 8 Deep ocean: the repair shop.
            Add("repairShop", B, CityRole.Producer, 2, 2, 1000, 7, perk: CityPerk.RepairShop);
            Add("aquarium", D, CityRole.Decor, 2, 1, 60, 7, glow: true);
            Add("pier", D, CityRole.Decor, 2, 1, 30, 7);
            Special("lighthouse", 2, 3, 1200, 7, glow: true);
            // 9 Candy pop.
            Add("candyHouse", B, CityRole.Home, 2, 2, 300, 8);
            Add("lollipop", G, CityRole.Decor, 1, 1, 30, 8);
            Special("lollipopGarden", 2, 2, 1100, 8);
            // 10 Toxic lab.
            Add("lab", B, CityRole.Home, 2, 2, 320, 9, glow: true);
            Add("tubeRack", D, CityRole.Decor, 1, 1, 40, 9, glow: true);
            Special("bubbleTube", 1, 1, 1100, 9, glow: true);
            // 11 Midnight.
            Add("streetLamp", D, CityRole.Decor, 1, 1, 35, 10, glow: true);
            Add("bench", D, CityRole.Decor, 1, 1, 30, 10);
            Special("watchTower", 2, 2, 1200, 10, glow: true);
            // 12 Aurora.
            Add("polarTent", B, CityRole.Home, 2, 2, 340, 11);
            Add("snowPine", G, CityRole.Decor, 1, 1, 35, 11);
            Special("auroraDome", 2, 2, 1300, 11, glow: true);
            // 13 Storm.
            Add("windVane", D, CityRole.Decor, 1, 1, 45, 12);
            Add("rainGarden", G, CityRole.Decor, 1, 1, 40, 12);
            Special("lightningRod", 1, 1, 1200, 12, glow: true);
            // 14 Cyber grid.
            Add("hologram", B, CityRole.Home, 2, 2, 380, 13, glow: true);
            Add("dataPost", D, CityRole.Decor, 1, 1, 45, 13, glow: true);
            Special("dataTower", 1, 1, 1300, 13, glow: true);
            // 15 Galaxy core.
            Add("planetStatue", D, CityRole.Decor, 1, 1, 60, 14, glow: true);
            Add("starFlower", G, CityRole.Decor, 1, 1, 45, 14, glow: true);
            Special("dish", 2, 2, 1400, 14);
            // 16 Crystal cave.
            Add("crystal", D, CityRole.Decor, 1, 1, 55, 15, glow: true);
            Add("lightStone", G, CityRole.Decor, 1, 1, 40, 15, glow: true);
            Special("crystalPalace", 2, 2, 1400, 15, glow: true);
            // 17 Festival.
            Add("lanterns", D, CityRole.Decor, 2, 1, 50, 16, glow: true);
            Add("balloonStand", D, CityRole.Decor, 1, 1, 60, 16);
            Special("fireworkTower", 1, 1, 1400, 16, glow: true);
            // 18 Factory.
            Add("gear", D, CityRole.Decor, 1, 1, 45, 17);
            Add("pipe", D, CityRole.Decor, 1, 1, 30, 17);
            Special("steamTower", 2, 2, 1450, 17);
            // 19 Funfair.
            Add("ticketBooth", D, CityRole.Decor, 1, 1, 60, 18, glow: true);
            Add("carousel", B, CityRole.Home, 2, 2, 400, 18, glow: true);
            Special("ferrisWheel", 2, 3, 1500, 18, glow: true);
            // 20 Roof.
            Add("roofGarden", G, CityRole.Decor, 2, 2, 60, 19);
            Add("solar", D, CityRole.Decor, 1, 1, 50, 19);
            Special("goldStatue", 2, 2, 1500, 19, glow: true);
            return list.ToArray();
        }

        private static Dictionary<string, CityPiece> Index()
        {
            var d = new Dictionary<string, CityPiece>();
            foreach (var p in All) d[p.id] = p;
            return d;
        }
    }
}
