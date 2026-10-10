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
    public partial class HuntSystem : MonoBehaviour
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
            public int hp, maxHp;
            /// <summary>The tile it is stepping (or being knocked) off: while it moves, a blow there still lands.</summary>
            public GridPos prevPos;
            /// <summary>The health bar over its head (turns to the camera) and its fill.</summary>
            public Transform bar, barFill;
            public Material barMat;
            public GuardBot bot;
            public HumanoidBot human;
            public Transform root;
            public bool dead, brute;
            /// <summary>Blows its energy shield still takes (the perfect city's guards), its bubble and material.</summary>
            public int shield;
            public Transform bubble;
            public Material bubbleMat;
            public float shieldFlash;
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
        /// <summary>What was just knocked out (a robot, a tower, a bug), for the daily quests.</summary>
        public event Action<DailyGoal> Felled;

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
        /// <summary>The monster is fighting (it sleeps by the gate until the crowd is beaten).</summary>
        public bool MonsterAwake { get; private set; }
        /// <summary>The tile the monster stands on.</summary>
        public GridPos MonsterTile => monsterPos;
        /// <summary>Where the monster waits from the start (by the gate to the tunnel); null = it rises later.</summary>
        public GridPos? MonsterSpot;
        /// <summary>The tile Fifi (foi's companion) hovers over, while it is with foi; the enemies can hit it there.</summary>
        public GridPos? CompanionTile;
        /// <summary>Fifi was hit (points of its health).</summary>
        public event Action<int> CompanionHurt;
        /// <summary>A blow landed on the monster while it was still asleep.</summary>
        public event Action<GridPos> SleepingHit;
        public int Total { get; private set; }
        public int Left
        {
            get
            {
                int n = 0;
                foreach (var b in bugs) if (!b.dead) n++;
                foreach (var g in guards) if (!g.dead) n++;
                n += TowersLeft;
                n += guardsToCome;
                return n;
            }
        }
        /// <summary>How big the guards and enforcers stand (they grow with the campaign).</summary>
        public float EnemyScale = 1f;
        public int MonsterHpLeft => monsterHp;
        public int MonsterHpTotal => level != null ? level.monsterHp : 1;

        /// <summary>0..1: the crowd counts for 60%, the monster for 40%.</summary>
        public float Progress => Total == 0 ? 1f : 0.6f * (Total - Left) / Total + (MonsterAwake || MonsterDown ? 0.4f * (1f - monsterHp / (float)Mathf.Max(1, MonsterHpTotal)) : 0f);

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
            // Only a few robots at a time; the rest of the floor's robots drop in as they fall (see UpdateReinforcements).
            guardCap = level.robotsAtOnce > 0 ? level.robotsAtOnce : level.robots;
            int first = Mathf.Min(level.robots, guardCap);
            for (int i = 0; i < first && free.Count > 0; i++) AddGuard(Take(free));
            guardsToCome = level.robots - Mathf.Min(first, guards.Count);
            nextGuardAt = Time.time + 2f;
            for (int i = 0; i < level.brutes && free.Count > 0; i++) AddGuard(Take(free), brute: true);
            // Guard towers, set well away from where foi starts.
            var far = new List<GridPos>();
            foreach (var p in free) if (Chebyshev(p, robot.Position) >= 4) far.Add(p);
            if (level.reactor && far.Count > 0) { var rp = NearestCentre(far); far.Remove(rp); free.Remove(rp); AddTower(rp, reactor: true); }
            foreach (var tp in TowerSpots(far, level.towers)) { far.Remove(tp); free.Remove(tp); AddTower(tp); }
            Total = bugs.Count + guards.Count + towers.Count + guardsToCome;
            MonsterAwake = false;
            if (MonsterSpot.HasValue) RaiseMonster(asleep: Total > 0);
            else if (Total == 0) RaiseMonster();
        }

        public void Stop()
        {
            running = false;
            guardsToCome = 0;
            foreach (var b in bugs) if (b.model != null) Destroy(b.model.gameObject);
            foreach (var g in guards)
            {
                if (g.root != null) Destroy(g.root.gameObject);
                if (!g.dead && grid != null) grid.SetOccupied(g.pos, false);
            }
            bugs.Clear();
            guards.Clear();
            StopTowers();
            ClearTelegraph();
            if (monster != null) Destroy(monster.gameObject);
            monster = null;
        }

        public void Freeze() => frozen = true;
        public void Resume() => frozen = false;

        // ---------- Spawning ----------

        /// <summary>The tile of a list nearest the middle of the floor.</summary>
        private GridPos NearestCentre(List<GridPos> list)
        {
            var centre = new GridPos(grid.Width / 2, grid.Height / 2);
            var best = list[0];
            foreach (var p in list) if (p.Manhattan(centre) < best.Manhattan(centre)) best = p;
            return best;
        }

        private List<GridPos> FreeTiles(int awayFromRobot)
        {
            var list = new List<GridPos>();
            foreach (var p in grid.AllPositions())
                if (grid.IsStandable(p) && !grid.IsSafe(p) && Chebyshev(p, robot.Position) >= awayFromRobot && (!MonsterSpot.HasValue || p != MonsterSpot.Value)) list.Add(p);
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
                // In the perfect city: pearl and gold guards lit in the floor's colour.
                g.bot = level.utopia ? GuardBot.Build(g.root, Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.5f, 1f), 0.6f, 1f), world, utopian: true)
                    : GuardBot.Build(g.root, Color.Lerp(accent, new Color(0.3f, 0.3f, 0.35f), 0.35f), world);
                g.bot.transform.localScale = Vector3.one * 0.62f * EnemyScale;
            }
            g.maxHp = Mathf.Max(1, g.hp);
            AddHealthBar(g);
            if (level.shieldEvery > 0 && guards.Count % level.shieldEvery == level.shieldEvery - 1) AddShield(g);
            g.from = g.to = At(p);
            g.root.position = g.to;
            grid.SetOccupied(p, true);
            guards.Add(g);
        }

        /// <summary>Robots of this floor still to drop in, how many may be on the floor at once, and when the next comes.</summary>
        private int guardsToCome, guardCap;
        private float nextGuardAt;

        /// <summary>
        /// A robot fell and there are more to come: a new one drops in from the sky onto a free tile away from foi
        /// (a ring of light marks the spot first), until the floor's count is reached.
        /// </summary>
        private void UpdateReinforcements()
        {
            if (guardsToCome <= 0 || Time.time < nextGuardAt) return;
            int alive = 0;
            foreach (var g in guards) if (!g.dead && !g.brute) alive++;
            if (alive >= guardCap) return;
            var spots = FreeTiles(4);
            spots.RemoveAll(p => grid.IsOccupied(p) || (Avoid != null && Avoid(p)));
            if (spots.Count == 0) { nextGuardAt = Time.time + 0.5f; return; }
            var at = Take(spots);
            AddGuard(at);
            var ng = guards[guards.Count - 1];
            ng.from = At(at) + Vector3.up * 7f; // it falls onto its tile
            ng.root.position = ng.from;
            ng.moveT = 0f;
            ng.cooldown = 1f;
            guardsToCome--;
            Shockwave.Create(At(at), 1.2f, new Color(1f, 0.55f, 0.35f));
            fx.Burst(At(at) + Vector3.up * 0.2f, new Color(1f, 0.6f, 0.35f), new Color(2f, 0.9f, 0.4f), 16, 3f);
            AudioManager.PlaySfx(Sfx.Warning, 0.35f, 1.4f);
            nextGuardAt = Time.time + Mathf.Lerp(1.4f, 0.8f, level.score / 100f);
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
            if (CompanionTile.HasValue && CompanionTile.Value == p) CompanionHurt?.Invoke(2);
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

        /// <summary>
        /// What a tap on the screen points at, judged on the screen itself: the bug, robot or monster whose drawn body
        /// (anywhere from its feet to its head) is nearest the finger, within <paramref name="radius"/> pixels.
        /// Tall robots are hit wherever they are touched (a ray to the floor would land a tile or two behind them).
        /// </summary>
        public bool PickOnScreen(Camera cam, Vector2 screen, float radius, out GridPos at)
        {
            at = default;
            if (cam == null || !running) return false;
            float best = radius;
            bool found = false;
            GridPos pick = default;
            void Try(Vector3 feet, float height, GridPos tile, float bias)
            {
                // The distance from the finger to the body's upright line, feet to head, on the screen.
                var a = cam.WorldToScreenPoint(feet);
                var b = cam.WorldToScreenPoint(feet + Vector3.up * height);
                if (a.z <= 0f || b.z <= 0f) return;
                var ab = (Vector2)(b - a);
                float t = ab.sqrMagnitude < 1f ? 0f : Mathf.Clamp01(Vector2.Dot(screen - (Vector2)a, ab) / ab.sqrMagnitude);
                float d = Vector2.Distance(screen, (Vector2)a + ab * t) * bias;
                if (d < best) { best = d; pick = tile; found = true; }
            }
            if (MonsterUp) Try(monster.transform.position, 2f * monster.transform.lossyScale.y, monsterPos, 0.8f);
            PickTowers(Try);
            PickDrones(Try);
            foreach (var g in guards)
                if (!g.dead) Try(g.root.position, g.bar != null ? g.bar.localPosition.y : 1.2f, g.pos, 0.8f); // fighters first when close
            foreach (var b in bugs)
                if (!b.dead) Try(b.model.transform.position, 0.3f, b.pos, 1f);
            at = pick;
            return found;
        }

        /// <summary>Something to strike still stands on (or is stepping off) this tile.</summary>
        public bool HasTargetAt(GridPos p)
        {
            if (MonsterUp && monsterPos == p) return true;
            if (TowerAt(p) || DroneAt(p)) return true;
            foreach (var g in guards) if (!g.dead && (g.pos == p || (g.moveT < 1f && g.prevPos == p))) return true;
            foreach (var b in bugs) if (!b.dead && b.pos == p) return true;
            return false;
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

        /// <summary>A blow on a tile: the monster, a robot or a bug there takes it. True if it hit something.</summary>
        public bool Strike(GridPos p, int damage)
        {
            if (StrikeTower(p, damage)) return true;
            if (MonsterUp && Chebyshev(p, monsterPos) == 0)
            {
                if (!MonsterAwake)
                {
                    // Asleep by the gate: the crowd comes first.
                    fx.Burst(At(p) + Vector3.up * 0.8f, new Color(0.75f, 0.8f, 0.9f), new Color(1.2f, 1.3f, 1.6f), 14, 3f);
                    AudioManager.PlaySfx(Sfx.Blocked, 0.8f, 1.4f);
                    SleepingHit?.Invoke(p);
                    return true;
                }
                int through = ThroughArmor(p, damage);
                if (through > 0) HitMonster(through);
                return true;
            }
            foreach (var g in guards)
            {
                if (g.dead || (g.pos != p && !(g.moveT < 1f && g.prevPos == p))) continue;
                // An energy shield takes the blow first (any weapon cracks it: one blow, one charge).
                if (g.shield > 0) { HitShield(g, p); return true; }
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
            if (StrikeDrone(p)) return true;
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
            Felled?.Invoke(DailyGoal.Bugs);
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
            Felled?.Invoke(DailyGoal.Robots);
            CheckCleared();
        }

        private void CheckCleared()
        {
            if (Left != 0 || MonsterDown) return;
            if (monster == null) RaiseMonster();
            else if (!MonsterAwake) Wake();
        }

        /// <summary>The crowd is beaten: the monster guarding the gate wakes up and the fight is on.</summary>
        private void Wake()
        {
            MonsterAwake = true;
            monsterTimer = level.monsterAttack + 0.6f;
            fx.Burst(At(monsterPos) + Vector3.up * 0.8f, new Color(0.7f, 0.4f, 1f), new Color(1.6f, 0.8f, 2.4f), 60, 7f);
            Shockwave.Create(At(monsterPos), 3f, new Color(0.8f, 0.5f, 1f));
            monster.Stomp();
            rig.Shake(1f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.45f);
            MonsterAppeared?.Invoke();
        }

        /// <param name="asleep">Placed at the start, guarding the gate: it only wakes when the crowd is beaten.</param>
        private void RaiseMonster(bool asleep = false)
        {
            // At its spot by the gate, else on a free tile a few steps from the robot, as central as possible.
            var centre = new GridPos(grid.Width / 2, grid.Height / 2);
            GridPos best = robot.Position;
            int score = int.MaxValue;
            if (MonsterSpot.HasValue && grid.IsStandable(MonsterSpot.Value)) { best = MonsterSpot.Value; score = -1; }
            foreach (var p in grid.AllPositions())
            {
                if (score < 0) break;
                if (!grid.IsStandable(p) || grid.IsSafe(p) || Chebyshev(p, robot.Position) < 3) continue;
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
            if (asleep)
            {
                MonsterAwake = false;
                return;
            }
            MonsterAwake = true;
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
            if (MonsterUp && MonsterAwake) { list.Add((monsterPos, true)); return list; }
            var all = new List<GridPos>();
            foreach (var g in guards) if (!g.dead) all.Add(g.pos);
            AddTowerTargets(all);
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
            UpdateTowers(dt);
            UpdateReinforcements();
            UpdateDrones(dt);
            if (MonsterUp && MonsterAwake) UpdateMonster(dt);
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
            g.prevPos = g.pos;
            g.pos = next;
            grid.SetOccupied(next, true);
            g.from = g.root.position;
            g.to = At(next);
            g.moveT = 0f;
            g.step = duration;
        }


        /// <summary>A small health bar over a guard's head: dark back, a fill that empties and turns from green to red.</summary>
        private void AddHealthBar(Guard g)
        {
            float top = 0f;
            foreach (var r in g.root.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y - g.root.position.y);
            g.bar = new GameObject("HealthBar").transform;
            g.bar.SetParent(g.root, false);
            g.bar.localPosition = new Vector3(0f, top + 0.28f, 0f);
            float w = g.brute ? 1.05f : 0.8f;
            Shapes.Rounded("Back", g.bar, Vector3.zero, new Vector3(w + 0.08f, 0.15f, 0.03f), 0.05f, MaterialFactory.Create(new Color(0.08f, 0.06f, 0.12f), Color.black));
            g.barMat = MaterialFactory.Create(new Color(0.4f, 1f, 0.45f), new Color(0.4f, 1.5f, 0.45f));
            g.barFill = new GameObject("Fill").transform;
            g.barFill.SetParent(g.bar, false);
            g.barFill.localPosition = new Vector3(-w * 0.5f, 0f, -0.025f);
            Shapes.Rounded("Fill", g.barFill, new Vector3(w * 0.5f, 0f, 0f), new Vector3(w, 0.09f, 0.02f), 0.04f, g.barMat);
            // Notches: one per point of health, so it reads how many blows are left.
            if (g.maxHp > 1 && g.maxHp <= 12)
                for (int i = 1; i < g.maxHp; i++)
                    Shapes.Rounded("Notch", g.bar, new Vector3(-w * 0.5f + w * i / g.maxHp, 0f, -0.04f), new Vector3(0.02f, 0.12f, 0.02f), 0.008f, MaterialFactory.Create(new Color(0.08f, 0.06f, 0.12f), Color.black));
        }

        private void UpdateHealthBar(Guard g)
        {
            if (g.bar == null) return;
            var cam = Camera.main;
            if (cam != null) g.bar.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            float k = Mathf.Clamp01(g.hp / (float)g.maxHp);
            g.barFill.localScale = new Vector3(Mathf.Max(0.001f, k), 1f, 1f);
            var c = Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(0.4f, 1f, 0.45f), k);
            MaterialFactory.SetColors(g.barMat, c, c * 1.5f);
        }
        private void UpdateGuard(Guard g, float dt)
        {
            UpdateHealthBar(g);
            UpdateShield(g, dt);
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
                    if (CompanionTile.HasValue && g.area.Contains(CompanionTile.Value)) CompanionHurt?.Invoke(g.brute ? 3 : 2);
                    g.area.Clear();
                }
                return;
            }
            if (g.moveT < 1f) return;
            // Within two tiles (diagonals count as one), the same reach as foi's blows: it swings as soon as it can.
            int near = Chebyshev(robot.Position, g.pos);
            // Fifi in reach and foi not (or now and then anyway): the robot goes for Fifi.
            bool atFifi = CompanionTile.HasValue && Chebyshev(CompanionTile.Value, g.pos) <= AttackReach && (near > AttackReach || rng.Next(4) == 0);
            if ((near <= AttackReach || atFifi) && g.cooldown <= 0f)
            {
                g.windup = 0f;
                g.target = atFifi ? CompanionTile.Value : robot.Position;
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
                if (CompanionTile.HasValue && Chebyshev(CompanionTile.Value, monsterPos) <= 1) CompanionHurt?.Invoke(2);
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
                if (CompanionTile.HasValue && attackTiles.Contains(CompanionTile.Value)) CompanionHurt?.Invoke(2);
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
            // On a healing island foi is out of reach: the monster waits (and growls) until it comes back.
            if (grid.IsSafe(robot.Position)) { monsterTimer = 0.6f; return; }
            // Farther than two tiles it can't hit foi: it leaps closer (or waits a moment and tries again).
            if (dist > AttackReach)
            {
                if (rng.NextDouble() < 0.5f + 0.4f * hard && TryLeap()) return;
                monsterTimer = 0.5f;
                return;
            }
            if (comboLeft <= 0 && rng.NextDouble() < hard * 0.8f + (enraged ? 0.25f : 0f)) comboLeft = hard > 0.6f ? 2 : 1;
            // Right next to it: a stomp all around. Two tiles off: a rock at foi's tile (and, later, its neighbours too).
            if (dist <= 1 && rng.Next(3) > 0)
            {
                attackIsRock = false;
                int r = level.score > 55 || enraged && level.score > 25 ? 2 : 1;
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                    {
                        var t = monsterPos + new GridPos(x, y);
                        if ((x != 0 || y != 0) && grid.IsStandable(t) && !grid.IsSafe(t)) attackTiles.Add(t);
                    }
                monster.Stomp();
            }
            else
            {
                attackIsRock = true;
                attackTiles.Add(robot.Position);
                if (level.score > 30 || enraged)
                    foreach (var d in DirectionExtensions.All)
                        if (rng.Next(2) == 0 && grid.IsStandable(robot.Position + d.ToOffset()) && !grid.IsSafe(robot.Position + d.ToOffset())) attackTiles.Add(robot.Position + d.ToOffset());
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
                    if (Chebyshev(t, robot.Position) != 2 || !grid.IsStandable(t) || grid.IsOccupied(t) || grid.IsGap(t) || grid.IsSafe(t)) continue;
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

        /// <summary>
        /// A fight is on near <paramref name="p"/>: a guard or enforcer within <paramref name="range"/> tiles, or vanG awake
        /// and a little farther. Bugs don't count.
        /// </summary>

        /// <summary>
        /// The nearest thing for Fifi to zap within <paramref name="range"/> tiles of <paramref name="from"/>: robots,
        /// towers and an awake vanG first, bugs only when none of those is in range. A sleeping vanG is left alone.
        /// </summary>
        public bool CompanionTarget(GridPos from, int range, out GridPos at)
        {
            int best = int.MaxValue;
            bool found = false;
            GridPos pick = from;
            void Consider(GridPos p)
            {
                int d = Chebyshev(p, from);
                if (d > range || d >= best) return;
                best = d;
                pick = p;
                found = true;
            }
            if (MonsterUp && MonsterAwake) Consider(monsterPos);
            foreach (var g in guards) if (!g.dead) Consider(g.pos);
            var towerTiles = new List<GridPos>();
            AddTowerTargets(towerTiles);
            foreach (var p in towerTiles) Consider(p);
            foreach (var dr in drones) if (!dr.dead) Consider(dr.pos);
            if (!found) foreach (var b in bugs) if (!b.dead) Consider(b.pos);
            at = pick;
            return found;
        }

        /// <summary>
        /// The best thing to strike within <paramref name="reach"/> tiles of <paramref name="from"/>, for blows that
        /// didn't point at anything (a tap on foi itself, hiding a robot behind it, or on the floor towards one): the
        /// one most in line with <paramref name="prefer"/> (a flat world direction), nearer ones a little first.
        /// Fighters (robots, towers, drones, vanG awake) before bugs; <paramref name="minDot"/> is how well in line it
        /// must be (-1 = any side).
        /// </summary>
        public bool BestInReach(GridPos from, int reach, Vector3 prefer, float minDot, out GridPos at)
        {
            at = from;
            if (!running) return false;
            float best = float.MinValue;
            bool found = false;
            GridPos pick = from;
            void Consider(GridPos p, float bonus)
            {
                int d = Chebyshev(p, from);
                if (d == 0 || d > reach) return;
                var dir = new Vector3(p.x - from.x, 0f, p.y - from.y).normalized;
                float dot = prefer.sqrMagnitude > 0.001f ? Vector3.Dot(dir, prefer) : 0f;
                if (dot < minDot) return;
                float score = dot - 0.15f * d + bonus;
                if (score <= best) return;
                best = score;
                pick = p;
                found = true;
            }
            if (MonsterUp && MonsterAwake) Consider(monsterPos, 0.3f);
            foreach (var g in guards) if (!g.dead) Consider(g.pos, 0.2f);
            foreach (var t in towers) if (!t.dead) Consider(t.pos, 0.1f);
            foreach (var dr in drones) if (!dr.dead) Consider(dr.pos, 0.1f);
            if (!found) foreach (var b in bugs) if (!b.dead) Consider(b.pos, 0f);
            at = pick;
            return found;
        }

        /// <summary>
        /// How much of a fight is on around <paramref name="p"/>, 0-1, for the music: robots close by (enforcers more),
        /// towers that can see foi (more while one aims at it), and vanG awake and near.
        /// </summary>
        public float Danger(GridPos p)
        {
            if (!running) return 0f;
            float d = 0f;
            foreach (var g in guards)
            {
                if (g.dead) continue;
                int r = Chebyshev(g.pos, p);
                if (r <= 4) d += g.brute ? 0.4f : 0.25f;
                if (r <= AttackReach) d += 0.15f;
                if (g.windup >= 0f && g.area.Contains(p)) d += 0.15f;
            }
            foreach (var t in towers)
            {
                if (t.dead) continue;
                int r = Chebyshev(t.pos, p);
                if (r <= (t.reactor ? ReactorRange : TowerRange)) d += t.reactor ? 0.35f : 0.15f;
                if (t.windup >= 0f && t.area.Contains(p)) d += 0.2f;
            }
            if (MonsterUp && MonsterAwake) d += Chebyshev(monsterPos, p) <= 4 ? 0.8f : 0.55f;
            return Mathf.Clamp01(d);
        }

        /// <summary>
        /// Where a bomb does the most within <paramref name="range"/> tiles of foi: the tile whose blast square (of
        /// <paramref name="radius"/>) holds the most (robots, towers and an awake vanG count double, bugs once). Never
        /// so close that foi stands in the blast.
        /// </summary>
        public bool BombSpot(GridPos from, int range, int radius, out GridPos at)
        {
            at = from;
            if (!running) return false;
            var weights = new Dictionary<GridPos, int>();
            void Mark(GridPos p, int w) { weights.TryGetValue(p, out var v); weights[p] = v + w; }
            if (MonsterUp && MonsterAwake) Mark(monsterPos, 3);
            foreach (var g in guards) if (!g.dead) Mark(g.pos, g.brute ? 3 : 2);
            foreach (var t in towers) if (!t.dead) Mark(t.pos, 2);
            foreach (var dr in drones) if (!dr.dead) Mark(dr.pos, 1);
            foreach (var b in bugs) if (!b.dead) Mark(b.pos, 1);
            int best = 0;
            foreach (var kv in weights)
            {
                var centre = kv.Key;
                if (Chebyshev(centre, from) > range || Chebyshev(centre, from) <= radius) continue;
                int score = 0;
                foreach (var o in weights) if (Chebyshev(o.Key, centre) <= radius) score += o.Value;
                if (score > best) { best = score; at = centre; }
            }
            return best > 0;
        }

        /// <summary>A guard, an enforcer or the monster stands on this tile (not a bug).</summary>
        public bool IsFighter(GridPos p)
        {
            if (MonsterUp && monsterPos == p) return true;
            if (TowerAt(p)) return true;
            foreach (var g in guards) if (!g.dead && g.pos == p) return true;
            return false;
        }

        public bool InCombat(GridPos p, int range)
        {
            if (!running) return false;
            foreach (var g in guards) if (!g.dead && Chebyshev(g.pos, p) <= range) return true;
            if (TowerNear(p, range)) return true;
            return MonsterUp && MonsterAwake && Chebyshev(monsterPos, p) <= range + 2;
        }

        /// <summary>How far the robots and vanG reach with a blow: two tiles, like foi; three is out of reach.</summary>
        public const int AttackReach = 2;

        public static int Chebyshev(GridPos a, GridPos b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
