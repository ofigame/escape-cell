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
    /// The core loop's floor crowd (<see cref="MissionType.Hunt"/>): bugs scuttle about at random — step on them or
    /// strike them; guard robots walk up to the robot and slam the tile it stands on (the tile glows red first) — beat
    /// them with the hammer. A crate that lands on either finishes it too. When all are gone, the big monster rises:
    /// it stomps the tiles around it and hurls rocks at the robot's tile (both shown in red first), and falls after
    /// enough hammer blows. Robots and the monster stand on their tiles (occupied); bugs don't block.
    /// </summary>
    public class HuntSystem : MonoBehaviour
    {
        private class Bug
        {
            public GridPos pos;
            public Vector3 from, to;
            public float moveT = 1f, wait;
            public BugModel model;
            public bool dead;
        }

        private class Guard
        {
            public GridPos pos;
            public Vector3 from, to;
            public float moveT = 1f, step, windup = -1f, cooldown, flash;
            public GridPos target;
            public int hp;
            public GuardBot bot;
            public HumanoidBot human;
            public Transform root;
            public bool dead, brute;
            /// <summary>The tiles its slam will hit (the target, and its neighbours for a brute).</summary>
            public readonly List<GridPos> area = new List<GridPos>();

            public void SetRaise(float k) { if (bot != null) bot.SetRaise(k); else human.SetRaise(k); }
            public void Flash() { if (bot != null) bot.Flash(); else human.Flash(); }
        }

        /// <summary>The robot was hit on a tile, taking this share of its health.</summary>
        public event Action<GridPos, float> Hit;
        public event Action MonsterAppeared;
        public event Action MonsterDefeated;
        /// <summary>A bug or robot was finished (its tile), for coins and combos.</summary>
        public event Action<GridPos, bool> Finished;

        /// <summary>Makes the monster at a tile (the game dresses it in the floor's guardian look).</summary>
        public Func<GridPos, int, Monster> CreateMonster;
        /// <summary>A tile the crowd should keep off (a warning, a pickup...).</summary>
        public Func<GridPos, bool> Avoid;

        private GridModel grid;
        private LevelData level;
        private Robot robot;
        private FxSystem fx;
        private CameraRig rig;
        private GridView view;
        private readonly List<Bug> bugs = new List<Bug>();
        private readonly List<Guard> guards = new List<Guard>();
        private Monster monster;
        private GridPos monsterPos;
        private int monsterHp;
        private float monsterTimer, attackWind = -1f;
        private readonly List<GridPos> attackTiles = new List<GridPos>();
        private bool attackIsRock, frozen, running;
        private System.Random rng;
        private Color bugColour;
        /// <summary>Dark floors and the later worlds get glowing bugs, so they always stand out.</summary>
        private bool BrightBugs => world >= 8 || Visual.WorldTheme.Current.tileTop.grayscale < 0.55f;
        private int world;

        public bool MonsterUp => monster != null && !monster.Dead;
        public bool MonsterDown { get; private set; }
        public int Total { get; private set; }
        public int Left
        {
            get
            {
                int n = 0;
                foreach (var b in bugs) if (!b.dead) n++;
                foreach (var g in guards) if (!g.dead) n++;
                return n;
            }
        }
        /// <summary>How big the guards and enforcers stand (they grow with the campaign).</summary>
        public float EnemyScale = 1f;
        public int MonsterHpLeft => monsterHp;
        public int MonsterHpTotal => level != null ? level.monsterHp : 1;

        /// <summary>0..1: the crowd counts for 60%, the monster for 40%.</summary>
        public float Progress => Total == 0 ? 1f : 0.6f * (Total - Left) / Total + (monster != null || MonsterDown ? 0.4f * (1f - monsterHp / (float)Mathf.Max(1, MonsterHpTotal)) : 0f);

        public void Init(GridView view, Robot robot, FxSystem fx, CameraRig rig)
        {
            this.view = view;
            this.robot = robot;
            this.fx = fx;
            this.rig = rig;
        }

        public void Begin(GridModel grid, LevelData level, int world, int seed)
        {
            Stop();
            this.grid = grid;
            this.level = level;
            rng = new System.Random(seed);
            this.world = world;
            bugColour = Color.HSVToRGB(Mathf.Repeat(world * 0.137f + 0.1f, 1f), 0.7f, 1f);
            running = true;
            frozen = false;
            MonsterDown = false;
            monsterHp = level.monsterHp;
            comboLeft = 0;
            leapT = -1f;
            var free = FreeTiles(Mathf.Clamp(Mathf.Min(grid.Width, grid.Height) / 2, 1, 3)); // not right next to the robot (small floors allow less room)
            for (int i = 0; i < level.bugs && free.Count > 0; i++) AddBug(Take(free));
            for (int i = 0; i < level.robots && free.Count > 0; i++) AddGuard(Take(free));
            for (int i = 0; i < level.brutes && free.Count > 0; i++) AddGuard(Take(free), brute: true);
            Total = bugs.Count + guards.Count;
            if (Total == 0) RaiseMonster();
        }

        public void Stop()
        {
            running = false;
            foreach (var b in bugs) if (b.model != null) Destroy(b.model.gameObject);
            foreach (var g in guards)
            {
                if (g.root != null) Destroy(g.root.gameObject);
                if (!g.dead && grid != null) grid.SetOccupied(g.pos, false);
            }
            bugs.Clear();
            guards.Clear();
            ClearTelegraph();
            if (monster != null) Destroy(monster.gameObject);
            monster = null;
        }

        public void Freeze() => frozen = true;
        public void Resume() => frozen = false;

        // ---------- Spawning ----------

        private List<GridPos> FreeTiles(int awayFromRobot)
        {
            var list = new List<GridPos>();
            foreach (var p in grid.AllPositions())
                if (grid.IsStandable(p) && Chebyshev(p, robot.Position) >= awayFromRobot) list.Add(p);
            return list;
        }

        private GridPos Take(List<GridPos> list)
        {
            int i = rng.Next(list.Count);
            var p = list[i];
            list.RemoveAt(i);
            return p;
        }

        private static Vector3 At(GridPos p) => GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY;

        private void AddBug(GridPos p)
        {
            var b = new Bug { pos = p, wait = (float)rng.NextDouble() };
            b.model = BugModel.Build(transform, bugColour, world % BugModel.Kinds, BrightBugs); // a new kind of bug every world
            b.from = b.to = At(p);
            b.model.transform.position = b.to;
            b.model.transform.rotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);
            bugs.Add(b);
        }

        private void AddGuard(GridPos p, bool brute = false)
        {
            var g = new Guard { pos = p, brute = brute, hp = brute ? level.robotHp * 2 + 2 : level.robotHp, step = level.robotStep * (0.8f + (float)rng.NextDouble() * 0.4f) };
            g.root = new GameObject(brute ? "Enforcer" : "GuardRobot").transform;
            g.root.SetParent(transform, false);
            var accent = Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.55f, 1f), 0.75f, 1f);
            if (brute)
            {
                // vanG's enforcer: a tall humanoid that hits much harder.
                g.human = HumanoidBot.Build(g.root, accent);
                g.human.transform.localScale = Vector3.one * 0.7f * EnemyScale;
            }
            else
            {
                // A new build of guard every world, in that world's colour.
                g.bot = GuardBot.Build(g.root, Color.Lerp(accent, new Color(0.3f, 0.3f, 0.35f), 0.35f), world);
                g.bot.transform.localScale = Vector3.one * 0.62f * EnemyScale;
            }
            g.from = g.to = At(p);
            g.root.position = g.to;
            grid.SetOccupied(p, true);
            guards.Add(g);
        }

        // ---------- Interaction ----------

        /// <summary>The robot landed on a tile: a bug there is squashed.</summary>
        public void OnRobotArrived(GridPos p)
        {
            foreach (var b in bugs) if (!b.dead && b.pos == p) KillBug(b);
        }

        /// <summary>
        /// A crate landed on a tile: a bug there is flattened; a guard is dented and knocked aside but never finished
        /// (robots must be beaten by foi, or dodging until the crates crush them would clear the floor for free).
        /// </summary>
        public void OnBlockLanded(GridPos p)
        {
            foreach (var b in bugs) if (!b.dead && b.pos == p) KillBug(b);
            foreach (var g in guards)
            {
                if (g.dead || g.pos != p) continue;
                g.hp = Mathf.Max(1, g.hp - 1);
                g.flash = 1f;
                g.Flash();
                g.windup = -1f;
                ClearTelegraph(g);
                fx.Dust(At(p), new Color(0.55f, 0.5f, 0.45f), 12, 3f);
                KnockBack(g);
            }
        }

        /// <summary>Is there something to strike on (or right next to) a tapped tile? Returns the tile it stands on.</summary>
        public bool TargetNear(GridPos tapped, out GridPos at)
        {
            var candidates = new List<GridPos>();
            if (MonsterUp) candidates.Add(monsterPos);
            foreach (var g in guards) if (!g.dead) candidates.Add(g.pos);
            foreach (var b in bugs) if (!b.dead) candidates.Add(b.pos);
            at = tapped;
            int best = 99;
            foreach (var p in candidates)
            {
                int d = Chebyshev(p, tapped);
                if (d > 1 || d >= best) continue;
                best = d;
                at = p;
            }
            return best <= 1;
        }

        /// <summary>A hammer blow on a tile: the monster, a robot or a bug there takes it. True if it hit something.</summary>
        /// <summary>A blow bounced off armour (too weak a weapon): where.</summary>
        public event Action<GridPos> Armored;

        /// <summary>
        /// What a blow does through the floor's armour: the damage above it; a blow that does not get through only
        /// scratches now and then (one in four), so a weak weapon makes the fight very long rather than impossible.
        /// </summary>
        private int ThroughArmor(GridPos p, int damage)
        {
            int armor = level != null ? level.armor : 0;
            if (damage > armor) return damage - armor;
            fx.Burst(At(p) + Vector3.up * 0.6f, new Color(0.75f, 0.8f, 0.9f), new Color(1.2f, 1.3f, 1.6f), 16, 4f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 1.5f);
            Armored?.Invoke(p);
            return rng.Next(4) == 0 ? 1 : 0;
        }

        public bool Strike(GridPos p, int damage)
        {
            if (MonsterUp && Chebyshev(p, monsterPos) == 0)
            {
                int through = ThroughArmor(p, damage);
                if (through > 0) HitMonster(through);
                return true;
            }
            foreach (var g in guards)
            {
                if (g.dead || g.pos != p) continue;
                g.hp -= ThroughArmor(p, damage);
                g.flash = 1f;
                g.Flash();
                g.windup = -1f;
                ClearTelegraph(g);
                fx.Burst(At(p) + Vector3.up * 0.4f, Palette.UiGold, Palette.CoinGlow, 34, 6f);
                Shockwave.Create(At(p), 1.4f, new Color(1f, 0.85f, 0.45f));
                AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.85f);
                rig.Shake(0.35f);
                if (g.hp <= 0) KillGuard(g);
                else KnockBack(g);
                return true;
            }
            foreach (var b in bugs)
            {
                if (b.dead || b.pos != p) continue;
                KillBug(b);
                return true;
            }
            return false;
        }

        private void KnockBack(Guard g)
        {
            var away = new GridPos(g.pos.x - robot.Position.x, g.pos.y - robot.Position.y);
            var dir = Mathf.Abs(away.x) >= Mathf.Abs(away.y) ? new GridPos(Math.Sign(away.x), 0) : new GridPos(0, Math.Sign(away.y));
            var next = g.pos + dir;
            if (!CanStep(g.pos, next))
            {
                // Pinned that way: any free side will do.
                next = g.pos;
                foreach (var d in DirectionExtensions.All)
                    if (CanStep(g.pos, g.pos + d.ToOffset())) { next = g.pos + d.ToOffset(); break; }
                if (next == g.pos) return;
            }
            MoveGuard(g, next, 0.12f);
        }

        private void KillBug(Bug b)
        {
            b.dead = true;
            // Squashed flat on its tile: a spray of its blood and a splat that fades, the body pressed into the floor.
            b.model.Squash();
            Destroy(b.model.gameObject, 0.9f);
            BloodSplat.Create(b.model.transform.position, b.model.Blood, rng.Next());
            fx.Burst(At(b.pos) + Vector3.up * 0.08f, b.model.Blood, b.model.Blood * 0.6f, 10, 2f);
            AudioManager.PlaySfx(Sfx.Squash, 0.6f, 1.6f);
            Finished?.Invoke(b.pos, false);
            CheckCleared();
        }

        private void KillGuard(Guard g)
        {
            g.dead = true;
            grid.SetOccupied(g.pos, false);
            ClearTelegraph(g);
            fx.Burst(At(g.pos) + Vector3.up * 0.5f, new Color(1f, 0.4f, 0.3f), new Color(2.4f, 0.6f, 0.3f), 40, 6f);
            Shockwave.Create(At(g.pos), 1.5f, new Color(1f, 0.6f, 0.3f));
            AudioManager.PlaySfx(Sfx.Impact, 0.9f, 0.9f);
            rig.Shake(0.5f);
            Destroy(g.root.gameObject);
            Finished?.Invoke(g.pos, true);
            CheckCleared();
        }

        private void CheckCleared()
        {
            if (Left == 0 && monster == null && !MonsterDown) RaiseMonster();
        }

        private void RaiseMonster()
        {
            // On a free tile a few steps from the robot, as central as possible.
            var centre = new GridPos(grid.Width / 2, grid.Height / 2);
            GridPos best = robot.Position;
            int score = int.MaxValue;
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsStandable(p) || Chebyshev(p, robot.Position) < 3) continue;
                int open = 0;
                foreach (var d in DirectionExtensions.All) if (grid.IsStandable(p + d.ToOffset())) open++;
                if (open < 3) continue;
                int s = p.Manhattan(centre);
                if (s < score) { score = s; best = p; }
            }
            monsterPos = best;
            grid.SetOccupied(monsterPos, true);
            monster = CreateMonster?.Invoke(monsterPos, level.monsterHp);
            monsterTimer = level.monsterAttack + 1f;
            fx.Burst(At(monsterPos) + Vector3.up * 0.6f, new Color(0.7f, 0.4f, 1f), new Color(1.6f, 0.8f, 2.4f), 60, 7f);
            Shockwave.Create(At(monsterPos), 2.5f, new Color(0.8f, 0.5f, 1f));
            rig.Shake(1f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.5f);
            MonsterAppeared?.Invoke();
        }

        private void HitMonster(int damage)
        {
            monsterHp = Mathf.Max(0, monsterHp - damage);
            monster.Hit(monsterHp);
            fx.Burst(At(monsterPos) + Vector3.up * 0.8f, Palette.UiGold, Palette.CoinGlow, 44, 7f);
            Shockwave.Create(At(monsterPos), 1.3f, new Color(1f, 0.85f, 0.45f));
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.7f);
            rig.Punch(0.6f);
            if (monsterHp > 0) return;
            monster.Defeat();
            grid.SetOccupied(monsterPos, false);
            ClearTelegraph();
            MonsterDown = true;
            MonsterDefeated?.Invoke();
        }

        /// <summary>The closest of what is left to beat (or the monster), for the HUD's edge arrows.</summary>
        public List<(GridPos, bool)> Targets(GridPos from, int max)
        {
            var list = new List<(GridPos, bool)>();
            if (MonsterUp) { list.Add((monsterPos, true)); return list; }
            var all = new List<GridPos>();
            foreach (var g in guards) if (!g.dead) all.Add(g.pos);
            foreach (var b in bugs) if (!b.dead) all.Add(b.pos);
            all.Sort((a, b) => a.Manhattan(from).CompareTo(b.Manhattan(from)));
            for (int i = 0; i < Mathf.Min(max, all.Count); i++) list.Add((all[i], false));
            return list;
        }

        // ---------- Update ----------

        private void Update()
        {
            if (!running || frozen || grid == null) return;
            float dt = Time.deltaTime;
            foreach (var b in bugs) if (!b.dead) UpdateBug(b, dt);
            foreach (var g in guards) if (!g.dead) UpdateGuard(g, dt);
            if (MonsterUp) UpdateMonster(dt);
        }

        private bool CanStep(GridPos from, GridPos p) => FloorRelief.StepOk(from, p) &&
            grid.IsStandable(p) && !grid.IsOccupied(p) && !grid.IsGap(p) && p != robot.Position && (Avoid == null || !Avoid(p));

        private void UpdateBug(Bug b, float dt)
        {
            if (b.moveT < 1f)
            {
                b.moveT = Mathf.Min(1f, b.moveT + dt / 0.28f);
                b.model.transform.position = Vector3.Lerp(b.from, b.to, b.moveT);
                return;
            }
            b.wait -= dt;
            if (b.wait > 0f) return;
            b.wait = 0.5f + (float)rng.NextDouble() * 0.9f;
            // Wander; later bugs run from the robot when it comes close.
            var options = new List<GridPos>();
            foreach (var d in DirectionExtensions.All)
            {
                var n = b.pos + d.ToOffset();
                if (!grid.IsStandable(n) || grid.IsOccupied(n) || grid.IsGap(n) || bugs.Exists(o => !o.dead && o.pos == n) || !FloorRelief.StepOk(b.pos, n)) continue;
                options.Add(n);
            }
            if (options.Count == 0) return;
            var next = options[rng.Next(options.Count)];
            if (level.score > 30 && Chebyshev(b.pos, robot.Position) <= 2)
                foreach (var o in options)
                    if (Chebyshev(o, robot.Position) > Chebyshev(next, robot.Position)) next = o;
            var dir = GridView.ToWorld(next) - GridView.ToWorld(b.pos);
            b.model.transform.rotation = Quaternion.LookRotation(dir);
            b.from = b.model.transform.position;
            b.to = At(next);
            b.pos = next;
            b.moveT = 0f;
            b.model.Scuttle();
            if (next == robot.Position) KillBug(b);
        }

        private void MoveGuard(Guard g, GridPos next, float duration)
        {
            grid.SetOccupied(g.pos, false);
            g.pos = next;
            grid.SetOccupied(next, true);
            g.from = g.root.position;
            g.to = At(next);
            g.moveT = 0f;
            g.step = duration;
        }

        private void UpdateGuard(Guard g, float dt)
        {
            if (g.flash > 0f) g.flash -= dt * 3f;
            g.cooldown -= dt;
            if (g.moveT < 1f)
            {
                g.moveT = Mathf.Min(1f, g.moveT + dt / 0.25f);
                g.root.position = Vector3.Lerp(g.from, g.to, g.moveT) + Vector3.up * Mathf.Sin(g.moveT * Mathf.PI) * 0.12f;
            }
            var toRobot = GridView.ToWorld(robot.Position) - g.root.position;
            toRobot.y = 0f;
            if (toRobot.sqrMagnitude > 0.01f) g.root.rotation = Quaternion.Slerp(g.root.rotation, Quaternion.LookRotation(toRobot), dt * 8f);

            if (g.windup >= 0f)
            {
                // Winding up: the tiles it will hit glow ever redder; the slam lands on them.
                g.windup += dt;
                float k = g.windup / (g.brute ? BruteWindTime : WindTime);
                g.SetRaise(k * 1.3f);
                foreach (var t in g.area) view.SetWarning(t, 0.4f + 0.6f * k);
                if (k >= 1f)
                {
                    g.windup = -1f;
                    g.cooldown = g.brute ? 1.3f : 0.55f;
                    g.SetRaise(0f);
                    foreach (var t in g.area) view.SetWarning(t, 0f);
                    Shockwave.Create(At(g.target), g.brute ? 2.2f : 1f, new Color(1f, 0.5f, 0.3f));
                    if (g.brute) fx.Dust(At(g.target), new Color(0.55f, 0.5f, 0.45f), 18, 4f);
                    AudioManager.PlaySfx(Sfx.Impact, g.brute ? 1f : 0.7f, g.brute ? 0.55f : 0.8f);
                    rig.Shake(g.brute ? 0.8f : 0.3f);
                    if (g.area.Contains(robot.Position)) Hit?.Invoke(robot.Position, g.brute ? BruteHitShare : GuardHitShare);
                    g.area.Clear();
                }
                return;
            }
            if (g.moveT < 1f) return;
            // Any tile touching it, diagonals too: it swings as soon as it can.
            int near = Chebyshev(robot.Position, g.pos);
            if (near <= 1 && g.cooldown <= 0f)
            {
                g.windup = 0f;
                g.target = robot.Position;
                g.area.Clear();
                g.area.Add(g.target);
                if (g.brute)
                    foreach (var d in DirectionExtensions.All)
                        if (grid.IsFloor(g.target + d.ToOffset())) g.area.Add(g.target + d.ToOffset());
                return;
            }
            int dist = robot.Position.Manhattan(g.pos);
            g.step -= dt;
            if (g.step > 0f || near <= 1) return;
            g.step = level.robotStep * (g.brute ? 1.3f : 0.72f);
            if (g.human != null) g.human.Step();
            // One step towards the robot along whichever axis brings it closer and is free.
            GridPos bestNext = g.pos;
            int bestDist = dist;
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (!CanStep(g.pos, n)) continue;
                int nd = robot.Position.Manhattan(n);
                if (nd < bestDist || (nd == bestDist && rng.Next(2) == 0 && nd < dist)) { bestDist = nd; bestNext = n; }
            }
            if (bestNext != g.pos) MoveGuard(g, bestNext, level.robotStep);
        }

        private float WindTime => Mathf.Lerp(0.7f, 0.45f, level.score / 100f);
        private float BruteWindTime => Mathf.Lerp(0.95f, 0.7f, level.score / 100f);
        /// <summary>Health shares a guard's slam and an enforcer's smash take.</summary>
        private const float GuardHitShare = 0.2f, BruteHitShare = 0.45f;
        /// <summary>The monster's blows hurt more on later floors.</summary>
        private float MonsterHitShare => Mathf.Lerp(0.24f, 0.42f, level.score / 100f);

        /// <summary>Blows still to come in the monster's current combo (later floors chain two or three).</summary>
        private int comboLeft;
        /// <summary>The monster's leap towards the robot: 0..1 while in the air, -1 when standing.</summary>
        private float leapT = -1f;
        private Vector3 leapFrom, leapTo;

        private void UpdateMonster(float dt)
        {
            float hard = level.score / 100f;
            bool enraged = monsterHp * 2 <= MonsterHpTotal;
            if (leapT >= 0f)
            {
                // In the air: a heavy arc onto the new tile, a quake where it lands.
                leapT = Mathf.Min(1f, leapT + dt / 0.55f);
                monster.transform.position = Vector3.Lerp(leapFrom, leapTo, leapT) + Vector3.up * Mathf.Sin(leapT * Mathf.PI) * 1.6f;
                if (leapT < 1f) return;
                leapT = -1f;
                Shockwave.Create(At(monsterPos), 2.4f, new Color(1f, 0.5f, 0.3f));
                fx.Dust(At(monsterPos), new Color(0.55f, 0.5f, 0.45f), 22, 4f);
                AudioManager.PlaySfx(Sfx.Impact, 1f, 0.5f);
                rig.Shake(0.6f);
                if (Chebyshev(robot.Position, monsterPos) <= 1) Hit?.Invoke(robot.Position, MonsterHitShare);
                monsterTimer = Mathf.Min(monsterTimer, 0.35f); // and straight into an attack
                return;
            }
            if (attackWind >= 0f)
            {
                attackWind += dt;
                // The wind-up shortens on later floors and when the monster is enraged.
                float wind = (attackIsRock ? 1.1f : 0.95f) * Mathf.Lerp(1f, 0.68f, hard) * (enraged ? 0.85f : 1f);
                float k = attackWind / wind;
                foreach (var t in attackTiles) view.SetWarning(t, 0.4f + 0.6f * k);
                if (k < 1f) return;
                attackWind = -1f;
                foreach (var t in attackTiles)
                {
                    view.SetWarning(t, 0f);
                    Shockwave.Create(At(t), 0.8f, new Color(1f, 0.45f, 0.3f));
                    fx.Dust(At(t), new Color(0.55f, 0.5f, 0.45f), 6, 2f);
                }
                rig.Shake(attackIsRock ? 0.4f : 0.7f);
                AudioManager.PlaySfx(Sfx.Impact, 1f, attackIsRock ? 1f : 0.6f);
                if (attackTiles.Contains(robot.Position)) Hit?.Invoke(robot.Position, MonsterHitShare);
                attackTiles.Clear();
                // A combo: the next blow follows almost at once.
                if (comboLeft > 0)
                {
                    comboLeft--;
                    monsterTimer = 0.25f;
                }
                return;
            }
            monsterTimer -= dt * (enraged ? 1.35f : 1f);
            if (monsterTimer > 0f) return;
            monsterTimer = level.monsterAttack * (0.85f + (float)rng.NextDouble() * 0.3f);
            attackTiles.Clear();
            int dist = Chebyshev(robot.Position, monsterPos);
            // Far away: from the second stretch of the campaign the monster leaps after the robot instead of waiting.
            if (dist >= 4 && hard > 0.12f && rng.NextDouble() < 0.35f + 0.4f * hard && TryLeap()) return;
            if (comboLeft <= 0 && rng.NextDouble() < hard * 0.8f + (enraged ? 0.25f : 0f)) comboLeft = hard > 0.6f ? 2 : 1;
            // Close by: a stomp all around. Farther: a rock at the robot's tile (and, later, its neighbours too).
            if (dist <= 2 && rng.Next(3) > 0)
            {
                attackIsRock = false;
                int r = level.score > 55 || enraged && level.score > 25 ? 2 : 1;
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                    {
                        var t = monsterPos + new GridPos(x, y);
                        if ((x != 0 || y != 0) && grid.IsStandable(t)) attackTiles.Add(t);
                    }
                monster.Stomp();
            }
            else
            {
                attackIsRock = true;
                attackTiles.Add(robot.Position);
                if (level.score > 30 || enraged)
                    foreach (var d in DirectionExtensions.All)
                        if (rng.Next(2) == 0 && grid.IsStandable(robot.Position + d.ToOffset())) attackTiles.Add(robot.Position + d.ToOffset());
                monster.Throw();
                ThrownRock.Create(At(monsterPos) + Vector3.up * 1.4f, At(robot.Position), 1.1f, new Color(0.6f, 0.5f, 0.45f));
            }
            attackWind = 0f;
        }

        /// <summary>Leaps to a free tile two away from the robot, on the side it came from. False when none is free.</summary>
        private bool TryLeap()
        {
            GridPos best = monsterPos;
            int bestScore = int.MaxValue;
            for (int x = -2; x <= 2; x++)
                for (int y = -2; y <= 2; y++)
                {
                    var t = robot.Position + new GridPos(x, y);
                    if (Chebyshev(t, robot.Position) != 2 || !grid.IsStandable(t) || grid.IsOccupied(t) || grid.IsGap(t)) continue;
                    int score = Chebyshev(t, monsterPos);
                    if (score < bestScore) { bestScore = score; best = t; }
                }
            if (best == monsterPos) return false;
            grid.SetOccupied(monsterPos, false);
            monsterPos = best;
            grid.SetOccupied(monsterPos, true);
            leapFrom = monster.transform.position;
            leapTo = At(monsterPos);
            leapTo.y = leapFrom.y;
            leapT = 0f;
            monster.Stomp();
            AudioManager.PlaySfx(Sfx.Hop, 1f, 0.45f);
            return true;
        }

        private void ClearTelegraph(Guard g)
        {
            foreach (var t in g.area) view?.SetWarning(t, 0f);
            g.area.Clear();
        }

        private void ClearTelegraph()
        {
            foreach (var t in attackTiles) view?.SetWarning(t, 0f);
            attackTiles.Clear();
            attackWind = -1f;
        }

        public static int Chebyshev(GridPos a, GridPos b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
