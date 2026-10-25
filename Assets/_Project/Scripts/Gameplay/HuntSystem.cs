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
            /// <summary>The fortress stage it belongs to.</summary>
            public int stage;
            /// <summary>Blows (or stomps) it takes: one early on, more further in.</summary>
            public int hp = 1;
        }

        private class Guard
        {
            public GridPos pos;
            public Vector3 from, to;
            public float moveT = 1f, step, windup = -1f, cooldown, flash;
            /// <summary>Seconds the current step takes (robots walk smoothly from tile to tile, one step running into the next).</summary>
            public float moveDur = 0.25f;
            /// <summary>One of foi's allies (see HuntSystemAllies): it never holds its tile against foi.</summary>
            public bool ally;
            /// <summary>A chest's vanG on the roads: the monster's model, moved like a warlord.</summary>
            public Monster mon;
            private bool monStomped;
            public GridPos target;
            public int hp, maxHp;
            /// <summary>Gun-post bullets' fractions of health not yet taken (each takes a share of its whole).</summary>
            public float chip;
            /// <summary>Until when it is roused by a blow on a robot near it (it comes at a run).</summary>
            public float alertUntil;
            /// <summary>Its wind-up is a smash at the rampart under foi's gun post (foi is up there).</summary>
            public bool atWall;
            /// <summary>The health its bar last showed (-1: never drawn).</summary>
            public int barHp = -1;
            /// <summary>Its renderers and their own colours (faded as it loses health).</summary>
            public List<(Renderer r, Color c, Material m)> tint;
            /// <summary>A chest's guardian on the roads (see HuntSystemTrail): the chest it keeps near, its number.</summary>
            public bool spitter;
            public GridPos home;
            public int chestId = -1;
            /// <summary>The tile it is stepping (or being knocked) off: while it moves, a blow there still lands.</summary>
            public GridPos prevPos;
            /// <summary>The health bar over its head (turns to the camera) and its fill.</summary>
            public Transform bar, barFill;
            public Material barMat;
            public GuardBot bot;
            public HumanoidBot human;
            /// <summary>Its wing of the crowd (-1 until it is given one): the side it comes at foi from.</summary>
            public int flank = -1;
            /// <summary>A challenge soldier (its kind decides how it fights in the battle).</summary>
            public SoldierModel soldier;
            public GroupKind soldierKind;
            /// <summary>A duel squad soldier (see HuntSystemSquads): its number in its squad, moved by the other game (live), its
            /// strength, where the other game last said it was, what it goes for (a soldier, or the rival foi) and until when it
            /// strikes back at a foi that hit it.</summary>
            public bool squad, remote, netSeen, aimFoi;
            public int squadId = -1;
            public float power = 1f, revengeUntil;
            public Vector3 netTo;
            public Guard aim, hitBy;
            /// <summary>One of the army's soldiers brought onto a floor: its group (its fall costs the pool one), its blow.</summary>
            public int poolGroup = -1, allyHit;
            /// <summary>Its blow in health points (0: the old share of foi's bar), and its armour: the share of a blow it takes off.</summary>
            public int blow;
            public float armorShare;
            /// <summary>The ice bomb: until when it stands frozen, and its block of ice.</summary>
            public float iceUntil;
            public Transform ice;
            public Transform root;
            public bool dead, brute;
            /// <summary>The fortress stage it belongs to, and its rank there (-1 classic, 0 easy … 99 an elite).</summary>
            public int stage, rank = -1;
            /// <summary>Blows its energy shield still takes (the perfect city's guards), its bubble and material.</summary>
            public int shield;
            public Transform bubble;
            public Material bubbleMat;
            public float shieldFlash;
            /// <summary>The floors' new ways of fighting (see HuntSystemFoes): it carries a gun; its wind-up follows foi;
            /// its lunge into the blow (1 down to 0) and which way; its red charge and a gunner's aiming line.</summary>
            public bool gunner, tracking;
            public float lunge;
            public Vector3 lungeDir;
            public Transform charge, aimLine;
            /// <summary>The tiles its slam will hit (the target, and its neighbours for a brute).</summary>
            public readonly List<GridPos> area = new List<GridPos>();

            public void SetRaise(float k)
            {
                if (mon != null) { if (k > 0.05f && !monStomped) { mon.Stomp(); monStomped = true; } else if (k <= 0f) monStomped = false; return; }
                if (bot != null) bot.SetRaise(k);
                else if (human != null) human.SetRaise(k);
                else if (soldier != null) soldier.SetRaise(k);
                else if (model != null) model.localScale = modelScale * (1f + Mathf.Clamp01(k) * 0.08f); // a plain model (the challenge's rival foi) swells to strike
            }

            /// <summary>A plain model (no robot rig): the challenge's rival foi. Its blows take <see cref="share"/> of foi's health.</summary>
            public Transform model;
            public Vector3 modelScale = Vector3.one;
            /// <summary>The weapon in a plain model's hand (the rival foi's own): it chops when the blow lands.</summary>
            public HeldWeapon held;
            /// <summary>Its weapon already swinging for the blow being wound up (started early: its contact meets the blow).</summary>
            public bool preSwung;
            /// <summary>A partner foi (2 against 2): fights like a foi (the duel's reach and pace), keeps the duel's gap.</summary>
            public bool partner;
            public float share = -1f;

            public void Flash()
            {
                if (mon != null) { mon.Hit(hp); return; }
                if (bot != null) bot.Flash();
                else if (human != null) human.Flash();
                else if (soldier != null) soldier.Flash();
            }
        }

        /// <summary>The robot was hit on a tile, taking this share of its health.</summary>
        public event Action<GridPos, float> Hit;

        /// <summary>Where the blow now landing on foi came from (for its shield).</summary>
        public Vector3 BlowFrom { get; private set; }

        /// <summary>Set by the game when foi's shield took the blow just now (the rival gains nothing from it).</summary>
        public bool BlowBlocked { get; set; }
        public event Action MonsterAppeared;
        public event Action MonsterDefeated;
        /// <summary>A bug or robot was finished (its tile), for coins and combos.</summary>
        public event Action<GridPos, bool> Finished;
        /// <summary>A bug was squashed (its tile): it may leave a skill behind.</summary>
        public event Action<GridPos> BugSquashed;
        /// <summary>What was just knocked out (a robot, a tower, a bug), for the daily quests.</summary>
        public event Action<DailyGoal> Felled;

        /// <summary>Makes the monster at a tile (the game dresses it in the floor's guardian look).</summary>
        public Func<GridPos, int, Monster> CreateMonster;
        /// <summary>A tile the crowd should keep off (a warning, a pickup...).</summary>
        public Func<GridPos, bool> Avoid;

        /// <summary>The duel's red ground outside the closing ring: nobody steps out into it (stepping back in is fine).</summary>
        public Func<GridPos, bool> Unsafe;

        /// <summary>The middle of the duel's safe ring.</summary>
        public GridPos SafeCentre;

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
        private bool BrightBugs => true; // always lit, so they never melt into the floor
        /// <summary>How many blows a bug takes on this floor: one on the first floors, up to five far in.</summary>
        private int BugHp => level.number <= 8 ? 1 : Mathf.Min(5, 2 + (level.number - 9) / 70);
        /// <summary>Its armour (looks): plain, plated, spiked, horned.</summary>
        private int BugTier => level.number <= 8 ? 0 : Mathf.Min(3, 1 + (level.number - 9) / 70);
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
                foreach (var g in guards) if (!g.dead) n++;
                n += TowersLeft;
                n += guardsToCome;
                return n;
            }
        }
        /// <summary>How big the guards and enforcers stand (they grow with the campaign).</summary>
        public float EnemyScale = 1f;
        /// <summary>The world whose roster of robots this floor draws on (-1: none, the old robots: the duel, the roads), and
        /// the floor within that world (0..9).</summary>
        public int RosterWorld = -1, RosterFloor;
        /// <summary>foi's health points (a robot's blow takes its points off them: their share of the bar).</summary>
        public int FoiHp = 100;
        /// <summary>The floors' robots and soldiers hit this many times their table's blow (foi's health grows a lot along
        /// the campaign: without it the late floors barely scratch it). 1 in the challenge's duel.</summary>
        public float FoeHitScale = 1f;
        /// <summary>Health points of an old-style robot (no roster) for each point of its strength.</summary>
        public float HpBase = 10f;
        /// <summary>foi's height (world units, measured at its size): every robot is sized against it.</summary>
        public float FoiHeight;

        /// <summary>How tall a model stands above its root (its renderers' tops).</summary>
        private static float HeightOf(Transform root)
        {
            float top = 0f;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y - root.position.y);
            return top;
        }
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

        /// <summary>Counts the Begins: a bolt or glob still in the air from an earlier level checks it and fizzles.</summary>
        private int beginGen;

        public void Begin(GridModel grid, LevelData level, int world, int seed)
        {
            Stop();
            beginGen++;
            flowGoal = new GridPos(-999, -999); // the path map is the new floor's
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
            SetupStages();
            if (stageCount > 1) SpawnStages();
            else SpawnClassic();
            Total = guards.Count + towers.Count + guardsToCome; // the bugs are optional (they leave gold and skills)
            MonsterAwake = false;
            if (NoMonster) { }
            else if (MonsterSpot.HasValue) RaiseMonster(asleep: Total > 0);
            else if (Total == 0) RaiseMonster();
        }

        private void SpawnClassic()
        {
            var free = FreeTiles(Mathf.Clamp(Mathf.Min(grid.Width, grid.Height) / 2, 1, 3)); // not right next to the robot (small floors allow less room)
            for (int i = 0; i < level.bugs && free.Count > 0; i++) AddBug(Take(free));
            // Only a few robots at a time; the rest of the floor's robots drop in as they fall (see UpdateReinforcements).
            guardCap = level.robotsAtOnce > 0 ? level.robotsAtOnce : level.robots;
            int first = Mathf.Min(level.robots, guardCap);
            for (int i = 0; i < first && free.Count > 0; i++) AddGuard(Take(free));
            guardsToCome = level.robots - Mathf.Min(first, guards.Count);
            hardToCome = 0;
            nextGuardAt = Time.time + 2f;
            for (int i = 0; i < level.brutes && free.Count > 0; i++) AddGuard(Take(free), brute: true);
            // vanG's warlords: the very strong tenth of the crowd, kept away from where foi starts.
            var away = free.FindAll(p => Chebyshev(p, robot.Position) >= 4);
            int elitesNow = level.hardAtOnce > 0 ? Mathf.Min(level.elites, level.hardAtOnce) : level.elites;
            for (int i = 0; i < elitesNow && away.Count > 0; i++) { var p = Take(away); free.Remove(p); AddGuard(p, brute: true, rank: 99); }
            if (level.hardAtOnce > 0)
            {
                // The battle arenas: a steady fight, never the whole crowd at once — the warlords drop in as others fall.
                hardToCome = Mathf.Max(0, level.elites - elitesNow);
                guardsToCome += hardToCome;
            }
            // Guard towers, set well away from where foi starts.
            var far = new List<GridPos>();
            foreach (var p in free) if (Chebyshev(p, robot.Position) >= 4) far.Add(p);
            if (level.reactor && far.Count > 0) { var rp = NearestCentre(far); far.Remove(rp); free.Remove(rp); AddTower(rp, reactor: true); }
            foreach (var tp in TowerSpots(far, level.towers)) { far.Remove(tp); free.Remove(tp); AddTower(tp); }
        }

        public void Stop()
        {
            running = false;
            guardsToCome = 0;
            hardToCome = 0;
            PlayerAway = false;
            foreach (var b in bugs) if (b.model != null) Destroy(b.model.gameObject);
            foreach (var g in guards)
            {
                if (g.root != null) Destroy(g.root.gameObject);
                if (!g.dead && grid != null) grid.SetOccupied(g.pos, false);
            }
            bugs.Clear();
            guards.Clear();
            StopAllies();
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
            var b = new Bug { pos = p, stage = StageOf(p), wait = (float)rng.NextDouble(), hp = BugHp };
            b.model = BugModel.Build(transform, bugColour, world % BugModel.Kinds, BrightBugs); // a new kind of bug every world
            b.model.Armor(BugTier);
            b.model.transform.localScale = Vector3.one * (1.5f + BugTier * 0.12f) * Mathf.Max(1f, EnemyScale * 0.65f) * 0.72f; // smaller now, beside a smaller foi
            b.from = b.to = At(p);
            b.model.transform.position = b.to;
            b.model.transform.rotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);
            bugs.Add(b);
        }

        /// <param name="rank">On fortress floors: 0 an easy one-blow robot, each rank up one more blow and a later
        /// world's build; 99 an elite enforcer (hard floors' last stage). -1 the classic guard.</param>
        private void AddGuard(GridPos p, bool brute = false, int rank = -1)
        {
            // Health points, blow and armour: from the world's roster (a robot of this world or an earlier one; the enforcers
            // and warlords half again or twice an advanced one), or the old robots' health (no roster).
            var type = new EnemyRoster.Type { world = Mathf.Max(0, RosterWorld), advanced = false };
            var stats = new EnemyRoster.Stats { hp = Mathf.Max(1, Mathf.RoundToInt(HpBase * (rank >= 99 ? 3.5f : brute ? 2.2f : rank == 0 ? 0.6f : 1f))) };
            if (RosterWorld >= 0)
            {
                if (rank >= 99) stats = EnemyRoster.Special(RosterWorld, RosterFloor, 2.2f);
                else if (brute) stats = EnemyRoster.Special(RosterWorld, RosterFloor);
                else
                {
                    type = EnemyRoster.Pick(rng, RosterWorld, RosterFloor);
                    stats = EnemyRoster.For(type, RosterWorld, RosterFloor);
                    if (rank > 0) { stats.hp = Mathf.RoundToInt(stats.hp * (1f + 0.05f * rank)); stats.damage = Mathf.RoundToInt(stats.damage * (1f + 0.05f * rank)); }
                }
            }
            int hp = Mathf.Max(1, stats.hp);
            var g = new Guard { pos = p, brute = brute, rank = rank, stage = StageOf(p), hp = hp, step = level.robotStep * (rank == 0 ? 1.25f : 1f) * (0.8f + (float)rng.NextDouble() * 0.4f) };
            g.blow = RosterWorld >= 0 ? stats.damage : 0;
            g.armorShare = RosterWorld >= 0 ? stats.armor : 0f;
            g.root = new GameObject(brute ? "Enforcer" : "GuardRobot").transform;
            g.root.SetParent(transform, false);
            var accent = RedFoes ? new Color(1f, 0.22f, 0.18f) : Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.55f, 1f), 0.75f, 1f);
            if (brute)
            {
                // vanG's enforcer: a tall humanoid that hits much harder.
                g.human = HumanoidBot.Build(g.root, accent, rank >= 99 ? Menace : 0);
                g.human.transform.localScale = Vector3.one * (rank >= 99 ? 0.92f : 0.7f) * EnemyScale;
            }
            else
            {
                // A new build of guard every world, in that world's colour.
                // In the perfect city: pearl and gold guards lit in the floor's colour.
                int build = rank < 0 ? world : Mathf.Max(0, world - 2 + rank * 2); // the easy ones an older build, each rank a newer one
                g.bot = RosterWorld >= 0 ? GuardBot.Build(g.root, Color.Lerp(RedFoes ? accent : EnemyRoster.Plate(type.world), new Color(0.3f, 0.3f, 0.35f), 0.3f), 0, false, type.Style)
                    : level.utopia ? GuardBot.Build(g.root, Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.5f, 1f), 0.6f, 1f), build, utopian: true)
                    : GuardBot.Build(g.root, Color.Lerp(accent, new Color(0.3f, 0.3f, 0.35f), 0.35f), build);
                g.bot.transform.localScale = Vector3.one * (rank == 0 ? 0.55f : 0.62f + Mathf.Max(0, rank) * 0.02f) * EnemyScale;
            }
            g.maxHp = Mathf.Max(1, g.hp);
            // Sized against foi, so the floors look big: a robot as tall as foi (the easy ones a touch smaller), the
            // enforcers two steps taller, the warlords taller still.
            if (FoiHeight > 0.05f)
            {
                var model = g.bot != null ? g.bot.transform : g.human.transform;
                float h = HeightOf(g.root);
                float want = FoiHeight * (g.human != null ? (rank >= 99 ? 1.15f : 1f) : rank > 0 || type.advanced ? 1f : 0.9f); // foi stands as tall as the second smallest: the plain robots a touch shorter
                if (h > 0.05f) model.localScale *= want / h;
                // Never spindly: a little broader than tall would make them.
                model.localScale = Vector3.Scale(model.localScale, g.human != null ? new Vector3(1.22f, 1f, 1.22f) : new Vector3(1.12f, 1f, 1.12f));
            }
            MaybeArm(g);
            AddHealthBar(g);
            if (level.shieldEvery > 0 && guards.Count % level.shieldEvery == level.shieldEvery - 1) AddShield(g);
            g.from = g.to = At(p);
            g.root.position = g.to;
            grid.SetOccupied(p, true);
            guards.Add(g);
        }

        /// <summary>Robots of this floor still to drop in, how many may be on the floor at once, and when the next comes.</summary>
        private int guardsToCome, guardCap;
        /// <summary>Of those, warlords still to come (only when the floor caps them, see LevelData.hardAtOnce).</summary>
        private int hardToCome;
        /// <summary>foi is up on a gun post (off the floor, out of reach): the robots gather below but cannot strike.</summary>
        public bool PlayerAway;
        /// <summary>The floor tile at the foot of the rampart under foi's gun (null: foi is not on a gun).</summary>
        public Func<GridPos?> WallTarget;
        /// <summary>A warlord smashed the rampart under foi's gun: this share of foi's health.</summary>
        public event Action<float> AwayHit;
        private float nextGuardAt;

        /// <summary>
        /// A robot fell and there are more to come: a new one drops in from the sky onto a free tile away from foi
        /// (a ring of light marks the spot first), until the floor's count is reached.
        /// </summary>
        private void UpdateReinforcements()
        {
            if (stageCount > 1) { UpdateStageReinforcements(); return; }
            if (guardsToCome <= 0 || Time.time < nextGuardAt) return;
            int alive = 0, hardAlive = 0;
            foreach (var g in guards) if (!g.dead) { if (!g.brute) alive++; else if (g.rank >= 99) hardAlive++; }
            bool hard = hardToCome > 0 && hardAlive < level.hardAtOnce;
            if (!hard && (alive >= guardCap || guardsToCome - hardToCome <= 0)) return;
            var spots = FreeTiles(4);
            spots.RemoveAll(p => grid.IsOccupied(p) || AllyAt(p) || (Avoid != null && Avoid(p)));
            if (spots.Count == 0) { nextGuardAt = Time.time + 0.5f; return; }
            var at = Take(spots);
            if (hard) { AddGuard(at, brute: true, rank: 99); hardToCome--; }
            else AddGuard(at);
            var ng = guards[guards.Count - 1];
            ng.from = At(at) + Vector3.up * 7f; // it falls onto its tile
            ng.root.position = ng.from;
            ng.moveT = 0f;
            ng.moveDur = 0.45f;
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
            foreach (var b in bugs) if (!b.dead && b.pos == p) HitBug(b, 1);
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
                g.hp = Mathf.Max(1, g.hp - Mathf.Max(1, g.maxHp / 10));
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
                if (BackOnly && monster != null && through > 0)
                {
                    // the giant: its weak spot is on its back (from the front a blow barely scratches it)
                    var toFoi = robot.transform.position - At(monsterPos); toFoi.y = 0f;
                    bool behind = toFoi.sqrMagnitude > 0.01f && Vector3.Dot(monster.Forward, toFoi.normalized) < -0.2f;
                    through = behind ? through * 2 : Mathf.Max(1, through / 5);
                    if (!behind) BackHint?.Invoke(At(monsterPos));
                }
                if (through > 0) HitMonster(through);
                return true;
            }
            foreach (var g in guards)
            {
                if (g.dead || (g.pos != p && !(g.moveT < 1f && g.prevPos == p))) continue;
                // An energy shield takes the blow first (any weapon cracks it: one blow, one charge).
                if (g.squad)
                {
                    // A duel soldier: the live rival's goes to its game; this game's takes it and strikes back at foi.
                    if (g.remote) { g.flash = 1f; g.Flash(); fx.Burst(At(p) + Vector3.up * 0.4f, Palette.UiGold, Palette.CoinGlow, 14, 4f); AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1.25f); SquadHitRemote?.Invoke(g.squadId, damage, true); return true; }
                    AudioManager.PlaySfx(Sfx.Blocked, 0.8f, 1f);
                    HurtSquad(g, damage, null, foiBlow: true);
                    return true;
                }
                if (g == challengeFoe && FoeRemote)
                {
                    // A live rival: the blow goes to the server (it keeps the health); here only the flash.
                    g.flash = 1f;
                    g.Flash();
                    fx.Burst(At(p) + Vector3.up * 0.4f, Palette.UiGold, Palette.CoinGlow, 20, 5f);
                    AudioManager.PlaySfx(Sfx.Impact, 0.7f, 1.15f);
                    RemoteFoeStruck?.Invoke();
                    ChallengeFoeStruck?.Invoke();
                    return true;
                }
                RaiseAlarm(g);
                // the rival foi's defence (the duel's block, perfect guard and dodge): the game decides it
                bool guarded = false;
                if (g == challengeFoe && FoeGuard != null)
                {
                    var met = FoeGuard(damage);
                    if (met == Combat.DefenseOutcome.Perfect) { Parry(g, p); return true; }
                    if (met == Combat.DefenseOutcome.Dodged) return true;
                    if (met == Combat.DefenseOutcome.Blocked || met == Combat.DefenseOutcome.Broke) { guarded = true; damage = Mathf.Max(1, Mathf.RoundToInt(damage * FoeGuardThrough)); }
                }
                else if (g == challengeFoe && FoeParries()) { Parry(g, p); return true; }
                if (g.shield > 0) { HitShield(g, p); return true; }
                if (g == challengeFoe && Time.time < FoeInvulnUntil) { HitShield(g, p); return true; } // (its backflip or iron body)
                if (g == challengeFoe && Time.time < FoeMirrorUntil) { FoeMirrored?.Invoke(damage); HitShield(g, p); return true; } // (its mirror: back at foi)
                g.hp -= g.armorShare > 0f ? Mathf.Max(1, Mathf.RoundToInt(damage * (1f - g.armorShare))) : ThroughArmor(p, damage);
                if (g == challengeFoe && g.hp <= 0 && FoeSturdy) { FoeSturdy = false; g.hp = 1; } // (its sturdy spare life)
                g.flash = 1f;
                g.Flash();
                if (g != challengeFoe) { g.windup = -1f; ClearTelegraph(g); } // (the rival foi trades blows: its own swing goes on)
                if (g == challengeFoe && guarded) ChallengeFoeBlocked?.Invoke(); // (blocked: no combo, no bar for it)
                else if (g == challengeFoe) { FoiRanOn(); ChallengeFoeStruck?.Invoke(); }
                fx.Burst(At(p) + Vector3.up * 0.4f, Palette.UiGold, Palette.CoinGlow, 34, 6f);
                Shockwave.Create(At(p), 1.4f, new Color(1f, 0.85f, 0.45f));
                AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.85f);
                rig.Shake(0.35f);
                if (g.hp <= 0)
                {
                    // A finishing blow by the burning stairs sends it tumbling into the flames.
                    var away = new GridPos(g.pos.x - robot.Position.x, g.pos.y - robot.Position.y);
                    var dir = Mathf.Abs(away.x) >= Mathf.Abs(away.y) ? new GridPos(Math.Sign(away.x), 0) : new GridPos(0, Math.Sign(away.y));
                    if (grid.IsLava(g.pos + dir)) BurnGuard(g, g.pos + dir);
                    else KillGuard(g);
                }
                else if (g != challengeFoe) KnockBack(g);
                else if (!guarded) FoeHurt(g); // the rival foi stands its ground and hits back
                return true;
            }
            if (StrikeDrone(p)) return true;
            foreach (var b in bugs)
            {
                if (b.dead || b.pos != p) continue;
                HitBug(b, damage);
                return true;
            }
            return false;
        }

        /// <summary>Where every robot still standing is (vanG and the warlords marked big), for the arena map.</summary>
        public void Enemies(System.Collections.Generic.List<(Vector3 at, bool big)> into)
        {
            into.Clear();
            if (!running) return;
            foreach (var g in guards) if (!g.dead && g.root != null) into.Add((g.root.position, g.rank >= 99 || g.brute));
            if (MonsterUp && monster != null) into.Add((monster.transform.position, true));
        }

        /// <summary>
        /// A handheld gun's shot along a ray (the arenas' crosshair): the first robot, bug or vanG its body passes
        /// through within <paramref name="range"/> takes an ordinary blow of <paramref name="damage"/> (armour, shields and
        /// knock-back as for a blade). Returns where the shot ended.
        /// </summary>
        public bool ShootBlow(Ray ray, float range, int damage, out Vector3 point)
        {
            point = ray.origin + ray.direction * range;
            if (!running) return false;
            float best = range;
            GridPos? tile = null;
            void Try(Vector3 feet, float height, float radius, GridPos at)
            {
                var d = ray.direction;
                var flat = new Vector2(d.x, d.z);
                var w = new Vector2(feet.x - ray.origin.x, feet.z - ray.origin.z);
                float t = flat.sqrMagnitude < 1e-6f ? 0f : Vector2.Dot(w, flat) / flat.sqrMagnitude;
                if (t <= 0f || t >= best) return;
                var p = ray.origin + d * t;
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(feet.x, feet.z)) > radius) return;
                if (p.y < feet.y - 0.15f || p.y > feet.y + height + 0.25f) return;
                best = t;
                tile = at;
            }
            foreach (var g in guards)
            {
                if (g.dead || g.root == null) continue;
                float h = g.bar != null ? Mathf.Max(0.8f, g.bar.position.y - g.root.position.y) : 1.2f;
                Try(g.root.position, h, Mathf.Max(0.45f, h * 0.32f), g.pos);
            }
            foreach (var b in bugs) if (!b.dead && b.model != null) Try(b.model.transform.position, 0.45f, 0.5f, b.pos);
            if (MonsterUp && monster != null) Try(monster.transform.position, 2f * monster.transform.lossyScale.y, 0.9f * monster.transform.lossyScale.x, monsterPos);
            if (!tile.HasValue)
            {
                var floor = new Plane(Vector3.up, new Vector3(0f, GridView.SurfaceY, 0f));
                if (floor.Raycast(ray, out float f) && f < range) point = ray.GetPoint(f);
                return false;
            }
            point = ray.GetPoint(best);
            return Strike(tile.Value, damage);
        }

        /// <summary>
        /// A gun-post bullet along a ray: the first robot or bug whose body it passes through takes it — a warlord or
        /// enforcer a tenth of its health, any other robot half, a bug all. Returns where it struck (or flew out).
        /// </summary>
        public bool Shoot(Ray ray, float range, out Vector3 point)
        {
            point = ray.origin + ray.direction * range;
            if (!running) return false;
            float bestT = range;
            Guard hitG = null;
            Bug hitB = null;
            bool Through(Vector3 feet, float height, float radius, out float t)
            {
                // Where the ray passes closest to the body's upright axis (seen from above), and whether that is inside it.
                var d = ray.direction;
                var flat = new Vector2(d.x, d.z);
                var w = new Vector2(feet.x - ray.origin.x, feet.z - ray.origin.z);
                t = flat.sqrMagnitude < 1e-6f ? 0f : Vector2.Dot(w, flat) / flat.sqrMagnitude;
                if (t <= 0f || t >= bestT) return false;
                var p = ray.origin + d * t;
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(feet.x, feet.z)) > radius) return false;
                return p.y >= feet.y - 0.15f && p.y <= feet.y + height + 0.15f;
            }
            foreach (var g in guards)
            {
                if (g.dead || g.root == null) continue;
                float h = g.bar != null ? Mathf.Max(0.8f, g.bar.position.y - g.root.position.y) : 1.2f;
                if (Through(g.root.position, h, Mathf.Max(0.45f, h * 0.32f), out float t)) { bestT = t; hitG = g; hitB = null; }
            }
            foreach (var b in bugs)
            {
                if (b.dead || b.model == null) continue;
                if (Through(b.model.transform.position, 0.45f, 0.5f, out float t)) { bestT = t; hitB = b; hitG = null; }
            }
            if (hitG == null && hitB == null)
            {
                // Down to the floor, if it gets there first.
                var floor = new Plane(Vector3.up, new Vector3(0f, GridView.SurfaceY, 0f));
                if (floor.Raycast(ray, out float f) && f < range) point = ray.GetPoint(f);
                return false;
            }
            point = ray.GetPoint(bestT);
            if (hitB != null) { HitBug(hitB, 99); return true; }
            var hg = hitG;
            if (hg.shield > 0) { HitShield(hg, hg.pos); return true; }
            RaiseAlarm(hg);
            hg.chip += hg.maxHp * (hg.brute || hg.rank >= 99 ? 0.1f : 0.5f);
            int dmg = Mathf.FloorToInt(hg.chip + 0.0001f);
            hg.chip -= dmg;
            hg.hp -= dmg;
            hg.flash = 1f;
            hg.Flash();
            fx.Burst(point, new Color(1f, 0.75f, 0.35f), new Color(2.2f, 1.2f, 0.4f), 10, 3f);
            if (hg.hp <= 0) KillGuard(hg);
            return true;
        }

        /// <summary>A robot was hit: every robot within six tiles of it (in its own stage) comes running for a while.</summary>
        private void RaiseAlarm(Guard hit)
        {
            float until = Time.time + 5f;
            foreach (var o in guards)
            {
                if (o.dead || Chebyshev(o.pos, hit.pos) > 6) continue;
                if (stageCount > 1 && o.stage != hit.stage) continue;
                o.alertUntil = until;
                if (o.step > level.robotStep * 0.3f) o.step = level.robotStep * 0.3f; // off at once
            }
        }

        private void KnockBack(Guard g)
        {
            var away = new GridPos(g.pos.x - robot.Position.x, g.pos.y - robot.Position.y);
            var dir = Mathf.Abs(away.x) >= Mathf.Abs(away.y) ? new GridPos(Math.Sign(away.x), 0) : new GridPos(0, Math.Sign(away.y));
            var next = g.pos + dir;
            // Knocked onto the fortress's burning stairs: it tumbles down into the flames.
            if (grid.IsLava(next)) { BurnGuard(g, next); return; }
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

        /// <summary>A robot knocked down the burning stairs: it falls, catches fire and is gone (it counts as beaten).</summary>
        private void BurnGuard(Guard g, GridPos into)
        {
            grid.SetOccupied(g.pos, false);
            g.dead = true;
            ClearTelegraph(g);
            var from = g.root.position;
            var at = At(into) + Vector3.down * 0.6f;
            fx.Burst(at + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.15f), new Color(3f, 1.2f, 0.25f), 46, 6f);
            fx.Dust(at, new Color(0.25f, 0.22f, 0.22f), 20, 3f);
            Shockwave.Create(At(into), 1.4f, new Color(1f, 0.55f, 0.2f));
            AudioManager.PlaySfx(Sfx.Squash, 0.9f, 0.7f);
            rig.Shake(0.5f);
            StartCoroutine(FallAndBurn(g.root, from, at));
            Finished?.Invoke(into, true);
            Felled?.Invoke(DailyGoal.Robots);
            CheckCleared();
        }

        private System.Collections.IEnumerator FallAndBurn(Transform body, Vector3 from, Vector3 to)
        {
            for (float t = 0f; t < 1f && body != null; t += Time.deltaTime / 0.5f)
            {
                body.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.5f;
                body.rotation *= Quaternion.Euler(Time.deltaTime * 400f, 0f, Time.deltaTime * 200f);
                body.localScale = Vector3.one * (1f - t * 0.6f);
                yield return null;
            }
            if (body != null) Destroy(body.gameObject);
        }

        /// <summary>A stomp or a blow on a bug: tough ones flatten, spring back and scurry off until their last one.</summary>
        private void HitBug(Bug b, int damage)
        {
            b.hp -= Mathf.Max(1, damage);
            if (b.hp <= 0) { KillBug(b); return; }
            b.model.Hurt();
            fx.Burst(At(b.pos) + Vector3.up * 0.15f, b.model.Blood, b.model.Blood * 0.6f, 6, 1.5f);
            AudioManager.PlaySfx(Sfx.Blocked, 0.4f, 1.8f);
            b.wait = 0f; // off it goes at once
        }

        private void KillBug(Bug b)
        {
            b.dead = true;
            // Squashed flat on its tile: a spray of its blood and a splat that fades, the body pressed into the floor.
            b.model.Squash();
            Destroy(b.model.gameObject, 0.9f);
            BloodSplat.Create(b.model.transform.position, b.model.Blood, rng.Next());
            fx.Burst(At(b.pos) + Vector3.up * 0.08f, b.model.Blood, b.model.Blood * 0.6f, 10, 2f);
            BugSquashed?.Invoke(b.pos);
            AudioManager.PlaySfx(Sfx.Squash, 0.6f, 1.6f);
            Finished?.Invoke(b.pos, false);
            Felled?.Invoke(DailyGoal.Bugs);
            CheckCleared();
        }

        /// <summary>The duel's rival is falling (its model about to go): the game shows its fall on a stand-in.</summary>
        public event Action<Transform> ChallengeFoeFalling;

        private void KillGuard(Guard g)
        {
            if (g == challengeFoe && g.model != null) ChallengeFoeFalling?.Invoke(g.model);
            g.dead = true;
            CheckChestFreed(g);
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
            CheckGates();
            if (Left != 0 || MonsterDown || NoMonster) return;
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
            monsterStage = 0;
            monsterPace = 1f;
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
            // a stage crossed: the monster changes (the next stage's share of its health)
            if (MonsterStages > 1 && monsterHp > 0 && level.monsterHp > 0)
            {
                int stage = Mathf.Clamp((int)((1f - monsterHp / (float)level.monsterHp) * MonsterStages), 0, MonsterStages - 1);
                if (stage > monsterStage)
                {
                    monsterStage = stage;
                    monsterPace = 1f + 0.25f * stage;
                    monster.Grow(stage);
                    monsterTimer = Mathf.Max(monsterTimer, 1.4f); // (a breath while it changes)
                    MonsterStage?.Invoke(stage);
                }
            }
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
            if (stageCount > 1)
            {
                // Fortress floors point at what is left in foi's stage; with it clear, at the open gate ahead.
                int rs = RobotStage;
                var here = all.FindAll(p => StageOf(p) == rs);
                if (here.Count == 0 && rs < gateOpen.Length && gateOpen[rs] && gateGroups[rs].Count > 0) here.Add(gateGroups[rs][0]);
                if (here.Count > 0) all = here;
            }
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
            PickEngaged();
            foreach (var g in guards) if (!g.dead) UpdateGuard(g, dt);
            UpdateAllies(dt);
            UpdateBotOrders();
            UpdateTowers(dt);
            UpdateReinforcements();
            UpdateDrones(dt);
            if (MonsterUp && MonsterAwake) UpdateMonster(dt);
        }

        /// <summary>The lane duel's line (null: none): the rival only steps along it, or back onto it.</summary>
        public int? LaneX;

        private bool OffLane(GridPos from, GridPos p) => LaneX is int lx && challengeFoe != null && from == challengeFoe.pos && p.x != lx && Math.Abs(p.x - lx) >= Math.Abs(from.x - lx);

        private bool CanStep(GridPos from, GridPos p) => FloorRelief.StepOk(from, p) && !PartnerAdjacent(from, p) && !OffLane(from, p) &&
            grid.IsStandable(p) && !grid.IsOccupied(p) && !AllyAt(p) && !grid.IsGap(p) && p != robot.Position && (Avoid == null || !Avoid(p)) && (Unsafe == null || !Unsafe(p) || Unsafe(from));

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
            if (next == robot.Position) HitBug(b, 1);
        }

        private void MoveGuard(Guard g, GridPos next, float duration)
        {
            if (g == challengeFoe && Chebyshev(next, robot.Position) < DuelGap && Chebyshev(next, robot.Position) < Chebyshev(g.pos, robot.Position)) return; // (a tile kept between the two foi)
            if (!g.ally) grid.SetOccupied(g.pos, false);
            g.prevPos = g.pos;
            g.pos = next;
            if (!g.ally) grid.SetOccupied(next, true);
            g.from = g.root.position;
            g.to = At(next);
            g.moveT = 0f;
            g.moveDur = Mathf.Max(0.08f, duration);
            g.step = 0f; // the next step follows straight on: a smooth walk, not a hop and a pause
        }

        /// <summary>Tiles a second a robot walks: nearly foi's own pace (a touch slower), quicker on harder floors and when roused.</summary>
        private float GuardSpeed(Guard g)
        {
            float v = ChallengeBattle ? Mathf.Lerp(3.0f, 4.2f, level.score / 100f) : Mathf.Lerp(3.3f, 4.6f, level.score / 100f);
            if (g.mon != null) v *= 0.8f;
            else if (g.brute) v *= g.rank >= 99 ? 0.85f : 0.9f;
            else if (g.rank == 0) v *= 0.92f;
            if (Time.time < g.alertUntil) v *= 1.12f;
            if (g == challengeFoe) v *= 1.35f; // the rival foi is as quick on its feet as foi
            return v;
        }


        /// <summary>A small health bar over a guard's head: dark back, a fill that empties and turns from green to red.</summary>
        /// <summary>
        /// No bar over a robot or a soldier (a crowd of bars was a clutter): its colours fade towards a dull grey as it
        /// loses health instead. (The bar's material stays for the code that still colours it.)
        /// </summary>
        private void AddHealthBar(Guard g)
        {
            g.barMat = MaterialFactory.Create(new Color(0.4f, 1f, 0.45f), new Color(0.4f, 1.5f, 0.45f));
            OwnedMaterials.Own(g.root.gameObject, g.barMat);
            g.barHp = g.hp;
        }

        private static MaterialPropertyBlock hurtBlock;

        /// <summary>See-through copies of the robots' materials (one per original, shared by every robot wearing it).</summary>
        private static readonly Dictionary<Material, Material> glassOf = new Dictionary<Material, Material>();

        private static Material GlassOf(Material m)
        {
            if (glassOf.TryGetValue(m, out var v) && v != null) return v;
            if (glassOf.Count > 400)
            {
                var gone = new List<Material>();
                foreach (var kv in glassOf) if (kv.Key == null || kv.Value == null) gone.Add(kv.Key);
                foreach (var k in gone) glassOf.Remove(k);
            }
            v = new Material(m) { name = m.name + " (glass)" };
            MaterialFactory.ConfigureTransparent(v);
            glassOf[m] = v;
            return v;
        }

        /// <summary>
        /// When its health changed: the more hurt, the more see-through it grows (down to a ghost of itself near the
        /// end). Unhurt it keeps its own solid materials; hurt it wears shared see-through copies with its alpha in a
        /// property block (no new material per robot). The rival foi stays solid: its health shows at the top.
        /// </summary>
        private void UpdateHealthBar(Guard g)
        {
            if (g.barHp == g.hp || g.root == null) return;
            g.barHp = g.hp;
            if (g == challengeFoe) return;
            float k = Mathf.Clamp01(g.hp / (float)Mathf.Max(1, g.maxHp));
            if (g.tint == null)
            {
                g.tint = new List<(Renderer, Color, Material)>();
                foreach (var r in g.root.GetComponentsInChildren<Renderer>(true))
                {
                    var m = r.sharedMaterial;
                    if (r is ParticleSystemRenderer || m == null || !m.HasProperty("_BaseColor")) continue;
                    if (m.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent) continue; // already see-through (glows, shields)
                    g.tint.Add((r, m.GetColor("_BaseColor"), m));
                }
            }
            if (hurtBlock == null) hurtBlock = new MaterialPropertyBlock();
            bool whole = k >= 0.999f;
            float alpha = Mathf.Lerp(0.3f, 0.92f, k);
            foreach (var (r, c, m) in g.tint)
            {
                if (r == null) continue;
                if (whole)
                {
                    r.sharedMaterial = m;
                    r.SetPropertyBlock(null);
                    continue;
                }
                r.sharedMaterial = GlassOf(m);
                hurtBlock.Clear();
                hurtBlock.SetColor("_BaseColor", new Color(c.r, c.g, c.b, c.a * alpha));
                r.SetPropertyBlock(hurtBlock);
            }
        }

        private void UpdateGuard(Guard g, float dt)
        {
            if (g == challengeFoe && FoeRemote) { UpdateHealthBar(g); UpdateRemoteFoe(g, dt); return; }
            if (g == challengeFoe && FoeHolding && g.windup < 0f)
            {
                // blocking: it stands its ground behind its shield, turned to foi, and starts no blow
                var toFoiG = GridView.ToWorld(robot.Position) - g.root.position; toFoiG.y = 0f;
                if (toFoiG.sqrMagnitude > 0.01f) g.root.rotation = Quaternion.Slerp(g.root.rotation, Quaternion.LookRotation(toFoiG), Time.deltaTime * 6f);
                if (g.moveT < 1f) { g.moveT = Mathf.Min(1f, g.moveT + dt / g.moveDur); g.root.position = Vector3.Lerp(g.from, g.to, g.moveT); }
                UpdateHealthBar(g);
                return;
            }
            if (g == challengeFoe && Time.time < foeStunUntil)
            {
                // stunned: it stands reeling, still turned to foi (never its back to it)
                var toFoi = GridView.ToWorld(robot.Position) - g.root.position; toFoi.y = 0f;
                if (toFoi.sqrMagnitude > 0.01f) g.root.rotation = Quaternion.Slerp(g.root.rotation, Quaternion.LookRotation(toFoi), Time.deltaTime * 6f);
                UpdateHealthBar(g);
                return;
            }
            if (g.squad) { UpdateSquad(g, dt); return; }
            UpdateHealthBar(g);
            if (g.windup < 0f) { IdleCharge(g); if (g == challengeFoe) FoeWindLeft = -1f; } // a blow cut short (struck, frozen, pushed): its charge and aim go
            if (Iced(g)) return;
            UpdateShield(g, dt);
            if (g.flash > 0f) g.flash -= dt * 3f;
            g.cooldown -= dt;
            if (g.moveT < 1f)
            {
                g.moveT = Mathf.Min(1f, g.moveT + dt / g.moveDur);
                g.root.position = Vector3.Lerp(g.from, g.to, g.moveT) + Vector3.up * Mathf.Abs(Mathf.Sin(g.moveT * Mathf.PI)) * 0.035f; // a light stride
            }
            UpdateLunge(g, dt);
            if (g.spitter) { UpdateSpitter(g, dt); return; }
            var toRobot = GridView.ToWorld(robot.Position) - g.root.position;
            toRobot.y = 0f;
            if (toRobot.sqrMagnitude > 0.01f) g.root.rotation = Quaternion.Slerp(g.root.rotation, Quaternion.LookRotation(toRobot), dt * 8f);

            if (g.windup >= 0f)
            {
                // Winding up: the tiles it will hit glow ever redder; the slam lands on them.
                g.windup += dt;
                float windTotal = (g.brute ? BruteWindTime : WindTime) * FoeWindScale(g);
                float k = g.windup / windTotal;
                if (g == challengeFoe) FoeWindLeft = Mathf.Max(0f, windTotal - g.windup); // (the training's perfect guard cue)
                // the weapon's swing starts early enough for its contact to meet the blow (the blow's moment is the rules')
                if (g.windup <= dt + 1e-4f) g.preSwung = false;
                if (g.held != null && !g.preSwung && windTotal - g.windup <= g.held.LeadSeconds) { g.held.SwingToContact(windTotal - g.windup); g.preSwung = true; }
                TrackFoi(g, k);
                ShowCharge(g, Mathf.Clamp01(k));
                g.SetRaise(k * 1.3f);
                if (g.soldier == null || g.area.Contains(robot.Position)) foreach (var t in g.area) view.SetWarning(t, 0.4f + 0.6f * k); // soldiers on soldiers: no red tiles
                if (k >= 1f)
                {
                    if (g == challengeFoe && toRobot.sqrMagnitude > 0.01f) g.root.rotation = Quaternion.LookRotation(toRobot); // (never a blow with its back turned)
                    if (g == challengeFoe) FoeWindLeft = -1f;
                    g.windup = -1f;
                    g.cooldown = ChallengeBattle
                        ? (g.brute ? 1.3f : g.soldier != null ? 0.6f + (float)rng.NextDouble() * 0.3f : 0.55f)
                        : (g.brute ? 1.0f : g.soldier != null ? 0.45f + (float)rng.NextDouble() * 0.25f : 0.38f); // the floors: they keep at it
                    FoeStruck(g);
                    if (g == challengeFoe && FoeWhiff > 0f && rng.NextDouble() < FoeWhiff) g.area.Clear(); // a blow into the air (a human slip)
                    if (g.held != null && !g.preSwung) g.held.Swing();
                    g.preSwung = false;
                    g.SetRaise(0f);
                    foreach (var t in g.area) view.SetWarning(t, 0f);
                    bool shot = LandBlow(g); // the floors: a gunner's tracer (its hit lands when it arrives), or a lunge and a slash
                    if (IsArcher(g)) ArrowFx(g.root.position, At(g.target));
                    else if (!shot) Shockwave.Create(At(g.target), g.brute ? 2.2f : 1f, new Color(1f, 0.5f, 0.3f));
                    if (g.brute) fx.Dust(At(g.target), new Color(0.55f, 0.5f, 0.45f), 18, 4f);
                    AudioManager.PlaySfx(Sfx.Impact, g.brute ? 1f : 0.7f, g.brute ? 0.55f : 0.8f);
                    rig.Shake(g.brute ? 0.8f : 0.3f);
                    if (g.atWall) { if (PlayerAway) AwayHit?.Invoke(ShareOf(g)); }
                    else if (!shot && g.area.Contains(robot.Position)) { BlowFrom = g.root.position; BlowBlocked = false; Hit?.Invoke(robot.Position, ShareOf(g)); if (g == challengeFoe && !BlowBlocked) { foiRun = 0; FoiStruckByFoe?.Invoke(); } }
                    g.atWall = false;
                    if (CompanionTile.HasValue && g.area.Contains(CompanionTile.Value)) CompanionHurt?.Invoke(g.brute ? 3 : 2);
                    HurtAllies(g, g.area);
                    g.area.Clear();
                }
                return;
            }
            if (g.moveT < 1f) return;
            if (stageCount > 1 && g.stage != RobotStage && StageOf(g.pos) != RobotStage) return; // it waits in its own stage
            // Within two tiles (diagonals count as one), the same reach as foi's blows: it swings as soon as it can.
            int near = Chebyshev(robot.Position, g.pos);
            // Fifi in reach and foi not (or now and then anyway): the robot goes for Fifi.
            if (g == challengeFoe && FoeTactics(g, near)) return;
            int reach = ReachOf(g);
            bool atFifi = CompanionTile.HasValue && Chebyshev(CompanionTile.Value, g.pos) <= AttackReach && (near > AttackReach || rng.Next(4) == 0);
            // An ally nearer than foi: it fights the ally (now and then foi in reach anyway).
            var ally = NearestAlly(g.pos, out int allyD);
            bool atAlly = ally != null && allyD < near && !(near <= reach && rng.Next(3) == 0);
            if (atAlly && allyD <= reach && g.cooldown <= 0f)
            {
                g.windup = 0f;
                g.target = ally.pos;
                g.area.Clear();
                g.area.Add(g.target);
                if (g.brute)
                    foreach (var d in DirectionExtensions.All)
                        if (grid.IsFloor(g.target + d.ToOffset())) g.area.Add(g.target + d.ToOffset());
                return;
            }
            // Only a few at a time come at foi: the rest hold back in a ring and wait their turn.
            bool waiting = !atAlly && !atFifi && !engaged.Contains(g) && !PlayerAway;
            if (waiting && near <= WaitRing + 1)
            {
                // Waiting its turn: it moves round to its wing's place on the ring (the crowd closes round foi).
                if (near >= WaitRing - 1 && g.moveT >= 1f && g.chestId < 0 && g.mon == null && (g.step -= dt) <= 0f)
                {
                    var spot = FlankSpot(g, robot.Position, WaitRing);
                    if (Chebyshev(spot, g.pos) > 1 && StepTowards(g, spot, 1f / (GuardSpeed(g) * 0.8f))) return;
                    g.step = 0.3f;
                }
                if (near < WaitRing - 1 && g.moveT >= 1f && (g.step -= dt) <= 0f)
                {
                    // Too close while waiting: a step back out.
                    GridPos back = g.pos; int bd = near;
                    foreach (var d in DirectionExtensions.All)
                    {
                        var n = g.pos + d.ToOffset();
                        if (CanStep(g.pos, n) && Chebyshev(robot.Position, n) > bd) { bd = Chebyshev(robot.Position, n); back = n; }
                    }
                    if (back != g.pos) MoveGuard(g, back, 1f / GuardSpeed(g));
                }
                return;
            }
            if ((near <= reach || atFifi) && g.cooldown <= 0f && !PlayerAway)
            {
                g.windup = 0f;
                g.tracking = !atFifi;
                g.target = atFifi ? CompanionTile.Value : robot.Position;
                g.area.Clear();
                g.area.Add(g.target);
                if (g.brute)
                    foreach (var d in DirectionExtensions.All)
                        if (grid.IsFloor(g.target + d.ToOffset())) g.area.Add(g.target + d.ToOffset());
                return;
            }
            // foi up on a gun post: the crowd holds its ground; the warlords march to the foot of the rampart under the
            // gun and smash at it from there (they never climb).
            var goal = atAlly ? ally.pos : robot.Position;
            if (!atAlly && !PlayerAway && g.mon == null && g.chestId < 0 && !g.spitter) goal = FlankGoal(g, near, reach);
            // A chest's guards keep by their chest while foi is away from it.
            if (g.chestId >= 0 && !atAlly && Chebyshev(robot.Position, g.home) > 8)
            {
                if (Chebyshev(g.pos, g.home) <= 2) return;
                goal = g.home;
            }
            if (PlayerAway && !atAlly)
            {
                if (g.rank < 99 || WallTarget == null || !(WallTarget() is GridPos wall)) return;
                goal = wall;
                if (Chebyshev(g.pos, wall) <= 1 && g.cooldown <= 0f)
                {
                    g.windup = 0f;
                    g.atWall = true;
                    g.target = wall;
                    g.area.Clear();
                    g.area.Add(wall);
                    return;
                }
            }
            int dist = goal.Manhattan(g.pos);
            g.step -= dt;
            if (g.step > 0f || (!PlayerAway && (atAlly ? allyD : near) <= Mathf.Max(g == challengeFoe ? DuelGap : 1, reach - 1)) || (PlayerAway && Chebyshev(g.pos, goal) <= 1)) return;
            float period = 1f / GuardSpeed(g);
            if (g.human != null) g.human.Step();
            // The arena city: round the buildings by the street map (distances from the goal), never stuck behind a block.
            if ((level.hardAtOnce > 0 || UseStreetMap) && !atAlly && FlowStep(g, robot.Position)) return; // one street map for all (wings would rebuild it per robot)
            // One step towards the robot along whichever axis brings it closer and is free.
            GridPos bestNext = g.pos;
            int bestDist = dist;
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (!CanStep(g.pos, n)) continue;
                int nd = goal.Manhattan(n);
                if (nd < bestDist || (nd == bestDist && rng.Next(2) == 0 && nd < dist)) { bestDist = nd; bestNext = n; }
            }
            if (bestNext != g.pos) MoveGuard(g, bestNext, period);
        }

        /// <summary>The robots allowed to come at foi right now (the nearest few); the rest wait in a ring.</summary>
        private readonly HashSet<Guard> engaged = new HashSet<Guard>();
        private readonly List<Guard> engageOrder = new List<Guard>();
        /// <summary>How far off the waiting robots hold.</summary>
        private const int WaitRing = 4;

        /// <summary>How many robots may fight foi at once: four at first, up to nine on the late floors.</summary>
        private int MaxEngaged => ChallengeBattle ? 999 : Mathf.Clamp(4 + world / 4, 4, 9);

        /// <summary>The challenge's battle: every robot fights at once (no waiting ring), archers shoot from afar.</summary>
        public bool ChallengeBattle;

        /// <summary>Robots find their way by the street map (the stage battle: round the middle stage to the bridges).</summary>
        public bool UseStreetMap;

        private void PickEngaged()
        {
            engaged.Clear();
            engageOrder.Clear();
            foreach (var g in guards) if (!g.dead && !g.ally && !g.spitter) engageOrder.Add(g);
            // Where the crowd stands, seen from foi: the wings fan out from that side.
            Vector2 sum = Vector2.zero;
            foreach (var g in engageOrder) sum += new Vector2(g.pos.x - robot.Position.x, g.pos.y - robot.Position.y);
            crowdAngle = sum.sqrMagnitude > 0.01f ? Mathf.Atan2(sum.y, sum.x) : crowdAngle;
            var at = robot.Position;
            engageOrder.Sort((a, b) => Chebyshev(a.pos, at).CompareTo(Chebyshev(b.pos, at)));
            int n = Mathf.Min(MaxEngaged, engageOrder.Count);
            for (int i = 0; i < n; i++) engaged.Add(engageOrder[i]);
            // A robot already swinging keeps its turn.
            foreach (var g in engageOrder) if (g.windup >= 0f) engaged.Add(g);
        }

        private int[,] flow;
        private GridPos flowGoal = new GridPos(-999, -999);
        private static readonly System.Collections.Generic.Queue<GridPos> flowQueue = new System.Collections.Generic.Queue<GridPos>();

        /// <summary>
        /// One step for a robot in the arena city along the street map: every walkable tile's walking distance to the
        /// goal (worked out again whenever the goal tile changes), so it walks round buildings instead of pressing into
        /// them. False when there is no way (or no better tile free), so the plain step is tried.
        /// </summary>
        private bool FlowStep(Guard g, GridPos goal)
        {
            int w = grid.Width, h = grid.Height;
            if (flow == null || flow.GetLength(0) != w || flow.GetLength(1) != h) { flow = new int[w, h]; flowGoal = new GridPos(-999, -999); }
            if (goal != flowGoal)
            {
                flowGoal = goal;
                for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) flow[x, y] = int.MaxValue;
                flowQueue.Clear();
                if (grid.InBounds(goal)) { flow[goal.x, goal.y] = 0; flowQueue.Enqueue(goal); }
                while (flowQueue.Count > 0)
                {
                    var p = flowQueue.Dequeue();
                    int next = flow[p.x, p.y] + 1;
                    foreach (var d in DirectionExtensions.All)
                    {
                        var n = p + d.ToOffset();
                        if (!grid.InBounds(n) || flow[n.x, n.y] <= next) continue;
                        if (!grid.IsFloor(n) || grid.IsLava(n) || grid.IsCage(n)) continue;
                        flow[n.x, n.y] = next;
                        flowQueue.Enqueue(n);
                    }
                }
            }
            if (!grid.InBounds(g.pos)) return false;
            int here = flow[g.pos.x, g.pos.y];
            if (here == int.MaxValue) return false;
            GridPos best = g.pos;
            int bestD = here;
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (!grid.InBounds(n) || !CanStep(g.pos, n)) continue;
                int nd = flow[n.x, n.y];
                if (nd < bestD || (nd == bestD && nd < here && rng.Next(2) == 0)) { bestD = nd; best = n; }
            }
            if (best == g.pos) return false;
            MoveGuard(g, best, g.ally ? 1f / AllySpeed : 1f / GuardSpeed(g));
            return true;
        }

        private float WindTime => ChallengeBattle ? Mathf.Lerp(0.7f, 0.45f, level.score / 100f) : Mathf.Lerp(0.58f, 0.36f, level.score / 100f);
        private float BruteWindTime => ChallengeBattle ? Mathf.Lerp(0.95f, 0.7f, level.score / 100f) : Mathf.Lerp(0.82f, 0.58f, level.score / 100f);
        /// <summary>Health shares a guard's slam and an enforcer's smash take.</summary>
        private const float GuardHitShare = 0.2f, BruteHitShare = 0.45f;
        /// <summary>A fortress robot's slam: the easy ones barely sting, each rank a little more, an elite most of all.</summary>
        /// <summary>How terrifying the warlords look on this floor (1 early … 4 from level 180).</summary>
        private int Menace => Mathf.Clamp(1 + level.number / 60, 1, 4);

        private float ShareOf(Guard g) => g.share >= 0f ? g.share : g.blow > 0 ? g.blow * FoeHitScale / (float)Mathf.Max(1, FoiHp) : g.rank >= 99 ? 0.55f : g.brute ? BruteHitShare : g.rank == 0 ? 0.12f : g.rank > 0 ? Mathf.Min(0.3f, 0.16f + g.rank * 0.025f) : GuardHitShare;
        /// <summary>The monster's blows hurt more on later floors.</summary>
        private float MonsterHitShare => 1.3f * (RosterWorld >= 0 ? EnemyRoster.VanGDamage(RosterWorld, RosterFloor) * Mathf.Max(1f, FoeHitScale * 0.8f) / (float)Mathf.Max(1, FoiHp) : Mathf.Lerp(0.36f, 0.6f, level.score / 100f));

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
                if (Chebyshev(robot.Position, monsterPos) <= 1) { BlowFrom = At(monsterPos); Hit?.Invoke(robot.Position, MonsterHitShare); }
                if (CompanionTile.HasValue && Chebyshev(CompanionTile.Value, monsterPos) <= 1) CompanionHurt?.Invoke(2);
                monsterTimer = Mathf.Min(monsterTimer, 0.35f); // and straight into an attack
                return;
            }
            if (attackWind >= 0f)
            {
                attackWind += dt;
                // The wind-up shortens on later floors and when the monster is enraged.
                float wind = specialKind >= 0 ? 1.35f * Mathf.Lerp(1f, 0.8f, hard) : (attackIsRock ? 1.1f : 0.95f) * Mathf.Lerp(1f, 0.68f, hard) * (enraged ? 0.85f : 1f);
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
                if (specialKind >= 0) LandSpecial();
                rig.Shake(attackIsRock ? 0.4f : 0.7f);
                AudioManager.PlaySfx(Sfx.Impact, 1f, attackIsRock ? 1f : 0.6f);
                if (attackTiles.Contains(robot.Position)) { BlowFrom = At(monsterPos); Hit?.Invoke(robot.Position, MonsterHitShare); }
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
            if (TrySpecial(dt, hard, enraged)) return; // the world's own big attack, now and then
            monsterTimer -= dt * (enraged ? 1.35f : 1f) * monsterPace;
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
            g.atWall = false; // a cancelled rampart swing: its next blow is an ordinary one
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

        /// <summary>The nearest the two foi come in the duel (in tiles, diagonals as one): a tile always between them.</summary>
        public const int DuelGap = 3;

        public static int Chebyshev(GridPos a, GridPos b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
