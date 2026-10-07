using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;
using Random = System.Random;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The moving enemies of the scenario document. Each one shows what it is about to do before it does it (a rail
    /// line, a scan cone, a swelling ring, a shadow, a drawn path), never kills without that warning, and has a counter
    /// the player can learn: step off the rail, leave the cone, get out of the ring, change rows, hop onto an eraser to
    /// topple it. They live on the floor's grid; <see cref="Hit"/> means one got the robot, <see cref="Shove"/> that
    /// one pushed it a tile along.
    /// </summary>
    public class EnemySystem : MonoBehaviour
    {
        private const float HopTime = 0.2f;

        private class Enemy
        {
            public EnemyKind kind;
            public GridPos pos;
            public Direction dir;
            public float timer, stun, warn = -1f;
            public Transform view, model, clawL, clawR;
            public Vector3 from, to;
            public float hopT = 1f;
            public readonly List<GridPos> line = new List<GridPos>();   // rail, scan cone, dive line, slide path, shot line
            public readonly List<Transform> marks = new List<Transform>();
            public GridPos? target;
            public float seen;                                          // drone: how long the robot has been in the cone
            public GridPos? shot;                                       // turret: where its shot is
            public Transform bullet;
        }

        /// <summary>An enemy got the robot on this tile (it hurts like a block).</summary>
        public event Action<GridPos> Hit;
        /// <summary>An enemy pushed the robot a tile in this direction (the robot is moved by the game).</summary>
        public event Action<Direction> Shove;

        private readonly List<Enemy> enemies = new List<Enemy>();
        private GridModel grid;
        private LevelData level;
        private Robot robot;
        private HazardSystem hazards;
        private GridView view;
        private FxSystem fx;
        private Random rng;
        private bool running;
        private float time;
        private Func<GridPos, bool> isProtected;
        private Func<GridPos, bool> isPainted;
        private Action<GridPos> unpaint;
        private Material railMat, coneMat, ringMat, pathMat, shadowMat, bulletMat;

        public void Init(GridView gridView, Robot bot, HazardSystem hazardSystem, FxSystem fxSystem)
        {
            view = gridView;
            robot = bot;
            hazards = hazardSystem;
            fx = fxSystem;
        }

        public void Begin(GridModel model, LevelData data, int seed, Func<GridPos, bool> protectedTile, Func<GridPos, bool> painted, Action<GridPos> erase)
        {
            Stop();
            grid = model;
            level = data;
            rng = new Random(seed * 17 + 3);
            isProtected = protectedTile;
            isPainted = painted;
            unpaint = erase;
            time = 0f;
            if (railMat == null)
            {
                railMat = MaterialFactory.CreateTransparent(new Color(1f, 0.85f, 0.3f, 0.45f), new Color(1.2f, 0.9f, 0.2f));
                coneMat = MaterialFactory.CreateTransparent(new Color(0.4f, 0.75f, 1f, 0.35f), new Color(0.4f, 1f, 2f));
                ringMat = MaterialFactory.CreateTransparent(new Color(0.85f, 0.6f, 0.35f, 0.6f), new Color(1.2f, 0.7f, 0.3f));
                pathMat = MaterialFactory.CreateTransparent(new Color(0.85f, 0.95f, 1f, 0.4f), new Color(0.6f, 0.9f, 1.4f));
                shadowMat = MaterialFactory.CreateTransparent(new Color(0.05f, 0.05f, 0.1f, 0.5f), Color.black);
                bulletMat = MaterialFactory.Create(new Color(1f, 0.4f, 0.45f), new Color(2.4f, 0.5f, 0.6f));
            }
            Spawn(EnemyKind.Sweeper, data.sweepers);
            Spawn(EnemyKind.Eraser, data.erasers);
            Spawn(EnemyKind.Drone, data.drones);
            Spawn(EnemyKind.Sandworm, data.sandworms);
            Spawn(EnemyKind.Crab, data.crabs);
            Spawn(EnemyKind.Turret, data.turrets);
            Spawn(EnemyKind.Penguin, data.penguins);
            Spawn(EnemyKind.Springbot, data.springbots);
            running = enemies.Count > 0;
        }

        public void Freeze() => running = false;
        public void Resume() => running = grid != null && enemies.Count > 0;

        public void Stop()
        {
            running = false;
            foreach (var e in enemies)
            {
                if (e.view != null) Destroy(e.view.gameObject);
                foreach (var m in e.marks) if (m != null) Destroy(m.gameObject);
                if (e.bullet != null) Destroy(e.bullet.gameObject);
            }
            enemies.Clear();
        }

        /// <summary>True when an enemy stands on the tile (blocks don't land there, the robot can't stand there).</summary>
        public bool Occupies(GridPos p)
        {
            foreach (var e in enemies) if (e.pos == p && e.kind != EnemyKind.Sandworm && e.kind != EnemyKind.Drone) return true;
            return false;
        }

        /// <summary>The robot landed on <paramref name="p"/>: an eraser there is toppled for a while.</summary>
        public void OnRobotArrived(GridPos p)
        {
            // Stepping onto a crab is as bad as a crab stepping onto you.
            foreach (var e in enemies)
                if (e.kind == EnemyKind.Crab && e.pos == p && e.stun <= 0f) { Hit?.Invoke(p); return; }
            foreach (var e in enemies)
                if (e.kind == EnemyKind.Eraser && e.pos == p && e.stun <= 0f)
                {
                    e.stun = 5f;
                    e.model.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    fx.Burst(GridView.ToWorld(p) + Vector3.up * 0.4f, new Color(0.7f, 0.7f, 0.75f), new Color(0.6f, 0.6f, 0.7f), 14, 3f);
                    AudioManager.PlaySfx(Sfx.Blocked, 0.8f, 1.2f);
                }
        }

        // ---------- Spawning ----------

        private void Spawn(EnemyKind kind, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var start = PickStart(kind);
                if (!start.HasValue) return;
                var e = new Enemy { kind = kind, pos = start.Value, dir = DirectionExtensions.All[rng.Next(4)], timer = 1.5f + (float)rng.NextDouble() };
                e.view = new GameObject(kind.ToString()).transform;
                e.view.SetParent(transform, false);
                e.view.position = e.from = e.to = At(e.pos);
                e.model = new GameObject("Model").transform;
                e.model.SetParent(e.view, false);
                switch (kind)
                {
                    case EnemyKind.Sweeper: EnemyModels.Sweeper(e.model); PlanRail(e); break;
                    case EnemyKind.Eraser: EnemyModels.Eraser(e.model); break;
                    case EnemyKind.Drone: EnemyModels.Drone(e.model); PlanPatrol(e); break;
                    case EnemyKind.Sandworm: EnemyModels.Sandworm(e.model); e.model.gameObject.SetActive(false); break;
                    case EnemyKind.Crab: EnemyModels.Crab(e.model, out e.clawL, out e.clawR); e.dir = rng.Next(2) == 0 ? Direction.PlusX : Direction.MinusX; break;
                    case EnemyKind.Turret: EnemyModels.Turret(e.model); AimTurret(e); break;
                    case EnemyKind.Penguin: EnemyModels.Penguin(e.model); break;
                    case EnemyKind.Springbot: EnemyModels.Springbot(e.model); break;
                }
                Face(e);
                enemies.Add(e);
            }
        }

        private GridPos? PickStart(EnemyKind kind)
        {
            var start = grid.StartSpot ?? grid.CenterFloor();
            var options = new List<GridPos>();
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsStandable(p) || p.Manhattan(robot.Position) < 3 || p.Manhattan(start) < 3) continue;
                if (isProtected != null && isProtected(p)) continue;
                if (enemies.Exists(o => o.pos == p)) continue;
                if (kind == EnemyKind.Turret && !IsEdge(p)) continue;
                options.Add(p);
            }
            if (options.Count == 0) return null;
            return options[rng.Next(options.Count)];
        }

        private bool IsEdge(GridPos p)
        {
            foreach (var d in DirectionExtensions.All)
                if (!grid.IsFloor(p + d.ToOffset())) return true;
            return false;
        }

        private static Vector3 At(GridPos p) => GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY;

        private Transform Mark(GridPos p, Material m, float size = 0.86f)
        {
            var t = Shapes.Primitive(PrimitiveType.Cylinder, "Mark", transform, At(p) + Vector3.up * 0.025f, new Vector3(size, 0.008f, size), m).transform;
            return t;
        }

        private void ClearMarks(Enemy e)
        {
            foreach (var m in e.marks) if (m != null) Destroy(m.gameObject);
            e.marks.Clear();
        }

        private void Face(Enemy e)
        {
            var o = e.dir.ToOffset();
            e.view.rotation = Quaternion.LookRotation(new Vector3(o.x, 0f, o.y));
        }

        private void HopTo(Enemy e, GridPos p)
        {
            e.from = e.view.position;
            e.to = At(p);
            e.pos = p;
            e.hopT = 0f;
        }

        private bool Free(GridPos p) =>
            grid.IsStandable(p) && !enemies.Exists(o => o.pos == p && o.kind != EnemyKind.Sandworm && o.kind != EnemyKind.Drone);

        private bool RobotOn(GridPos p) => robot.IsAlive && !robot.IsHovering && robot.Position == p;

        /// <summary>The enemy walks into the robot: a push along its way if there is room, else a hit.</summary>
        private void Bump(Enemy e, Direction dir)
        {
            var o = dir.ToOffset();
            var beyond = robot.Position + o;
            if (grid.IsStandable(beyond) && !enemies.Exists(x => x.pos == beyond)) Shove?.Invoke(dir);
            else Hit?.Invoke(robot.Position);
        }

        // ---------- Update ----------

        private void Update()
        {
            if (!running) return;
            float dt = Time.deltaTime;
            time += dt;
            foreach (var e in enemies.ToArray())
            {
                if (e.hopT < 1f)
                {
                    e.hopT = Mathf.Min(1f, e.hopT + dt / HopTime);
                    e.view.position = Vector3.Lerp(e.from, e.to, e.hopT) + Vector3.up * Mathf.Sin(e.hopT * Mathf.PI) * 0.18f;
                }
                if (e.stun > 0f)
                {
                    e.stun -= dt;
                    if (e.stun <= 0f) e.model.localRotation = Quaternion.identity;
                    continue;
                }
                switch (e.kind)
                {
                    case EnemyKind.Sweeper: UpdateSweeper(e, dt); break;
                    case EnemyKind.Eraser: UpdateEraser(e, dt); break;
                    case EnemyKind.Drone: UpdateDrone(e, dt); break;
                    case EnemyKind.Sandworm: UpdateWorm(e, dt); break;
                    case EnemyKind.Crab: UpdateCrab(e, dt); break;
                    case EnemyKind.Turret: UpdateTurret(e, dt); break;
                    case EnemyKind.Penguin: UpdatePenguin(e, dt); break;
                    case EnemyKind.Springbot: UpdateSpring(e, dt); break;
                }
                if (!running) return;
            }
        }

        // Süpürgeç: back and forth along its rail (drawn in yellow), one tile every 0.7 s.
        private void PlanRail(Enemy e)
        {
            bool row = rng.Next(2) == 0;
            e.dir = row ? Direction.PlusX : Direction.PlusY;
            for (int i = 0; i < Mathf.Max(grid.Width, grid.Height); i++)
            {
                var p = row ? new GridPos(i, e.pos.y) : new GridPos(e.pos.x, i);
                if (grid.IsFloor(p)) { e.line.Add(p); e.marks.Add(Mark(p, railMat, 0.3f)); }
            }
        }

        private void UpdateSweeper(Enemy e, float dt)
        {
            e.timer -= dt;
            if (e.timer > 0f || e.hopT < 1f) return;
            e.timer = 0.7f;
            var next = e.pos + e.dir.ToOffset();
            if (!e.line.Contains(next) || !grid.IsStandable(next) || enemies.Exists(o => o != e && o.pos == next))
            {
                e.dir = e.dir.Opposite();
                Face(e);
                return;
            }
            if (RobotOn(next)) { Bump(e, e.dir); return; }
            HopTo(e, next);
        }

        // Silgi-bot: slowly to the nearest painted tile (or after the robot), greying what it rolls over.
        private void UpdateEraser(Enemy e, float dt)
        {
            e.timer -= dt;
            if (e.timer > 0f || e.hopT < 1f) return;
            e.timer = 1.1f;
            if (isPainted != null && isPainted(e.pos)) unpaint?.Invoke(e.pos);
            GridPos goal = robot.Position;
            int best = int.MaxValue;
            if (isPainted != null)
                foreach (var p in grid.AllPositions())
                    if (isPainted(p) && p.Manhattan(e.pos) < best) { best = p.Manhattan(e.pos); goal = p; }
            var step = StepTowards(e.pos, goal);
            if (!step.HasValue) return;
            e.dir = DirOf(e.pos, step.Value);
            Face(e);
            if (RobotOn(step.Value)) { Bump(e, e.dir); return; }
            HopTo(e, step.Value);
        }

        // Gözcü dron: patrols a line, scanning the three tiles ahead; a second in the cone calls a block down.
        private void PlanPatrol(Enemy e)
        {
            e.dir = rng.Next(2) == 0 ? Direction.PlusX : Direction.PlusY;
        }

        private void UpdateDrone(Enemy e, float dt)
        {
            e.timer -= dt;
            if (e.timer <= 0f && e.hopT >= 1f)
            {
                e.timer = 1.2f;
                var next = e.pos + e.dir.ToOffset();
                if (!grid.IsFloor(next)) { e.dir = e.dir.Opposite(); Face(e); }
                else HopTo(e, next);
            }
            // The cone: the next three tiles ahead.
            e.line.Clear();
            var o = e.dir.ToOffset();
            for (int i = 1; i <= 3; i++)
            {
                var p = new GridPos(e.pos.x + o.x * i, e.pos.y + o.y * i);
                if (grid.IsFloor(p)) e.line.Add(p);
            }
            while (e.marks.Count < e.line.Count) e.marks.Add(Mark(e.pos, coneMat, 0.8f));
            for (int i = 0; i < e.marks.Count; i++)
            {
                bool on = i < e.line.Count;
                e.marks[i].gameObject.SetActive(on);
                if (on) e.marks[i].position = At(e.line[i]) + Vector3.up * 0.03f;
            }
            if (e.line.Contains(robot.Position) && !robot.IsHovering)
            {
                e.seen += dt;
                if (e.seen >= 1f)
                {
                    e.seen = 0f;
                    hazards.DropAt(robot.Position);
                    AudioManager.PlaySfx(Sfx.Warning, 0.6f, 1.6f);
                }
            }
            else e.seen = 0f;
        }

        // Kum solucanı: a ring swells for 1.5 s, then the worm dives along a three-tile line from it.
        private void UpdateWorm(Enemy e, float dt)
        {
            if (e.warn < 0f)
            {
                e.timer -= dt;
                if (e.timer > 0f) return;
                // Next dive near the robot.
                var c = RandomNear(robot.Position, 2);
                e.pos = c;
                e.dir = DirectionExtensions.All[rng.Next(4)];
                e.line.Clear();
                var o = e.dir.ToOffset();
                for (int i = 0; i < 3; i++)
                {
                    var p = new GridPos(c.x + o.x * i, c.y + o.y * i);
                    if (grid.IsFloor(p)) e.line.Add(p);
                }
                ClearMarks(e);
                foreach (var p in e.line) e.marks.Add(Mark(p, ringMat, p == c ? 0.9f : 0.5f));
                e.warn = 1.5f;
                return;
            }
            e.warn -= dt;
            // The ring swells as the dive nears.
            float swell = 1f + (1.5f - e.warn) * 0.15f + Mathf.Sin(time * 20f) * 0.05f;
            if (e.marks.Count > 0 && e.marks[0] != null) e.marks[0].localScale = new Vector3(0.9f * swell, 0.008f, 0.9f * swell);
            if (e.warn > 0f) return;
            // The dive.
            e.view.position = At(e.pos);
            Face(e);
            e.model.gameObject.SetActive(true);
            fx.Dust(At(e.pos), new Color(0.85f, 0.65f, 0.4f), 20, 3f);
            AudioManager.PlaySfx(Sfx.Impact, 0.6f, 0.6f);
            foreach (var p in e.line) if (RobotOn(p)) { Hit?.Invoke(p); break; }
            ClearMarks(e);
            e.warn = -1f;
            e.timer = 3.2f + (float)rng.NextDouble();
            StartCoroutineHide(e);
        }

        private void StartCoroutineHide(Enemy e) => StartCoroutine(HideLater(e));

        private System.Collections.IEnumerator HideLater(Enemy e)
        {
            yield return new WaitForSeconds(0.6f);
            if (e.model != null) e.model.gameObject.SetActive(false);
        }

        // Yengeç-bot: sideways along its row; every few seconds the claws go up, then it changes row.
        private void UpdateCrab(Enemy e, float dt)
        {
            if (e.warn >= 0f)
            {
                e.warn -= dt;
                if (e.clawL != null) e.clawL.localPosition = new Vector3(-0.38f, 0.5f, 0.14f);
                if (e.clawR != null) e.clawR.localPosition = new Vector3(0.38f, 0.5f, 0.14f);
                if (e.warn > 0f) return;
                if (e.clawL != null) e.clawL.localPosition = new Vector3(-0.38f, 0.28f, 0.14f);
                if (e.clawR != null) e.clawR.localPosition = new Vector3(0.38f, 0.28f, 0.14f);
                var side = rng.Next(2) == 0 ? Direction.PlusY : Direction.MinusY;
                var to = e.pos + side.ToOffset();
                if (!Free(to)) to = e.pos + side.Opposite().ToOffset();
                if (Free(to))
                {
                    if (RobotOn(to)) Hit?.Invoke(to);
                    else HopTo(e, to);
                }
                e.warn = -1f;
                e.timer = 3f;
                return;
            }
            e.timer -= dt;
            if (e.timer <= 0f) { e.warn = 0.6f; return; }
            e.seen += dt;
            if (e.hopT < 1f || e.seen < 0.6f) return;
            e.seen = 0f;
            var next = e.pos + e.dir.ToOffset();
            if (!Free(next)) { e.dir = e.dir.Opposite(); return; }
            if (RobotOn(next)) { Hit?.Invoke(next); return; }
            HopTo(e, next);
        }

        // Taret: fires a slow shot along its line every 2.5 s; the line glows while it charges.
        private void AimTurret(Enemy e)
        {
            foreach (var d in DirectionExtensions.All)
                if (grid.IsFloor(e.pos + d.ToOffset()) && !grid.IsFloor(e.pos + d.Opposite().ToOffset())) { e.dir = d; break; }
            grid.SetOccupied(e.pos, true);
            var o = e.dir.ToOffset();
            for (var p = e.pos + o; grid.IsFloor(p); p = p + o) e.line.Add(p);
            e.timer = 2.5f;
        }

        private void UpdateTurret(Enemy e, float dt)
        {
            if (e.shot.HasValue)
            {
                e.seen += dt;
                if (e.seen < 0.35f) return;
                e.seen = 0f;
                var next = e.shot.Value + e.dir.ToOffset();
                if (!grid.IsFloor(next) || grid.IsOccupied(next) || grid.IsWall(next))
                {
                    if (e.bullet != null) Destroy(e.bullet.gameObject);
                    e.shot = null;
                    return;
                }
                e.shot = next;
                e.bullet.position = At(next) + Vector3.up * 0.35f;
                if (RobotOn(next)) { Hit?.Invoke(next); Destroy(e.bullet.gameObject); e.shot = null; }
                return;
            }
            e.timer -= dt;
            bool charging = e.timer < 1f;
            if (charging) foreach (var p in e.line) view.SetWarning(p, 0.2f + 0.2f * Mathf.Sin(time * 18f));
            if (e.timer > 0f) return;
            e.timer = 2.5f;
            e.shot = e.pos;
            e.seen = 0f;
            e.bullet = Shapes.Primitive(PrimitiveType.Sphere, "Shot", transform, At(e.pos) + Vector3.up * 0.35f, Vector3.one * 0.22f, bulletMat).transform;
            AudioManager.PlaySfx(Sfx.Hop, 0.6f, 0.6f);
        }

        // Penguen-bot: picks a direction, draws its slide path, then slides until it meets something; a robot in the way
        // is pushed along (or hit, against a wall).
        private void UpdatePenguin(Enemy e, float dt)
        {
            if (e.warn < 0f)
            {
                e.timer -= dt;
                if (e.timer > 0f || e.hopT < 1f) return;
                e.dir = DirectionExtensions.All[rng.Next(4)];
                Face(e);
                e.line.Clear();
                for (var p = e.pos + e.dir.ToOffset(); Free(p) || RobotOn(p); p = p + e.dir.ToOffset())
                {
                    e.line.Add(p);
                    if (e.line.Count >= 6) break;
                }
                if (e.line.Count == 0) { e.timer = 0.5f; return; }
                ClearMarks(e);
                foreach (var p in e.line) e.marks.Add(Mark(p, pathMat, 0.45f));
                e.warn = 0.9f;
                return;
            }
            e.warn -= dt;
            if (e.warn > 0f) return;
            // The slide, a tile at a time.
            if (e.hopT < 1f) return;
            if (e.line.Count == 0)
            {
                ClearMarks(e);
                e.warn = -1f;
                e.timer = 2.2f;
                return;
            }
            var next = e.line[0];
            e.line.RemoveAt(0);
            if (RobotOn(next)) { Bump(e, e.dir); e.line.Clear(); return; }
            HopTo(e, next);
            e.hopT = 0.4f; // quicker than a step: it slides
        }

        // Yay-bot: every 2 s its shadow lands a tile closer to the robot, then it hops there.
        private void UpdateSpring(Enemy e, float dt)
        {
            if (e.warn < 0f)
            {
                e.timer -= dt;
                if (e.timer > 0f || e.hopT < 1f) return;
                var step = StepTowards(e.pos, robot.Position, allowRobot: true);
                if (!step.HasValue) { e.timer = 0.5f; return; }
                e.target = step;
                ClearMarks(e);
                e.marks.Add(Mark(step.Value, shadowMat, 0.7f));
                e.warn = 0.8f;
                return;
            }
            e.warn -= dt;
            if (e.warn > 0f) return;
            var t = e.target.Value;
            e.dir = DirOf(e.pos, t);
            Face(e);
            HopTo(e, t);
            ClearMarks(e);
            e.warn = -1f;
            e.timer = 1.2f;
            if (RobotOn(t)) Hit?.Invoke(t);
        }

        // ---------- Helpers ----------

        private GridPos? StepTowards(GridPos from, GridPos goal, bool allowRobot = false)
        {
            GridPos? best = null;
            int bestD = from.Manhattan(goal);
            foreach (var d in DirectionExtensions.All)
            {
                var n = from + d.ToOffset();
                if (!(Free(n) || (allowRobot && RobotOn(n)))) continue;
                int dn = n.Manhattan(goal);
                if (dn < bestD) { bestD = dn; best = n; }
            }
            return best;
        }

        private static Direction DirOf(GridPos from, GridPos to)
        {
            int dx = to.x - from.x, dy = to.y - from.y;
            return Mathf.Abs(dx) >= Mathf.Abs(dy) ? (dx >= 0 ? Direction.PlusX : Direction.MinusX) : (dy >= 0 ? Direction.PlusY : Direction.MinusY);
        }

        private GridPos RandomNear(GridPos c, int radius)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var p = new GridPos(c.x + rng.Next(-radius, radius + 1), c.y + rng.Next(-radius, radius + 1));
                if (grid.IsStandable(p) && (isProtected == null || !isProtected(p))) return p;
            }
            return c;
        }
    }
}
