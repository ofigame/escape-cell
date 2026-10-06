using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The city in 3D: the plot's cells (the same tiles as the main game), the placed pieces with their care state
    /// (dust when it is due soon, a spinning wrench when it needs care), coins piling up at mines, the ghost of a piece
    /// being placed, the residents strolling about, and evening lights.
    /// </summary>
    public class CityScene : MonoBehaviour
    {
        private class PieceView
        {
            public CityPlaced data;
            public Transform root, model, dust, wrench, coins;
            public List<(Material m, Color color, Color glow)> lights;
            public float pop = 1f;
            public CareStage stage = (CareStage)(-1);
            public int coinCount = -1;
        }

        private class Resident
        {
            public int index;
            public Robot robot;
            public GridPos pos;
            public Vector3 from, to;
            public float t = 1f, wait, dance;
            public Transform star;
        }

        private readonly Dictionary<int, PieceView> views = new Dictionary<int, PieceView>();
        private readonly List<Resident> residents = new List<Resident>();
        private readonly System.Random rng = new System.Random();
        private Transform root, ghost;
        private GridView gridView;
        private Transform player;
        private CityPiece ghostPiece;
        private int ghostRot;
        private Vector2Int ghostCell;
        private int selected;
        private float time;
        private bool night;
        private Light sun;
        private Material dustMat, webMat, wrenchMat, coinMat;

        public bool Shown => root != null;

        /// <summary>Residents walk around the robot's spot too.</summary>
        public void Init(GridView view, Transform robot)
        {
            gridView = view;
            player = robot;
        }

        public void Show()
        {
            Hide();
            root = new GameObject("City").transform;
            root.SetParent(transform, false);
            dustMat = MaterialFactory.CreateTransparent(new Color(0.55f, 0.48f, 0.42f, 0.55f), Color.black);
            webMat = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.5f), new Color(0.3f, 0.3f, 0.3f));
            wrenchMat = MaterialFactory.Create(new Color(1f, 0.55f, 0.15f), new Color(2.2f, 0.9f, 0.15f));
            coinMat = MaterialFactory.Create(Palette.Coin, Palette.CoinGlow);
            foreach (var p in City.Pieces) AddView(p, pop: false);
            SpawnResidents();
            ApplyNight(CityClock.IsNight);
        }

        public void Hide()
        {
            if (root != null) Destroy(root.gameObject);
            root = null;
            ghost = null;
            views.Clear();
            residents.Clear();
            selected = 0;
            if (night) ApplyNight(false);
        }

        // ---------- Pieces ----------

        private static Vector3 Center(CityPiece piece, int x, int y, int rot)
        {
            var s = piece.Size(rot);
            return new Vector3(x + (s.x - 1) * 0.5f, GridView.SurfaceY, y + (s.y - 1) * 0.5f);
        }

        private void AddView(CityPlaced p, bool pop)
        {
            var v = new PieceView { data = p };
            v.root = new GameObject(p.id + " " + p.uid).transform;
            v.root.SetParent(root, false);
            v.model = new GameObject("Model").transform;
            v.model.SetParent(v.root, false);
            v.lights = CityModels.Build(p.Piece, v.model).Lights;
            v.pop = pop ? 0f : 1f;
            Place(v);
            views[p.uid] = v;
            SetLights(v);
            RefreshCare(v);
        }

        private void Place(PieceView v)
        {
            var p = v.data;
            v.root.position = Center(p.Piece, p.x, p.y, p.rot);
            v.model.localRotation = Quaternion.Euler(0f, p.rot * 90f, 0f);
        }

        /// <summary>A piece was placed, moved, rotated or brought back by undo.</summary>
        public void Refresh(int uid)
        {
            if (root == null) return;
            var p = City.Get(uid);
            if (p == null) { Remove(uid); return; }
            if (views.TryGetValue(uid, out var v))
            {
                v.data = p;
                Place(v);
                v.pop = 0.4f;
                RefreshCare(v);
            }
            else AddView(p, pop: true);
        }

        public void Remove(int uid)
        {
            if (!views.TryGetValue(uid, out var v)) return;
            Destroy(v.root.gameObject);
            views.Remove(uid);
            if (selected == uid) selected = 0;
        }

        /// <summary>Re-reads the whole city (after undo, repairs, a new day).</summary>
        public void RefreshAll()
        {
            if (root == null) return;
            var alive = new HashSet<int>();
            foreach (var p in City.Pieces)
            {
                alive.Add(p.uid);
                Refresh(p.uid);
            }
            foreach (var uid in new List<int>(views.Keys))
                if (!alive.Contains(uid)) Remove(uid);
        }

        public Vector3 PieceTop(int uid)
        {
            if (!views.TryGetValue(uid, out var v)) return Vector3.zero;
            return v.root.position + Vector3.up * 1.2f;
        }

        private void RefreshCare(PieceView v)
        {
            var stage = City.Stage(v.data);
            if (stage == v.stage) return;
            v.stage = stage;
            if (v.dust != null) Destroy(v.dust.gameObject);
            if (v.wrench != null) Destroy(v.wrench.gameObject);
            v.dust = v.wrench = null;
            if (stage == CareStage.Fresh) return;

            // Dust and cobwebs over the piece.
            var s = v.data.Piece.Size(v.data.rot);
            v.dust = new GameObject("Dust").transform;
            v.dust.SetParent(v.root, false);
            var r = new System.Random(v.data.uid);
            int puffs = 4 + s.x * s.y * 2;
            for (int i = 0; i < puffs; i++)
            {
                var at = new Vector3(((float)r.NextDouble() - 0.5f) * s.x * 0.8f, 0.05f + (float)r.NextDouble() * 0.9f, ((float)r.NextDouble() - 0.5f) * s.y * 0.8f);
                Shapes.Primitive(PrimitiveType.Sphere, "Puff", v.dust, at, new Vector3(0.22f, 0.08f, 0.22f), dustMat);
            }
            for (int i = 0; i < 2; i++)
                Shapes.Rounded("Web", v.dust, new Vector3((i - 0.5f) * s.x * 0.7f, 0.7f, s.y * 0.42f), new Vector3(0.3f, 0.3f, 0.01f), 0.01f, webMat)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            if (stage != CareStage.NeedsCare) return;
            // A spinning wrench: this one needs care (and stopped helping until then).
            v.wrench = new GameObject("Wrench").transform;
            v.wrench.SetParent(v.root, false);
            v.wrench.localPosition = new Vector3(0f, 1.9f + (v.data.Piece.id == "lighthouse" || v.data.Piece.id == "ferrisWheel" ? 1f : 0f), 0f);
            Shapes.Rounded("Handle", v.wrench, new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.42f, 0.06f), 0.03f, wrenchMat);
            foreach (float side in new[] { -1f, 1f })
                Shapes.Rounded("Jaw", v.wrench, new Vector3(side * 0.1f, 0.13f, 0f), new Vector3(0.08f, 0.18f, 0.06f), 0.03f, wrenchMat);
            Shapes.Rounded("Head", v.wrench, new Vector3(0f, 0.05f, 0f), new Vector3(0.28f, 0.08f, 0.06f), 0.03f, wrenchMat);
        }

        private void RefreshCoins(PieceView v)
        {
            if (v.data.Piece.perk != CityPerk.Mine) return;
            int coins = City.MineCoins(v.data);
            int shown = coins <= 0 ? 0 : 1 + coins / 12;
            if (shown == v.coinCount) return;
            v.coinCount = shown;
            if (v.coins != null) Destroy(v.coins.gameObject);
            v.coins = null;
            if (shown == 0) return;
            v.coins = new GameObject("Coins").transform;
            v.coins.SetParent(v.root, false);
            v.coins.localPosition = new Vector3(0f, 1.25f, 0.2f);
            for (int i = 0; i < shown; i++)
                Shapes.Primitive(PrimitiveType.Cylinder, "Coin", v.coins, new Vector3(Mathf.Sin(i * 2.4f) * 0.15f, i * 0.09f, Mathf.Cos(i * 2.4f) * 0.15f), new Vector3(0.3f, 0.025f, 0.3f), coinMat);
        }

        private void SetLights(PieceView v)
        {
            foreach (var (m, color, glow) in v.lights) MaterialFactory.SetColors(m, color, glow * (night ? 1.5f : 0.35f));
        }

        // ---------- Ghost and selection ----------

        /// <summary>The piece being placed: floats above its cells, which glow green where it fits and red where it does not.</summary>
        public void ShowGhost(CityPiece piece, Vector2Int cell, int rot, int ignoreUid)
        {
            if (root == null) return;
            if (ghostPiece != piece || ghost == null)
            {
                if (ghost != null) Destroy(ghost.gameObject);
                ghost = new GameObject("Ghost").transform;
                ghost.SetParent(root, false);
                var model = new GameObject("Model").transform;
                model.SetParent(ghost, false);
                CityModels.Build(piece, model);
                ghostPiece = piece;
            }
            ghostRot = rot;
            ghostCell = cell;
            ghost.GetChild(0).localRotation = Quaternion.Euler(0f, rot * 90f, 0f);
            ghost.position = Center(piece, cell.x, cell.y, rot) + Vector3.up * 0.25f;
            TintCells(piece, cell, rot, City.Fits(piece, cell.x, cell.y, rot, ignoreUid));
        }

        public void HideGhost()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            ghostPiece = null;
            ClearTint();
            if (selected != 0) Select(selected);
        }

        /// <summary>A placed piece picked up for moving disappears while its ghost is carried around.</summary>
        public void SetHidden(int uid, bool hidden)
        {
            if (views.TryGetValue(uid, out var v)) v.root.gameObject.SetActive(!hidden);
        }

        public void Select(int uid)
        {
            ClearTint();
            selected = uid;
            if (uid == 0 || !views.TryGetValue(uid, out var v)) return;
            var s = v.data.Piece.Size(v.data.rot);
            for (int i = 0; i < s.x; i++)
                for (int j = 0; j < s.y; j++)
                    gridView.SetTint(new GridPos(v.data.x + i, v.data.y + j), new Color(0.5f, 0.95f, 1f), new Color(0.4f, 1.6f, 2f));
        }

        private readonly List<GridPos> tinted = new List<GridPos>();

        private void TintCells(CityPiece piece, Vector2Int cell, int rot, bool ok)
        {
            ClearTint();
            var s = piece.Size(rot);
            for (int i = 0; i < s.x; i++)
                for (int j = 0; j < s.y; j++)
                {
                    var p = new GridPos(cell.x + i, cell.y + j);
                    if (p.x < 0 || p.y < 0 || p.x >= City.Size || p.y >= City.Size) continue;
                    bool free = ok || City.At(p.x, p.y) == null;
                    gridView.SetTint(p, ok ? new Color(0.45f, 1f, 0.55f) : free ? new Color(1f, 0.75f, 0.5f) : new Color(1f, 0.35f, 0.4f),
                        ok ? new Color(0.4f, 1.8f, 0.6f) : new Color(1.8f, 0.3f, 0.35f));
                    tinted.Add(p);
                }
        }

        private void ClearTint()
        {
            for (int x = 0; x < City.Size; x++)
                for (int y = 0; y < City.Size; y++)
                    gridView.SetTint(new GridPos(x, y), null);
            tinted.Clear();
        }

        // ---------- Residents ----------

        private void SpawnResidents()
        {
            // Everyone with a wish is out and about, plus a few more, so the street stays lively but not crowded.
            var order = new List<int>();
            foreach (var r in City.Requests) if (!order.Contains(r.resident)) order.Add(r.resident);
            for (int i = 0; i < Residents.Count; i++) if (Residents.Rescued(i) && !order.Contains(i)) order.Add(i);
            int count = Mathf.Min(order.Count, 8);
            for (int k = 0; k < count; k++)
            {
                var free = FreeCell();
                if (!free.HasValue) break;
                int i = order[k];
                var bot = Robot.Create(root);
                bot.ApplyWorld(Residents.World(i));
                bot.enabled = false;
                bot.transform.position = GridView.ToWorld(free.Value);
                bot.transform.localScale = Vector3.one * 0.75f;
                bot.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                var res = new Resident { index = i, robot = bot, pos = free.Value, from = bot.transform.position, to = bot.transform.position, wait = (float)rng.NextDouble() * 2f };
                if (City.IsBestFriend(i)) res.star = BestFriendStar(bot.transform);
                residents.Add(res);
            }
        }

        private Transform BestFriendStar(Transform parent)
        {
            var star = new GameObject("BestFriend").transform;
            star.SetParent(parent, false);
            star.localPosition = new Vector3(0f, 1.25f, 0f);
            var gold = MaterialFactory.Create(new Color(1f, 0.85f, 0.3f), new Color(2.2f, 1.6f, 0.3f));
            for (int i = 0; i < 5; i++)
                Shapes.Rounded("Ray", star, Vector3.zero, new Vector3(0.07f, 0.26f, 0.05f), 0.02f, gold).transform.localRotation = Quaternion.Euler(0f, 0f, i * 72f);
            return star;
        }

        public void MakeBestFriend(int resident)
        {
            var r = residents.Find(x => x.index == resident);
            if (r != null && r.star == null) r.star = BestFriendStar(r.robot.transform);
        }

        private GridPos? FreeCell()
        {
            var options = new List<GridPos>();
            for (int x = 0; x < City.Size; x++)
                for (int y = 0; y < City.Size; y++)
                    if (Walkable(new GridPos(x, y))) options.Add(new GridPos(x, y));
            if (options.Count == 0) return null;
            return options[rng.Next(options.Count)];
        }

        private bool Walkable(GridPos p)
        {
            if (p.x < 0 || p.y < 0 || p.x >= City.Size || p.y >= City.Size) return false;
            var at = City.At(p.x, p.y);
            if (at != null && !IsFlat(at.Piece)) return false;
            foreach (var r in residents) if (r.pos == p) return false;
            return true;
        }

        /// <summary>Paths, floors and the pier can be walked on.</summary>
        public static bool IsFlat(CityPiece p) => p.id == "floor" || p.id == "lightPath" || p.id == "pier";

        /// <summary>Where a resident stands (for its speech bubble); false when it is not out.</summary>
        public bool ResidentHead(int resident, out Vector3 world)
        {
            var r = residents.Find(x => x.index == resident);
            world = r != null ? r.robot.transform.position + Vector3.up * 1.05f : Vector3.zero;
            return r != null;
        }

        public void Dance(int resident)
        {
            var r = residents.Find(x => x.index == resident);
            if (r != null) r.dance = 1.6f;
        }

        /// <summary>
        /// Walking: pieces standing between the camera and the robot step aside (hide) so the robot is never lost
        /// behind a house. Passing the same point twice shows everything again.
        /// </summary>
        public void ClearView(Vector3 camera, Vector3 target)
        {
            bool off = camera == target;
            var a = new Vector2(camera.x, camera.z);
            var b = new Vector2(target.x, target.z);
            var ab = b - a;
            float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
            foreach (var v in views.Values)
            {
                bool hide = false;
                if (!off)
                {
                    var s = v.data.Piece.Size(v.data.rot);
                    var c = new Vector2(v.root.position.x, v.root.position.z);
                    float t = Vector2.Dot(c - a, ab) / len2;
                    float reach = Mathf.Max(s.x, s.y) * 0.5f + 0.15f;
                    // Only what is in front of the robot as seen from the camera, and tall enough to matter.
                    hide = t > 0f && t < 1f && (a + ab * t - c).magnitude < reach && (b - c).magnitude > reach * 0.6f && !IsFlat(v.data.Piece);
                }
                if (v.root.gameObject.activeSelf == hide) v.root.gameObject.SetActive(!hide);
            }
        }

        // ---------- Night ----------

        public void ApplyNight(bool on)
        {
            night = on;
            if (sun == null) sun = FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.intensity = on ? 0.5f : 1f;
                sun.color = on ? new Color(0.65f, 0.7f, 1f) : Color.white;
            }
            RenderSettings.ambientLight = Palette.Ambient * (on ? 0.45f : 0.8f);
            foreach (var v in views.Values) SetLights(v);
        }

        // ---------- Every frame ----------

        private float careCheck;

        private void Update()
        {
            if (root == null) return;
            float dt = Time.deltaTime;
            time += dt;

            careCheck -= dt;
            if (careCheck <= 0f)
            {
                careCheck = 2f;
                foreach (var v in views.Values) { RefreshCare(v); RefreshCoins(v); }
                if (night != CityClock.IsNight) ApplyNight(!night);
            }

            foreach (var v in views.Values)
            {
                if (v.pop < 1f)
                {
                    v.pop = Mathf.Min(1f, v.pop + dt * 3f);
                    float s = v.pop < 1f ? Mathf.Lerp(0.2f, 1f, v.pop) + Mathf.Sin(v.pop * Mathf.PI) * 0.15f : 1f;
                    v.model.localScale = Vector3.one * s;
                }
                if (v.wrench != null) v.wrench.localRotation = Quaternion.Euler(0f, time * 160f, 20f);
                if (v.coins != null) v.coins.localPosition = new Vector3(0f, 1.25f + Mathf.Sin(time * 2.5f) * 0.05f, 0.2f);
            }
            if (ghost != null) ghost.GetChild(0).localPosition = Vector3.up * Mathf.Sin(time * 5f) * 0.06f;

            foreach (var r in residents) UpdateResident(r, dt);
        }

        private void UpdateResident(Resident r, float dt)
        {
            if (r.star != null) r.star.localRotation = Quaternion.Euler(0f, time * 90f, 0f);
            if (r.dance > 0f)
            {
                r.dance -= dt;
                r.robot.transform.position = r.to + Vector3.up * Mathf.Abs(Mathf.Sin(r.dance * 9f)) * 0.35f;
                r.robot.transform.rotation = Quaternion.Euler(0f, r.dance * 500f, 0f);
                return;
            }
            if (r.t < 1f)
            {
                r.t = Mathf.Min(1f, r.t + dt / 0.35f);
                r.robot.transform.position = Vector3.Lerp(r.from, r.to, r.t) + Vector3.up * Mathf.Sin(r.t * Mathf.PI) * 0.25f;
                return;
            }
            r.wait -= dt;
            if (r.wait > 0f) return;
            r.wait = 0.8f + (float)rng.NextDouble() * 2.5f;
            var dir = DirectionExtensions.All[rng.Next(4)];
            var o = dir.ToOffset();
            r.robot.transform.rotation = Quaternion.LookRotation(new Vector3(o.x, 0f, o.y));
            var n = r.pos + o;
            if (!Walkable(n)) return;
            // Keep out of the player's way.
            if (player != null && player.gameObject.activeInHierarchy && (GridView.ToWorld(n) - player.position).sqrMagnitude < 0.8f) return;
            r.pos = n;
            r.from = r.robot.transform.position;
            r.to = GridView.ToWorld(n);
            r.t = 0f;
        }
    }
}
