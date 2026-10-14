using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge's army battle, seen live: two armies of six groups of thirty stand along their edges of the board.
    /// Each turn one group a side marches to the middle and they fight it out until one group is down (or the clock runs
    /// out); the survivors walk back to their places and can be sent again.
    /// The soldiers fight with some sense: a group marches as a block until the enemy is close, then each soldier picks
    /// its foe — one its kind beats, one already hurt, one not already swarmed — and re-thinks now and then; archers
    /// keep their distance, step back from blades that reach them and shoot whoever threatens their side; a blow from
    /// behind lands harder; the family with the edge (swords over archers, spears over swords, archers over spears)
    /// hits harder. Soldiers are <see cref="SoldierModel"/>s in the side's colour; heavy ones are bigger, with shields.
    /// </summary>
    public class ClashSystem : MonoBehaviour
    {
        /// <summary>A clash ended: soldiers left in the side's group (0 = mine, 1 = theirs).</summary>
        public event Action<int, int> ClashOver;

        private class Unit
        {
            public int team, group, slot;
            public GroupKind kind;
            public Transform root;
            public SoldierModel model;
            public Vector3 home;
            public float hp, maxHp, dmg, speed, range, cooldown, cd, think;
            public bool fighting, returning, dead;
            public Unit target;
            public float swing = -1f;
            public bool Archer => Army.Family(kind) == 2;
        }

        private readonly List<Unit> units = new List<Unit>();
        // Over each group: its kind and soldiers left; under each of mine a ring (lit while it can be picked).
        private readonly TMPro.TextMeshPro[,] labels = new TMPro.TextMeshPro[2, Army.Groups];
        private readonly Transform[] rings = new Transform[Army.Groups];
        private readonly Material[] ringMats = new Material[Army.Groups];
        private readonly Vector3[,] spots = new Vector3[2, Army.Groups];
        private readonly bool[] pickable = new bool[Army.Groups];
        private int pickedGroup = -1;
        private readonly List<(Transform arrow, Vector3 from, Unit target, float dmg, float t)> arrows = new List<(Transform, Vector3, Unit, float, float)>();
        private Vector3 centre;
        private float clashTime;
        private bool clashing;
        private FxSystem fx;
        private float unitHeight = 0.8f;
        private Material arrowMat;
        // How many soldiers are after each foe this frame (so they spread out instead of all piling on one).
        private readonly Dictionary<Unit, int> targetedBy = new Dictionary<Unit, int>();

        /// <summary>The block a group marches in: columns across, rows deep, and the gaps.</summary>
        private const int Cols = 6;
        private const float GapX = 0.42f, GapZ = 0.46f;

        public static readonly Color Mine = new Color(0.3f, 0.62f, 1f), Theirs = new Color(1f, 0.32f, 0.3f);

        public void Init(FxSystem fxSystem) => fx = fxSystem;

        /// <summary>Where a group stands (team 0 mine, 1 theirs).</summary>
        public Vector3 Spot(int team, int group) => spots[team, group];

        /// <summary>Lights the rings of the groups that can be sent this turn (none: all dark), and the one picked.</summary>
        public void SetPickable(bool[] can, int picked)
        {
            pickedGroup = picked;
            for (int g = 0; g < Army.Groups; g++) pickable[g] = can != null && can[g];
        }

        /// <summary>Updates the soldier counts over the groups.</summary>
        public void Label(int team, int group, int left)
        {
            var t = labels[team, group];
            if (t == null) return;
            t.text = Loc.T("army.kind." + Army.KindOf(group)) + "\n" + left;
            t.alpha = left > 0 ? 1f : 0.3f;
        }

        private Vector3 Slot(int team, Vector3 spot, int i)
        {
            int rows = Mathf.CeilToInt(Army.Soldiers / (float)Cols);
            float side = team == 0 ? -1f : 1f; // the front row faces the middle
            return spot + new Vector3((i % Cols - (Cols - 1) * 0.5f) * GapX, 0f, (i / Cols - (rows - 1) * 0.5f) * GapZ * side);
        }

        /// <summary>Lays out both armies: each group's soldiers in a block at its spot, facing the middle.</summary>
        public void Begin(Vector3 middle, Vector3[] mySpots, Vector3[] theirSpots, float[] myPower, float[] theirPower, float soldierHeight)
        {
            Clear();
            centre = middle;
            unitHeight = soldierHeight;
            for (int team = 0; team < 2; team++)
                for (int g = 0; g < Army.Groups; g++)
                {
                    var spot = team == 0 ? mySpots[g] : theirSpots[g];
                    float power = team == 0 ? myPower[g] : theirPower[g];
                    spots[team, g] = spot;
                    MakeLabel(team, g, spot);
                    if (team == 0) MakeRing(g, spot);
                    for (int i = 0; i < Army.Soldiers; i++)
                    {
                        var u = MakeUnit(team, g, Army.KindOf(g), power, Slot(team, spot, i));
                        u.slot = i;
                        units.Add(u);
                    }
                }
        }

        public void Clear()
        {
            for (int t = 0; t < 2; t++) for (int g = 0; g < Army.Groups; g++) { if (labels[t, g] != null) Destroy(labels[t, g].gameObject); labels[t, g] = null; }
            for (int g = 0; g < Army.Groups; g++) { if (rings[g] != null) Destroy(rings[g].gameObject); rings[g] = null; pickable[g] = false; }
            pickedGroup = -1;
            foreach (var u in units) if (u.root != null) Destroy(u.root.gameObject);
            units.Clear();
            foreach (var a in arrows) if (a.arrow != null) Destroy(a.arrow.gameObject);
            arrows.Clear();
            clashing = false;
        }

        public int Alive(int team, int group = -1)
        {
            int n = 0;
            foreach (var u in units) if (!u.dead && u.team == team && (group < 0 || u.group == group)) n++;
            return n;
        }

        /// <summary>
        /// Hands a side's living soldiers over to the duel (their models, as they stand), out of the army battle.
        /// </summary>
        public List<(Transform root, SoldierModel model, int group, GroupKind kind)> TakeSurvivors(int team)
        {
            var list = new List<(Transform, SoldierModel, int, GroupKind)>();
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var u = units[i];
                if (u.dead || u.team != team) continue;
                u.root.SetParent(null, true);
                u.model.SetRaise(0f);
                list.Add((u.root, u.model, u.group, u.kind));
                units.RemoveAt(i);
            }
            return list;
        }

        public bool Clashing => clashing;

        /// <summary>Where the fight is (the middle of the soldiers fighting), for the camera to follow; null when none.</summary>
        public Vector3? FightCentre
        {
            get
            {
                if (!clashing) return null;
                Vector3 c = Vector3.zero; int n = 0;
                foreach (var u in units) if (!u.dead && u.fighting) { c += u.root.position; n++; }
                return n > 0 ? c / n : (Vector3?)null;
            }
        }

        /// <summary>Sends a group from each side to the middle; they fight until one is gone (or 35 s pass).</summary>
        public void StartClash(int myGroup, int theirGroup)
        {
            foreach (var u in units)
                if (!u.dead && ((u.team == 0 && u.group == myGroup) || (u.team == 1 && u.group == theirGroup)))
                {
                    u.fighting = true;
                    u.returning = false;
                    u.target = null;
                    u.think = UnityEngine.Random.value * 0.3f;
                    u.cd = 0.3f + UnityEngine.Random.value * 0.6f;
                }
            clashing = true;
            clashTime = 0f;
            AudioManager.PlaySfx(Sfx.Warning, 0.6f, 0.8f);
        }

        private void MakeLabel(int team, int g, Vector3 spot)
        {
            var t = new GameObject("GroupLabel").AddComponent<TMPro.TextMeshPro>();
            t.transform.SetParent(transform, false);
            t.transform.position = spot + Vector3.up * (unitHeight * 2.2f + 1.3f);
            t.font = SquashBot.UI.UiFactory.Font;
            t.textPreprocessor = new SquashBot.UI.RtlTextPreprocessor(t);
            t.fontSize = 4.2f;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.color = Color.white;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.outlineWidth = 0.35f;
            t.outlineColor = team == 0 ? new Color(0.05f, 0.2f, 0.55f) : new Color(0.55f, 0.08f, 0.08f);
            t.rectTransform.sizeDelta = new Vector2(8f, 3f);
            labels[team, g] = t;
            Label(team, g, Army.Soldiers);
        }

        private void MakeRing(int g, Vector3 spot)
        {
            ringMats[g] = MaterialFactory.Create(Mine, Mine * 2f);
            rings[g] = Shapes.Primitive(PrimitiveType.Cylinder, "PickRing", transform, spot + new Vector3(0f, 0.05f, 0f), new Vector3(Cols * GapX + 0.8f, 0.02f, 3.2f), ringMats[g]).transform;
        }

        private Unit MakeUnit(int team, int group, GroupKind kind, float power, Vector3 at)
        {
            bool heavy = Army.Heavy(kind);
            var u = new Unit { team = team, group = group, kind = kind, home = at };
            var colour = team == 0 ? Mine : Theirs;
            u.model = SoldierModel.Build(transform, kind, heavy ? colour : Color.Lerp(colour, Color.white, 0.3f));
            u.root = u.model.transform;
            u.root.position = at;
            u.root.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, team == 0 ? 1f : -1f));
            u.root.localScale = Vector3.one * unitHeight * (heavy ? 1.12f : 1f);
            // The stats of the kind, times the group's development.
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
            return u;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main;
            for (int t = 0; t < 2; t++)
                for (int g = 0; g < Army.Groups; g++)
                    if (labels[t, g] != null && cam != null) labels[t, g].transform.rotation = Quaternion.LookRotation(labels[t, g].transform.position - cam.transform.position);
            for (int g = 0; g < Army.Groups; g++)
            {
                if (rings[g] == null) continue;
                bool on = pickable[g] || g == pickedGroup;
                rings[g].gameObject.SetActive(on);
                if (!on) continue;
                var c = g == pickedGroup ? new Color(1f, 0.82f, 0.3f) : Mine;
                float glow = g == pickedGroup ? 3.5f : 2.2f + Mathf.Sin(Time.time * 5f) * 1.4f;
                MaterialFactory.SetColors(ringMats[g], c, c * glow);
            }
            UpdateArrows(dt);

            // Where each fighting side's block is and whether the enemy is close enough to break ranks.
            Vector3 c0 = Vector3.zero, c1 = Vector3.zero;
            int n0 = 0, n1 = 0;
            foreach (var u in units)
            {
                if (u.dead || !u.fighting) continue;
                if (u.team == 0) { c0 += u.root.position; n0++; } else { c1 += u.root.position; n1++; }
            }
            if (n0 > 0) c0 /= n0;
            if (n1 > 0) c1 /= n1;
            targetedBy.Clear();
            foreach (var u in units) if (!u.dead && u.fighting && u.target != null) targetedBy[u.target] = (targetedBy.TryGetValue(u.target, out int k) ? k : 0) + 1;
            bool contact = n0 > 0 && n1 > 0 && (c0 - c1).sqrMagnitude < 5.5f * 5.5f;

            foreach (var u in units)
            {
                if (u.dead) continue;
                if (u.swing >= 0f)
                {
                    u.swing += dt / 0.3f;
                    u.model.SetRaise(Mathf.Sin(Mathf.Clamp01(u.swing) * Mathf.PI) * 1.1f);
                    if (u.swing >= 1f) { u.swing = -1f; u.model.SetRaise(0f); }
                }
                if (u.returning) { Walk(u, u.home, dt, out bool there); if (there) { u.returning = false; Face(u, centre); } continue; }
                if (!u.fighting) continue;
                u.cd -= dt;
                if (!contact && u.target == null)
                {
                    // Marching: the block keeps its shape on the way to the middle (archers stop at their range).
                    var mine = u.team == 0 ? c0 : c1;
                    var foe = u.team == 0 ? c1 : c0;
                    var dir = foe - mine; dir.y = 0f;
                    float stop = u.Archer ? u.range * 0.9f : 0.5f;
                    if (dir.magnitude > stop + 1f)
                    {
                        var offset = u.root.position - mine;
                        offset.y = 0f;
                        var goal = mine + offset + dir.normalized * Mathf.Min(dir.magnitude - stop, 1.5f);
                        Walk(u, goal, dt, out _);
                        continue;
                    }
                }
                u.think -= dt;
                if (u.target == null || u.target.dead || !u.target.fighting || u.think <= 0f)
                {
                    u.target = Choose(u);
                    u.think = 0.6f + UnityEngine.Random.value * 0.4f;
                }
                if (u.target == null) continue;
                var to = u.target.root.position - u.root.position;
                to.y = 0f;
                float d = to.magnitude;
                if (u.Archer)
                {
                    // An archer: a blade at arm's length means a step back (towards its own side) first.
                    var threat = NearestFoe(u, out float td, meleeOnly: true);
                    if (threat != null && td < 1.4f)
                    {
                        var away = u.root.position - threat.root.position; away.y = 0f;
                        var back = new Vector3(0f, 0f, u.team == 0 ? -1f : 1f);
                        Walk(u, u.root.position + (away.normalized + back * 0.6f).normalized * 1.2f, dt * 0.85f, out _);
                        if (u.cd <= 0f && td < u.range) Strike(u, threat);
                        continue;
                    }
                }
                if (d > u.range)
                {
                    // The block's outer files swing wide and come at the foe from the side (it is closed round, not met head on).
                    int wing = u.Archer ? 0 : u.slot % Cols == 0 ? -1 : u.slot % Cols == Cols - 1 ? 1 : (u.slot % Cols == 1 && u.slot / Cols % 2 == 1) ? -1 : 0;
                    var goal = u.target.root.position - to.normalized * (u.range * 0.8f);
                    if (wing != 0 && d > 1.8f) goal += new Vector3(-to.z, 0f, to.x).normalized * wing * Mathf.Min(1.6f, d * 0.4f);
                    Walk(u, goal, dt, out _);
                    continue;
                }
                Face(u, u.target.root.position);
                if (u.cd <= 0f) Strike(u, u.target);
            }
            if (!clashing) return;
            clashTime += dt;
            int mineLeft = 0, theirsLeft = 0;
            foreach (var u in units) if (!u.dead && u.fighting) { if (u.team == 0) mineLeft++; else theirsLeft++; }
            if (mineLeft > 0 && theirsLeft > 0 && clashTime < 35f) return;
            // Over: the survivors go back to their places (the block closes up again).
            clashing = false;
            int myGroup = -1, theirGroup = -1;
            foreach (var u in units)
            {
                if (!u.fighting) continue;
                if (u.team == 0) myGroup = u.group; else theirGroup = u.group;
                u.fighting = false;
                u.target = null;
                if (!u.dead) u.returning = true;
            }
            for (int t = 0; t < 2; t++)
            {
                int g = t == 0 ? myGroup : theirGroup;
                if (g < 0) continue;
                int i = 0;
                foreach (var u in units) if (!u.dead && u.team == t && u.group == g) u.home = Slot(t, spots[t, g], i++);
            }
            ClashOver?.Invoke(myGroup >= 0 ? Alive(0, myGroup) : 0, theirGroup >= 0 ? Alive(1, theirGroup) : 0);
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
            if (u.Archer) Shoot(u, target, dmg);
            else Hit(target, dmg, u.team);
        }

        /// <summary>
        /// The foe worth fighting: near, one its kind beats, one already hurt, one not already swarmed by its side, and
        /// for a blade an archer in reach is a prize; for an archer, whoever is closest to its own side's soldiers.
        /// </summary>
        private Unit Choose(Unit u)
        {
            Unit best = null;
            float bestScore = float.MaxValue;
            foreach (var o in units)
            {
                if (o.dead || !o.fighting || o.team == u.team) continue;
                float d = Vector3.Distance(o.root.position, u.root.position);
                float score = d;
                int edge = Army.Edge(u.kind, o.kind);
                score -= edge * 1.2f;
                score -= (1f - o.hp / o.maxHp) * 1.5f;
                int swarm = targetedBy.TryGetValue(o, out int k) ? k - (u.target == o ? 1 : 0) : 0;
                score += swarm * (u.Archer ? 0.3f : 0.9f);
                if (!u.Archer && o.Archer && d < 3f) score -= 1f;
                if (u.Archer && !o.Archer) score -= 0.5f; // the blades coming for its side first
                if (score < bestScore) { bestScore = score; best = o; }
            }
            return best;
        }

        private Unit NearestFoe(Unit u, out float dist, bool meleeOnly)
        {
            Unit best = null;
            dist = float.MaxValue;
            foreach (var o in units)
            {
                if (o.dead || !o.fighting || o.team == u.team || (meleeOnly && o.Archer)) continue;
                float d = Vector3.Distance(o.root.position, u.root.position);
                if (d < dist) { dist = d; best = o; }
            }
            return best;
        }

        private void Walk(Unit u, Vector3 goal, float dt, out bool there)
        {
            var to = goal - u.root.position;
            to.y = 0f;
            there = to.magnitude < 0.08f;
            if (there) return;
            var step = Vector3.ClampMagnitude(to, u.speed * dt);
            // Keep a little apart from the others.
            foreach (var o in units)
            {
                if (o == u || o.dead) continue;
                var away = u.root.position - o.root.position;
                away.y = 0f;
                float d = away.sqrMagnitude;
                if (d > 0.000001f && d < 0.36f * 0.36f) { d = Mathf.Sqrt(d); step += away / d * (0.36f - d) * 0.5f; }
            }
            u.root.position += step;
            u.root.position = new Vector3(u.root.position.x, u.home.y, u.root.position.z);
            if (step.sqrMagnitude > 0.00001f && !(u.target != null && u.Archer))
                u.root.rotation = Quaternion.Slerp(u.root.rotation, Quaternion.LookRotation(new Vector3(step.x, 0f, step.z)), dt * 10f);
            else if (u.target != null) Face(u, u.target.root.position);
        }

        private static void Face(Unit u, Vector3 at)
        {
            var to = at - u.root.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f) u.root.rotation = Quaternion.Slerp(u.root.rotation, Quaternion.LookRotation(to), 0.35f);
        }

        private void Hit(Unit target, float dmg, int byTeam)
        {
            if (target.dead) return;
            target.hp -= dmg;
            target.model.Flash();
            if (fx != null) fx.Burst(target.root.position + Vector3.up * unitHeight * 0.6f, byTeam == 0 ? Mine : Theirs, (byTeam == 0 ? Mine : Theirs) * 1.6f, 4, 2.5f);
            if (target.hp > 0f) return;
            target.dead = true;
            target.fighting = false;
            if (fx != null) fx.Burst(target.root.position + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.3f), new Color(2f, 0.8f, 0.3f), 10, 4f);
            AudioManager.PlaySfx(Sfx.Impact, 0.3f, 1.3f + UnityEngine.Random.value * 0.3f);
            Destroy(target.root.gameObject);
        }

        private void Shoot(Unit from, Unit target, float dmg)
        {
            if (arrowMat == null) arrowMat = MaterialFactory.Create(new Color(0.95f, 0.85f, 0.6f), new Color(0.6f, 0.45f, 0.1f));
            var arrow = Shapes.Rounded("Arrow", transform, from.root.position + Vector3.up * unitHeight * 0.6f, new Vector3(0.04f, 0.04f, 0.4f), 0.01f, arrowMat).transform;
            arrows.Add((arrow, arrow.position, target, dmg, 0f));
            AudioManager.PlaySfx(Sfx.Hop, 0.2f, 2f);
        }

        private void UpdateArrows(float dt)
        {
            for (int i = arrows.Count - 1; i >= 0; i--)
            {
                var a = arrows[i];
                a.t += dt / 0.4f;
                if (a.arrow == null) { arrows.RemoveAt(i); continue; }
                var end = a.target.root != null ? a.target.root.position + Vector3.up * unitHeight * 0.55f : a.arrow.position;
                var p = Vector3.Lerp(a.from, end, a.t) + Vector3.up * Mathf.Sin(Mathf.Clamp01(a.t) * Mathf.PI) * 0.8f;
                var dir = p - a.arrow.position;
                if (dir.sqrMagnitude > 0.0001f) a.arrow.rotation = Quaternion.LookRotation(dir);
                a.arrow.position = p;
                arrows[i] = a;
                if (a.t < 1f) continue;
                Destroy(a.arrow.gameObject);
                arrows.RemoveAt(i);
                if (!a.target.dead) Hit(a.target, a.dmg, 1 - a.target.team);
            }
        }
    }
}
