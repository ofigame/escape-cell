using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The duel's squads: up to two soldiers of each of the six groups behind each foi. Left alone a soldier waits where
    /// it started (it only strikes back at whoever hits it); called back, it returns there. A group sent
    /// to attack (its tag at the bottom tapped) runs at the rival foi; enemy soldiers in its way are fought first, then
    /// on to the foi. A soldier struck by a foi strikes back at it. The player's soldiers are allies, the rival's are
    /// guards; in a live match each game moves its own soldiers and sends their state, and a blow on the other game's
    /// soldier (or foi) goes to that game (or the server).
    /// </summary>
    public partial class HuntSystem
    {
        /// <summary>The duel's squads are on the floor.</summary>
        public bool SquadsOn { get; private set; }

        /// <summary>Which groups are sent to attack: [0] the player's, [1] the rival's.</summary>
        private readonly bool[,] squadAttack = new bool[2, Army.Groups];

        /// <summary>A blow of one of mine on the live rival's soldier (its number there, the damage, a foi's blow).</summary>
        public event Action<int, int, bool> SquadHitRemote;
        /// <summary>A blow of one of mine on the live rival's foi (health points, 1% each).</summary>
        public event Action<int> SquadHitFoe;
        /// <summary>One of mine fell (its group): gone from the army's pool for an hour.</summary>
        public event Action<int> SquadFell;
        /// <summary>The rival (the AI) sent a group at the player: its group.</summary>
        public event Action<int> RivalSentGroup;

        private float botOrderAt;

        /// <summary>The AI sends no group for a while (the duel has just started: the soldiers wait to be called).</summary>
        public void DelayBotOrders(float seconds) => botOrderAt = Time.time + seconds;

        // A soldier's own strengths by kind: health, damage, tiles a second, reach (tiles), seconds between blows.
        private static readonly float[,] SquadStats =
        {
            { 6f, 2f, 3.6f, 1f, 0.8f },   // light swords
            { 10f, 2.2f, 2.8f, 1f, 1.0f }, // heavy swords
            { 6f, 2f, 3.4f, 2f, 0.9f },   // light spears
            { 10f, 2.2f, 2.7f, 2f, 1.1f }, // heavy spears
            { 4.5f, 1.8f, 3.4f, 4f, 1.1f }, // light bows
            { 7f, 2.4f, 2.8f, 4f, 1.3f },  // heavy bows
        };

        public static readonly Color MineColour = new Color(0.3f, 0.62f, 1f), TheirsColour = new Color(1f, 0.32f, 0.3f);

        /// <summary>
        /// A soldier into the duel near a tile: <paramref name="mine"/> the player's (an ally) or the rival's;
        /// <paramref name="id"/> its number in its squad (group × 2 + slot); <paramref name="remote"/> moved by the other
        /// game (the live rival's).
        /// </summary>
        public void AddSquadSoldier(bool mine, int id, GridPos near, Army.Stats stats, bool remote = false, float hpShare = 1f)
        {
            int group = Mathf.Clamp(id / SoldierPool.PerDuel, 0, Army.Groups - 1);
            var kind = Army.KindOf(group);
            var tile = FreeNear(near);
            if (!tile.HasValue) return;
            var model = SoldierModel.Build(transform, kind, Army.Heavy(kind) ? (mine ? MineColour : TheirsColour) : Color.Lerp(mine ? MineColour : TheirsColour, Color.white, 0.3f));
            model.transform.localScale = Vector3.one * Mathf.Max(0.4f, FoiHeight * 0.62f) * (Army.Heavy(kind) ? 1.12f : 1f) * Army.SizeFor(group, stats); // trained soldiers stand taller
            int hp = Mathf.Max(1, stats.hp);
            var s = new Guard
            {
                pos = tile.Value, hp = Mathf.Max(1, Mathf.RoundToInt(hp * Mathf.Clamp01(hpShare))), maxHp = hp, rank = mine ? -1 : 1, ally = mine,
                squad = true, squadId = id, remote = remote, power = stats.hit, armorShare = stats.armor, cooldown = (float)rng.NextDouble(), soldier = model, soldierKind = kind,
            };
            s.root = model.transform;
            s.root.SetParent(transform, true);
            s.home = tile.Value; // where it stands guard (it waits there until called)
            s.root.position = At(tile.Value);
            s.netTo = s.root.position;
            s.from = s.to = s.root.position;
            AddHealthBar(s);
            if (mine) { MaterialFactory.SetColors(s.barMat, MineColour, MineColour * 1.6f); allies.Add(s); }
            else { grid.SetOccupied(tile.Value, true); guards.Add(s); }
            SquadsOn = true;
        }

        /// <summary>Every squad soldier leaves the floor (the duel is over).</summary>
        public void ClearSquads()
        {
            foreach (var list in new[] { guards, allies })
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var s = list[i];
                    if (!s.squad) continue;
                    if (!s.dead && !s.ally) grid?.SetOccupied(s.pos, false);
                    if (s.root != null) Destroy(s.root.gameObject);
                    list.RemoveAt(i);
                }
            for (int t = 0; t < 2; t++) for (int g = 0; g < Army.Groups; g++) squadAttack[t, g] = false;
            SquadsOn = false;
        }

        /// <summary>A side's group sent to attack (true) or called back to guard (false).</summary>
        public void SetSquadOrder(bool mine, int group, bool attack)
        {
            if (group < 0 || group >= Army.Groups) return;
            squadAttack[mine ? 0 : 1, group] = attack;
            foreach (var s in Squad(mine)) if ((int)s.soldierKind == group) s.aim = null;
        }

        public bool SquadOrder(bool mine, int group) => group >= 0 && group < Army.Groups && squadAttack[mine ? 0 : 1, group];

        /// <summary>A side's soldiers standing in a group.</summary>
        public int SquadAlive(bool mine, int group)
        {
            int n = 0;
            foreach (var s in Squad(mine)) if ((int)s.soldierKind == group) n++;
            return n;
        }

        public int[] SquadAliveByGroup(bool mine)
        {
            var a = new int[Army.Groups];
            foreach (var s in Squad(mine)) a[(int)s.soldierKind]++;
            return a;
        }

        /// <summary>A side's squad soldiers still standing.</summary>
        private IEnumerable<Guard> Squad(bool mine)
        {
            var list = mine ? allies : guards;
            for (int i = 0; i < list.Count; i++) if (list[i].squad && !list[i].dead) yield return list[i];
        }

        /// <summary>After a knock-down: the soldiers left back in their places behind their foi (the duel starts again).</summary>
        public void RegroupSquads()
        {
            foreach (var mine in new[] { true, false })
                foreach (var s in Squad(mine))
                {
                    if (s.remote) continue;
                    var spot = FormationSpot(s);
                    var tile = FreeNear(spot);
                    if (!tile.HasValue) continue;
                    if (!s.ally) grid.SetOccupied(s.pos, false);
                    s.pos = tile.Value;
                    if (!s.ally) grid.SetOccupied(s.pos, true);
                    s.home = s.pos;
                    s.root.position = s.from = s.to = At(s.pos);
                    s.moveT = 1f;
                    s.windup = -1f;
                    s.aim = null;
                    s.revengeUntil = 0f;
                    s.SetRaise(0f);
                }
        }

        // ---------- Each frame ----------

        private GridPos? OwnFoi(Guard s) => s.ally ? robot.Position : challengeFoe != null && !challengeFoe.dead ? challengeFoe.pos : (GridPos?)null;
        private GridPos? EnemyFoi(Guard s) => s.ally ? (challengeFoe != null && !challengeFoe.dead ? challengeFoe.pos : (GridPos?)null) : robot.IsAlive ? robot.Position : (GridPos?)null;

        private static int SquadReach(Guard s) => Mathf.RoundToInt(SquadStats[(int)s.soldierKind, 3]);

        /// <summary>Where a soldier stands guard: behind its foi (away from the rival), its group's place in two rows.</summary>
        private GridPos FormationSpot(Guard s)
        {
            var own = OwnFoi(s) ?? s.pos;
            var enemy = EnemyFoi(s);
            Vector2 back = enemy.HasValue ? new Vector2(own.x - enemy.Value.x, own.y - enemy.Value.y) : new Vector2(0f, s.ally ? -1f : 1f);
            if (back.sqrMagnitude < 0.01f) back = new Vector2(0f, s.ally ? -1f : 1f);
            back.Normalize();
            var side = new Vector2(-back.y, back.x);
            // Rows of six behind the foi, in the groups' order: swords nearest, spears, then the archers furthest back.
            int i = s.squadId;
            float row = 2f + (i / 6) * 0.9f;
            float lat = i % 6 - 2.5f;
            var p = new Vector2(own.x, own.y) + back * row + side * lat;
            return new GridPos(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
        }

        private void UpdateSquad(Guard s, float dt)
        {
            UpdateHealthBar(s);
            if (s.flash > 0f) s.flash -= dt * 3f;
            if (s.remote) { FollowSquadNet(s, dt); return; }
            s.cooldown -= dt;
            if (s.moveT < 1f)
            {
                s.moveT = Mathf.Min(1f, s.moveT + dt / s.moveDur);
                s.root.position = Vector3.Lerp(s.from, s.to, s.moveT) + Vector3.up * Mathf.Abs(Mathf.Sin(s.moveT * Mathf.PI)) * 0.035f;
            }
            int team = s.ally ? 0 : 1;
            int group = (int)s.soldierKind;
            var enemyFoi = EnemyFoi(s);
            var ownFoi = OwnFoi(s);
            int reach = SquadReach(s);

            // A swing under way: it lands on what it was aimed at, if that is still in reach.
            if (s.windup >= 0f)
            {
                s.windup += dt;
                float k = s.windup / 0.35f;
                s.SetRaise(k * 1.3f);
                FaceTile(s, s.aimFoi && enemyFoi.HasValue ? enemyFoi.Value : s.aim != null ? s.aim.pos : s.pos, dt);
                if (k < 1f) return;
                s.windup = -1f;
                s.SetRaise(0f);
                float cd = SquadStats[group, 4] * (0.85f + (float)rng.NextDouble() * 0.3f);
                s.cooldown = s.aimFoi ? cd * 1.4f : cd;
                LandSquadBlow(s, enemyFoi, reach);
                return;
            }

            // What to go for: a foi that just struck it; else (sent to attack) a soldier in the way, then the foi; else (on
            // guard, behind its foi) nothing, unless a soldier just hit it: it strikes back.
            Guard foe = null;
            bool atFoi = false;
            if (Time.time < s.revengeUntil && s.hitBy == null && enemyFoi.HasValue && Chebyshev(enemyFoi.Value, s.pos) <= 5) atFoi = true;
            else if (squadAttack[team, group])
            {
                foe = NearestSquadFoe(s, s.pos, 2);
                if (foe == null && enemyFoi.HasValue) atFoi = true;
            }
            else if (Time.time < s.revengeUntil && s.hitBy != null && !s.hitBy.dead && Chebyshev(s.hitBy.pos, s.pos) <= 5)
                foe = s.hitBy; // on guard: it only strikes back at the soldier that hit it
            s.aim = foe;
            s.aimFoi = atFoi;
            GridPos goal;
            if (atFoi || foe != null)
            {
                var at = atFoi ? enemyFoi.Value : foe.pos;
                int d = Chebyshev(at, s.pos);
                FaceTile(s, at, dt);
                if (d <= reach)
                {
                    if (s.cooldown <= 0f && s.moveT >= 1f) s.windup = 0f;
                    if (reach > 1 || d <= 1) return; // in reach: hold (spears and bows keep their distance)
                }
                goal = at;
            }
            else
            {
                goal = s.home; // on guard: back to (and still on) the place it started from
                if (enemyFoi.HasValue) FaceTile(s, enemyFoi.Value, dt);
                if (goal == s.pos) return;
            }
            if (s.moveT < 1f) return;
            s.step -= dt;
            if (s.step > 0f) return;
            float speed = SquadStats[group, 2] * (squadAttack[team, group] ? 1.15f : 1f);
            if (!SquadStep(s, goal, 1f / speed)) s.step = 0.2f;
        }

        private void FaceTile(Guard s, GridPos at, float dt)
        {
            var to = At(at) - s.root.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) s.root.rotation = Quaternion.Slerp(s.root.rotation, Quaternion.LookRotation(to), dt * 10f);
        }

        /// <summary>The enemy soldier nearest a tile within <paramref name="within"/> tiles (or null).</summary>
        private Guard NearestSquadFoe(Guard s, GridPos from, int within)
        {
            Guard best = null;
            int bd = within + 1;
            foreach (var o in Squad(!s.ally))
            {
                int d = Chebyshev(o.pos, from);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        /// <summary>One step towards a tile (the free neighbour that brings it closest). False when boxed in.</summary>
        private bool SquadStep(Guard s, GridPos goal, float period)
        {
            GridPos best = s.pos;
            int bd = Chebyshev(goal, s.pos) * 4 + goal.Manhattan(s.pos);
            foreach (var d in DirectionExtensions.All)
            {
                var n = s.pos + d.ToOffset();
                if (!CanStep(s.pos, n) || (challengeFoe != null && !challengeFoe.dead && n == challengeFoe.pos)) continue;
                int nd = Chebyshev(goal, n) * 4 + goal.Manhattan(n);
                if (nd < bd) { bd = nd; best = n; }
            }
            if (best == s.pos) return false;
            MoveGuard(s, best, period);
            return true;
        }

        /// <summary>The damage of a soldier's blow (its kind and power; the fraction a chance of one more).</summary>
        private int SquadDamage(Guard s, Guard target)
        {
            float d = s.power * (0.85f + (float)rng.NextDouble() * 0.3f); // its own blow (the army's table)
            if (target != null && target.soldier != null)
            {
                int edge = Army.Edge(s.soldierKind, target.soldierKind);
                d *= edge > 0 ? 1.3f : edge < 0 ? 0.77f : 1f;
            }
            int whole = Mathf.FloorToInt(d);
            return Mathf.Max(1, whole + (rng.NextDouble() < d - whole ? 1 : 0));
        }

        private void LandSquadBlow(Guard s, GridPos? enemyFoi, int reach)
        {
            var colour = s.ally ? MineColour : TheirsColour;
            if (s.aimFoi)
            {
                if (!enemyFoi.HasValue || Chebyshev(enemyFoi.Value, s.pos) > reach + 1) return;
                if (IsArcher(s)) ArrowFx(s.root.position, At(enemyFoi.Value));
                else Shockwave.Create(At(enemyFoi.Value), 0.8f, colour);
                AudioManager.PlaySfx(Sfx.Impact, 0.4f, 1.2f);
                int dmg = SquadDamage(s, null);
                if (s.ally)
                {
                    if (FoeRemote) { SquadHitFoe?.Invoke(dmg); challengeFoe.flash = 1f; challengeFoe.Flash(); }
                    else SquadOnFoe(dmg);
                }
                else Hit?.Invoke(robot.Position, dmg * (1f - Shop.ArmorShare) / Mathf.Max(1, FoiHp)); // the AI's soldier on foi: its blow off foi's health points, less its armour
                return;
            }
            var t = s.aim;
            if (t == null || t.dead || Chebyshev(t.pos, s.pos) > reach + 1) return;
            if (IsArcher(s)) ArrowFx(s.root.position, At(t.pos));
            else Shockwave.Create(At(t.pos), 0.7f, colour);
            AudioManager.PlaySfx(Sfx.Impact, 0.35f, 1.3f);
            int damage = SquadDamage(s, t);
            if (t.remote) { t.flash = 1f; t.Flash(); SquadHitRemote?.Invoke(t.squadId, damage, false); }
            else HurtSquad(t, damage, s);
        }

        /// <summary>A soldier of the player's squad landed on the AI's foi.</summary>
        private void SquadOnFoe(int dmg)
        {
            var g = challengeFoe;
            if (g == null || g.dead) return;
            if (g.shield > 0) { HitShield(g, g.pos); return; }
            g.hp -= dmg;
            g.flash = 1f;
            g.Flash();
            fx.Burst(At(g.pos) + Vector3.up * 0.5f, MineColour, MineColour * 1.8f, 10, 3f);
            if (g.hp <= 0) KillGuard(g);
        }

        /// <summary>A blow on a squad soldier of this game (<paramref name="byFoi"/>: the foi that struck it, it strikes back).</summary>
        private void HurtSquad(Guard t, int damage, Guard by, bool foiBlow = false)
        {
            if (t.dead) return;
            t.hp -= Mathf.Max(1, Mathf.RoundToInt(damage * (1f - t.armorShare))); // its armour takes its share off
            t.flash = 1f;
            t.Flash();
            var c = t.ally ? MineColour : TheirsColour;
            fx.Burst(t.root.position + Vector3.up * 0.4f, c, c * 1.6f, 8, 2.5f);
            // It strikes back: at the foi that hit it, or the soldier (a live rival's soldier's blow: the nearest of them).
            t.revengeUntil = Time.time + 4f;
            t.hitBy = foiBlow ? null : by ?? NearestSquadFoe(t, t.pos, 4);
            if (t.hp > 0) return;
            if (t.ally) KillAlly(t);
            else KillGuard(t);
        }

        /// <summary>The other game says one of my soldiers took a blow (its number, the damage; a foi's blow: it strikes back).</summary>
        public void SquadStruckRemote(int id, int damage, bool byFoi)
        {
            foreach (var s in Squad(true))
                if (s.squadId == id) { HurtSquad(s, Mathf.Clamp(damage, 1, 60), null, byFoi); return; }
        }

        // ---------- The AI's orders ----------

        /// <summary>
        /// The AI's squad (no live rival): every few seconds it looks at the fight — groups of the player's coming at it
        /// get an answer (the groups that beat their kind), and now and then it sends one or two groups at foi; a group
        /// worn down is called back.
        /// </summary>
        private void UpdateBotOrders()
        {
            if (FoeRemote || !SquadsOn || Time.time < botOrderAt) return;
            botOrderAt = Time.time + 2.5f + (float)rng.NextDouble() * 3f;
            var mine = SquadAliveByGroup(true);
            var theirs = SquadAliveByGroup(false);
            var foeFoi = challengeFoe != null && !challengeFoe.dead ? challengeFoe.pos : (GridPos?)null;
            // The player's soldiers near its foi: answered by the groups with the edge over them.
            var threat = new List<int>();
            foreach (var s in Squad(true))
                if (foeFoi.HasValue && Chebyshev(s.pos, foeFoi.Value) <= 6 && !threat.Contains((int)s.soldierKind)) threat.Add((int)s.soldierKind);
            foreach (int g in threat)
            {
                int best = -1;
                for (int o = 0; o < Army.Groups; o++)
                    if (theirs[o] > 0 && !squadAttack[1, o] && Army.Edge(Army.KindOf(o), Army.KindOf(g)) > 0) { best = o; break; }
                if (best >= 0) { squadAttack[1, best] = true; RivalSentGroup?.Invoke(best); }
            }
            // An attack of its own now and then (bolder rivals more often).
            int sent = 0;
            for (int g = 0; g < Army.Groups; g++) if (squadAttack[1, g] && theirs[g] > 0) sent++;
            if (sent < 3 && rng.NextDouble() < 0.25 + 0.35 * FoeBoldness)
            {
                int pick = rng.Next(Army.Groups);
                for (int k = 0; k < Army.Groups; k++)
                {
                    int g = (pick + k) % Army.Groups;
                    if (theirs[g] > 0 && !squadAttack[1, g]) { squadAttack[1, g] = true; RivalSentGroup?.Invoke(g); break; }
                }
            }
        }

        /// <summary>
        /// One of the army's soldiers brought onto a floor: an ally of its group near foi (its kind's own strengths, its
        /// group's armour and attack), fighting the floor's robots like foi's other allies. Its fall costs the army's
        /// pool a soldier for an hour.
        /// </summary>
        public bool AddLevelSoldier(int group, GridPos near, Army.Stats stats)
        {
            var tile = FreeNear(near);
            if (!tile.HasValue) return false;
            var kind = Army.KindOf(group);
            var model = SoldierModel.Build(transform, kind, Army.Heavy(kind) ? MineColour : Color.Lerp(MineColour, Color.white, 0.3f));
            model.transform.localScale = Vector3.one * Mathf.Max(0.4f, FoiHeight * 0.62f) * (Army.Heavy(kind) ? 1.12f : 1f) * Army.SizeFor(group, stats); // trained soldiers stand taller
            // Its health in the floor robots' blows, its blow in theirs (robots take a few).
            int hp = Mathf.Max(1, stats.hp);
            int hit = Mathf.Max(1, Mathf.RoundToInt(stats.hit));
            var a = new Guard { pos = tile.Value, hp = hp, maxHp = hp, rank = -1, step = 0.2f + (float)rng.NextDouble() * 0.4f, ally = true, poolGroup = group, allyHit = hit, armorShare = stats.armor, soldier = model, soldierKind = kind };
            a.root = model.transform;
            a.root.SetParent(transform, true);
            a.root.position = At(tile.Value);
            a.from = a.to = a.root.position;
            AddHealthBar(a);
            MaterialFactory.SetColors(a.barMat, MineColour, MineColour * 1.6f);
            allies.Add(a);
            fx.Burst(At(tile.Value) + Vector3.up * 0.5f, MineColour, MineColour * 2f, 14, 3.5f);
            return true;
        }

        // ---------- Live: the squads' state ----------

        /// <summary>This game's squad for the other game: "id:x,z,h" per soldier standing (x, z in tenths, h its health
        /// in tenths, "*" after one swinging now), ";" between.</summary>
        public string SquadState()
        {
            var sb = new StringBuilder(160);
            bool first = true;
            foreach (var s in Squad(true))
            {
                if (!first) sb.Append(';');
                first = false;
                var p = s.root.position;
                sb.Append(s.squadId).Append(':')
                  .Append(Mathf.RoundToInt(p.x * 10f).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Mathf.RoundToInt(p.z * 10f).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Mathf.Clamp(Mathf.RoundToInt(10f * s.hp / Mathf.Max(1, s.maxHp)), 1, 10));
                if (s.windup >= 0f && s.windup < 0.2f) sb.Append('*');
            }
            return sb.ToString();
        }

        /// <summary>
        /// The other game's squad as it says now: each soldier moves after its word (a new one shows up, one no longer in
        /// the list has fallen). <paramref name="power"/>: the rival's soldiers' strength (their health bars).
        /// </summary>
        public void ApplySquadState(string body, Func<int, Army.Stats> stats, GridPos near)
        {
            if (body == null) return;
            var seen = new HashSet<int>();
            foreach (var part0 in body.Split(';'))
            {
                if (part0.Length == 0) continue;
                var part = part0;
                bool swinging = part.EndsWith("*");
                if (swinging) part = part.Substring(0, part.Length - 1);
                int colon = part.IndexOf(':');
                if (colon <= 0 || !int.TryParse(part.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;
                if (id < 0 || id >= Army.Groups * SoldierPool.PerDuel) continue;
                var v = part.Substring(colon + 1).Split(',');
                if (v.Length < 3 || !int.TryParse(v[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) || !int.TryParse(v[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)
                    || !int.TryParse(v[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h)) continue;
                seen.Add(id);
                Guard s = null;
                foreach (var o in guards) if (o.squad && o.remote && o.squadId == id) { s = o; break; }
                if (s == null)
                {
                    if (fallenRemote.Contains(id)) continue; // fell already: never back this duel
                    var tile = new GridPos(Mathf.RoundToInt(x / 10f), Mathf.RoundToInt(z / 10f));
                    AddSquadSoldier(false, id, grid.IsStandable(tile) ? tile : near, stats(id / SoldierPool.PerDuel), remote: true, hpShare: h / 10f);
                    continue;
                }
                if (s.dead) continue;
                s.netTo = new Vector3(x / 10f, s.root.position.y, z / 10f);
                s.netSeen = true;
                int hp = Mathf.Clamp(Mathf.CeilToInt(s.maxHp * h / 10f), 1, s.maxHp);
                if (hp < s.hp) { s.flash = 1f; s.Flash(); }
                s.hp = hp;
                if (swinging && s.windup < 0f) { s.windup = 0f; s.SetRaise(1f); }
            }
            // The ones gone from its list have fallen.
            foreach (var o in guards)
            {
                if (!o.squad || !o.remote || o.dead || seen.Contains(o.squadId) || !o.netSeen) continue;
                fallenRemote.Add(o.squadId);
                KillGuard(o);
            }
        }

        private readonly HashSet<int> fallenRemote = new HashSet<int>();

        /// <summary>A new duel: the live rival's soldiers may all show up again.</summary>
        public void ResetRemoteSquad() => fallenRemote.Clear();

        private void FollowSquadNet(Guard s, float dt)
        {
            if (s.windup >= 0f)
            {
                s.windup += dt;
                if (s.windup > 0.3f) { s.windup = -1f; s.SetRaise(0f); }
            }
            if (!s.netSeen) { s.netSeen = true; return; }
            var p = s.root.position;
            var to = new Vector3(s.netTo.x, p.y, s.netTo.z);
            float gap = Vector3.Distance(p, to);
            if (gap > 0.01f)
            {
                var next = gap > 3f ? to : Vector3.MoveTowards(p, to, Mathf.Max(gap * 10f, 2.5f) * dt);
                var step = next - p;
                if (step.sqrMagnitude > 0.000001f) s.root.rotation = Quaternion.Slerp(s.root.rotation, Quaternion.LookRotation(step), dt * 10f);
                s.root.position = next;
            }
            var tile = new GridPos(Mathf.RoundToInt(s.root.position.x), Mathf.RoundToInt(s.root.position.z));
            if (tile != s.pos && grid.InBounds(tile))
            {
                grid.SetOccupied(s.pos, false);
                s.pos = tile;
                grid.SetOccupied(tile, true);
            }
        }
    }
}
