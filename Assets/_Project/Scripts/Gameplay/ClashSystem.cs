using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge's army battle, seen live: two armies of six groups of ten stand along their edges of the board.
    /// Each turn one group a side marches to the middle and they fight it out — swords and spears close in, archers
    /// shoot from range, the family that has the edge (swords over archers, spears over swords, archers over spears)
    /// hits harder — until one group is down (or the clock runs out). The survivors walk back to their places and can be
    /// sent again. Soldiers are small robots in the side's colour holding their kind's weapon; heavy ones are bigger.
    /// </summary>
    public class ClashSystem : MonoBehaviour
    {
        /// <summary>A clash ended: soldiers left in the side's group (0 = mine, 1 = theirs).</summary>
        public event Action<int, int> ClashOver;

        private class Unit
        {
            public int team, group;
            public GroupKind kind;
            public Transform root;
            public GuardBot bot;
            public Vector3 home;
            public float hp, maxHp, dmg, speed, range, cooldown, cd;
            public bool fighting, returning, dead;
            public Unit target;
            public float swing = -1f;
        }

        private readonly List<Unit> units = new List<Unit>();
        // Over each group: its kind and soldiers left; under each of mine a ring (lit while it can be picked).
        private readonly TMPro.TextMeshPro[,] labels = new TMPro.TextMeshPro[2, Army.Groups];
        private readonly Transform[] rings = new Transform[Army.Groups];
        private readonly Material[] ringMats = new Material[Army.Groups];
        private readonly Vector3[,] spots = new Vector3[2, Army.Groups];
        private readonly bool[] pickable = new bool[Army.Groups];
        private int pickedGroup = -1;

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
        private readonly List<(Transform arrow, Vector3 from, Unit target, float dmg, float t)> arrows = new List<(Transform, Vector3, Unit, float, float)>();
        private Vector3 centre;
        private float clashTime;
        private bool clashing;
        private FxSystem fx;
        private float unitHeight = 0.8f;

        public static readonly Color Mine = new Color(0.3f, 0.62f, 1f), Theirs = new Color(1f, 0.32f, 0.3f);

        public void Init(FxSystem fxSystem) => fx = fxSystem;

        /// <summary>Lays out both armies: each group's ten soldiers in a 2×5 block at its spot, facing the middle.</summary>
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
                        var at = spot + new Vector3((i % 5 - 2f) * 0.42f, 0f, (i / 5 - 0.5f) * 0.5f * (team == 0 ? -1f : 1f));
                        units.Add(MakeUnit(team, g, Army.KindOf(g), power, at));
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

        /// <summary>Where a side's living soldiers stand now (for the duel that follows), and their group.</summary>
        public List<(Vector3 at, int group)> Survivors(int team)
        {
            var list = new List<(Vector3, int)>();
            foreach (var u in units) if (!u.dead && u.team == team) list.Add((u.root.position, u.group));
            return list;
        }

        /// <summary>
        /// Hands a side's living soldiers over to the duel (their models, as they stand), out of the army battle.
        /// </summary>
        public List<(Transform root, GuardBot bot, int group, GroupKind kind)> TakeSurvivors(int team)
        {
            var list = new List<(Transform, GuardBot, int, GroupKind)>();
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var u = units[i];
                if (u.dead || u.team != team) continue;
                u.root.SetParent(null, true);
                u.bot.SetRaise(0f);
                list.Add((u.root, u.bot, u.group, u.kind));
                units.RemoveAt(i);
            }
            return list;
        }

        public bool Clashing => clashing;

        /// <summary>Sends a group from each side to the middle; they fight until one is gone (or 25 s pass).</summary>
        public void StartClash(int myGroup, int theirGroup)
        {
            foreach (var u in units)
                if (!u.dead && ((u.team == 0 && u.group == myGroup) || (u.team == 1 && u.group == theirGroup)))
                {
                    u.fighting = true;
                    u.returning = false;
                    u.cd = 0.3f + UnityEngine.Random.value * 0.5f;
                }
            clashing = true;
            clashTime = 0f;
            AudioManager.PlaySfx(Sfx.Warning, 0.6f, 0.8f);
        }

        private void MakeLabel(int team, int g, Vector3 spot)
        {
            var t = new GameObject("GroupLabel").AddComponent<TMPro.TextMeshPro>();
            t.transform.SetParent(transform, false);
            t.transform.position = spot + Vector3.up * (unitHeight * 2.2f + 1f);
            t.font = SquashBot.UI.UiFactory.Font;
            t.textPreprocessor = new SquashBot.UI.RtlTextPreprocessor(t);
            t.fontSize = 7f;
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
            rings[g] = Shapes.Primitive(PrimitiveType.Cylinder, "PickRing", transform, spot + new Vector3(0f, 0.05f, 0f), new Vector3(3.2f, 0.02f, 2.2f), ringMats[g]).transform;
        }

        private Unit MakeUnit(int team, int group, GroupKind kind, float power, Vector3 at)
        {
            bool heavy = Army.Heavy(kind);
            int fam = Army.Family(kind);
            var u = new Unit { team = team, group = group, kind = kind, home = at };
            u.root = new GameObject("Soldier").transform;
            u.root.SetParent(transform, false);
            u.root.position = at;
            u.root.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, team == 0 ? 1f : -1f));
            var colour = team == 0 ? Mine : Theirs;
            u.bot = GuardBot.Build(u.root, heavy ? Color.Lerp(colour, Color.white, 0.1f) : Color.Lerp(colour, Color.white, 0.35f), fam == 0 ? 0 : fam == 1 ? 2 : 4);
            // A bright team disc under each soldier: the sides read at a glance from above.
            Shapes.Primitive(PrimitiveType.Cylinder, "Team", u.root, new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.01f, 0.36f), MaterialFactory.Create(colour, colour * 1.8f));
            // Sized to the soldier height (heavy ones a size up, broad).
            var model = u.bot.transform;
            float h = 0f;
            foreach (var r in u.root.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.max.y - at.y);
            if (h > 0.05f) model.localScale *= unitHeight * (heavy ? 1.15f : 0.95f) / h;
            model.localScale = Vector3.Scale(model.localScale, new Vector3(heavy ? 1.2f : 1.05f, 1f, heavy ? 1.2f : 1.05f));
            Weapon(u.root, fam, colour, unitHeight);
            // The stats of the kind, times the group's development.
            switch (kind)
            {
                case GroupKind.LightSword: u.maxHp = 6f; u.dmg = 2f; u.speed = 3.2f; u.range = 1.0f; u.cooldown = 0.8f; break;
                case GroupKind.HeavySword: u.maxHp = 10f; u.dmg = 2.2f; u.speed = 2.2f; u.range = 1.0f; u.cooldown = 1.0f; break;
                case GroupKind.LightSpear: u.maxHp = 6f; u.dmg = 2f; u.speed = 3.0f; u.range = 1.5f; u.cooldown = 0.9f; break;
                case GroupKind.HeavySpear: u.maxHp = 10f; u.dmg = 2.2f; u.speed = 2.1f; u.range = 1.5f; u.cooldown = 1.1f; break;
                case GroupKind.LightBow: u.maxHp = 4.5f; u.dmg = 2f; u.speed = 3.0f; u.range = 4.5f; u.cooldown = 1.1f; break;
                default: u.maxHp = 7f; u.dmg = 2.6f; u.speed = 2.2f; u.range = 4.5f; u.cooldown = 1.3f; break;
            }
            u.maxHp *= power;
            u.dmg *= power;
            u.hp = u.maxHp;
            return u;
        }

        /// <summary>The kind's weapon in the soldier's right hand: a sword, a long spear, or a bow.</summary>
        private static void Weapon(Transform root, int fam, Color colour, float h)
        {
            var metal = MaterialFactory.Create(new Color(0.85f, 0.87f, 0.92f), new Color(0.15f, 0.15f, 0.18f));
            var wood = MaterialFactory.Create(new Color(0.55f, 0.36f, 0.2f), Color.black);
            var hand = new Vector3(0.22f * h, 0.45f * h, 0.08f * h);
            if (fam == 0)
            {
                Shapes.Rounded("Blade", root, hand + new Vector3(0f, 0.22f * h, 0.05f * h), new Vector3(0.05f, 0.45f, 0.08f) * h, 0.015f, metal);
                Shapes.Rounded("Guard", root, hand + new Vector3(0f, -0.02f * h, 0.05f * h), new Vector3(0.16f, 0.04f, 0.06f) * h, 0.01f, wood);
            }
            else if (fam == 1)
            {
                Shapes.Primitive(PrimitiveType.Cylinder, "Shaft", root, hand + new Vector3(0f, 0.25f * h, 0.05f * h), new Vector3(0.04f, 0.5f, 0.04f) * h, wood);
                Shapes.Rounded("Tip", root, hand + new Vector3(0f, 0.78f * h, 0.05f * h), new Vector3(0.07f, 0.16f, 0.04f) * h, 0.01f, metal);
            }
            else
            {
                for (int i = -2; i <= 2; i++)
                {
                    var seg = Shapes.Rounded("Bow", root, hand + new Vector3(0f, i * 0.1f * h, 0.12f * h - Mathf.Abs(i) * 0.03f * h), new Vector3(0.04f, 0.12f, 0.04f) * h, 0.01f, wood).transform;
                    seg.localRotation = Quaternion.Euler(i * -14f, 0f, 0f);
                }
            }
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
            foreach (var u in units)
            {
                if (u.dead) continue;
                if (u.swing >= 0f)
                {
                    u.swing += dt / 0.3f;
                    u.bot.SetRaise(Mathf.Sin(Mathf.Clamp01(u.swing) * Mathf.PI) * 1.2f);
                    if (u.swing >= 1f) { u.swing = -1f; u.bot.SetRaise(0f); }
                }
                if (u.returning) { Walk(u, u.home, dt, out bool there); if (there) { u.returning = false; Face(u, centre); } continue; }
                if (!u.fighting) continue;
                u.cd -= dt;
                if (u.target == null || u.target.dead || !u.target.fighting) u.target = Nearest(u);
                if (u.target == null) continue;
                var to = u.target.root.position - u.root.position;
                to.y = 0f;
                if (to.magnitude > u.range) { Walk(u, u.target.root.position, dt, out _); continue; }
                Face(u, u.target.root.position);
                if (u.cd > 0f) continue;
                u.cd = u.cooldown * (0.9f + UnityEngine.Random.value * 0.2f);
                u.swing = 0f;
                float dmg = u.dmg;
                int edge = Army.Edge(u.kind, u.target.kind);
                if (edge > 0) dmg *= 1.3f;
                else if (edge < 0) dmg *= 0.77f;
                if (Army.Family(u.kind) == 2) Shoot(u, u.target, dmg);
                else Hit(u.target, dmg, u.team);
            }
            if (!clashing) return;
            clashTime += dt;
            int mine = 0, theirs = 0;
            foreach (var u in units) if (!u.dead && u.fighting) { if (u.team == 0) mine++; else theirs++; }
            if (mine > 0 && theirs > 0 && clashTime < 25f) return;
            // Over: the survivors go back to their places.
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
            ClashOver?.Invoke(myGroup >= 0 ? Alive(0, myGroup) : 0, theirGroup >= 0 ? Alive(1, theirGroup) : 0);
        }

        private Unit Nearest(Unit u)
        {
            Unit best = null;
            float bd = float.MaxValue;
            foreach (var o in units)
            {
                if (o.dead || !o.fighting || o.team == u.team) continue;
                float d = (o.root.position - u.root.position).sqrMagnitude;
                if (d < bd) { bd = d; best = o; }
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
                float d = away.magnitude;
                if (d > 0.001f && d < 0.38f) step += away / d * (0.38f - d) * 0.5f;
            }
            u.root.position += step;
            u.root.position = new Vector3(u.root.position.x, u.home.y + Mathf.Abs(Mathf.Sin(Time.time * 12f + u.home.x)) * 0.03f, u.root.position.z);
            if (step.sqrMagnitude > 0.00001f) u.root.rotation = Quaternion.Slerp(u.root.rotation, Quaternion.LookRotation(new Vector3(step.x, 0f, step.z)), dt * 10f);
        }

        private static void Face(Unit u, Vector3 at)
        {
            var to = at - u.root.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f) u.root.rotation = Quaternion.LookRotation(to);
        }

        private void Hit(Unit target, float dmg, int byTeam)
        {
            if (target.dead) return;
            target.hp -= dmg;
            target.bot.Flash();
            if (fx != null) fx.Burst(target.root.position + Vector3.up * unitHeight * 0.6f, byTeam == 0 ? Mine : Theirs, (byTeam == 0 ? Mine : Theirs) * 1.6f, 5, 2.5f);
            if (target.hp > 0f) return;
            target.dead = true;
            target.fighting = false;
            if (fx != null) fx.Burst(target.root.position + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.3f), new Color(2f, 0.8f, 0.3f), 14, 4f);
            AudioManager.PlaySfx(Sfx.Impact, 0.35f, 1.3f + UnityEngine.Random.value * 0.3f);
            Destroy(target.root.gameObject);
        }

        private void Shoot(Unit from, Unit target, float dmg)
        {
            var arrow = Shapes.Rounded("Arrow", transform, from.root.position + Vector3.up * unitHeight * 0.6f, new Vector3(0.04f, 0.04f, 0.4f), 0.01f,
                MaterialFactory.Create(new Color(0.95f, 0.85f, 0.6f), new Color(0.6f, 0.45f, 0.1f))).transform;
            arrows.Add((arrow, arrow.position, target, dmg, 0f));
            AudioManager.PlaySfx(Sfx.Hop, 0.25f, 2f);
        }

        private void UpdateArrows(float dt)
        {
            for (int i = arrows.Count - 1; i >= 0; i--)
            {
                var a = arrows[i];
                a.t += dt / 0.35f;
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
