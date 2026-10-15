using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The Army Battle: two armies of six groups (thirty soldiers each) in the arena. Each side fights with one group at a
    /// time, its active group; the others stand by, faded, and can neither attack nor be attacked. The player moves the
    /// active group with the stick (like foi) and taps where it should attack: blades run at the foes there, archers shoot
    /// from range. The rival's side is played by the AI, or by another player (live): then each game moves its own
    /// soldiers and sends their state (<see cref="StateBody"/>); a blow on the other side's soldier is sent to its game,
    /// which applies it (<see cref="ApplyHit"/>).
    /// Team 0 is always this game's own army (the south end, left on screen), team 1 the rival's.
    /// </summary>
    public class ArmyBattle : MonoBehaviour
    {
        private class Unit
        {
            public int team, group, slot, index; // index: 0..179 in its team (group * 30 + slot)
            public GroupKind kind;
            public Transform root;
            public SoldierModel model;
            public float hp, maxHp, dmg, speed, range, cooldown, cd, swing = -1f;
            public bool dead, faded;
            public Unit target;
            public Vector3 netTo; // a live rival's soldier: where its game last said it was
            public bool netSeen;
            public bool Archer => Army.Family(kind) == 2;
        }

        private const int Cols = 6;
        private const float GapX = 0.46f, GapZ = 0.5f, AttackRadius = 4.5f;

        private readonly List<Unit> units = new List<Unit>();
        private readonly Vector3[,] centre = new Vector3[2, Army.Groups];
        private readonly int[] active = { 0, 0 };
        private readonly Vector3?[] attackAt = new Vector3?[2];
        private readonly TMPro.TextMeshPro[,] labels = new TMPro.TextMeshPro[2, Army.Groups];
        private readonly List<(Transform arrow, Vector3 from, Unit target, float dmg, float t)> arrows = new List<(Transform, Vector3, Unit, float, float)>();
        private Material arrowMat;
        private FxSystem fx;
        private float unitHeight = 0.7f;
        private Vector3 boundsMin, boundsMax;
        private Vector3 stickVel;
        private bool running;

        /// <summary>The rival's side is another player's (live): its soldiers move by its state, blows go to its game.</summary>
        public bool RemoteRival { get; private set; }

        /// <summary>The AI's boldness for the rival (AI only): how soon it charges.</summary>
        public float RivalBoldness = 0.5f;

        /// <summary>A blow of one of mine on the live rival's soldier (its index in its army, the damage).</summary>
        public event Action<int, float> HitRemote;

        public static readonly Color Mine = new Color(0.3f, 0.62f, 1f), Theirs = new Color(1f, 0.32f, 0.3f);

        public void Init(FxSystem fxSystem) => fx = fxSystem;

        /// <summary>The group a side fights with now.</summary>
        public int Active(int team) => active[team];

        /// <summary>Soldiers standing in a side's group, and in its whole army.</summary>
        public int Alive(int team, int group) { int n = 0; foreach (var u in units) if (!u.dead && u.team == team && u.group == group) n++; return n; }
        public int Alive(int team) { int n = 0; foreach (var u in units) if (!u.dead && u.team == team) n++; return n; }

        public int[] AliveByGroup(int team)
        {
            var a = new int[Army.Groups];
            foreach (var u in units) if (!u.dead && u.team == team) a[u.group]++;
            return a;
        }

        /// <summary>Where a side's group stands (the middle of its soldiers standing, else where it was set).</summary>
        public Vector3 GroupCentre(int team, int group)
        {
            Vector3 c = Vector3.zero; int n = 0;
            foreach (var u in units) if (!u.dead && u.team == team && u.group == group) { c += u.root.position; n++; }
            return n > 0 ? c / n : centre[team, group];
        }

        /// <summary>The middle of the fight (both active groups), for the camera.</summary>
        public Vector3 FightCentre => (GroupCentre(0, active[0]) + GroupCentre(1, active[1])) * 0.5f;

        // ---------- Setting up ----------

        /// <summary>
        /// Both armies in their places (mine along the south end, the rival's along the north, two lines of three), full
        /// strength, group 0 active on both sides.
        /// </summary>
        public void Begin(Vector3[] mySpots, Vector3[] theirSpots, float[] myPower, float[] theirPower, float soldierHeight, bool remoteRival, Vector3 min, Vector3 max, bool mineNorth = false)
        {
            Clear();
            // Which way each army faces: the one at the south end looks north (+z), the other south.
            front[0] = mineNorth ? -1f : 1f;
            front[1] = -front[0];
            unitHeight = soldierHeight;
            RemoteRival = remoteRival;
            boundsMin = min;
            boundsMax = max;
            for (int team = 0; team < 2; team++)
                for (int g = 0; g < Army.Groups; g++)
                {
                    var spot = team == 0 ? mySpots[g] : theirSpots[g];
                    centre[team, g] = spot;
                    for (int i = 0; i < Army.Soldiers; i++)
                    {
                        var u = MakeUnit(team, g, i, team == 0 ? myPower[g] : theirPower[g], spot + Offset(team, i));
                        units.Add(u);
                    }
                    MakeLabel(team, g);
                }
            active[0] = active[1] = 0;
            attackAt[0] = attackAt[1] = null;
            RefreshFades();
            running = false; // standing until the match starts (Go)
        }

        /// <summary>The match starts: the armies may move and fight.</summary>
        public void Go() => running = true;

        private readonly float[] front = { 1f, -1f };

        public void Clear()
        {
            foreach (var u in units) if (u.root != null) Destroy(u.root.gameObject);
            units.Clear();
            foreach (var a in arrows) if (a.arrow != null) Destroy(a.arrow.gameObject);
            arrows.Clear();
            for (int t = 0; t < 2; t++) for (int g = 0; g < Army.Groups; g++) { if (labels[t, g] != null) Destroy(labels[t, g].gameObject); labels[t, g] = null; }
            running = false;
        }

        /// <summary>Stops everything (a match is over): no more moving or blows.</summary>
        public void Halt() { running = false; stickVel = Vector3.zero; attackAt[0] = attackAt[1] = null; foreach (var u in units) { u.target = null; if (u.model != null) u.model.SetRaise(0f); } }

        /// <summary>A soldier's place in its group's block (the front row towards the middle).</summary>
        private Vector3 Offset(int team, int i)
        {
            int rows = Mathf.CeilToInt(Army.Soldiers / (float)Cols);
            float side = -front[team]; // the front row towards the middle
            return new Vector3((i % Cols - (Cols - 1) * 0.5f) * GapX, 0f, (i / Cols - (rows - 1) * 0.5f) * GapZ * side);
        }

        private Unit MakeUnit(int team, int group, int slot, float power, Vector3 at)
        {
            var kind = Army.KindOf(group);
            bool heavy = Army.Heavy(kind);
            var u = new Unit { team = team, group = group, slot = slot, index = group * Army.Soldiers + slot, kind = kind };
            var colour = team == 0 ? Mine : Theirs;
            u.model = SoldierModel.Build(transform, kind, heavy ? colour : Color.Lerp(colour, Color.white, 0.3f));
            u.root = u.model.transform;
            u.root.position = at;
            u.netTo = at;
            u.root.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, front[team]));
            u.root.localScale = Vector3.one * unitHeight * (heavy ? 1.12f : 1f);
            // The kind's own strengths (as in the old clashes), times the group's development.
            switch (kind)
            {
                case GroupKind.LightSword: u.maxHp = 6f; u.dmg = 2f; u.speed = 3.2f; u.range = 0.75f; u.cooldown = 0.8f; break;
                case GroupKind.HeavySword: u.maxHp = 10f; u.dmg = 2.2f; u.speed = 2.3f; u.range = 0.75f; u.cooldown = 1.0f; break;
                case GroupKind.LightSpear: u.maxHp = 6f; u.dmg = 2f; u.speed = 3.0f; u.range = 1.15f; u.cooldown = 0.9f; break;
                case GroupKind.HeavySpear: u.maxHp = 10f; u.dmg = 2.2f; u.speed = 2.2f; u.range = 1.15f; u.cooldown = 1.1f; break;
                case GroupKind.LightBow: u.maxHp = 4.5f; u.dmg = 1.8f; u.speed = 3.1f; u.range = 4.5f; u.cooldown = 1.1f; break;
                default: u.maxHp = 7f; u.dmg = 2.4f; u.speed = 2.3f; u.range = 4.8f; u.cooldown = 1.3f; break;
            }
            u.maxHp *= power;
            u.dmg *= power;
            u.hp = u.maxHp;
            u.cd = UnityEngine.Random.value * u.cooldown;
            return u;
        }

        private void MakeLabel(int team, int g)
        {
            var t = new GameObject("GroupLabel").AddComponent<TMPro.TextMeshPro>();
            t.transform.SetParent(transform, false);
            t.font = SquashBot.UI.UiFactory.Font;
            t.textPreprocessor = new SquashBot.UI.RtlTextPreprocessor(t);
            t.fontSize = 3f;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.color = new Color(1f, 0.85f, 0.3f);
            t.outlineWidth = 0.3f;
            t.outlineColor = new Color32(20, 12, 0, 255);
            t.rectTransform.sizeDelta = new Vector2(6f, 1.4f);
            labels[team, g] = t;
        }

        // ---------- Orders ----------

        /// <summary>A side fights with another group now (one with soldiers standing).</summary>
        public void SetActive(int team, int group)
        {
            if (group < 0 || group >= Army.Groups || Alive(team, group) == 0 || active[team] == group) return;
            active[team] = group;
            attackAt[team] = null;
            foreach (var u in units) if (u.team == team) u.target = null;
            RefreshFades();
        }

        /// <summary>The stick on this game's active group: a speed and way on the ground (zero: stand).</summary>
        public void Move(Vector3 velocity)
        {
            stickVel = velocity;
            if (velocity.sqrMagnitude > 0.01f) attackAt[0] = null; // moving off: the attack order is dropped
        }

        /// <summary>A tap: this game's active group attacks there.</summary>
        public void AttackAt(Vector3 point) { attackAt[0] = point; foreach (var u in units) if (u.team == 0) u.target = null; }

        private void RefreshFades()
        {
            foreach (var u in units)
            {
                bool faded = u.group != active[u.team];
                if (u.faded == faded || u.model == null) continue;
                u.faded = faded;
                u.model.SetFaded(faded);
            }
        }

        // ---------- Each frame ----------

        private float aiThink;

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateLabels();
            UpdateArrows(dt);
            if (!running || dt <= 0f) return;
            // A side whose active group is gone fights on with the next one standing.
            for (int t = 0; t < 2; t++)
            {
                if (t == 1 && RemoteRival) continue;
                if (Alive(t, active[t]) > 0) continue;
                for (int k = 1; k <= Army.Groups; k++)
                {
                    int g = (active[t] + k) % Army.Groups;
                    if (Alive(t, g) > 0) { SetActive(t, g); break; }
                }
            }
            if (!RemoteRival) UpdateAi(dt);
            // The active groups' blocks: moved by the stick (mine) or the AI (theirs).
            for (int t = 0; t < 2; t++)
            {
                if (t == 1 && RemoteRival) continue;
                var v = t == 0 ? stickVel : aiVel;
                if (v.sqrMagnitude < 0.0001f) continue;
                var c = centre[t, active[t]] + v * dt;
                c.x = Mathf.Clamp(c.x, boundsMin.x + 1.2f, boundsMax.x - 1.2f);
                c.z = Mathf.Clamp(c.z, boundsMin.z + 1.2f, boundsMax.z - 1.2f);
                centre[t, active[t]] = c;
            }
            foreach (var u in units)
            {
                if (u.dead) continue;
                if (u.team == 1 && RemoteRival) { FollowNet(u, dt); continue; }
                UpdateUnit(u, dt);
            }
        }

        private void UpdateUnit(Unit u, float dt)
        {
            u.cd -= dt;
            if (u.swing >= 0f)
            {
                u.swing += dt / 0.25f;
                u.model.SetRaise(u.swing < 0.5f ? u.swing * 2.2f : Mathf.Max(0f, 2.2f - u.swing * 2.2f));
                if (u.swing >= 1f) { u.swing = -1f; u.model.SetRaise(0f); }
            }
            bool isActive = u.group == active[u.team];
            var home = centre[u.team, u.group] + Offset(u.team, u.slot);
            if (!isActive) return; // standing by: neither moving nor fighting
            int foeTeam = 1 - u.team;
            var order = attackAt[u.team];
            if (order.HasValue)
            {
                if (u.target == null || u.target.dead || u.target.group != active[foeTeam]) u.target = Choose(u, order.Value);
                if (u.target == null && !AnyFoeNear(foeTeam, order.Value)) attackAt[u.team] = null; // nothing left there: back to the block
            }
            else u.target = null;
            if (u.target != null)
            {
                var to = u.target.root.position - u.root.position;
                to.y = 0f;
                float d = to.magnitude;
                if (d > u.range) { Walk(u, u.target.root.position - to.normalized * (u.range * 0.85f), dt); return; }
                Face(u, u.target.root.position);
                if (u.cd <= 0f) Strike(u, u.target);
                return;
            }
            Walk(u, home, dt);
        }

        /// <summary>The foe a soldier goes for: from the rival's active group, near the attack point, the nearest to it.</summary>
        private Unit Choose(Unit u, Vector3 order)
        {
            Unit best = null;
            float bestD = float.MaxValue;
            int foeTeam = 1 - u.team, foeGroup = active[foeTeam];
            foreach (var o in units)
            {
                if (o.dead || o.team != foeTeam || o.group != foeGroup) continue;
                var po = o.root.position;
                if ((po - order).sqrMagnitude > AttackRadius * AttackRadius) continue;
                float d = (po - u.root.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }

        private bool AnyFoeNear(int foeTeam, Vector3 at)
        {
            foreach (var o in units)
                if (!o.dead && o.team == foeTeam && o.group == active[foeTeam] && (o.root.position - at).sqrMagnitude <= AttackRadius * AttackRadius) return true;
            return false;
        }

        private void Walk(Unit u, Vector3 goal, float dt)
        {
            var p = u.root.position;
            var to = goal - p;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0025f) return;
            var step = Vector3.ClampMagnitude(to, u.speed * dt);
            p += step;
            p.x = Mathf.Clamp(p.x, boundsMin.x, boundsMax.x);
            p.z = Mathf.Clamp(p.z, boundsMin.z, boundsMax.z);
            u.root.position = p;
            if (step.sqrMagnitude > 0.000001f) u.root.rotation = Quaternion.Slerp(u.root.rotation, Quaternion.LookRotation(step), dt * 10f);
        }

        private static void Face(Unit u, Vector3 at)
        {
            var to = at - u.root.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f) u.root.rotation = Quaternion.Slerp(u.root.rotation, Quaternion.LookRotation(to), 0.35f);
        }

        private void Strike(Unit u, Unit target)
        {
            u.cd = u.cooldown * (0.85f + UnityEngine.Random.value * 0.3f);
            u.swing = 0f;
            float dmg = u.dmg * (0.85f + UnityEngine.Random.value * 0.3f);
            int edge = Army.Edge(u.kind, target.kind);
            if (edge > 0) dmg *= 1.3f;
            else if (edge < 0) dmg *= 0.77f;
            // From behind (the target faces away): it lands harder.
            var from = u.root.position - target.root.position; from.y = 0f;
            if (from.sqrMagnitude > 0.0001f && Vector3.Dot(target.root.forward, from.normalized) < -0.3f) dmg *= 1.25f;
            if (u.Archer)
            {
                if (arrowMat == null) arrowMat = MaterialFactory.Create(new Color(0.95f, 0.85f, 0.6f), new Color(0.6f, 0.45f, 0.1f));
                var arrow = Shapes.Rounded("Arrow", transform, u.root.position + Vector3.up * unitHeight * 0.6f, new Vector3(0.04f, 0.04f, 0.4f), 0.01f, arrowMat).transform;
                arrows.Add((arrow, arrow.position, target, dmg, 0f));
                AudioManager.PlaySfx(Sfx.Hop, 0.2f, 2f);
            }
            else Land(target, dmg);
        }

        /// <summary>A blow reaching a soldier: on mine (or the AI's), its health; on a live rival's, a word to its game.</summary>
        private void Land(Unit target, float dmg)
        {
            if (target.dead || target.faded) return;
            if (target.team == 1 && RemoteRival)
            {
                target.model.Flash();
                HitRemote?.Invoke(target.index, dmg);
                return;
            }
            Hurt(target, dmg);
        }

        private void Hurt(Unit target, float dmg)
        {
            target.hp -= dmg;
            target.model.Flash();
            if (fx != null) fx.Burst(target.root.position + Vector3.up * unitHeight * 0.6f, target.team == 1 ? Mine : Theirs, (target.team == 1 ? Mine : Theirs) * 1.6f, 4, 2.5f);
            if (target.hp > 0f) return;
            Die(target);
        }

        private void Die(Unit u)
        {
            if (u.dead) return;
            u.dead = true;
            if (fx != null) fx.Burst(u.root.position + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.3f), new Color(2f, 0.8f, 0.3f), 10, 4f);
            AudioManager.PlaySfx(Sfx.Impact, 0.3f, 1.3f + UnityEngine.Random.value * 0.3f);
            u.root.gameObject.SetActive(false);
        }

        private void UpdateArrows(float dt)
        {
            for (int i = arrows.Count - 1; i >= 0; i--)
            {
                var a = arrows[i];
                if (a.arrow == null) { arrows.RemoveAt(i); continue; }
                a.t += dt / 0.45f;
                var end = a.target.root != null ? a.target.root.position + Vector3.up * unitHeight * 0.5f : a.from;
                var p = Vector3.Lerp(a.from, end, a.t) + Vector3.up * Mathf.Sin(Mathf.Clamp01(a.t) * Mathf.PI) * 0.8f;
                var dir = p - a.arrow.position;
                if (dir.sqrMagnitude > 0.0001f) a.arrow.rotation = Quaternion.LookRotation(dir);
                a.arrow.position = p;
                arrows[i] = a;
                if (a.t < 1f) continue;
                Destroy(a.arrow.gameObject);
                arrows.RemoveAt(i);
                if (running && a.dmg > 0f) Land(a.target, a.dmg);
            }
        }

        private void UpdateLabels()
        {
            var cam = Camera.main;
            for (int t = 0; t < 2; t++)
                for (int g = 0; g < Army.Groups; g++)
                {
                    var l = labels[t, g];
                    if (l == null) continue;
                    int n = Alive(t, g);
                    l.gameObject.SetActive(n > 0);
                    if (n == 0) continue;
                    l.text = Loc.T("army.kind." + Army.KindOf(g)) + "  " + n;
                    l.alpha = g == active[t] ? 1f : 0.5f;
                    l.transform.position = GroupCentre(t, g) + Vector3.up * (unitHeight * 1.6f + 0.25f);
                    if (cam != null) l.transform.rotation = Quaternion.LookRotation(l.transform.position - cam.transform.position);
                }
        }

        // ---------- The AI's side ----------

        private Vector3 aiVel;

        private void UpdateAi(float dt)
        {
            aiThink -= dt;
            int mine = active[0];
            if (aiThink <= 0f)
            {
                aiThink = 1.2f + UnityEngine.Random.value * 1.5f;
                // The group with the edge over the player's active one (else the strongest standing).
                int best = active[1];
                float bestScore = float.MinValue;
                for (int g = 0; g < Army.Groups; g++)
                {
                    int n = Alive(1, g);
                    if (n == 0) continue;
                    float score = n + Army.Edge(Army.KindOf(g), Army.KindOf(mine)) * 25f + (g == active[1] ? 8f : 0f);
                    if (score > bestScore) { bestScore = score; best = g; }
                }
                if (best != active[1]) SetActive(1, best);
            }
            // Close in on the player's active group and attack it; archers keep their distance.
            var me = GroupCentre(0, mine);
            var us = GroupCentre(1, active[1]);
            bool archers = Army.Family(Army.KindOf(active[1])) == 2;
            float keep = archers ? 3.8f : 0.6f;
            var to = me - us; to.y = 0f;
            float d = to.magnitude;
            float bold = Mathf.Lerp(0.7f, 1.1f, RivalBoldness);
            aiVel = d > keep + 0.5f ? to.normalized * 2.2f * bold : Vector3.zero;
            attackAt[1] = d < keep + AttackRadius ? me : (Vector3?)null;
        }

        // ---------- Live: the other game's soldiers ----------

        /// <summary>
        /// This game's state for the other game: its active group, how many of its soldiers stand, and the active group's
        /// soldiers (x, z in tenths, "-" fallen, and "*" after one striking now). A few hundred bytes.
        /// </summary>
        public string StateBody()
        {
            var sb = new StringBuilder(400);
            int g = active[0];
            sb.Append("\"g\":").Append(g).Append(",\"n\":").Append(Alive(0)).Append(",\"u\":\"");
            for (int i = 0; i < Army.Soldiers; i++)
            {
                var u = units[g * Army.Soldiers + i]; // team 0 comes first, in order
                if (i > 0) sb.Append(';');
                if (u.dead) { sb.Append('-'); continue; }
                var p = u.root.position;
                sb.Append(Mathf.RoundToInt(p.x * 10f).ToString(CultureInfo.InvariantCulture)).Append(',').Append(Mathf.RoundToInt(p.z * 10f).ToString(CultureInfo.InvariantCulture));
                if (u.swing >= 0f && u.swing < 0.2f) sb.Append('*');
            }
            sb.Append('"');
            return sb.ToString();
        }

        [Serializable] private class NetState { public int g, n; public string u; }

        /// <summary>The other game's state: its active group and where its soldiers are (the latest replaces the last).</summary>
        public void ApplyState(string json)
        {
            NetState s;
            try { s = JsonUtility.FromJson<NetState>(json); }
            catch (Exception) { return; }
            if (s == null || s.u == null || s.g < 0 || s.g >= Army.Groups) return;
            if (active[1] != s.g) { active[1] = s.g; RefreshFades(); }
            var parts = s.u.Split(';');
            for (int i = 0; i < Army.Soldiers && i < parts.Length; i++)
            {
                var u = units[Army.Groups * Army.Soldiers + s.g * Army.Soldiers + i];
                var part = parts[i];
                if (part == "-") { if (!u.dead) Die(u); continue; }
                bool striking = part.EndsWith("*");
                if (striking) part = part.Substring(0, part.Length - 1);
                var xz = part.Split(',');
                if (xz.Length != 2 || !int.TryParse(xz[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) || !int.TryParse(xz[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)) continue;
                u.netTo = new Vector3(x / 10f, u.root.position.y, z / 10f);
                if (!u.netSeen) { u.netSeen = true; u.root.position = u.netTo; }
                if (striking && u.swing < 0f) u.swing = 0f;
            }
        }

        private void FollowNet(Unit u, float dt)
        {
            if (u.swing >= 0f)
            {
                u.swing += dt / 0.25f;
                u.model.SetRaise(u.swing < 0.5f ? u.swing * 2.2f : Mathf.Max(0f, 2.2f - u.swing * 2.2f));
                if (u.swing >= 1f) { u.swing = -1f; u.model.SetRaise(0f); }
            }
            if (!u.netSeen) return;
            var p = u.root.position;
            var to = u.netTo - p; to.y = 0f;
            float gap = to.magnitude;
            if (gap < 0.01f) return;
            var step = gap > 3f ? to : Vector3.ClampMagnitude(to, Mathf.Max(gap * 10f, u.speed) * dt);
            u.root.position = p + step;
            if (step.sqrMagnitude > 0.000001f) u.root.rotation = Quaternion.Slerp(u.root.rotation, Quaternion.LookRotation(step), dt * 10f);
        }

        /// <summary>The other game's soldier landed a blow on one of mine (its index in my army).</summary>
        public void ApplyHit(int index, float damage)
        {
            if (index < 0 || index >= Army.Groups * Army.Soldiers || damage <= 0f) return;
            var u = units[index];
            if (u.dead || u.group != active[0]) return; // standing by: it cannot be hit
            Hurt(u, Mathf.Min(damage, 40f));
        }
    }
}
