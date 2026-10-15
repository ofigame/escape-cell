using UnityEngine;

namespace SquashBot.OpenWorld
{
    /// <summary>Everything that goes into the barn: crops, fruit and the animals' produce.</summary>
    public enum Item
    {
        Wheat, Corn, Carrot, Tomato, Sunflower, CactusFruit, Pumpkin,
        Apple, Orange, Olive, Walnut,
        Egg, Milk, Wool, GoatMilk, Honey,
    }

    public enum Crop { Wheat, Corn, Carrot, Tomato, Sunflower, CactusFruit, Pumpkin }

    public enum TreeKind { Apple, Orange, Olive, Walnut }

    /// <summary>The farm buildings: the coop, barns and pen keep animals; the hive is its own.</summary>
    public enum BuildingKind { Coop, CowBarn, Pen, GoatBarn, Hive }

    public struct CropDef
    {
        public Crop crop; public int seed; public int seconds; public int yield; public int price; public int house; public Item item; public Color colour;
    }

    public struct TreeDef
    {
        public TreeKind kind; public int sapling; public int growSeconds; public int fruitSeconds; public int yield; public int price; public int house; public Item item; public Color colour;
    }

    public struct BuildingDef
    {
        public BuildingKind kind; public int price; public int animalPrice; public int capacity; public Item feed; public int feedPer; public int seconds; public Item product; public int house; public Color colour;
    }

    public struct HouseDef
    {
        public int price; public int land; public int plots; public int buildings; public int trees;
    }

    /// <summary>
    /// The open world's tables (Docs/AcikDunya_Tasarim.md): crops, orchard trees, animal buildings, house levels, barn
    /// sizes and the co-op's prices. Times are real seconds.
    /// </summary>
    public static class OwData
    {
        public static readonly CropDef[] Crops =
        {
            new CropDef { crop = Crop.Wheat, seed = 1, seconds = 120, yield = 3, price = 1, house = 0, item = Item.Wheat, colour = new Color(0.95f, 0.8f, 0.35f) },
            new CropDef { crop = Crop.Corn, seed = 3, seconds = 600, yield = 3, price = 3, house = 0, item = Item.Corn, colour = new Color(1f, 0.85f, 0.2f) },
            new CropDef { crop = Crop.Carrot, seed = 6, seconds = 1800, yield = 3, price = 6, house = 1, item = Item.Carrot, colour = new Color(1f, 0.5f, 0.15f) },
            new CropDef { crop = Crop.Tomato, seed = 12, seconds = 3600, yield = 3, price = 12, house = 1, item = Item.Tomato, colour = new Color(0.9f, 0.18f, 0.15f) },
            new CropDef { crop = Crop.Sunflower, seed = 25, seconds = 10800, yield = 3, price = 28, house = 2, item = Item.Sunflower, colour = new Color(1f, 0.85f, 0.1f) },
            new CropDef { crop = Crop.CactusFruit, seed = 50, seconds = 28800, yield = 4, price = 45, house = 3, item = Item.CactusFruit, colour = new Color(0.85f, 0.25f, 0.5f) },
            new CropDef { crop = Crop.Pumpkin, seed = 90, seconds = 43200, yield = 4, price = 80, house = 4, item = Item.Pumpkin, colour = new Color(0.95f, 0.35f, 0.15f) },
        };

        public static readonly TreeDef[] Trees =
        {
            new TreeDef { kind = TreeKind.Apple, sapling = 60, growSeconds = 21600, fruitSeconds = 10800, yield = 4, price = 9, house = 1, item = Item.Apple, colour = new Color(0.9f, 0.15f, 0.15f) },
            new TreeDef { kind = TreeKind.Orange, sapling = 120, growSeconds = 36000, fruitSeconds = 18000, yield = 4, price = 16, house = 2, item = Item.Orange, colour = new Color(1f, 0.55f, 0.1f) },
            new TreeDef { kind = TreeKind.Olive, sapling = 250, growSeconds = 64800, fruitSeconds = 28800, yield = 6, price = 22, house = 3, item = Item.Olive, colour = new Color(0.4f, 0.5f, 0.2f) },
            new TreeDef { kind = TreeKind.Walnut, sapling = 400, growSeconds = 86400, fruitSeconds = 43200, yield = 6, price = 35, house = 4, item = Item.Walnut, colour = new Color(0.55f, 0.4f, 0.25f) },
        };

