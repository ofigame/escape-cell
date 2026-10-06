using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The roof camp in 3D: the decorations the player bought stand on their tiles of the 6x6 roof platform,
    /// and the rescued friends hop around between them.
    /// </summary>
    public class CampScene : MonoBehaviour
    {
        private class Friend
        {
            public Robot robot;
            public GridPos pos;
            public Vector3 from, to;
            public float t = 1f, wait;
        }

        private readonly List<Friend> friends = new List<Friend>();
        private readonly HashSet<GridPos> blocked = new HashSet<GridPos>();
        private readonly Dictionary<string, Transform> decor = new Dictionary<string, Transform>();
        private Transform root;
        private GridPos player;
        private System.Random rng = new System.Random();
        private float time;

        public bool Shown => root != null;

        public void Show(GridPos playerTile)
        {
            Hide();
            player = playerTile;
            root = new GameObject("Camp").transform;
            root.SetParent(transform, false);
            blocked.Clear();
            blocked.Add(player);
            foreach (var d in Camp.Decor)
                if (Camp.Owns(d)) Place(d, pop: false);

            for (int i = 0; i < Camp.Friends.Length; i++)
            {
                if (!Camp.Rescued(i)) continue;
                var free = FreeTile();
                if (!free.HasValue) break;
                var bot = Robot.Create(root);
                bot.ApplyWorld(Camp.FriendWorld(i));
                bot.enabled = false;
                bot.transform.position = GridView.ToWorld(free.Value);
                bot.transform.localScale = Vector3.one * 0.8f;
                bot.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                friends.Add(new Friend { robot = bot, pos = free.Value, from = bot.transform.position, to = bot.transform.position, wait = (float)rng.NextDouble() * 2f });
                blocked.Add(free.Value);
            }
        }

        public void Hide()
        {
            if (root != null) Destroy(root.gameObject);
            root = null;
            friends.Clear();
            decor.Clear();
            blocked.Clear();
        }

        /// <summary>A decoration was just bought: it pops up on its tile.</summary>
        public void Add(CampDecor d)
        {
            if (root == null || decor.ContainsKey(d.id)) return;
            Place(d, pop: true);
        }

        private GridPos? FreeTile()
        {
            var options = new List<GridPos>();
            for (int x = 0; x < Camp.Size; x++)
                for (int y = 0; y < Camp.Size; y++)
                {
                    var p = new GridPos(x, y);
                    if (!blocked.Contains(p) && !IsDecorTile(p)) options.Add(p);
                }
            if (options.Count == 0) return null;
            return options[rng.Next(options.Count)];
        }

        private static bool IsDecorTile(GridPos p)
        {
            foreach (var d in Camp.Decor)
                if (Camp.Owns(d) && d.x == p.x && d.y == p.y) return true;
            return false;
        }

        private void Update()
        {
            if (root == null) return;
            time += Time.deltaTime;
            foreach (var f in friends)
            {
                if (f.t < 1f)
                {
                    f.t = Mathf.Min(1f, f.t + Time.deltaTime / 0.3f);
                    f.robot.transform.position = Vector3.Lerp(f.from, f.to, f.t) + Vector3.up * Mathf.Sin(f.t * Mathf.PI) * 0.3f;
                    continue;
                }
                f.wait -= Time.deltaTime;
                if (f.wait > 0f) continue;
                f.wait = 1f + (float)rng.NextDouble() * 2.5f;
                // Hop to a free neighbour (sometimes just turn around and look).
                var dir = DirectionExtensions.All[rng.Next(4)];
                var n = f.pos + dir.ToOffset();
                var o = dir.ToOffset();
                f.robot.transform.rotation = Quaternion.LookRotation(new Vector3(o.x, 0f, o.y));
                if (n.x < 0 || n.y < 0 || n.x >= Camp.Size || n.y >= Camp.Size || blocked.Contains(n) || IsDecorTile(n)) continue;
                blocked.Remove(f.pos);
                blocked.Add(n);
                f.pos = n;
                f.from = f.robot.transform.position;
                f.to = GridView.ToWorld(n);
                f.t = 0f;
            }
            foreach (var kv in decor)
                if (kv.Value.localScale.x < 1f) kv.Value.localScale = Vector3.one * Mathf.Min(1f, kv.Value.localScale.x + Time.deltaTime * 3f);
            if (decor.TryGetValue("campfire", out var fire)) fire.GetChild(fire.childCount - 1).localScale = Vector3.one * (0.9f + Mathf.Sin(time * 12f) * 0.12f);
        }

        // ---------- Decoration models ----------

        private void Place(CampDecor d, bool pop)
        {
            var t = new GameObject(d.id).transform;
            t.SetParent(root, false);
            t.position = GridView.ToWorld(new GridPos(d.x, d.y)) + Vector3.up * GridView.SurfaceY;
            t.localScale = pop ? Vector3.one * 0.05f : Vector3.one;
            Build(t, d.id);
            decor[d.id] = t;
        }

        private static Material M(Color c, Color glow) => MaterialFactory.Create(c, glow);

        private static void Build(Transform t, string id)
        {
            switch (id)
            {
                case "flag":
                    Shapes.Rounded("Pole", t, new Vector3(0f, 0.6f, 0f), new Vector3(0.05f, 1.2f, 0.05f), 0.02f, M(Color.white, Color.black));
                    Shapes.Rounded("Cloth", t, new Vector3(0.2f, 1.05f, 0f), new Vector3(0.36f, 0.24f, 0.02f), 0.02f, M(new Color(0.3f, 0.9f, 0.85f), new Color(0.2f, 0.6f, 0.6f)));
                    break;
                case "campfire":
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Shapes.Rounded("Stone", t, new Vector3(Mathf.Cos(a) * 0.26f, 0.05f, Mathf.Sin(a) * 0.26f), new Vector3(0.12f, 0.1f, 0.12f), 0.04f, M(new Color(0.55f, 0.55f, 0.6f), Color.black));
                    }
                    Shapes.Rounded("Log", t, new Vector3(0f, 0.07f, 0f), new Vector3(0.4f, 0.08f, 0.08f), 0.04f, M(new Color(0.5f, 0.3f, 0.18f), Color.black)).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    Shapes.Rounded("Flame", t, new Vector3(0f, 0.22f, 0f), new Vector3(0.18f, 0.3f, 0.18f), 0.08f, M(new Color(1f, 0.6f, 0.2f), new Color(2.6f, 1f, 0.15f)));
                    break;
                case "lamps":
                    foreach (var o in new[] { new Vector3(-0.25f, 0f, 0f), new Vector3(0.25f, 0f, 0f) })
                    {
                        Shapes.Rounded("Post", t, o + new Vector3(0f, 0.45f, 0f), new Vector3(0.05f, 0.9f, 0.05f), 0.02f, M(new Color(0.3f, 0.3f, 0.38f), Color.black));
                        Shapes.Rounded("Bulb", t, o + new Vector3(0f, 0.95f, 0f), new Vector3(0.14f, 0.14f, 0.14f), 0.06f, M(new Color(1f, 0.9f, 0.6f), new Color(2.2f, 1.8f, 0.8f)));
                    }
                    break;
                case "tent":
                {
                    var cloth = M(new Color(1f, 0.55f, 0.35f), new Color(0.3f, 0.1f, 0.05f));
                    foreach (float side in new[] { -1f, 1f })
                        Shapes.Rounded("Side", t, new Vector3(side * 0.18f, 0.28f, 0f), new Vector3(0.44f, 0.04f, 0.7f), 0.02f, cloth).transform.localRotation = Quaternion.Euler(0f, 0f, side * -55f);
                    Shapes.Rounded("Door", t, new Vector3(0f, 0.18f, 0.34f), new Vector3(0.16f, 0.3f, 0.02f), 0.02f, M(new Color(0.3f, 0.2f, 0.3f), Color.black));
                    break;
                }
                case "antenna":
                    Shapes.Rounded("Mast", t, new Vector3(0f, 0.75f, 0f), new Vector3(0.07f, 1.5f, 0.07f), 0.03f, M(new Color(0.7f, 0.72f, 0.8f), Color.black));
                    Shapes.Primitive(PrimitiveType.Sphere, "Dish", t, new Vector3(0.12f, 1.3f, 0f), new Vector3(0.4f, 0.4f, 0.1f), M(Color.white, Color.black)).transform.localRotation = Quaternion.Euler(0f, 60f, 0f);
                    Shapes.Rounded("Tip", t, new Vector3(0f, 1.55f, 0f), new Vector3(0.1f, 0.1f, 0.1f), 0.05f, M(new Color(1f, 0.3f, 0.35f), new Color(2.2f, 0.3f, 0.4f)));
                    break;
                case "hammock":
                    foreach (float s in new[] { -0.36f, 0.36f })
                        Shapes.Rounded("Post", t, new Vector3(s, 0.35f, 0f), new Vector3(0.06f, 0.7f, 0.06f), 0.02f, M(new Color(0.55f, 0.35f, 0.2f), Color.black));
                    Shapes.Rounded("Net", t, new Vector3(0f, 0.38f, 0f), new Vector3(0.66f, 0.04f, 0.3f), 0.02f, M(new Color(0.95f, 0.85f, 0.4f), new Color(0.2f, 0.15f, 0.02f)));
                    break;
                case "garden":
                    Shapes.Rounded("Bed", t, new Vector3(0f, 0.08f, 0f), new Vector3(0.8f, 0.16f, 0.8f), 0.04f, M(new Color(0.5f, 0.35f, 0.25f), Color.black));
                    for (int i = 0; i < 5; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Bush", t, new Vector3((i % 3 - 1) * 0.22f, 0.24f + (i % 2) * 0.05f, (i / 3 - 0.5f) * 0.3f), Vector3.one * (0.2f + (i % 2) * 0.06f),
                            M(new Color(0.4f, 0.8f, 0.45f), new Color(0.1f, 0.3f, 0.1f)));
                    Shapes.Primitive(PrimitiveType.Sphere, "Flower", t, new Vector3(0.2f, 0.36f, 0.15f), Vector3.one * 0.08f, M(new Color(1f, 0.5f, 0.7f), new Color(1f, 0.3f, 0.5f)));
                    break;
                case "solar":
                    Shapes.Rounded("Stand", t, new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.4f, 0.06f), 0.02f, M(new Color(0.6f, 0.6f, 0.68f), Color.black));
                    Shapes.Rounded("Panel", t, new Vector3(0f, 0.42f, 0f), new Vector3(0.7f, 0.04f, 0.5f), 0.02f, M(new Color(0.2f, 0.35f, 0.75f), new Color(0.1f, 0.3f, 0.8f))).transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                    break;
                case "telescope":
                    Shapes.Rounded("Leg", t, new Vector3(0f, 0.25f, 0f), new Vector3(0.05f, 0.5f, 0.05f), 0.02f, M(new Color(0.35f, 0.32f, 0.42f), Color.black));
                    Shapes.Rounded("Tube", t, new Vector3(0.05f, 0.58f, 0.05f), new Vector3(0.12f, 0.12f, 0.6f), 0.05f, M(new Color(0.95f, 0.8f, 0.4f), new Color(0.4f, 0.3f, 0.05f))).transform.localRotation = Quaternion.Euler(-35f, 30f, 0f);
                    break;
                case "fountain":
                {
                    var stone = M(new Color(0.85f, 0.85f, 0.92f), Color.black);
                    var water = M(new Color(0.45f, 0.8f, 1f), new Color(0.3f, 0.9f, 1.4f));
                    Shapes.Primitive(PrimitiveType.Cylinder, "Basin", t, new Vector3(0f, 0.1f, 0f), new Vector3(0.8f, 0.1f, 0.8f), stone);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Water", t, new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.01f, 0.7f), water);
                    Shapes.Rounded("Column", t, new Vector3(0f, 0.4f, 0f), new Vector3(0.12f, 0.4f, 0.12f), 0.04f, stone);
                    Shapes.Primitive(PrimitiveType.Sphere, "Spout", t, new Vector3(0f, 0.66f, 0f), Vector3.one * 0.18f, water);
                    break;
                }
                case "statue":
                {
                    // Number 47 in gold: the hero who started it all.
                    var gold = M(new Color(1f, 0.82f, 0.35f), new Color(0.6f, 0.4f, 0.05f));
                    Shapes.Rounded("Plinth", t, new Vector3(0f, 0.15f, 0f), new Vector3(0.6f, 0.3f, 0.6f), 0.05f, M(new Color(0.8f, 0.8f, 0.88f), Color.black));
                    Shapes.Rounded("Body", t, new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.2f, 0.24f), 0.05f, gold);
                    Shapes.Rounded("Head", t, new Vector3(0f, 0.8f, 0f), new Vector3(0.42f, 0.38f, 0.4f), 0.07f, gold);
                    Shapes.Rounded("Arm", t, new Vector3(0.22f, 0.72f, 0f), new Vector3(0.06f, 0.3f, 0.08f), 0.02f, gold).transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                    break;
                }
            }
        }
    }
}
