using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// foi's allies in the battle arenas and on their roads: robots set free from the roads' chests. Each keeps near
    /// foi until an enemy robot (or vanG) comes within sight, then goes for it and slams it; the enemy robots fight back,
    /// going for whoever is nearer, foi or an ally. Allies are kept apart from the enemies (they never count towards
    /// the floor's robots, foi's blows and shots pass them by); their number is carried from road to arena and on.
    /// </summary>
    public partial class HuntSystem
    {
        private readonly List<Guard> allies = new List<Guard>();
        private static readonly Color AllyColour = new Color(0.3f, 0.85f, 1f);

        /// <summary>Allies still standing.</summary>
        public int AlliesAlive
        {
            get { int n = 0; foreach (var a in allies) if (!a.dead) n++; return n; }
        }

        /// <summary>An ally a little way from foi (a free tile near <paramref name="near"/>); false when there is no room.</summary>
        public bool AddAlly(GridPos near, int hp)
        {
            GridPos p = near;
            bool found = false;
            for (int r = 0; r <= 5 && !found; r++)
                for (int dx = -r; dx <= r && !found; dx++)
                    for (int dy = -r; dy <= r && !found; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var q = near + new GridPos(dx, dy);
                        if (r >= 2 && grid.IsStandable(q) && !grid.IsOccupied(q) && !AllyAt(q) && !grid.IsGap(q)) { p = q; found = true; } // not right on foi
                    }
            if (!found) return false;
            var a = new Guard { pos = p, hp = Mathf.Max(1, hp), maxHp = Mathf.Max(1, hp), step = 0.3f, rank = -1, ally = true };
            a.root = new GameObject("Ally").transform;
            a.root.SetParent(transform, false);
            a.bot = GuardBot.Build(a.root, AllyColour, Mathf.Max(0, world));
            a.bot.transform.localScale = Vector3.one * 0.62f * EnemyScale;
            if (FoiHeight > 0.05f)
            {
                float h = HeightOf(a.root);
                if (h > 0.05f) a.bot.transform.localScale *= FoiHeight / h;
                a.bot.transform.localScale = Vector3.Scale(a.bot.transform.localScale, new Vector3(1.12f, 1f, 1.12f));
            }
            // A glowing ring at its feet: one of foi's.
            Shapes.Primitive(PrimitiveType.Cylinder, "AllyRing", a.root, new Vector3(0f, 0.03f, 0f), new Vector3(0.85f, 0.01f, 0.85f), MaterialFactory.Create(AllyColour, AllyColour * 2.2f));
            AddHealthBar(a);
            MaterialFactory.SetColors(a.barMat, AllyColour, AllyColour * 1.6f);
            a.from = a.to = At(p);
            a.root.position = a.to;
            allies.Add(a);
            fx.Burst(At(p) + Vector3.up * 0.6f, AllyColour, AllyColour * 2f, 18, 4f);
            return true;
        }

        private void StopAllies()
        {
            foreach (var a in allies)
            {
                if (a.root != null) Destroy(a.root.gameObject);
            }
            allies.Clear();
        }

        /// <summary>Tiles a second an ally walks: foi's own running pace, so it never falls behind.</summary>
        private const float AllySpeed = 5f;

        /// <summary>Is an ally standing on (or stepping onto) a tile?</summary>
        private bool AllyAt(GridPos p)
        {
            foreach (var a in allies) if (!a.dead && a.pos == p) return true;
            return false;
        }

        /// <summary>How slowly allies swing (1 = their own pace).</summary>
        public float AllyPace = 1f;

        /// <summary>Damage the rival foi's blow does to one of the player's duel soldiers (its weapon's).</summary>
        public int FoeSoldierDamage = 3;

        /// <summary>Damage each ally's blow does (later roads' allies hit harder).</summary>
        public int AllyDamage = 1;

        private void UpdateAllies(float dt)
        {
            // By index: an ally's kill can free a chest whose allies join the list right now.
            for (int i = 0; i < allies.Count; i++) if (!allies[i].dead) UpdateAlly(allies[i], dt);
        }

        private void UpdateAlly(Guard a, float dt)
        {
            if (a.squad) { UpdateSquad(a, dt); return; }
            if (a.bar != null && Camera.main != null) a.bar.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
            if (a.barFill != null) a.barFill.localScale = new Vector3(Mathf.Max(0.001f, a.hp / (float)a.maxHp), 1f, 1f);
            if (a.flash > 0f) a.flash -= dt * 3f;
            a.cooldown -= dt;
            if (a.moveT < 1f)
            {
                a.moveT = Mathf.Min(1f, a.moveT + dt / a.moveDur);
                a.root.position = Vector3.Lerp(a.from, a.to, a.moveT) + Vector3.up * Mathf.Abs(Mathf.Sin(a.moveT * Mathf.PI)) * 0.035f;
            }
            // The nearest enemy in sight (vanG too, once awake).
            Guard foe = null;
            int foeD = 9;
            foreach (var g in guards)
            {
                if (g.dead) continue;
                int d = Chebyshev(g.pos, a.pos);
                if (d < foeD) { foeD = d; foe = g; }
            }
            bool atMonster = false;
            if (MonsterUp && MonsterAwake && Chebyshev(monsterPos, a.pos) < foeD) { atMonster = true; foeD = Chebyshev(monsterPos, a.pos); }
            var look = atMonster ? monsterPos : foe != null ? foe.pos : robot.Position;
            var toward = GridView.ToWorld(look) - a.root.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.01f) a.root.rotation = Quaternion.Slerp(a.root.rotation, Quaternion.LookRotation(toward), dt * 8f);

            if (a.windup >= 0f)
            {
                a.windup += dt;
                float k = a.windup / 0.4f;
                a.SetRaise(k * 1.3f);
                if (k < 1f) return;
                a.windup = -1f;
                a.cooldown = (0.6f + (float)rng.NextDouble() * 0.3f) * AllyPace;
                a.SetRaise(0f);
                if (IsArcher(a)) ArrowFx(a.root.position, At(a.target));
                else Shockwave.Create(At(a.target), 0.9f, AllyColour);
                AudioManager.PlaySfx(Sfx.Impact, 0.45f, 1.1f);
                if (MonsterUp && MonsterAwake && a.target == monsterPos) { HitMonster(AllyDamage); return; }
                foreach (var g in guards)
                    if (!g.dead && (g.pos == a.target || (g.moveT < 1f && g.prevPos == a.target))) { AllyHit(g, a); break; }
                return;
            }
            if (a.moveT < 1f) return;
            int reach = ReachOf(a);
            // An archer with a foe at its elbow: a step back first.
            if (IsArcher(a) && foe != null && foeD <= 1 && a.step <= 0f && StepAway(a, foe.pos, 1f / AllySpeed)) { a.step = 0.4f; return; }
            if ((foe != null || atMonster) && foeD <= reach && a.cooldown <= 0f)
            {
                a.windup = 0f;
                a.target = look;
                return;
            }
            a.step -= dt;
            if (a.step > 0f) return;
            GridPos goal;
            if ((foe != null || atMonster) && foeD <= Mathf.Max(1, reach - 1)) return; // in reach: hold (archers keep their distance)
            if (foe != null && !atMonster && foeD > 1) goal = AllyFlank(a, foe.pos); // round the foe on its own side, not in a queue
            else if (foe != null || atMonster) goal = look;
            else if (Chebyshev(a.pos, robot.Position) > 2) goal = robot.Position; // keep up with foi (as quick as it)
            else return;
            int dist = goal.Manhattan(a.pos);
            GridPos best = a.pos;
            int bestD = dist;
            foreach (var d in DirectionExtensions.All)
            {
                var n = a.pos + d.ToOffset();
                if (!CanStep(a.pos, n)) continue;
                int nd = goal.Manhattan(n);
                if (nd < bestD || (nd == bestD && rng.Next(2) == 0 && nd < dist)) { bestD = nd; best = n; }
            }
            // Round the bends of a road: the street map towards foi when the plain step is stuck.
            if (best == a.pos && level.hardAtOnce > 0 && !(foe != null || atMonster)) { FlowStep(a, goal); return; }
            if (best != a.pos) MoveGuard(a, best, 1f / AllySpeed);
        }

        /// <summary>The tile beside a foe on the ally's side of it (allies fan out round their foe), or the foe's tile.</summary>
        private GridPos AllyFlank(Guard a, GridPos foe)
        {
            if (a.flank < 0) a.flank = flankCount++ % FlankAngles.Length;
            float baseA = Mathf.Atan2(a.pos.y - foe.y, a.pos.x - foe.x);
            float ang = baseA + FlankAngles[a.flank] * 0.6f * Mathf.Deg2Rad;
            var p = foe + new GridPos(Mathf.RoundToInt(Mathf.Cos(ang)), Mathf.RoundToInt(Mathf.Sin(ang)));
            return grid.IsStandable(p) ? p : foe;
        }

        /// <summary>An ally's blow on an enemy robot: shields and health as for foi's blows (no knock-back).</summary>
        private void AllyHit(Guard g, Guard by)
        {
            RaiseAlarm(g);
            if (g.shield > 0) { HitShield(g, g.pos); return; }
            g.hp -= AllyDamage + Edge(by, g);
            g.flash = 1f;
            g.Flash();
            fx.Burst(At(g.pos) + Vector3.up * 0.4f, AllyColour, AllyColour * 1.8f, 16, 4f);
            if (g.hp <= 0) { AllyKill = true; KillGuard(g); AllyKill = false; }
        }

        /// <summary>The robot being finished right now fell to an ally (no kill streak for foi).</summary>
        public bool AllyKill { get; private set; }

        /// <summary>The ally nearest a tile (and how far), or null.</summary>
        private Guard NearestAlly(GridPos from, out int dist)
        {
            Guard best = null;
            dist = int.MaxValue;
            foreach (var a in allies)
            {
                if (a.dead) continue;
                int d = Chebyshev(a.pos, from);
                if (d < dist) { dist = d; best = a; }
            }
            return best;
        }

        /// <summary>An enemy's slam landed on these tiles: allies there take blows (a warlord's more).</summary>
        private void HurtAllies(Guard by, List<GridPos> area)
        {
            int dmg = by.rank >= 99 ? 3 : by.brute ? 2 : 1;
            foreach (var a in allies)
            {
                if (a.dead || !area.Contains(a.pos)) continue;
                if (a.squad) { HurtSquad(a, by == challengeFoe ? FoeSoldierDamage : dmg + Edge(by, a), by, foiBlow: by == challengeFoe); continue; }
                a.hp -= dmg + Edge(by, a);
                a.Flash();
                if (a.hp <= 0) KillAlly(a);
            }
        }

        private void KillAlly(Guard a)
        {
            a.dead = true;
            if (a.squad) SquadFell?.Invoke((int)a.soldierKind);
            fx.Burst(At(a.pos) + Vector3.up * 0.5f, AllyColour, AllyColour * 2f, 30, 5f);
            Shockwave.Create(At(a.pos), 1.2f, AllyColour);
            AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1.3f);
            Destroy(a.root.gameObject);
        }
    }
}