        public static readonly BuildingDef[] Buildings =
        {
            new BuildingDef { kind = BuildingKind.Coop, price = 150, animalPrice = 40, capacity = 6, feed = Item.Wheat, feedPer = 1, seconds = 1200, product = Item.Egg, house = 0, colour = new Color(0.95f, 0.85f, 0.6f) },
            new BuildingDef { kind = BuildingKind.CowBarn, price = 600, animalPrice = 250, capacity = 4, feed = Item.Corn, feedPer = 2, seconds = 3600, product = Item.Milk, house = 1, colour = new Color(0.72f, 0.2f, 0.15f) },
            new BuildingDef { kind = BuildingKind.Pen, price = 900, animalPrice = 400, capacity = 4, feed = Item.Carrot, feedPer = 2, seconds = 10800, product = Item.Wool, house = 2, colour = new Color(0.6f, 0.45f, 0.3f) },
            new BuildingDef { kind = BuildingKind.GoatBarn, price = 900, animalPrice = 600, capacity = 4, feed = Item.Tomato, feedPer = 1, seconds = 7200, product = Item.GoatMilk, house = 2, colour = new Color(0.55f, 0.6f, 0.7f) },
            new BuildingDef { kind = BuildingKind.Hive, price = 800, animalPrice = 0, capacity = 1, feed = Item.Wheat, feedPer = 0, seconds = 14400, product = Item.Honey, house = 3, colour = new Color(1f, 0.75f, 0.2f) },
        };

        /// <summary>Tent, hut, house, farmhouse, manor: what each costs, how wide its land is and what it holds.</summary>
        public static readonly HouseDef[] Houses =
        {
            new HouseDef { price = 0, land = 12, plots = 4, buildings = 1, trees = 0 },
            new HouseDef { price = 300, land = 16, plots = 8, buildings = 2, trees = 3 },
            new HouseDef { price = 1500, land = 20, plots = 14, buildings = 3, trees = 6 },
            new HouseDef { price = 6000, land = 26, plots = 22, buildings = 4, trees = 10 },
            new HouseDef { price = 20000, land = 32, plots = 32, buildings = 5, trees = 16 },
        };

        public static readonly int[] BarnSizes = { 50, 120, 250, 500 };
        public static readonly int[] BarnPrices = { 0, 400, 1500, 5000 };

        /// <summary>The co-op's base price of each item (crops and fruit as above, the animals' produce).</summary>
        public static int Price(Item i)
        {
            foreach (var c in Crops) if (c.item == i) return c.price;
            foreach (var t in Trees) if (t.item == i) return t.price;
            switch (i)
            {
                case Item.Egg: return 4;
                case Item.Milk: return 22;
                case Item.Wool: return 50;
                case Item.GoatMilk: return 45;
                case Item.Honey: return 70;
                default: return 1;
            }
        }

        public static Color ColourOf(Item i)
        {
            foreach (var c in Crops) if (c.item == i) return c.colour;
            foreach (var t in Trees) if (t.item == i) return t.colour;
            switch (i)
            {
                case Item.Egg: return new Color(0.98f, 0.95f, 0.88f);
                case Item.Milk: return Color.white;
                case Item.Wool: return new Color(0.92f, 0.9f, 0.85f);
                case Item.GoatMilk: return new Color(0.95f, 0.95f, 0.9f);
                case Item.Honey: return new Color(1f, 0.72f, 0.15f);
                default: return Color.white;
            }
        }

        public static CropDef Of(Crop c) => Crops[(int)c];
        public static TreeDef Of(TreeKind t) => Trees[(int)t];
        public static BuildingDef Of(BuildingKind b) => Buildings[(int)b];

        /// <summary>Gold → money: the first entry 10 to 1 (the escape's gold), later gold a quarter of that (40 to 1).</summary>
        public const int FirstRate = 10, LaterRate = 40;

        /// <summary>Soldiers: what one costs, and how many a group may have (more with a bigger house).</summary>
        public const int SoldierPrice = 50;
    }
}
