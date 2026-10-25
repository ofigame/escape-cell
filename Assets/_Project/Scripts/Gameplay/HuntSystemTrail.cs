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
    /// The open world's roads (see GameManagerTrail): no vanG at the end, robots placed in bands by the clearings, and
    /// the chests' guardians — poison spitters. A spitter keeps close to its chest; when foi comes within seven tiles it
    /// winds up (the tiles round foi glow red) and lobs a green glob that leaves a poison puddle there for a while. When
    /// the last guardian of a chest falls, the chest is free to open.
    /// </summary>
    public partial class HuntSystem
    {
        /// <summary>No monster on this floor (the roads): clearing the robots never raises vanG.</summary>
        public bool NoMonster;

        /// <summary>The battle arenas and their roads: enemy robots wear red (foi's allies are blue).</summary>
        public bool RedFoes;

        /// <summary>A spitter's glob landed: the tiles of its poison puddle.</summary>
        public event Action<List<GridPos>> Poisoned;
        /// <summary>The last guardian of a chest fell: the chest's number.</summary>
        public event Action<int> ChestFreed;

        private static readonly Color PoisonColour = new Color(0.45f, 0.95f, 0.3f);

        /// <summary>A robot of a road's band, or (spitter) a chest's guardian kept near its chest.</summary>
        public void AddTrailGuard(GridPos p, bool spitter, GridPos home, int chestId, int rank = 0)
        {
            if (!grid.IsStandable(p) || grid.IsOccupied(p)) return;
            AddGuard(p, brute: spitter, rank: spitter ? 97 : rank);
            var g = guards[guards.Count - 1];
            g.spitter = spitter;
            g.home = home;
            g.chestId = chestId;
            if (spitter) { g.hp = g.maxHp = Mathf.Max(g.maxHp * 2, Mathf.RoundToInt((level.robotHp * 3 + 3) * HpBase)); g.cooldown = 1.5f; }
            Total++;
        }

        /// <summary>
        /// A chest's vanG: the monster's model, fighting like a warlord (an area smash) and keeping by its chest. Its
        /// health grows road by road.
        /// </summary>
        public void AddChestBoss(GridPos p, GridPos home, int chestId, int hp)
        {
            if (CreateMonster == null || !grid.IsStandable(p) || grid.IsOccupied(p)) return;
            hp = Mathf.Max(1, Mathf.RoundToInt(hp * HpBase)); // (in health points)
            var g = new Guard { pos = p, brute = true, rank = 99, hp = hp, maxHp = hp, home = home, chestId = chestId, step = 0.5f, cooldown = 1.5f };
            g.root = new GameObject("ChestVanG").transform;
            g.root.SetParent(transform, false);
            g.root.position = At(p);
            g.mon = CreateMonster(p, hp);
            g.mon.transform.SetParent(g.root, true);
            g.mon.transform.localPosition = Vector3.zero;
            g.from = g.to = At(p);
            grid.SetOccupied(p, true);
            guards.Add(g);
            Total++;
        }

        private Guard challengeFoe;

        /// <summary>foi's blow landed on the rival foi (the duel's combos and power bar count it).</summary>
        public event Action ChallengeFoeStruck;
        /// <summary>The rival foi's blow landed on foi.</summary>
        public event Action FoiStruckByFoe;

        private float foeStunUntil;

        /// <summary>The rival foi reels for a while (a heavy blow, an uppercut, a combo broken): no steps, no swings.</summary>
        public void StunFoe(float seconds)
        {
            var g = challengeFoe;
            if (g == null || g.dead) return;
            foeStunUntil = Mathf.Max(foeStunUntil, Time.time + seconds);
            g.windup = -1f;
            ClearTelegraph(g);
            foeCombo = 0;
        }

        /// <summary>The rival foi's reeling ends now (it can act again at once).</summary>
        public void EndFoeStun() => foeStunUntil = 0f;

        /// <summary>The rival foi's model (for its stand-in in the big moments), or null.</summary>
        public Transform ChallengeFoeModel => challengeFoe != null && !challengeFoe.dead ? challengeFoe.model : null;

        /// <summary>The rival foi lands on a tile (after a leap): there at once, its old tile free.</summary>
        public void PlaceFoe(GridPos p)
        {
            var g = challengeFoe;
            if (g == null || g.dead || !grid.InBounds(p)) return;
            grid.SetOccupied(g.pos, false);
            g.pos = g.prevPos = p;
            grid.SetOccupied(p, true);
            g.root.position = g.from = g.to = At(p);
            g.moveT = 1f;
        }

        /// <summary>The rival foi knocked back to a tile (sliding there over <paramref name="seconds"/>; stun it first, or it walks on).</summary>
        public System.Collections.IEnumerator ShoveFoe(GridPos p, float seconds)
        {
            var g = challengeFoe;
            if (g == null || g.dead || !grid.InBounds(p)) yield break;
            grid.SetOccupied(g.pos, false);
            grid.SetOccupied(p, true); // (taken at once: nothing steps in meanwhile)
            var from = g.root.position; var to = At(p);
            for (float e = 0f; e < seconds && g.root != null && !g.dead; e += Time.deltaTime)
            {
                float k = 1f - (1f - e / seconds) * (1f - e / seconds); // fast, then slowing
                g.root.position = Vector3.Lerp(from, to, k);
                yield return null;
            }
            if (g.dead) yield break;
            g.pos = g.prevPos = p;
            g.root.position = g.from = g.to = to;
            g.moveT = 1f;
        }

        /// <summary>Where the rival foi stands as a tile (null once fallen).</summary>
        public GridPos? ChallengeFoeTile => challengeFoe != null && !challengeFoe.dead ? challengeFoe.pos : (GridPos?)null;

        /// <summary>The share of foi's health one of the rival foi's blows takes.</summary>
        public float ChallengeFoeShare => challengeFoe != null ? challengeFoe.share : 0f;

        /// <summary>
        /// The challenge's rival foi for the duel: <paramref name="look"/> (a copy of foi in the rival's colours) fighting
        /// like a warlord (an area smash), taking <paramref name="hp"/> blows and each of its blows taking
        /// <paramref name="hitShare"/> of foi's health.
        /// </summary>
        public void AddChallengeFoe(GridPos p, int hp, float hitShare, Transform look, HeldWeapon held = null, int shieldHits = 0)
        {
            // Never no rival (that would be a duel won by default): the nearest free tile if this one is taken.
            if (!grid.IsStandable(p) || grid.IsOccupied(p)) p = FreeNear(p) ?? p;
            ResetFoeState();
            var g = new Guard { pos = p, brute = false, rank = 98, hp = hp, maxHp = hp, step = 0.4f, cooldown = 1.0f, share = hitShare };
            g.root = new GameObject("RivalFoi").transform;
            g.root.SetParent(transform, false);
            g.root.position = At(p);
            look.SetParent(g.root, false);
            look.localPosition = Vector3.zero;
            look.localRotation = Quaternion.identity; // it faces where its root faces: at foi
            g.model = look;
            g.modelScale = look.localScale;
            g.held = held;
            AddHealthBar(g);
            g.from = g.to = At(p);
            grid.SetOccupied(p, true);
            guards.Add(g);
            Total++;
            challengeFoe = g;
            // Its own shield (from its profile): the first blows only crack the bubble.
            if (shieldHits > 0) { AddShield(g); g.shield = shieldHits; }
        }

        // ---------- A live rival foi (another player's, moved by its own game) ----------

        /// <summary>The rival foi is another player's: no AI, it goes where its game says, its health comes from the server.</summary>
        public bool FoeRemote;

        /// <summary>foi's blow landed on the live rival (the server is told; it decides the health).</summary>
        public event Action RemoteFoeStruck;

        private Vector3 remoteTo;
        private float remoteYaw;
        private int remoteSwings = -1;
        private bool remoteSeen;

        /// <summary>Where the live rival's game says it is now (the latest only), its facing and how many blows it has swung.</summary>
        public void RemoteFoeState(Vector3 pos, float yaw, int swings)
        {
            remoteTo = pos;
            remoteYaw = yaw;
            var g = challengeFoe;
            if (g == null || g.dead) return;
            if (!remoteSeen) { remoteSeen = true; g.root.position = pos; remoteSwings = swings; }
            if (swings > remoteSwings && remoteSwings >= 0)
            {
                // It swung: the blow shows here (the damage, if any, comes from the server).
                if (g.held != null) g.held.SwingLate(); // (known only once swung: from its contact on, no wind-up)
                g.SetRaise(1f);
                foeRaiseUntil = Time.time + 0.15f;
            }
            remoteSwings = swings;
        }

        private float foeRaiseUntil;

        /// <summary>The live rival's health as the server has it (a share of its full health).</summary>
        public void SetFoeHealth(float share)
        {
            var g = challengeFoe;
            if (g == null || g.dead) return;
            int hp = Mathf.Clamp(Mathf.CeilToInt(g.maxHp * share - 0.001f), 0, g.maxHp);
            if (hp < g.hp) { g.flash = 1f; g.Flash(); }
            g.hp = Mathf.Max(1, hp); // it falls only when the server says so
        }

        private void UpdateRemoteFoe(Guard g, float dt)
        {
            if (!remoteSeen) return;
            // Smoothly after the latest word (states come some 15 times a second).
            var p = g.root.position;
            var to = new Vector3(remoteTo.x, p.y, remoteTo.z);
            float gap = Vector3.Distance(p, to);
            g.root.position = gap > 3f ? to : Vector3.MoveTowards(p, to, Mathf.Max(gap * 12f, 2f) * dt);
            g.root.rotation = Quaternion.Slerp(g.root.rotation, Quaternion.Euler(0f, remoteYaw, 0f), dt * 14f);
            if (Time.time > foeRaiseUntil) g.SetRaise(0f);
            var tile = new GridPos(Mathf.RoundToInt(g.root.position.x), Mathf.RoundToInt(g.root.position.z));
            if (tile != g.pos && grid.InBounds(tile))
            {
                grid.SetOccupied(g.pos, false);
                g.pos = tile;
                grid.SetOccupied(tile, true);
            }
        }

        /// <summary>The rival's tactics start fresh each duel (nothing carried over from the last one).</summary>
        private void ResetFoeState()
        {
            foeStunUntil = 0f;
            foeCombo = 0;
            remoteSeen = false;
            remoteSwings = -1;
            foeCounter = false;
            foeHurtStreak = 0;
            foeHurtAt = -9f;
            foeFleeUntil = foeBackOffUntil = foeDodgeAt = 0f;
        }

        /// <summary>Where the rival foi stands (null once it has fallen).</summary>
        public Vector3? ChallengeFoeAt => challengeFoe != null && !challengeFoe.dead ? challengeFoe.root.position : (Vector3?)null;

        /// <summary>The rival foi's health as a share of its whole (0 when there is none).</summary>
        public float ChallengeFoeHealth => challengeFoe != null && !challengeFoe.dead ? challengeFoe.hp / (float)Mathf.Max(1, challengeFoe.maxHp) : 0f;

        /// <summary>The rival foi uses a heart: this share of its health back.</summary>
        public void HealFoe(float share)
        {
            var g = challengeFoe;
            if (g == null || g.dead) return;
            g.hp = Mathf.Min(g.maxHp, g.hp + Mathf.RoundToInt(g.maxHp * share));
            fx.Burst(g.root.position + Vector3.up * 0.6f, new Color(1f, 0.45f, 0.55f), new Color(2.4f, 0.6f, 0.8f), 20, 4f);
            AudioManager.PlaySfx(Sfx.Shield, 0.7f, 1.6f);
        }

        /// <summary>The rival foi turned (flat) along <paramref name="dir"/>: its root, its model straight with it.</summary>
        public void TurnFoe(Vector3 dir)
        {
            var g = challengeFoe;
            dir.y = 0f;
            if (g == null || g.root == null || dir.sqrMagnitude < 1e-4f) return;
            g.root.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            if (g.model != null) g.model.localRotation = Quaternion.identity;
        }

        /// <summary>The rival foi raises its shield: a bubble that takes <paramref name="hits"/> blows (until EndFoeShield).</summary>
        public void ShieldFoe(int hits)
        {
            var g = challengeFoe;
            if (g == null || g.dead) return;
            if (g.bubble == null) AddShield(g);
            g.shield = Mathf.Max(1, hits);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.1f);
        }

        /// <summary>The rival foi's shield is spent (its time is up).</summary>
        public void EndFoeShield()
        {
            var g = challengeFoe;
            if (g == null) return;
            g.shield = 0;
            if (g.bubble != null) { Destroy(g.bubble.gameObject); g.bubble = null; }
        }

        /// <summary>The rival foi has fallen (or there is none).</summary>
        public bool ChallengeFoeDown => challengeFoe == null || challengeFoe.dead;

        /// <summary>A free tile near <paramref name="p"/> (no robot, no ally, not foi's), or null.</summary>
        private GridPos? FreeNear(GridPos p)
        {
            for (int r = 0; r <= 8; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var q = p + new GridPos(dx, dy);
                        if (grid.IsStandable(q) && !grid.IsOccupied(q) && !AllyAt(q) && q != robot.Position) return q;
                    }
            return null;
        }

        /// <summary>The rival foi leaves the ring (between the duel's rounds): gone at once, its tile free.</summary>
        public void RemoveChallengeFoe()
        {
            var g = challengeFoe;
            challengeFoe = null;
            ResetFoeState();
            if (g == null) return;
            if (!g.dead)
            {
                g.dead = true;
                ClearTelegraph(g);
                grid.SetOccupied(g.pos, false);
            }
            engaged.Remove(g);
            if (g.root != null) Destroy(g.root.gameObject);
        }

        public void ClearChallengeFoe() { challengeFoe = null; ChallengeBattle = false; UseStreetMap = false; ResetFoeState(); }

        /// <summary>The rival foi's state, for tests.</summary>
        public string FoeDebug => challengeFoe == null ? "none" : $"pos {challengeFoe.pos} foi {robot.Position} hp {challengeFoe.hp} wind {challengeFoe.windup:0.0} cd {challengeFoe.cooldown:0.0} step {challengeFoe.step:0.0} moveT {challengeFoe.moveT:0.0} engaged {engaged.Contains(challengeFoe)} combo {foeCombo} back {Time.time < foeBackOffUntil} flee {Time.time < foeFleeUntil}";

        // ---------- Wings: robots split up and close round their target ----------

        /// <summary>The side the crowd is on, from foi (radians); the wings fan out round it.</summary>
        private float crowdAngle;
        private int flankCount;
        // The wings in the order they are handed out (neighbours go to opposite sides): straight on, left, right, wider…
        private static readonly float[] FlankAngles = { 0f, -60f, 60f, -110f, 110f, -30f, 30f, -150f, 150f, 180f };

        private Vector2 FlankDir(Guard g)
        {
            if (g.flank < 0) g.flank = flankCount++ % FlankAngles.Length;
            float a = crowdAngle + FlankAngles[g.flank] * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>The tile <paramref name="dist"/> from <paramref name="target"/> on the robot's wing (or the nearest standable one).</summary>
        private GridPos FlankSpot(Guard g, GridPos target, int dist)
        {
            var dir = FlankDir(g);
            for (int r = dist; r >= 1; r--)
            {
                var p = target + new GridPos(Mathf.RoundToInt(dir.x * r), Mathf.RoundToInt(dir.y * r));
                if (grid.IsStandable(p) && !grid.IsGap(p)) return p;
            }
            return target;
        }

        /// <summary>
        /// Where a robot heads for foi: far off, a point a few tiles out on its wing (so the crowd splits and comes from
        /// several sides); near, the tile beside foi on its side (so they end up round foi, not stacked in a line).
        /// </summary>
        private GridPos FlankGoal(Guard g, int near, int reach)
        {
            var target = robot.Position;
            if (LaneX.HasValue && g == challengeFoe) return target; // (the lane duel: straight along the line)
            if (near > 6) return FlankSpot(g, target, 4);
            if (near > reach) return FlankSpot(g, target, Mathf.Max(1, reach));
            return target;
        }

        /// <summary>One step towards a tile (whichever free neighbour brings it closer). False when none does.</summary>
        private bool StepTowards(Guard g, GridPos goal, float period)
        {
            GridPos best = g.pos;
            int bd = Chebyshev(goal, g.pos);
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (!CanStep(g.pos, n)) continue;
                int nd = Chebyshev(goal, n);
                // Never closer to foi than the ring while waiting.
                if (Chebyshev(robot.Position, n) < WaitRing - 1) continue;
                if (nd < bd) { bd = nd; best = n; }
            }
            if (best == g.pos) return false;
            MoveGuard(g, best, period);
            return true;
        }

        // ---------- The challenge battle's ways of fighting ----------

        /// <summary>A challenge soldier's extra blow on one its kind beats (swords over archers, spears over swords, archers over spears).</summary>
        private static int Edge(Guard by, Guard target) =>
            by.soldier != null && target.soldier != null && Army.Edge(by.soldierKind, target.soldierKind) > 0 ? 1 : 0;

        private static bool IsArcher(Guard g) => g.soldier != null && Army.Family(g.soldierKind) == 2;

        /// <summary>How far a robot strikes from: archers 4 tiles, spears 2, swords 1 (others as ever).</summary>
        private int ReachOf(Guard g)
        {
            if (g == challengeFoe) return FoeReach; // (the duel: from the gap kept, a spear one tile more)
            if (g.soldier == null) return g.gunner ? GunReach : AttackReach;
            int fam = Army.Family(g.soldierKind);
            return fam == 2 ? 4 : fam == 1 ? 2 : 1;
        }

        /// <summary>One step away from a tile (the free neighbour farthest from it). False when boxed in.</summary>
        private bool StepAway(Guard g, GridPos from, float period)
        {
            GridPos best = g.pos;
            int bd = Chebyshev(from, g.pos);
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (!CanStep(g.pos, n)) continue;
                int nd = Chebyshev(from, n);
                if (nd > bd || (nd == bd && rng.Next(3) == 0 && best == g.pos)) { bd = nd; best = n; }
            }
            if (best == g.pos) return false;
            MoveGuard(g, best, period);
            return true;
        }

        private float foeFleeUntil, foeBackOffUntil, foeDodgeAt;

        /// <summary>How bold the rival's foi is (its profile): bold ones press on, careful ones step back and dodge more.</summary>
        public float FoeBoldness = 0.5f;
        /// <summary>A computer rival's humanity: the share of its blows that miss, its wind-up against the usual (its
        /// reaction), how readily it runs off when hurt.</summary>
        public float FoeWhiff, FoeReactScale = 1f, FoeFlee = 0.35f;
        /// <summary>The rival's moves: untouchable until, mirroring until (the blow goes back at foi: <see cref="FoeMirrored"/>),
        /// and its sturdy spare life this round.</summary>
        public float FoeInvulnUntil, FoeMirrorUntil;
        /// <summary>A giant level: the monster takes its blows from behind (the front barely scratches it).</summary>
        public bool BackOnly;
        /// <summary>The world's monster's stages (level 10: 2, the big tests 3): crossing a share of its health it changes
        /// (<see cref="MonsterStage"/>), bigger, faster to attack.</summary>
        public int MonsterStages = 1;
        public System.Action<int> MonsterStage;
        private int monsterStage;
        private float monsterPace = 1f;
        public System.Action<Vector3> BackHint;
        public bool FoeSturdy;
        public System.Action<int> FoeMirrored;

        /// <summary>The rival's health points at full.</summary>
        public int ChallengeFoeMaxHp => challengeFoe != null ? challengeFoe.maxHp : 1;

        /// <summary>A little health back for the rival, without the heart's show (the vampire).</summary>
        public void HealFoeQuiet(float share)
        {
            var g = challengeFoe;
            if (g == null || g.dead || share <= 0f) return;
            g.hp = Mathf.Min(g.maxHp, g.hp + Mathf.Max(1, Mathf.RoundToInt(g.maxHp * share)));
        }

        /// <summary>
        /// The rival foi's own sense, before its usual fighting: badly hurt and foi close, it falls back behind its
        /// soldiers for a while; right after a blow it steps back out of reach now and then (hit and run). True when it
        /// moved this frame.
        /// </summary>
        private bool FoeTactics(Guard g, int near)
        {
            if (g.windup >= 0f || g.moveT < 1f) return false;
            // Caught out in the red: back into the ring first, quickly.
            if (Unsafe != null && Unsafe(g.pos))
            {
                if ((g.step -= Time.deltaTime) > 0f) return true;
                g.step = 0f;
                if (StepInside(g)) return true;
            }
            if (g.hp < g.maxHp * (0.15f + 0.35f * FoeFlee) && near <= 3 && Time.time > foeFleeUntil + 6f) foeFleeUntil = Time.time + 1.2f; // (a short breather, then back at it)
            // Between blows, face to face with foi: now and then a step to the side (a dodge), more for a careful one.
            if (near <= DuelGap && g.cooldown > 0.2f && Time.time > foeDodgeAt && g.step <= 0f)
            {
                foeDodgeAt = Time.time + 0.6f;
                if (rng.NextDouble() < 0.12 + 0.2 * (1f - FoeBoldness) && Sidestep(g)) return true;
            }
            bool flee = Time.time < foeFleeUntil || Time.time < foeBackOffUntil;
            if (!flee) return false;
            if ((g.step -= Time.deltaTime) > 0f) return true;
            g.step = 0f;
            return StepAway(g, robot.Position, 1f / (GuardSpeed(g) * 1.9f)); // a quick getaway
        }

        /// <summary>A step toward the middle of the duel's safe ring.</summary>
        private bool StepInside(Guard g)
        {
            GridPos best = g.pos;
            float bd = Vector2.Distance(new Vector2(g.pos.x, g.pos.y), new Vector2(SafeCentre.x, SafeCentre.y));
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (!CanStep(g.pos, n)) continue;
                float nd = Vector2.Distance(new Vector2(n.x, n.y), new Vector2(SafeCentre.x, SafeCentre.y));
                if (nd < bd) { bd = nd; best = n; }
            }
            if (best == g.pos) return false;
            MoveGuard(g, best, 1f / (GuardSpeed(g) * 1.6f));
            return true;
        }

        /// <summary>How well the AI rival fights back (0..1, by its rank and boldness): how often it parries.</summary>
        public float FoeSkill = 0.5f;

        /// <summary>The duel's defence for the AI rival: asked as foi's blow lands (its damage), it tells how the blow met
        /// the rival's defence; a blocked blow takes <see cref="FoeGuardThrough"/> of its damage. Null: the old parries.</summary>
        public Func<int, Combat.DefenseOutcome> FoeGuard;
        public float FoeGuardThrough = 1f;
        /// <summary>foi's blow was blocked by the rival (no combo, no bar).</summary>
        public event Action ChallengeFoeBlocked;
        /// <summary>The rival holds its shield up: it stands its ground, turned to foi, and starts no blow.</summary>
        public bool FoeHolding;
        /// <summary>
        /// The floors' perfect guard: the robot whose blow it met (the one nearest <paramref name="from"/>) staggers — its next
        /// blow waits <paramref name="seconds"/>, it flashes and reels.
        /// </summary>
        public void StaggerAttacker(Vector3 from, float seconds)
        {
            Guard best = null;
            float bd = 2.5f * 2.5f;
            foreach (var g in guards)
            {
                if (g.dead || g.root == null) continue;
                float d = (g.root.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = g; }
            }
            if (best == null) return;
            best.cooldown = Mathf.Max(best.cooldown, seconds);
            best.windup = -1f;
            best.flash = 1f;
            best.Flash();
            if (best.model != null) { var sk = best.model.GetComponentInChildren<SkinnedHero>(); if (sk != null) sk.Stagger(robot.transform.position, 0.7f); }
        }

        /// <summary>Seconds until the rival's blow lands (its wind-up), or -1 when it is not winding up.</summary>
        public float FoeWindLeft { get; private set; } = -1f;

        /// <summary>The AI rival parried a blow of foi's (foi's combo is broken; it strikes straight back).</summary>
        public event Action FoeParried;

        /// <summary>foi's blows landing on the rival in a row (a pause, a blow taken or a parry ends the run).</summary>
        private int foiRun;
        private float foiRunAt = -9f, parryAt = -9f;

        private void FoiRanOn()
        {
            foiRun = Time.time - foiRunAt < 1.6f ? foiRun + 1 : 1;
            foiRunAt = Time.time;
        }

        /// <summary>
        /// Whether the AI rival parries this blow: seldom the first, ever more likely the longer foi's run of blows
        /// (a skilled one sooner); never while it reels, and not twice in a breath.
        /// </summary>
        private bool FoeParries()
        {
            if (FoeRemote || Time.time < foeStunUntil || Time.time - parryAt < 0.7f) return false;
            int run = Time.time - foiRunAt < 1.6f ? foiRun : 0;
            float chance = (0.12f + 0.18f * run) * (0.6f + FoeSkill);
            return rng.NextDouble() < Mathf.Min(0.75f, chance);
        }

        /// <summary>The parry: the blow turned aside in a flash, and the rival strikes straight back at foi.</summary>
        private void Parry(Guard g, GridPos p)
        {
            foiRun = 0;
            parryAt = Time.time;
            var at = At(p) + Vector3.up * 0.9f;
            fx.Burst(at, new Color(0.75f, 0.92f, 1f), new Color(1.8f, 2.2f, 2.6f), 26, 5f);
            Shockwave.Create(At(p), 1.6f, new Color(0.6f, 0.85f, 1f));
            AudioManager.PlaySfx(Sfx.Shield, 1f, 1.25f);
            if (g.model != null) { var hold = g.model.GetComponentInChildren<ShieldHold>(); if (hold != null) hold.Raise(); }
            foeCounter = true;
            foeCombo = 0;
            g.cooldown = 0f;
            g.windup = 0f;
            g.target = robot.Position;
            g.area.Clear();
            g.area.Add(g.target);
            FoeParried?.Invoke();
        }

        /// <summary>Blows left in the rival foi's current flurry.</summary>
        private int foeCombo;

        /// <summary>The rival foi's wind-up: quicker inside a flurry.</summary>
        private float FoeWindScale(Guard g) => g != challengeFoe ? 1f : (foeCounter ? 0.28f : foeCombo > 0 ? 0.36f : 0.5f) * FoeReactScale;

        private float foeHurtAt = -9f;
        private int foeHurtStreak;
        private bool foeCounter;

        /// <summary>
        /// The rival foi took a blow. A second one close behind and it hits straight back (a quick counter at foi) and
        /// then gets away.
        /// </summary>
        private void FoeHurt(Guard g)
        {
            foeHurtStreak = Time.time - foeHurtAt < 1.6f ? foeHurtStreak + 1 : 1;
            foeHurtAt = Time.time;
            if (g.windup >= 0f) return; // already swinging: that blow lands
            if (foeHurtStreak < 2 || Chebyshev(robot.Position, g.pos) > ReachOf(g)) return;
            foeHurtStreak = 0;
            foeCounter = true;
            foeCombo = 0;
            g.cooldown = 0f;
            g.windup = 0f;
            g.target = robot.Position;
            g.area.Clear();
            g.area.Add(g.target);
        }

        /// <summary>
        /// The rival foi just struck. It attacks in flurries — two to four quick blows (more for a bold one), each
        /// following hard on the last — and when a flurry is spent it backs off a moment (a careful one more often).
        /// </summary>
        private void FoeStruck(Guard g)
        {
            if (g != challengeFoe) return;
            if (foeCounter)
            {
                // The counter landed (or missed): away, quickly.
                foeCounter = false;
                g.cooldown = 0.6f;
                foeBackOffUntil = Time.time + 1.2f;
                return;
            }
            if (foeCombo > 0)
            {
                foeCombo--;
                if (foeCombo > 0) { g.cooldown = 0.08f; return; }
            }
            else
            {
                foeCombo = 1 + rng.Next(3, 5) + (rng.NextDouble() < FoeBoldness ? 1 : 0); // this blow opens a flurry of 4-6
                g.cooldown = 0.08f;
                return;
            }
            g.cooldown = 0.3f + (float)rng.NextDouble() * 0.25f;
            if (rng.NextDouble() < 0.08 + 0.15 * (1f - FoeBoldness)) foeBackOffUntil = Time.time + 0.5f;
        }

        /// <summary>A step to a free neighbour as far from foi as it stands now (round foi, not away): false when there is none.</summary>
        private bool Sidestep(Guard g)
        {
            int near = Chebyshev(robot.Position, g.pos);
            var options = new List<GridPos>();
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (CanStep(g.pos, n) && Chebyshev(robot.Position, n) == near) options.Add(n);
            }
            if (options.Count == 0) return false;
            MoveGuard(g, options[rng.Next(options.Count)], 1f / (GuardSpeed(g) * 1.9f));
            return true;
        }

        private Material arrowMat;

        /// <summary>An arrow's flight from a shooter to where it lands (a quick arc).</summary>
        private void ArrowFx(Vector3 from, Vector3 to)
        {
            if (arrowMat == null) arrowMat = MaterialFactory.Create(new Color(0.95f, 0.85f, 0.6f), new Color(0.6f, 0.45f, 0.1f));
            var arrow = Shapes.Rounded("Arrow", transform, from + Vector3.up * 0.5f, new Vector3(0.04f, 0.04f, 0.4f), 0.01f, arrowMat).transform;
            StartCoroutine(Fly(arrow, from + Vector3.up * 0.5f, to + Vector3.up * 0.3f));
            AudioManager.PlaySfx(Sfx.Hop, 0.2f, 2f);
        }

        private System.Collections.IEnumerator Fly(Transform arrow, Vector3 a, Vector3 b)
        {
            for (float t = 0f; t < 1f && arrow != null; t += Time.deltaTime / 0.25f)
            {
                var p = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
                var dir = p - arrow.position;
                if (dir.sqrMagnitude > 0.0001f) arrow.rotation = Quaternion.LookRotation(dir);
                arrow.position = p;
                yield return null;
            }
            if (arrow != null) Destroy(arrow.gameObject);
        }

        /// <summary>A spitter's turn: back towards its chest if it strayed, else face foi and lob poison when in range.</summary>
        private void UpdateSpitter(Guard g, float dt)
        {
            var toFoi = robot.transform.position - g.root.position;
            toFoi.y = 0f;
            if (toFoi.sqrMagnitude > 0.01f) g.root.rotation = Quaternion.Slerp(g.root.rotation, Quaternion.LookRotation(toFoi), dt * 6f);
            int near = Chebyshev(robot.Position, g.pos);
            if (g.windup >= 0f)
            {
                g.windup += dt;
                float k = g.windup / 0.9f;
                g.SetRaise(k * 1.3f);
                foreach (var t in g.area) view.SetWarning(t, 0.4f + 0.6f * k);
                if (k < 1f) return;
                g.windup = -1f;
                g.cooldown = 2.4f;
                g.SetRaise(0f);
                foreach (var t in g.area) view.SetWarning(t, 0f);
                var tiles = new List<GridPos>(g.area);
                g.area.Clear();
                ThrownRock.Create(g.root.position + Vector3.up * 1.6f, At(g.target), 0.45f, PoisonColour);
                AudioPlay(0.8f);
                StartCoroutine(Splash(tiles, 0.45f));
                return;
            }
            if (g.cooldown <= 0f && near <= 7 && !PlayerAway)
            {
                // Wind up: foi's tile and a couple round it.
                g.windup = 0f;
                g.target = robot.Position;
                g.area.Clear();
                g.area.Add(g.target);
                foreach (var d in DirectionExtensions.All)
                    if (rng.Next(2) == 0 && grid.IsFloor(g.target + d.ToOffset())) g.area.Add(g.target + d.ToOffset());
                return;
            }
            // Keep near the chest: step back when more than three tiles off.
            if (g.moveT < 1f) return;
            g.step -= dt;
            if (g.step > 0f || Chebyshev(g.pos, g.home) <= 3) return;
            g.step = level.robotStep * 1.2f;
            GridPos best = g.pos;
            int bestD = g.home.Manhattan(g.pos);
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (!CanStep(g.pos, n)) continue;
                int nd = g.home.Manhattan(n);
                if (nd < bestD) { bestD = nd; best = n; }
            }
            if (best != g.pos) MoveGuard(g, best, level.robotStep);
        }

        private System.Collections.IEnumerator Splash(List<GridPos> tiles, float delay)
        {
            int gen = beginGen;
            yield return new WaitForSeconds(delay);
            if (!running || gen != beginGen) yield break; // the level it was spat in is over
            foreach (var t in tiles) fx.Burst(At(t) + Vector3.up * 0.2f, PoisonColour, PoisonColour * 2f, 10, 3f);
            Poisoned?.Invoke(tiles);
        }

        private void AudioPlay(float pitch) => Audio.AudioManager.PlaySfx(Audio.Sfx.Hop, 0.7f, pitch);

        /// <summary>A guard fell: if it was a chest's last guardian, the chest is free.</summary>
        private void CheckChestFreed(Guard dead)
        {
            if (dead.chestId < 0) return;
            foreach (var o in guards) if (!o.dead && o.chestId == dead.chestId) return;
            ChestFreed?.Invoke(dead.chestId);
        }
    }
}
