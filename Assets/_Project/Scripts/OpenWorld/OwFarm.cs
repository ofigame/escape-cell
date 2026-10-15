using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.OpenWorld
{
    public enum TargetKind { None, Plot, Tree, Building, House, Barn, Coop, Empty, Defence }

    /// <summary>Something foi can act on: what it is, which one, and where.</summary>
    public struct Target
    {
        public TargetKind kind;
        public int index;
        public Vector3 at;
        public Vector2Int cell;
    }

    /// <summary>
    /// The player's farm drawn from the save: the house (tent → manor), the barn, a fence round the land, the plots with
    /// their crops growing in real time, the orchard trees with fruit when ready, the animal buildings with their
    /// animals. Cells are 2 units, fixed round the farm's middle, so the land can grow without moving anything.
    /// </summary>
    public class OwFarm : MonoBehaviour
    {
        public const float Cell = 2f;

        private OwWorld world;
        private OwSave save;
        private Transform root;
        private readonly List<Transform> plotCrops = new List<Transform>();
        private readonly List<Transform> treeFruit = new List<Transform>();
        private readonly List<Transform> buildingReady = new List<Transform>();
        private readonly List<Material> mats = new List<Material>();
        private readonly Dictionary<int, Material> cropMats = new Dictionary<int, Material>();
        private Material sprout, ready, soil, dirt, white, black, pink, brown, wool, bee;
        private float tick;

        public static OwFarm Create(Transform parent, OwWorld world, OwSave save)
        {
            var go = new GameObject("OwFarm");
            go.transform.SetParent(parent, false);
            go.transform.position = OwWorld.Origin;
            var f = go.AddComponent<OwFarm>();
            f.world = world;
            f.save = save;
            f.Materials();
            f.Rebuild();
            return f;
        }

        private Material M(Color c, float glow = 0f) { var m = MaterialFactory.Create(c, c * glow); mats.Add(m); return m; }

        private void Materials()
        {
            sprout = M(new Color(0.45f, 0.78f, 0.3f));
            ready = M(new Color(1f, 0.9f, 0.4f), 1.6f);
            soil = M(new Color(0.42f, 0.27f, 0.16f));
            dirt = M(new Color(0.55f, 0.46f, 0.3f));
            white = M(new Color(0.96f, 0.95f, 0.92f));
            black = M(new Color(0.15f, 0.13f, 0.13f));
            pink = M(new Color(1f, 0.6f, 0.6f));
            brown = M(new Color(0.55f, 0.38f, 0.25f));
            wool = M(new Color(0.92f, 0.9f, 0.84f));
            bee = M(new Color(1f, 0.8f, 0.2f));
        }

        private Material CropMat(int i)
        {
            if (!cropMats.TryGetValue(i, out var m)) cropMats[i] = m = M(OwData.ColourOf((Item)i));
            return m;
        }

        private void OnDestroy() { foreach (var m in mats) if (m != null) Destroy(m); }

        // ---------- Cells ----------

        public int HalfCells => save.House.land / 4;

        public static Vector2 CellCentre(Vector2Int c) => OwWorld.FarmCentre + new Vector2((c.x + 0.5f) * Cell, (c.y + 0.5f) * Cell);

        public bool OnLand(Vector2Int c) => c.x >= -HalfCells && c.x < HalfCells && c.y >= -HalfCells && c.y < HalfCells;

        public static Vector2Int CellAt(Vector2 p) => new Vector2Int(Mathf.FloorToInt((p.x - OwWorld.FarmCentre.x) / Cell), Mathf.FloorToInt((p.y - OwWorld.FarmCentre.y) / Cell));

        /// <summary>The cells a building takes (2 × 2 from its corner cell).</summary>
        public static IEnumerable<Vector2Int> BuildingCells(int x, int z)
        {
            for (int dx = 0; dx < 2; dx++) for (int dz = 0; dz < 2; dz++) yield return new Vector2Int(x + dx, z + dz);
        }

        public bool Free(Vector2Int c)
        {
            if (!OnLand(c)) return false;
            foreach (var p in save.plots) if (p.x == c.x && p.z == c.y) return false;
            foreach (var t in save.trees) if (t.x == c.x && t.z == c.y) return false;
            foreach (var b in save.buildings) foreach (var bc in BuildingCells(b.x, b.z)) if (bc == c) return false;
            return true;
        }

        public bool FreeForBuilding(Vector2Int c)
        {
            foreach (var bc in BuildingCells(c.x, c.y)) if (!Free(bc)) return false;
            return true;
        }

        public Vector2 HouseAt => OwWorld.FarmCentre + new Vector2(-3f, save.House.land * 0.5f + 5f);
        public Vector2 BarnAt => OwWorld.FarmCentre + new Vector2(6f, save.House.land * 0.5f + 5f);

        // ---------- Drawing ----------

        public void Rebuild()
        {
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("Farm").transform;
            root.SetParent(transform, false);
            plotCrops.Clear();
            treeFruit.Clear();
            buildingReady.Clear();
            int land = save.House.land;
            var c = OwWorld.FarmCentre;
            float y = OwWorld.Height(c.x, c.y);
            // The land: tilled earth inside a fence.
            Shapes.Rounded("Land", root, new Vector3(c.x, y + 0.02f, c.y), new Vector3(land, 0.08f, land), 0.05f, dirt);
            for (int i = 0; i <= land; i += 2)
                foreach (var (px, pz) in new[] { (c.x - land * 0.5f + i, c.y - land * 0.5f - 0.6f), (c.x - land * 0.5f + i, c.y + land * 0.5f + 0.6f), (c.x - land * 0.5f - 0.6f, c.y - land * 0.5f + i), (c.x + land * 0.5f + 0.6f, c.y - land * 0.5f + i) })
                    Shapes.Rounded("Post", root, new Vector3(px, y + 0.45f, pz), new Vector3(0.15f, 0.9f, 0.15f), 0.03f, world.Wood);
            foreach (var (px, pz, sx, sz) in new[] { (c.x, c.y - land * 0.5f - 0.6f, land + 1.2f, 0.1f), (c.x, c.y + land * 0.5f + 0.6f, land + 1.2f, 0.1f), (c.x - land * 0.5f - 0.6f, c.y, 0.1f, land + 1.2f), (c.x + land * 0.5f + 0.6f, c.y, 0.1f, land + 1.2f) })
                Shapes.Rounded("Rail", root, new Vector3(px, y + 0.7f, pz), new Vector3(sx, 0.1f, sz), 0.03f, world.Wood);

            // The house by level, the barn beside it.
            var h = HouseAt;
            var house = new GameObject("Home").transform;
            house.SetParent(root, false);
            house.localPosition = new Vector3(h.x, y, h.y);
            house.localRotation = Quaternion.Euler(0f, 180f, 0f);
            switch (save.house)
            {
                case 0:
                    // A tent: two leaning canvas sheets and a pole.
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Canvas", house, new Vector3(s * 0.8f, 1f, 0f), new Vector3(0.1f, 2.4f, 3f), 0.03f, world.Wall).transform.localRotation = Quaternion.Euler(0f, 0f, s * 38f);
                    Shapes.Rounded("Pole", house, new Vector3(0f, 1f, -1.6f), new Vector3(0.1f, 2f, 0.1f), 0.03f, world.Wood);
                    break;
                default:
                    world.HouseModel(house, Vector3.zero, 0.8f + save.house * 0.25f, world.Wall, world.Roof);
                    if (save.house >= 4) world.HouseModel(house, new Vector3(4.8f, 0f, 0.6f), 1.2f, world.Wall, world.Roof);
                    break;
            }
            var b = BarnAt;
            var barn = new GameObject("Barn").transform;
            barn.SetParent(root, false);
            barn.localPosition = new Vector3(b.x, y, b.y);
            float bs = 0.9f + save.barn * 0.15f;
            Shapes.Rounded("Body", barn, new Vector3(0f, 1.5f * bs, 0f), new Vector3(3.6f, 3f, 3.6f) * bs, 0.05f, world.BarnRed);
            Shapes.Rounded("Roof", barn, new Vector3(0f, 3.2f * bs, 0f), new Vector3(4f, 0.5f, 4f) * bs, 0.05f, world.Wood);
            Shapes.Rounded("Door", barn, new Vector3(0f, 1f * bs, -1.82f * bs), new Vector3(1.6f, 2f, 0.06f) * bs, 0.03f, world.Wall);

            // Plots.
            foreach (var p in save.plots)
            {
                var pc = CellCentre(new Vector2Int(p.x, p.z));
                var plot = new GameObject("Plot").transform;
                plot.SetParent(root, false);
                plot.localPosition = new Vector3(pc.x, y + 0.06f, pc.y);
                Shapes.Rounded("Soil", plot, new Vector3(0f, 0.05f, 0f), new Vector3(1.8f, 0.12f, 1.8f), 0.04f, soil);
                var crop = new GameObject("Crop").transform;
                crop.SetParent(plot, false);
                for (int i = 0; i < 4; i++)
                    Shapes.Primitive(PrimitiveType.Sphere, "Plant", crop, new Vector3((i % 2 - 0.5f) * 0.8f, 0.3f, (i / 2 - 0.5f) * 0.8f), new Vector3(0.5f, 0.6f, 0.5f), sprout);
                plotCrops.Add(crop);
            }
            // Trees.
            foreach (var t in save.trees)
            {
                var tc = CellCentre(new Vector2Int(t.x, t.z));
                var tree = new GameObject("Tree").transform;
                tree.SetParent(root, false);
                tree.localPosition = new Vector3(tc.x, y, tc.y);
                Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 0.7f, 0f), new Vector3(0.28f, 0.7f, 0.28f), world.Wood);
                Shapes.Primitive(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0f, 1.9f, 0f), Vector3.one * 1.7f, world.Leaf);
                var fruit = new GameObject("Fruit").transform;
                fruit.SetParent(tree, false);
                var fm = CropMat((int)OwData.Of((TreeKind)t.kind).item);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    Shapes.Primitive(PrimitiveType.Sphere, "Fruit", fruit, new Vector3(Mathf.Cos(a) * 0.75f, 1.7f + (i % 2) * 0.4f, Mathf.Sin(a) * 0.75f), Vector3.one * 0.26f, fm);
                }
                treeFruit.Add(fruit);
            }
            // Defences, where foi raised them (a broken one lies in rubble until it is repaired).
            defenceRoots.Clear();
            foreach (var d in save.defences)
            {
                var dt = new GameObject("Defence").transform;
                dt.SetParent(root, false);
                dt.localPosition = new Vector3(d.x, OwWorld.Height(d.x, d.z), d.z);
                dt.localRotation = Quaternion.Euler(0f, d.rot, 0f);
                var whole = new GameObject("Whole").transform;
                whole.SetParent(dt, false);
                DefenceModel(whole, (DefenceKind)d.kind, d.level);
                var rubble = new GameObject("Rubble").transform;
                rubble.SetParent(dt, false);
                for (int i = 0; i < 4; i++)
                    Shapes.Rounded("Stone", rubble, new Vector3((i % 2 - 0.5f) * 0.9f, 0.18f, (i / 2 - 0.5f) * 0.7f), new Vector3(0.6f, 0.36f, 0.5f), 0.06f, world.Dark)
                        .transform.localRotation = Quaternion.Euler(0f, i * 37f, i * 9f);
                whole.gameObject.SetActive(d.hp > 0);
                rubble.gameObject.SetActive(d.hp <= 0);
                defenceRoots.Add(dt);
            }

            // Animal buildings.
            foreach (var bd in save.buildings)
            {
                var def = OwData.Of((BuildingKind)bd.kind);
                var bc = CellCentre(new Vector2Int(bd.x, bd.z)) + new Vector2(Cell * 0.5f, Cell * 0.5f);
                var bt = new GameObject(def.kind.ToString()).transform;
                bt.SetParent(root, false);
                bt.localPosition = new Vector3(bc.x, y, bc.y);
                BuildingModel(bt, def, bd.animals);
                var mark = Shapes.Primitive(PrimitiveType.Sphere, "Ready", bt, new Vector3(0f, 3.4f, 0f), Vector3.one * 0.5f, ready).transform;
                buildingReady.Add(mark);
            }
            tick = 0f;
            Tick();
        }

        private void BuildingModel(Transform t, BuildingDef def, int animals)
        {
            var body = MaterialOf(def);
            if (def.kind == BuildingKind.Hive)
            {
                for (int i = 0; i < 3; i++)
                    Shapes.Rounded("Box", t, new Vector3((i - 1) * 1.1f, 0.6f, 0f), new Vector3(0.8f, 1.1f, 0.8f), 0.08f, bee);
                return;
            }
            // A low shed at the back, a fenced yard in front with the animals in it.
            Shapes.Rounded("Shed", t, new Vector3(0f, 1f, 1.1f), new Vector3(3.6f, 2f, 1.6f), 0.05f, body);
            Shapes.Rounded("Roof", t, new Vector3(0f, 2.1f, 1.1f), new Vector3(3.9f, 0.3f, 2f), 0.05f, world.Wood);
            for (int i = 0; i < 5; i++)
                Shapes.Rounded("Rail", t, new Vector3(-1.8f + i * 0.9f, 0.35f, -1.8f), new Vector3(0.12f, 0.7f, 0.12f), 0.03f, world.Wood);
            Shapes.Rounded("Bar", t, new Vector3(0f, 0.55f, -1.8f), new Vector3(3.7f, 0.1f, 0.1f), 0.03f, world.Wood);
            for (int i = 0; i < animals; i++)
            {
                var at = new Vector3(-1.2f + (i % 3) * 1.2f, 0f, -0.9f + (i / 3) * 0.8f);
                AnimalModel(t, def.kind, at);
            }
        }

        private Material MaterialOf(BuildingDef def) => def.kind == BuildingKind.CowBarn ? world.BarnRed : def.kind == BuildingKind.Coop ? white : brown;

        private void AnimalModel(Transform parent, BuildingKind kind, Vector3 at)
        {
            var a = new GameObject("Animal").transform;
            a.SetParent(parent, false);
            a.localPosition = at;
            switch (kind)
            {
                case BuildingKind.Coop: // a hen
                    Shapes.Primitive(PrimitiveType.Sphere, "Body", a, new Vector3(0f, 0.25f, 0f), new Vector3(0.35f, 0.32f, 0.42f), white);
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", a, new Vector3(0f, 0.45f, -0.18f), Vector3.one * 0.2f, white);
                    Shapes.Rounded("Comb", a, new Vector3(0f, 0.57f, -0.18f), new Vector3(0.04f, 0.1f, 0.12f), 0.02f, pink);
                    break;
                case BuildingKind.CowBarn: // a cow
                    Shapes.Rounded("Body", a, new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 0.5f, 0.95f), 0.12f, white);
                    Shapes.Rounded("Spot", a, new Vector3(0.2f, 0.65f, 0.1f), new Vector3(0.2f, 0.3f, 0.35f), 0.08f, black);
                    Shapes.Rounded("Head", a, new Vector3(0f, 0.75f, -0.55f), new Vector3(0.35f, 0.35f, 0.35f), 0.08f, white);
                    Shapes.Rounded("Nose", a, new Vector3(0f, 0.68f, -0.74f), new Vector3(0.28f, 0.16f, 0.06f), 0.04f, pink);
                    foreach (float s in new[] { -0.18f, 0.18f }) foreach (float f in new[] { -0.3f, 0.3f })
                        Shapes.Rounded("Leg", a, new Vector3(s, 0.18f, f), new Vector3(0.1f, 0.36f, 0.1f), 0.03f, white);
                    break;
                case BuildingKind.Pen: // a sheep
                    Shapes.Primitive(PrimitiveType.Sphere, "Wool", a, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.5f, 0.8f), wool);
                    Shapes.Rounded("Head", a, new Vector3(0f, 0.55f, -0.42f), new Vector3(0.24f, 0.26f, 0.26f), 0.08f, black);
                    break;
                default: // a goat
                    Shapes.Rounded("Body", a, new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.4f, 0.75f), 0.1f, brown);
                    Shapes.Rounded("Head", a, new Vector3(0f, 0.65f, -0.42f), new Vector3(0.26f, 0.28f, 0.28f), 0.08f, brown);
                    foreach (float s in new[] { -0.07f, 0.07f })
                        Shapes.Rounded("Horn", a, new Vector3(s, 0.85f, -0.38f), new Vector3(0.04f, 0.18f, 0.04f), 0.02f, wool);
                    break;
            }
        }

        // ---------- Defences ----------

        private readonly List<Transform> defenceRoots = new List<Transform>();
        private Material stone, iron, flag, fire;

        /// <summary>A defence was hit: if it broke, its rubble shows.</summary>
        public void DefenceHit(int i, bool broken)
        {
            if (i < 0 || i >= defenceRoots.Count || defenceRoots[i] == null) return;
            var t = defenceRoots[i];
            var whole = t.Find("Whole");
            var rubble = t.Find("Rubble");
            if (whole != null) whole.gameObject.SetActive(!broken);
            if (rubble != null) rubble.gameObject.SetActive(broken);
        }

        private void DefenceModel(Transform t, DefenceKind kind, int level)
        {
            if (stone == null)
            {
                stone = M(new Color(0.62f, 0.58f, 0.52f));
                iron = M(new Color(0.2f, 0.2f, 0.24f));
                flag = M(new Color(0.3f, 0.55f, 1f));
                fire = M(new Color(1f, 0.55f, 0.15f), 2.4f);
            }
            float s = 1f + (level - 1) * 0.12f;
            switch (kind)
            {
                case DefenceKind.Fence:
                    foreach (float x in new[] { -0.9f, 0.9f }) Shapes.Rounded("Post", t, new Vector3(x, 0.5f, 0f), new Vector3(0.16f, 1f, 0.16f), 0.03f, world.Wood);
                    foreach (float y in new[] { 0.35f, 0.75f }) Shapes.Rounded("Rail", t, new Vector3(0f, y, 0f), new Vector3(2f, 0.12f, 0.1f), 0.03f, world.Wood);
                    break;
                case DefenceKind.Wall:
                    Shapes.Rounded("Wall", t, new Vector3(0f, 0.9f * s, 0f), new Vector3(2f, 1.8f * s, 0.9f), 0.06f, stone);
                    for (int i = 0; i < 3; i++) Shapes.Rounded("Merlon", t, new Vector3(-0.7f + i * 0.7f, 1.95f * s, 0f), new Vector3(0.45f, 0.4f, 0.9f), 0.05f, stone);
                    break;
                case DefenceKind.Gate:
                    foreach (float x in new[] { -1.2f, 1.2f }) Shapes.Rounded("Pillar", t, new Vector3(x, 1.2f, 0f), new Vector3(0.6f, 2.4f, 0.9f), 0.06f, stone);
                    Shapes.Rounded("Doors", t, new Vector3(0f, 0.9f, 0f), new Vector3(1.8f, 1.8f, 0.2f), 0.05f, world.Wood);
                    Shapes.Rounded("Beam", t, new Vector3(0f, 2.3f, 0f), new Vector3(3f, 0.3f, 0.9f), 0.05f, world.Wood);
                    break;
                case DefenceKind.ArcherTower:
                    foreach (float x in new[] { -0.6f, 0.6f }) foreach (float z in new[] { -0.6f, 0.6f })
                        Shapes.Rounded("Leg", t, new Vector3(x, 1.4f * s, z), new Vector3(0.2f, 2.8f * s, 0.2f), 0.04f, world.Wood);
                    Shapes.Rounded("Deck", t, new Vector3(0f, 2.9f * s, 0f), new Vector3(1.8f, 0.2f, 1.8f), 0.04f, world.Wood);
                    Shapes.Rounded("Roof", t, new Vector3(0f, 3.9f * s, 0f), new Vector3(2f, 0.25f, 2f), 0.04f, world.Roof);
                    Shapes.Primitive(PrimitiveType.Sphere, "Archer", t, new Vector3(0f, 3.3f * s, 0f), new Vector3(0.45f, 0.6f, 0.45f), flag);
                    break;
                case DefenceKind.FireTower:
                    Shapes.Rounded("Tower", t, new Vector3(0f, 1.5f * s, 0f), new Vector3(1.4f, 3f * s, 1.4f), 0.1f, stone);
                    Shapes.Rounded("Brazier", t, new Vector3(0f, 3.15f * s, 0f), new Vector3(1.1f, 0.3f, 1.1f), 0.08f, iron);
                    Shapes.Primitive(PrimitiveType.Sphere, "Flame", t, new Vector3(0f, 3.5f * s, 0f), new Vector3(0.8f, 0.7f, 0.8f), fire);
                    break;
                case DefenceKind.Cannon:
                case DefenceKind.HeavyCannon:
                {
                    float c = kind == DefenceKind.HeavyCannon ? 1.4f : 1f;
                    Shapes.Rounded("Base", t, new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.4f, 1.4f) * c, 0.06f, world.Wood);
                    var barrel = Shapes.Primitive(PrimitiveType.Cylinder, "Barrel", t, new Vector3(0f, 0.8f * c, 0.3f * c), new Vector3(0.45f, 0.75f, 0.45f) * c, iron).transform;
                    barrel.localRotation = Quaternion.Euler(70f, 0f, 0f);
                    foreach (float x in new[] { -0.65f, 0.65f })
                        Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", t, new Vector3(x * c, 0.45f * c, 0f), new Vector3(0.8f, 0.08f, 0.8f) * c, world.Wood).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                }
                case DefenceKind.Watchtower:
                    Shapes.Rounded("Shaft", t, new Vector3(0f, 2.5f, 0f), new Vector3(0.9f, 5f, 0.9f), 0.06f, world.Wood);
                    Shapes.Rounded("Lookout", t, new Vector3(0f, 5.1f, 0f), new Vector3(1.6f, 0.8f, 1.6f), 0.06f, world.Wood);
                    Shapes.Rounded("Pole", t, new Vector3(0f, 6.1f, 0f), new Vector3(0.08f, 1.4f, 0.08f), 0.02f, iron);
                    Shapes.Rounded("Flag", t, new Vector3(0.4f, 6.5f, 0f), new Vector3(0.8f, 0.5f, 0.04f), 0.02f, flag);
                    break;
            }
        }

        /// <summary>Whether a defence fits at a map point: inside the reach round the farm, clear of the house, the barn,
        /// the land and the other defences.</summary>
        public bool DefenceFits(Vector2 p, DefenceKind kind)
        {
            float r = OwBattleData.Of(kind).radius;
            if ((p - OwWorld.FarmCentre).magnitude > OwBattleData.Reach(save.House.land)) return false;
            if ((p - HouseAt).magnitude < r + 3f || (p - BarnAt).magnitude < r + 2.6f) return false;
            float half = save.House.land * 0.5f;
            if (Mathf.Abs(p.x - OwWorld.FarmCentre.x) < half + r && Mathf.Abs(p.y - OwWorld.FarmCentre.y) < half + r) return false;
            foreach (var d in save.defences)
                if ((new Vector2(d.x, d.z) - p).magnitude < r + OwBattleData.Of((DefenceKind)d.kind).radius - 0.2f) return false;
            return true;
        }

        // ---------- Pictures for the lists ----------

        public Texture IconOf(Item i) => OwIcons.Get("item" + i, t => ProduceModel(t, i));
        public Texture IconOf(TreeKind k) => OwIcons.Get("tree" + k, t => TreeModel(t, k));
        public Texture IconOf(BuildingKind k) => OwIcons.Get("b" + k, t => BuildingModel(t, OwData.Of(k), k == BuildingKind.Hive ? 0 : 2));
        public Texture IconOf(DefenceKind k) => OwIcons.Get("d" + k, t => DefenceModel(t, k, 1));

        public Texture PlotIcon() => OwIcons.Get("plot", t =>
        {
            Shapes.Rounded("Soil", t, new Vector3(0f, 0.05f, 0f), new Vector3(1.8f, 0.12f, 1.8f), 0.04f, soil);
            for (int i = 0; i < 4; i++)
                Shapes.Primitive(PrimitiveType.Sphere, "Plant", t, new Vector3((i % 2 - 0.5f) * 0.8f, 0.3f, (i / 2 - 0.5f) * 0.8f), new Vector3(0.5f, 0.6f, 0.5f), sprout);
        });

        public Texture HouseIcon(int level) => OwIcons.Get("house" + level, t => world.HouseModel(t, Vector3.zero, 0.8f + level * 0.25f, world.Wall, world.Roof));

        public Texture BarnIcon() => OwIcons.Get("barn", t =>
        {
            Shapes.Rounded("Body", t, new Vector3(0f, 1.5f, 0f), new Vector3(3.6f, 3f, 3.6f), 0.05f, world.BarnRed);
            Shapes.Rounded("Roof", t, new Vector3(0f, 3.2f, 0f), new Vector3(4f, 0.5f, 4f), 0.05f, world.Wood);
            Shapes.Rounded("Door", t, new Vector3(0f, 1f, -1.82f), new Vector3(1.6f, 2f, 0.06f), 0.03f, world.Wall);
        });

        private void TreeModel(Transform t, TreeKind kind)
        {
            Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, 0.7f, 0f), new Vector3(0.28f, 0.7f, 0.28f), world.Wood);
            Shapes.Primitive(PrimitiveType.Sphere, "Leaves", t, new Vector3(0f, 1.9f, 0f), Vector3.one * 1.7f, world.Leaf);
            var fm = CropMat((int)OwData.Of(kind).item);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Shapes.Primitive(PrimitiveType.Sphere, "Fruit", t, new Vector3(Mathf.Cos(a) * 0.75f, 1.7f + (i % 2) * 0.4f, Mathf.Sin(a) * 0.75f), Vector3.one * 0.26f, fm);
            }
        }

        /// <summary>What a harvest looks like: a sheaf of wheat, a cob, a carrot, a jar of honey, a bottle of milk...</summary>
        private void ProduceModel(Transform t, Item i)
        {
            var c = CropMat((int)i);
            switch (i)
            {
                case Item.Wheat:
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k * Mathf.PI * 2f / 7f;
                        var stalk = Shapes.Primitive(PrimitiveType.Cylinder, "Stalk", t, new Vector3(Mathf.Cos(a) * 0.08f, 0.5f, Mathf.Sin(a) * 0.08f), new Vector3(0.05f, 0.5f, 0.05f), c).transform;
                        stalk.localRotation = Quaternion.Euler(Mathf.Sin(a) * 10f, 0f, Mathf.Cos(a) * 10f);
                        Shapes.Primitive(PrimitiveType.Sphere, "Ear", t, new Vector3(Mathf.Cos(a) * 0.2f, 1.05f, Mathf.Sin(a) * 0.2f), new Vector3(0.1f, 0.3f, 0.1f), c);
                    }
                    Shapes.Primitive(PrimitiveType.Cylinder, "Band", t, new Vector3(0f, 0.5f, 0f), new Vector3(0.24f, 0.05f, 0.24f), world.Roof);
                    break;
                case Item.Corn:
                    Shapes.Primitive(PrimitiveType.Sphere, "Cob", t, new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.6f, 0.3f), c);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Husk", t, new Vector3(s * 0.15f, 0.35f, 0f), new Vector3(0.08f, 0.6f, 0.25f), 0.04f, sprout).transform.localRotation = Quaternion.Euler(0f, 0f, s * 20f);
                    break;
                case Item.Carrot:
                    Shapes.Primitive(PrimitiveType.Sphere, "Root", t, new Vector3(0f, 0.4f, 0f), new Vector3(0.22f, 0.7f, 0.22f), c).transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                    for (int k = 0; k < 3; k++)
                        Shapes.Rounded("Leaf", t, new Vector3(-0.15f + k * 0.06f, 0.85f, 0f), new Vector3(0.05f, 0.35f, 0.05f), 0.02f, sprout).transform.localRotation = Quaternion.Euler(0f, 0f, 30f - k * 15f);
                    break;
                case Item.Sunflower:
                    Shapes.Primitive(PrimitiveType.Cylinder, "Stem", t, new Vector3(0f, 0.4f, 0f), new Vector3(0.06f, 0.4f, 0.06f), sprout);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Petals", t, new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 0.03f, 0.6f), c).transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Seeds", t, new Vector3(0f, 0.9f, -0.03f), new Vector3(0.3f, 0.03f, 0.3f), brown).transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
                    break;
                case Item.Egg:
                    Shapes.Primitive(PrimitiveType.Cylinder, "Nest", t, new Vector3(0f, 0.08f, 0f), new Vector3(0.8f, 0.08f, 0.8f), brown);
                    for (int k = 0; k < 3; k++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Egg", t, new Vector3((k - 1) * 0.2f, 0.25f, (k % 2) * 0.1f), new Vector3(0.18f, 0.24f, 0.18f), c);
                    break;
                case Item.Milk:
                case Item.GoatMilk:
                    Shapes.Primitive(PrimitiveType.Cylinder, "Bottle", t, new Vector3(0f, 0.35f, 0f), new Vector3(0.32f, 0.35f, 0.32f), white);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Neck", t, new Vector3(0f, 0.8f, 0f), new Vector3(0.16f, 0.12f, 0.16f), white);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Cap", t, new Vector3(0f, 0.95f, 0f), new Vector3(0.18f, 0.04f, 0.18f), i == Item.Milk ? world.Roof : flagMat);
                    break;
                case Item.Wool:
                    for (int k = 0; k < 5; k++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Puff", t, new Vector3(Mathf.Cos(k) * 0.2f, 0.25f + (k % 2) * 0.15f, Mathf.Sin(k) * 0.2f), Vector3.one * 0.35f, wool);
                    break;
                case Item.Honey:
                    Shapes.Primitive(PrimitiveType.Cylinder, "Jar", t, new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.3f, 0.4f), c);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Lid", t, new Vector3(0f, 0.63f, 0f), new Vector3(0.42f, 0.05f, 0.42f), world.Roof);
                    break;
                case Item.Olive:
                case Item.Walnut:
                    for (int k = 0; k < 3; k++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Nut", t, new Vector3((k - 1) * 0.22f, 0.15f + (k == 1 ? 0.15f : 0f), 0f), i == Item.Olive ? new Vector3(0.18f, 0.24f, 0.18f) : Vector3.one * 0.26f, c);
                    break;
                default: // round fruit: tomato, cactus fruit, pumpkin, apple, orange
                    float w = i == Item.Pumpkin ? 0.7f : 0.45f;
                    Shapes.Primitive(PrimitiveType.Sphere, "Fruit", t, new Vector3(0f, w * 0.4f, 0f), new Vector3(w, w * (i == Item.Pumpkin ? 0.7f : 0.9f), w), c);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Stem", t, new Vector3(0f, w * 0.8f, 0f), new Vector3(0.05f, 0.08f, 0.05f), i == Item.Pumpkin ? brown : sprout);
                    break;
            }
        }

        private Material flagMat => flag != null ? flag : (flag = M(new Color(0.3f, 0.55f, 1f)));

        // ---------- Growing ----------

        /// <summary>A plot's share grown (0 none … 1 ready); -1: nothing sown.</summary>
        public float Growth(PlotState p)
        {
            if (p.crop < 0) return -1f;
            var def = OwData.Of((Crop)p.crop);
            return Mathf.Clamp01((OwSave.Now - p.planted) / (float)def.seconds);
        }

        /// <summary>A tree's fruit ready to pick; before its first fruit, it is still growing.</summary>
        public bool FruitReady(TreeState t)
        {
            var def = OwData.Of((TreeKind)t.kind);
            long since = t.picked > 0 ? t.picked + def.fruitSeconds : t.planted + def.growSeconds;
            return OwSave.Now >= since;
        }

        public long FruitIn(TreeState t)
        {
            var def = OwData.Of((TreeKind)t.kind);
            long at = t.picked > 0 ? t.picked + def.fruitSeconds : t.planted + def.growSeconds;
            return System.Math.Max(0, at - OwSave.Now);
        }

        /// <summary>A building's produce ready to collect (fed and its time passed; a hive needs no feed).</summary>
        public bool ProduceReady(BuildingState b)
        {
            var def = OwData.Of((BuildingKind)b.kind);
            if (b.animals <= 0 && def.kind != BuildingKind.Hive) return false;
            return b.fed > 0 && OwSave.Now >= b.fed + def.seconds;
        }

        private void Update()
        {
            tick -= Time.deltaTime;
            if (tick <= 0f) Tick();
            float bob = Mathf.Sin(Time.time * 4f) * 0.15f;
            foreach (var m in buildingReady) if (m != null && m.gameObject.activeSelf) m.localPosition = new Vector3(0f, 3.4f + bob, 0f);
        }

        private void Tick()
        {
            tick = 0.5f;
            for (int i = 0; i < plotCrops.Count && i < save.plots.Count; i++)
            {
                var p = save.plots[i];
                var crop = plotCrops[i];
                float g = Growth(p);
                crop.gameObject.SetActive(g >= 0f);
                if (g < 0f) continue;
                crop.localScale = new Vector3(1f, Mathf.Lerp(0.15f, 1f, g), 1f);
                var mat = g >= 1f ? CropMat((int)OwData.Of((Crop)p.crop).item) : sprout;
                foreach (var r in crop.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial != mat) r.sharedMaterial = mat;
            }
            for (int i = 0; i < treeFruit.Count && i < save.trees.Count; i++) treeFruit[i].gameObject.SetActive(FruitReady(save.trees[i]));
            for (int i = 0; i < buildingReady.Count && i < save.buildings.Count; i++) buildingReady[i].gameObject.SetActive(ProduceReady(save.buildings[i]));
        }

        // ---------- What is near ----------

        /// <summary>The nearest thing within reach of a map point (a plot, tree, building, the house, the barn, the co-op), or an
        /// empty cell of the land foi stands on.</summary>
        public Target Nearest(Vector2 p, float reach = 2.2f)
        {
            var best = new Target { kind = TargetKind.None };
            float bestD = reach;
            void Try(TargetKind k, int i, Vector2 at, float r)
            {
                float d = Vector2.Distance(p, at) - r;
                if (d >= bestD) return;
                bestD = d;
                best = new Target { kind = k, index = i, at = OwWorld.World(at) };
            }
            for (int i = 0; i < save.plots.Count; i++) Try(TargetKind.Plot, i, CellCentre(new Vector2Int(save.plots[i].x, save.plots[i].z)), 0.6f);
            for (int i = 0; i < save.trees.Count; i++) Try(TargetKind.Tree, i, CellCentre(new Vector2Int(save.trees[i].x, save.trees[i].z)), 0.8f);
            for (int i = 0; i < save.buildings.Count; i++) Try(TargetKind.Building, i, CellCentre(new Vector2Int(save.buildings[i].x, save.buildings[i].z)) + Vector2.one * Cell * 0.5f, 1.8f);
            for (int i = 0; i < save.defences.Count; i++) Try(TargetKind.Defence, i, new Vector2(save.defences[i].x, save.defences[i].z), OwBattleData.Of((DefenceKind)save.defences[i].kind).radius);
            Try(TargetKind.House, 0, HouseAt, 2.6f);
            Try(TargetKind.Barn, 0, BarnAt, 2.4f);
            Try(TargetKind.Coop, 0, OwWorld.CoopAt + new Vector2(0f, -3f), 4.5f);
            if (best.kind == TargetKind.None)
            {
                var c = CellAt(p);
                if (OnLand(c) && Free(c)) best = new Target { kind = TargetKind.Empty, cell = c, at = OwWorld.World(CellCentre(c)) };
            }
            return best;
        }

        /// <summary>Round obstacles on the farm for foi to walk round (map units: x, z, radius).</summary>
        public void AddBlockers(List<Vector3> into)
        {
            into.Add(new Vector3(HouseAt.x, HouseAt.y, 2.4f + save.house * 0.3f));
            into.Add(new Vector3(BarnAt.x, BarnAt.y, 2.2f));
            foreach (var d in save.defences)
                if (d.hp > 0 && d.kind != (int)DefenceKind.Gate && d.kind != (int)DefenceKind.Fence) into.Add(new Vector3(d.x, d.z, OwBattleData.Of((DefenceKind)d.kind).radius * 0.8f));
            foreach (var b in save.buildings)
            {
                var c = CellCentre(new Vector2Int(b.x, b.z)) + Vector2.one * Cell * 0.5f + new Vector2(0f, 1.1f);
                into.Add(new Vector3(c.x, c.y, 1.4f));
            }
        }
    }
}
