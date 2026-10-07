using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The signature rules of the upper floors, switched on per level by <see cref="LevelData.rules"/>:
    /// currents, sticky candy, spreading poison, darkness, ice, wind, lasers and teleports, trampolines,
    /// cracking glass, blinking tiles and rolling barrels. (Hunting blocks live in the hazard system.)
    /// Special tiles are picked from the platform with a seeded random, so a level always looks the same.
    /// </summary>
    public class FloorRules : MonoBehaviour
    {
        /// <summary>Something rule-made (a laser, a barrel) hit this tile; the game treats it like a block impact.</summary>
        public event Action<GridPos> Hit;

        private GridModel grid;
        private GridView view;
        private Robot robot;
        private HazardSystem hazards;
        private FxSystem fx;
        private CameraRig rig;
        private LevelData level;
        private FloorRule rules;
        private Func<GridPos, bool> isProtected;
        private System.Random rng;
        private bool running;
        private float time;
        private readonly List<GameObject> spawned = new List<GameObject>();

        private enum Special { None, Current, Sticky, Ice, Trampoline, Glass, BlinkA, BlinkB, Teleport }
        private Special[,] special;
        private Direction[,] flow;
        private readonly Dictionary<GridPos, Transform> currentArrows = new Dictionary<GridPos, Transform>();
        private int pushChain; // currents in a row without the player moving: a safety stop
        private bool stuck;
        private Direction? slide;                 // the direction the robot is travelling (ice keeps it going)
        private GridPos? teleportLock;            // the pad the robot just arrived on through a teleport
        private readonly List<GridPos> pads = new List<GridPos>();
        private readonly HashSet<GridPos> cracked = new HashSet<GridPos>();
        private readonly HashSet<GridPos> poisoned = new HashSet<GridPos>();
        private float poisonTimer, windTimer, laserTimer, barrelTimer, blinkClock;
        private GridPos? poisonNext;
        private Direction windDir;
        private bool windWarning;
        private int blinkOff = -1;                // which group is a hole right now (0 = A, 1 = B, -1 = none)
        private float difficulty;

        public bool Has(FloorRule r) => (rules & r) != 0;

        public void Init(GridView gridView, Robot bot, HazardSystem hazardSystem, FxSystem fxSystem, CameraRig cameraRig)
        {
            view = gridView;
            robot = bot;
            hazards = hazardSystem;
            fx = fxSystem;
            rig = cameraRig;
        }

        public void Begin(GridModel model, LevelData data, int seed, Func<GridPos, bool> protectedTile)
        {
            Stop();
            grid = model;
            level = data;
            rules = data.rules;
            isProtected = protectedTile;
            rng = new System.Random(seed);
            special = new Special[grid.Width, grid.Height];
            flow = new Direction[grid.Width, grid.Height];
            stuck = false;
            slide = null;
            teleportLock = null;
            time = 0f;
            running = true;
            robot.CanLeave = CanLeave;
            if (rules == FloorRule.None) return;

            float d = difficulty = LevelCatalog.Difficulty(Mathf.Clamp(seed, 0, LevelCatalog.LevelCount - 1));
            if (Has(FloorRule.Current)) Mark(Special.Current, 0.22f);
            if (Has(FloorRule.Current)) FixCurrentLoops();
            if (Has(FloorRule.Sticky)) Mark(Special.Sticky, 0.22f);
            if (Has(FloorRule.Ice)) Mark(Special.Ice, 0.34f);
            if (Has(FloorRule.Trampoline)) Mark(Special.Trampoline, 0.14f);
            if (Has(FloorRule.Glass)) Mark(Special.Glass, 0.4f);
            if (Has(FloorRule.Blink)) { Mark(Special.BlinkA, 0.16f); Mark(Special.BlinkB, 0.16f); }
            if (Has(FloorRule.Teleport)) PlacePads();
            if (Has(FloorRule.Dark)) view.Light = Lit;

            poisonTimer = Mathf.Lerp(3.6f, 2.2f, d);
            windTimer = 3f;
            laserTimer = 2.5f;
            barrelTimer = 3f;
            blinkClock = 0f;
            if (Has(FloorRule.Poison)) SeedPoison();
        }

        public void Freeze() => running = false;
        public void Resume() => running = grid != null;

        public void Stop()
        {
            running = false;
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
            currentArrows.Clear();
            streaks.Clear();
            beams.Clear();
            rolls.Clear();
            pads.Clear();
            cracked.Clear();
            poisoned.Clear();
            covered.Clear();
            poisonNext = null;
            blinkOff = -1;
            windWarning = false;
            if (view != null) view.Light = null;
            if (robot != null) robot.CanLeave = null;
            rules = FloorRule.None;
        }

        // ---------- Tools ----------

        private readonly List<(GridPos pos, float left)> covered = new List<(GridPos, float)>();

        /// <summary>Bridge tool over poison: the tile is safe for a while, then the poison seeps back.</summary>
        public void Cover(GridPos p, float seconds)
        {
            if (!poisoned.Contains(p)) return;
            poisoned.Remove(p);
            grid.SetTile(p, TileState.Solid);
            view.SetTint(p, new Color(0.6f, 0.95f, 1f), new Color(0.3f, 1.2f, 1.6f));
            covered.Add((p, seconds));
        }

        /// <summary>EMP: lasers about to fire and rolling barrels are switched off.</summary>
        public void ClearActive()
        {
            foreach (var b in beams) if (b.bar != null) Destroy(b.bar.gameObject);
            beams.Clear();
            foreach (var r in rolls)
            {
                if (r.body != null) { fx.Burst(r.body.position, new Color(0.75f, 0.45f, 0.25f), Color.black, 12, 3f); Destroy(r.body.gameObject); }
                if (r.arrow != null) Destroy(r.arrow.gameObject);
            }
            rolls.Clear();
            laserTimer = Mathf.Max(laserTimer, 2f);
            barrelTimer = Mathf.Max(barrelTimer, 2f);
        }

        private void UpdateCovered(float dt)
        {
            for (int i = covered.Count - 1; i >= 0; i--)
            {
                var (p, left) = covered[i];
                left -= dt;
                if (left > 0f) { covered[i] = (p, left); continue; }
                covered.RemoveAt(i);
                if (robot.Position != p || !robot.IsAlive) Poison(p);
                else covered.Add((p, 0.5f)); // wait until the robot steps off
            }
        }

        // ---------- Placing special tiles ----------

        /// <summary>Marks a share of the free floor tiles (never the start, keys, door or other specials).</summary>
        /// <summary>
        /// Currents must never trap the robot: following the arrows from any current has to end somewhere. Where a
        /// chain of currents comes back on itself (two facing each other, or a ring), one of them is turned to point
        /// off the chain, onto a plain tile.
        /// </summary>
        private void FixCurrentLoops()
        {
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (special[x, y] != Special.Current) continue;
                    var seen = new HashSet<GridPos>();
                    var q = new GridPos(x, y);
                    while (grid.InBounds(q) && special[q.x, q.y] == Special.Current)
                    {
                        if (!seen.Add(q))
                        {
                            Redirect(q);
                            break;
                        }
                        q += flow[q.x, q.y].ToOffset();
                    }
                }
        }

        private void Redirect(GridPos p)
        {
            Direction? best = null;
            foreach (var d in DirectionExtensions.All)
            {
                var n = p + d.ToOffset();
                if (!grid.InBounds(n) || !grid.IsFloor(n)) continue;
                if (special[n.x, n.y] == Special.Current) continue;
                best = d;
                if (grid.IsStandable(n)) break;
            }
            if (!best.HasValue)
            {
                // Hemmed in by currents: this one becomes a plain tile.
                special[p.x, p.y] = Special.None;
                if (currentArrows.TryGetValue(p, out var gone) && gone != null) Destroy(gone.gameObject);
                currentArrows.Remove(p);
                view.SetTint(p, null);
                return;
            }
            flow[p.x, p.y] = best.Value;
            if (currentArrows.TryGetValue(p, out var arrow) && arrow != null)
            {
                var o = best.Value.ToOffset();
                arrow.localRotation = Quaternion.LookRotation(new Vector3(o.x, 0f, o.y));
            }
        }

        private void Mark(Special kind, float share)
        {
            var free = FreeTiles();
            int count = Mathf.Max(1, Mathf.RoundToInt(grid.FloorCount * share));
            for (int i = 0; i < count && free.Count > 0; i++)
            {
                int k = rng.Next(free.Count);
                var p = free[k];
                free.RemoveAt(k);
                special[p.x, p.y] = kind;
                Decorate(p, kind);
            }
        }

        private List<GridPos> FreeTiles()
        {
            var list = new List<GridPos>();
            var start = grid.StartSpot ?? grid.CenterFloor();
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsFloor(p) || special[p.x, p.y] != Special.None) continue;
                if (p == start || (grid.DoorSpot.HasValue && p == grid.DoorSpot.Value) || grid.KeySpots.Contains(p)) continue;
                if (isProtected != null && isProtected(p)) continue;
                list.Add(p);
            }
            return list;
        }

        private void PlacePads()
        {
            var free = FreeTiles();
            if (free.Count < 2) return;
            var a = free[rng.Next(free.Count)];
            GridPos b = a;
            int best = -1;
            foreach (var p in free)
                if (p.Manhattan(a) > best) { best = p.Manhattan(a); b = p; }
            foreach (var p in new[] { a, b })
            {
                special[p.x, p.y] = Special.Teleport;
                pads.Add(p);
                Decorate(p, Special.Teleport);
            }
        }

        private Material Mat(Color c, Color glow) => MaterialFactory.Create(c, glow);

        /// <summary>Paints the tile and hangs a small marking on it so each special tile reads at a glance.</summary>
        private void Decorate(GridPos p, Special kind)
        {
            var surface = view.Surface(p);
            if (surface == null) return;
            switch (kind)
            {
                case Special.Current:
                {
                    var dirs = DirectionExtensions.All;
                    var dir = dirs[rng.Next(dirs.Length)];
                    flow[p.x, p.y] = dir;
                    view.SetTint(p, new Color(0.55f, 0.85f, 0.95f), new Color(0.1f, 0.35f, 0.45f));
                    var arrow = new GameObject("Current").transform;
                    arrow.SetParent(surface, false);
                    currentArrows[p] = arrow;
                    arrow.localPosition = new Vector3(0f, 0.06f, 0f);
                    var o = dir.ToOffset();
                    arrow.localRotation = Quaternion.LookRotation(new Vector3(o.x, 0f, o.y));
                    var m = Mat(new Color(0.85f, 1f, 1f), new Color(0.6f, 1.6f, 2f));
                    for (int i = 0; i < 2; i++)
                    {
                        var chevron = new GameObject("Chevron").transform;
                        chevron.SetParent(arrow, false);
                        chevron.localPosition = new Vector3(0f, 0f, -0.12f + i * 0.2f);
                        Shapes.Rounded("L", chevron, new Vector3(-0.07f, 0f, -0.04f), new Vector3(0.18f, 0.02f, 0.05f), 0.02f, m).transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
                        Shapes.Rounded("R", chevron, new Vector3(0.07f, 0f, -0.04f), new Vector3(0.18f, 0.02f, 0.05f), 0.02f, m).transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
                    }
                    arrow.gameObject.AddComponent<FlowArrow>();
                    break;
                }
                case Special.Sticky:
                    view.SetTint(p, new Color(1f, 0.6f, 0.82f), new Color(0.35f, 0.08f, 0.2f));
                    var goo = Mat(new Color(1f, 0.45f, 0.75f), new Color(0.6f, 0.1f, 0.35f));
                    Shapes.Rounded("Goo", surface, new Vector3(0f, 0.07f, 0f), new Vector3(0.5f, 0.04f, 0.42f), 0.02f, goo);
                    Shapes.Rounded("Drop", surface, new Vector3(0.14f, 0.09f, 0.1f), new Vector3(0.14f, 0.05f, 0.14f), 0.025f, goo);
                    break;
                case Special.Ice:
                    view.SetTint(p, new Color(0.82f, 0.95f, 1f), new Color(0.35f, 0.55f, 0.7f));
                    var shine = Mat(Color.white, new Color(1.2f, 1.5f, 1.7f));
                    Shapes.Rounded("Glint", surface, new Vector3(-0.12f, 0.06f, 0.12f), new Vector3(0.22f, 0.01f, 0.04f), 0.01f, shine).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    break;
                case Special.Trampoline:
                {
                    view.SetTint(p, new Color(1f, 0.85f, 0.45f), new Color(0.4f, 0.25f, 0.05f));
                    var spring = Mat(new Color(1f, 0.55f, 0.2f), new Color(1.4f, 0.6f, 0.1f));
                    for (int i = 0; i < 3; i++)
                        Shapes.Rounded("Coil", surface, new Vector3(0f, 0.07f + i * 0.04f, 0f), new Vector3(0.36f - i * 0.04f, 0.025f, 0.36f - i * 0.04f), 0.012f, spring);
                    Shapes.Rounded("Pad", surface, new Vector3(0f, 0.19f, 0f), new Vector3(0.42f, 0.04f, 0.42f), 0.02f, Mat(new Color(1f, 0.9f, 0.5f), new Color(0.6f, 0.45f, 0.1f)));
                    break;
                }
                case Special.Glass:
                    view.SetTint(p, new Color(0.75f, 0.9f, 1f), new Color(0.3f, 0.5f, 0.7f));
                    break;
                case Special.BlinkA:
                case Special.BlinkB:
                {
                    bool a = kind == Special.BlinkA;
                    view.SetTint(p, a ? new Color(1f, 0.55f, 0.95f) : new Color(0.55f, 0.75f, 1f), a ? new Color(0.5f, 0.1f, 0.45f) : new Color(0.1f, 0.25f, 0.55f));
                    break;
                }
                case Special.Teleport:
                {
                    view.SetTint(p, new Color(0.8f, 0.6f, 1f), new Color(0.45f, 0.2f, 0.7f));
                    var ring = Mat(new Color(0.85f, 0.6f, 1f), new Color(1.6f, 0.6f, 2.4f));
                    const int pieces = 10;
                    var root = new GameObject("Portal").transform;
                    root.SetParent(surface, false);
                    root.localPosition = new Vector3(0f, 0.07f, 0f);
                    for (int i = 0; i < pieces; i++)
                    {
                        float ang = i * Mathf.PI * 2f / pieces;
                        Shapes.Rounded("Piece", root, new Vector3(Mathf.Cos(ang) * 0.24f, 0f, Mathf.Sin(ang) * 0.24f), new Vector3(0.08f, 0.03f, 0.05f), 0.015f, ring)
                            .transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                    }
                    root.gameObject.AddComponent<Spin>();
                    break;
                }
            }
        }

        // ---------- Robot ----------

        /// <summary>Sticky candy: the first swipe only pulls the robot free.</summary>
        private bool CanLeave(Direction dir)
        {
            if (!stuck) return true;
            stuck = false;
            fx.Dust(robot.transform.position + Vector3.up * 0.1f, new Color(1f, 0.5f, 0.8f), 6, 1.2f);
            return false;
        }

        /// <summary>
        /// The robot landed on <paramref name="p"/>. Returns true when a rule moved it on (the next landing
        /// will come back here).
        /// </summary>
        public bool OnArrived(GridPos p, GridPos left)
        {
            if (grid == null || !robot.IsAlive) return false;

            // Glass cracks under the first step and shatters when the robot steps off.
            if (cracked.Contains(left) && left != p)
            {
                cracked.Remove(left);
                hazards.Collapse(left, 8f);
                view.SetTint(left, new Color(0.75f, 0.9f, 1f), new Color(0.3f, 0.5f, 0.7f));
                AudioManager.PlaySfx(Sfx.Blocked, 0.5f, 1.6f);
            }

            var kind = grid.InBounds(p) ? special[p.x, p.y] : Special.None;
            if (kind != Special.Current) pushChain = 0;
            var travel = slide ?? robot.Facing;
            slide = null;
            if (teleportLock.HasValue && teleportLock.Value != p) teleportLock = null;

            switch (kind)
            {
                case Special.Glass:
                    if (!cracked.Contains(p))
                    {
                        cracked.Add(p);
                        view.SetTint(p, new Color(1f, 0.85f, 0.9f), new Color(0.6f, 0.2f, 0.3f));
                        fx.Dust(GridView.ToWorld(p) + Vector3.up * 0.08f, Color.white, 8, 1.4f);
                        AudioManager.PlaySfx(Sfx.Click, 0.6f, 1.8f);
                    }
                    return false;

                case Special.Sticky:
                    stuck = true;
                    AudioManager.PlaySfx(Sfx.Bump, 0.5f, 0.6f);
                    return false;

                case Special.Current:
                    // Never an endless ping-pong between currents: after a few pushes in a row the robot is let go.
                    if (++pushChain > 6)
                    {
                        pushChain = 0;
                        return false;
                    }
                    return Push(flow[p.x, p.y], 1, 0.3f, 1.4f, Sfx.Hop);

                case Special.Ice:
                    return Push(travel, 1, 0.12f, 1.7f, Sfx.Click);

                case Special.Trampoline:
                {
                    AudioManager.PlaySfx(Sfx.Hop, 1f, 1.5f);
                    view.Bounce(p, 2f);
                    if (Push(travel, 2, 1.5f, 0.9f, Sfx.Hop)) return true;
                    return Push(travel, 1, 1.4f, 0.9f, Sfx.Hop);
                }

                case Special.Teleport:
                {
                    if (teleportLock.HasValue && teleportLock.Value == p) return false;
                    var other = pads[0] == p ? pads[1] : pads[0];
                    if (!grid.IsStandable(other)) return false;
                    fx.Burst(robot.transform.position + Vector3.up * 0.4f, new Color(0.8f, 0.6f, 1f), new Color(1.6f, 0.6f, 2.4f), 18, 4f);
                    AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.6f);
                    teleportLock = other;
                    robot.RescueTo(other);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Shoves the robot <paramref name="distance"/> tiles. Into holes and fire it goes (and falls); against the
        /// platform's edge, a pillar or a landed block it simply stops.
        /// </summary>
        private bool Push(Direction dir, int distance, float height, float speed, Sfx sound)
        {
            var o = dir.ToOffset();
            var target = new GridPos(robot.Position.x + o.x * distance, robot.Position.y + o.y * distance);
            if (!grid.Exists(target) || grid.IsOccupied(target)) return false;
            slide = dir;
            robot.Shove(dir, distance, height, speed);
            AudioManager.PlaySfx(sound, 0.4f, 1.3f, 0.05f);
            return true;
        }

        // ---------- Timed rules ----------

        private void Update()
        {
            if (!running || grid == null) return;
            float dt = Time.deltaTime;
            time += dt;

            if (covered.Count > 0) UpdateCovered(dt);
            if (Has(FloorRule.Poison)) UpdatePoison(dt);
            if (Has(FloorRule.Wind)) UpdateWind(dt);
            if (Has(FloorRule.Laser)) UpdateLasers(dt);
            if (Has(FloorRule.Barrel)) UpdateBarrels(dt);
            if (Has(FloorRule.Blink)) UpdateBlink(dt);
        }

        // Poison: starts on one far tile and spreads to a neighbour every few seconds, glowing green first.
        private void SeedPoison()
        {
            GridPos? far = null;
            int best = -1;
            foreach (var p in FreeTiles())
                if (p.Manhattan(robot.Position) > best) { best = p.Manhattan(robot.Position); far = p; }
            if (far.HasValue) Poison(far.Value);
        }

        private void Poison(GridPos p)
        {
            grid.SetTile(p, TileState.Poison);
            poisoned.Add(p);
            view.SetTint(p, new Color(0.45f, 0.9f, 0.35f), new Color(0.35f, 1.2f, 0.2f));
            var surface = view.Surface(p);
            if (surface != null)
            {
                var bubble = Mat(new Color(0.6f, 1f, 0.45f), new Color(0.5f, 1.5f, 0.3f));
                for (int i = 0; i < 3; i++)
                    Shapes.Rounded("Bubble", surface, new Vector3(-0.15f + i * 0.15f, 0.08f, (i % 2) * 0.12f - 0.06f), Vector3.one * (0.07f + i * 0.02f), 0.035f, bubble);
            }
            fx.Dust(GridView.ToWorld(p) + Vector3.up * 0.1f, new Color(0.5f, 1f, 0.4f), 8, 1.2f);
        }

        private void UpdatePoison(float dt)
        {
            if (poisoned.Count >= grid.FloorCount * 0.35f) return;
            poisonTimer -= dt;
            if (poisonNext.HasValue)
            {
                view.SetWarning(poisonNext.Value, 0.5f + 0.5f * Mathf.Sin(time * 20f));
                if (poisonTimer <= 0f)
                {
                    var p = poisonNext.Value;
                    poisonNext = null;
                    if (grid.GetTile(p) == TileState.Solid && !grid.IsOccupied(p)) Poison(p);
                    poisonTimer = Mathf.Lerp(3.6f, 2.2f, difficulty);
                }
                return;
            }
            if (poisonTimer > 1f) return;
            // Pick the next tile: a solid neighbour of the poison, away from the door and keys.
            var options = new List<GridPos>();
            foreach (var q in poisoned)
                foreach (var dir in DirectionExtensions.All)
                {
                    var n = q + dir.ToOffset();
                    if (grid.IsStandable(n) && special[n.x, n.y] == Special.None && (isProtected == null || !isProtected(n)) && !grid.KeySpots.Contains(n)) options.Add(n);
                }
            if (options.Count == 0) { poisonTimer = 3f; return; }
            poisonNext = options[rng.Next(options.Count)];
        }

        // Darkness: tiles fade out beyond a couple of steps from the robot.
        private float Lit(GridPos p)
        {
            var r = robot.transform.position;
            float dist = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(r.x, r.z));
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 2.3f, dist));
        }

        // Wind: an arrow warning, then a gust that pushes the robot one tile.
        private readonly List<Transform> streaks = new List<Transform>();

        private void UpdateWind(float dt)
        {
            windTimer -= dt;
            if (!windWarning && windTimer <= 1.6f)
            {
                windWarning = true;
                windDir = DirectionExtensions.All[rng.Next(4)];
                AudioManager.PlaySfx(Sfx.Warning, 0.5f, 0.6f);
                ShowStreaks(true);
            }
            if (windWarning) MoveStreaks(dt);
            if (windTimer > 0f) return;

            windWarning = false;
            windTimer = Mathf.Lerp(7f, 4.5f, difficulty);
            ShowStreaks(false);
            rig.Shake(0.4f);
            AudioManager.PlaySfx(Sfx.Fall, 0.6f, 1.4f);
            if (robot.IsAlive && !robot.IsHopping && !robot.IsHovering) Push(windDir, 1, 0.25f, 1.2f, Sfx.Hop);
        }

        private void ShowStreaks(bool on)
        {
            if (streaks.Count == 0)
            {
                var m = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.55f), new Color(0.8f, 0.9f, 1f));
                for (int i = 0; i < 14; i++)
                {
                    var s = Shapes.Rounded("Wind", transform, Vector3.zero, new Vector3(0.5f, 0.025f, 0.025f), 0.012f, m).transform;
                    streaks.Add(s);
                    spawned.Add(s.gameObject);
                }
            }
            var o = windDir.ToOffset();
            var rot = Quaternion.LookRotation(new Vector3(o.x, 0f, o.y)) * Quaternion.Euler(0f, 90f, 0f);
            foreach (var s in streaks)
            {
                s.gameObject.SetActive(on);
                s.rotation = rot;
                s.position = new Vector3((float)rng.NextDouble() * grid.Width, 0.25f + (float)rng.NextDouble() * 0.9f, (float)rng.NextDouble() * grid.Height);
            }
        }

        private void MoveStreaks(float dt)
        {
            var o = windDir.ToOffset();
            var v = new Vector3(o.x, 0f, o.y) * 6f * dt;
            foreach (var s in streaks)
            {
                s.position += v;
                if (s.position.x < -2f || s.position.z < -2f || s.position.x > grid.Width + 1 || s.position.z > grid.Height + 1)
                    s.position -= new Vector3(o.x * (grid.Width + 3), 0f, o.y * (grid.Height + 3));
            }
        }

        // Lasers: a row or column flickers red for a moment, then a beam sweeps it.
        private class Beam
        {
            public List<GridPos> line;
            public float warn, fire;
            public Transform bar;
            public Material material;
            public bool fired;
        }

        private readonly List<Beam> beams = new List<Beam>();

        private void UpdateLasers(float dt)
        {
            laserTimer -= dt;
            if (laserTimer <= 0f)
            {
                laserTimer = Mathf.Lerp(5f, 3f, difficulty);
                TrySpawnBeam();
            }
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                var b = beams[i];
                if (b.warn > 0f)
                {
                    b.warn -= dt;
                    float flicker = 0.3f + 0.3f * Mathf.Sin(time * 30f);
                    MaterialFactory.SetColors(b.material, new Color(1f, 0.3f, 0.35f, flicker), new Color(2f, 0.2f, 0.25f) * flicker);
                    b.bar.localScale = new Vector3(1f, 0.25f, 0.25f);
                    foreach (var p in b.line) view.SetWarning(p, flicker + 0.3f);
                    continue;
                }
                if (!b.fired)
                {
                    b.fired = true;
                    b.fire = 0.4f;
                    MaterialFactory.SetColors(b.material, new Color(1f, 0.6f, 0.65f, 0.95f), new Color(4f, 0.6f, 0.7f));
                    b.bar.localScale = Vector3.one;
                    AudioManager.PlaySfx(Sfx.Impact, 0.7f, 1.8f);
                    rig.Shake(0.3f);
                    if (robot.IsAlive && !robot.IsHovering && b.line.Contains(robot.Position)) Hit?.Invoke(robot.Position);
                }
                b.fire -= dt;
                if (b.fire <= 0f)
                {
                    Destroy(b.bar.gameObject);
                    beams.RemoveAt(i);
                }
            }
        }

        private void TrySpawnBeam()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                bool row = rng.Next(2) == 0;
                int index = row ? Mathf.Clamp(robot.Position.y + rng.Next(-1, 2), 0, grid.Height - 1) : Mathf.Clamp(robot.Position.x + rng.Next(-1, 2), 0, grid.Width - 1);
                var line = new List<GridPos>();
                foreach (var p in grid.AllPositions())
                    if (grid.IsFloor(p) && (row ? p.y == index : p.x == index)) line.Add(p);
                if (line.Count == 0) continue;
                if (!SafetyChecker.HasEscape(grid, robot.Position, line, 3)) continue;

                var material = MaterialFactory.CreateTransparent(new Color(1f, 0.3f, 0.35f, 0.4f), new Color(2f, 0.2f, 0.25f));
                var bar = new GameObject("Laser").transform;
                bar.SetParent(transform, false);
                float length = row ? grid.Width + 0.6f : grid.Height + 0.6f;
                var center = row ? new Vector3((grid.Width - 1) * 0.5f, 0.35f, index) : new Vector3(index, 0.35f, (grid.Height - 1) * 0.5f);
                bar.position = center;
                Shapes.Rounded("Beam", bar, Vector3.zero, row ? new Vector3(length, 0.1f, 0.1f) : new Vector3(0.1f, 0.1f, length), 0.04f, material);
                spawned.Add(bar.gameObject);
                beams.Add(new Beam { line = line, warn = 1.2f, bar = bar, material = material });
                AudioManager.PlaySfx(Sfx.Warning, 0.5f, 1.6f);
                return;
            }
        }

        // Barrels: one rolls along a row or column; anything in its path gets flattened.
        private class Roll
        {
            public List<GridPos> line;
            public Direction dir;
            public float pos, warn;
            public Transform body, arrow;
        }

        private readonly List<Roll> rolls = new List<Roll>();

        private void UpdateBarrels(float dt)
        {
            barrelTimer -= dt;
            if (barrelTimer <= 0f)
            {
                barrelTimer = Mathf.Lerp(6f, 3.5f, difficulty);
                TrySpawnBarrel();
            }
            for (int i = rolls.Count - 1; i >= 0; i--)
            {
                var r = rolls[i];
                var o = r.dir.ToOffset();
                if (r.warn > 0f)
                {
                    r.warn -= dt;
                    foreach (var p in r.line) view.SetWarning(p, 0.35f + 0.25f * Mathf.Sin(time * 16f));
                    r.arrow.localScale = Vector3.one * (1f + Mathf.Sin(time * 14f) * 0.15f);
                    continue;
                }
                if (r.arrow.gameObject.activeSelf) r.arrow.gameObject.SetActive(false);
                float before = r.pos;
                r.pos += dt * 4f;
                var start = r.line[0];
                var at = new Vector3(start.x + o.x * r.pos, 0.3f, start.y + o.y * r.pos);
                r.body.position = at;
                r.body.Rotate(new Vector3(o.y, 0f, -o.x), 400f * dt, Space.World);

                // Tiles crossed this frame.
                for (int k = Mathf.Max(0, Mathf.CeilToInt(before)); k <= Mathf.FloorToInt(r.pos) && k < r.line.Count; k++)
                {
                    var p = r.line[k];
                    if (robot.IsAlive && !robot.IsHovering && robot.Position == p && !robot.IsHopping) Hit?.Invoke(p);
                }
                if (r.pos > r.line.Count + 1f)
                {
                    Destroy(r.body.gameObject);
                    Destroy(r.arrow.gameObject);
                    rolls.RemoveAt(i);
                }
            }
        }

        private void TrySpawnBarrel()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                bool row = rng.Next(2) == 0;
                int index = row ? Mathf.Clamp(robot.Position.y + rng.Next(-1, 2), 0, grid.Height - 1) : Mathf.Clamp(robot.Position.x + rng.Next(-1, 2), 0, grid.Width - 1);
                bool forward = rng.Next(2) == 0;
                var dir = row ? (forward ? Direction.PlusX : Direction.MinusX) : (forward ? Direction.PlusY : Direction.MinusY);
                var line = new List<GridPos>();
                int n = row ? grid.Width : grid.Height;
                for (int k = 0; k < n; k++)
                {
                    int c = forward ? k : n - 1 - k;
                    line.Add(row ? new GridPos(c, index) : new GridPos(index, c));
                }
                var danger = line.FindAll(p => grid.IsFloor(p));
                if (danger.Count == 0 || !SafetyChecker.HasEscape(grid, robot.Position, danger, 3)) continue;

                var wood = MaterialFactory.Create(new Color(0.75f, 0.45f, 0.25f), Color.black);
                var band = MaterialFactory.Create(new Color(0.95f, 0.8f, 0.3f), new Color(0.8f, 0.5f, 0.1f));
                var body = new GameObject("Barrel").transform;
                body.SetParent(transform, false);
                var o = dir.ToOffset();
                body.position = new Vector3(line[0].x - o.x, 0.3f, line[0].y - o.y);
                var axis = Quaternion.LookRotation(new Vector3(o.y, 0f, -o.x));
                var cyl = Shapes.Primitive(PrimitiveType.Cylinder, "Body", body, Vector3.zero, new Vector3(0.5f, 0.32f, 0.5f), wood).transform;
                cyl.rotation = axis * Quaternion.Euler(90f, 0f, 0f);
                foreach (float off in new[] { -0.18f, 0.18f })
                {
                    Shapes.Primitive(PrimitiveType.Cylinder, "Band", cyl, new Vector3(0f, off / 0.32f * 0.5f, 0f), new Vector3(1.04f, 0.08f, 1.04f), band);
                }
                var arrow = new GameObject("Arrow").transform;
                arrow.SetParent(transform, false);
                arrow.position = new Vector3(line[0].x - o.x * 0.9f, 0.1f, line[0].y - o.y * 0.9f);
                arrow.rotation = Quaternion.LookRotation(new Vector3(o.x, 0f, o.y));
                var red = MaterialFactory.Create(new Color(1f, 0.4f, 0.4f), new Color(2f, 0.3f, 0.3f));
                Shapes.Rounded("L", arrow, new Vector3(-0.08f, 0f, 0f), new Vector3(0.26f, 0.03f, 0.07f), 0.02f, red).transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
                Shapes.Rounded("R", arrow, new Vector3(0.08f, 0f, 0f), new Vector3(0.26f, 0.03f, 0.07f), 0.02f, red).transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
                spawned.Add(body.gameObject);
                spawned.Add(arrow.gameObject);
                rolls.Add(new Roll { line = line, dir = dir, pos = -1f, warn = 1.3f, body = body, arrow = arrow });
                AudioManager.PlaySfx(Sfx.Warning, 0.5f, 0.8f);
                return;
            }
        }

        // Blinking tiles: the pink group and the blue group take turns dropping out (with a flash first).
        private void UpdateBlink(float dt)
        {
            blinkClock += dt;
            const float period = 7f;
            float t = blinkClock % period;
            // 0-1 all on, 1-2 pink flashes, 2-4 pink gone, 4-4.5 all on, 4.5-5.5 blue flashes, 5.5-7 blue gone.
            int wantOff = t >= 2f && t < 4f ? 0 : t >= 5.5f ? 1 : -1;
            int flashing = t >= 1f && t < 2f ? 0 : t >= 4.5f && t < 5.5f ? 1 : -1;

            if (flashing >= 0)
                foreach (var p in grid.AllPositions())
                    if (special[p.x, p.y] == (flashing == 0 ? Special.BlinkA : Special.BlinkB)) view.SetWarning(p, 0.5f + 0.5f * Mathf.Sin(time * 24f));

            if (wantOff == blinkOff) return;
            if (blinkOff >= 0) SetGroup(blinkOff, on: true);
            blinkOff = wantOff;
            if (blinkOff >= 0) SetGroup(blinkOff, on: false);
        }

        private void SetGroup(int group, bool on)
        {
            var kind = group == 0 ? Special.BlinkA : Special.BlinkB;
            foreach (var p in grid.AllPositions())
            {
                if (special[p.x, p.y] != kind) continue;
                if (on)
                {
                    if (grid.GetTile(p) != TileState.Broken) continue;
                    grid.SetTile(p, TileState.Solid);
                    view.Repair(p);
                }
                else if (grid.GetTile(p) == TileState.Solid && !grid.IsOccupied(p))
                {
                    grid.SetTile(p, TileState.Broken);
                    view.Break(p);
                }
            }
            AudioManager.PlaySfx(Sfx.Click, 0.4f, on ? 1.2f : 0.7f);
        }
    }

    /// <summary>Chevrons that drift along their tile to show which way the current flows.</summary>
    public class FlowArrow : MonoBehaviour
    {
        private Transform[] chevrons;

        private void Start()
        {
            chevrons = new Transform[transform.childCount];
            for (int i = 0; i < chevrons.Length; i++) chevrons[i] = transform.GetChild(i);
        }

        private void Update()
        {
            for (int i = 0; i < chevrons.Length; i++)
            {
                float z = Mathf.Repeat(Time.time * 0.6f + i * 0.5f, 1f);
                chevrons[i].localPosition = new Vector3(0f, 0f, -0.22f + z * 0.44f);
            }
        }
    }

    /// <summary>Slow spin for teleport pads.</summary>
    public class Spin : MonoBehaviour
    {
        private void Update() => transform.Rotate(0f, 90f * Time.deltaTime, 0f);
    }
}
