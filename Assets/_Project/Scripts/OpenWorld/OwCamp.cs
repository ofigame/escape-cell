using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.OpenWorld
{
    /// <summary>
    /// A bandit camp in a region they hold: a ring of stakes (stone walls at the Fortress), watchtowers on it that shoot,
    /// tents and the chief's hut inside, and the bandits. foi's rescue operation: its soldiers come along, and it sets up
    /// cannons where it stands — they pound the walls, the huts and the bandits. Won when every bandit (and the chief)
    /// is down: the camp falls and the region is free.
    /// </summary>
    public class OwCamp : MonoBehaviour
    {
        private enum PartKind { Wall, Tower, Tent, Hut }

        private class Part { public PartKind kind; public Vector2 pos; public float radius, hp, maxHp, cd; public Transform root; public bool dead; }
        private class Foe { public Vector2 pos, home; public float hp, maxHp, damage, range, speed, cd; public bool chief, archer, dead; public Transform root, fill; public HumanoidBot bot; }
        private class Ally { public int group; public bool ranged; public Vector2 pos; public float hp, damage, cd; public Transform root; public SoldierModel model; public bool dead; }
        private class Cannon { public Vector2 pos; public float hp = 150f, cd; public Transform root; public bool dead; }
        private class Shell { public Vector2 at; public float t, damage, splash; }

        public const int CannonPrice = 300, MaxCannons = 4;

        public Vector2Int Region { get; private set; }
        public Vector2 Middle { get; private set; }
        public bool Started { get; private set; }
        public bool Fortress => Region == OwRegions.Fortress;
        public int Left { get { int n = 0; foreach (var f in foes) if (!f.dead) n++; return n; } }
        public int Cannons { get { int n = 0; foreach (var c in cannons) if (!c.dead) n++; return n; } }
        public long Reward => Fortress ? 10000 : Mathf.RoundToInt(200f * strength * strength);
        public event Action<bool> Ended;

        private OwSave save;
        private FxSystem fx;
        private Func<Vector2> foiPos;
        private Action<float> hurtFoi;
        private float strength;
        private readonly List<Part> parts = new List<Part>();
        private readonly List<Foe> foes = new List<Foe>();
        private readonly List<Ally> allies = new List<Ally>();
        private readonly List<Cannon> cannons = new List<Cannon>();
        private readonly List<Shell> shells = new List<Shell>();
        private readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        private Material barBack, barFill;

        public static OwCamp Create(Transform parent, OwSave save, Vector2Int region, FxSystem fx, Func<Vector2> foiPos, Action<float> hurtFoi)
        {
            var go = new GameObject("BanditCamp " + region);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<OwCamp>();
            c.save = save; c.fx = fx; c.foiPos = foiPos; c.hurtFoi = hurtFoi;
            c.Region = region;
            c.Middle = OwRegions.CampAt(region);
            c.strength = OwRegions.Strength(region);
            c.Build();
            return c;
        }

        private Material Mat(Color c, float glow = 0f)
        {
            if (!mats.TryGetValue(c, out var m)) { mats[c] = m = MaterialFactory.Create(c, c * glow); OwnedMaterials.Own(gameObject, m); }
            return m;
        }

        // ---------- The camp ----------

        private void Build()
        {
            barBack = Mat(new Color(0.08f, 0.06f, 0.1f));
            barFill = Mat(new Color(1f, 0.35f, 0.3f), 1.4f);
            var stake = Mat(new Color(0.42f, 0.28f, 0.16f));
            var stone = Mat(new Color(0.45f, 0.4f, 0.38f));
            var cloth = Mat(new Color(0.55f, 0.18f, 0.14f));
            var dark = Mat(new Color(0.22f, 0.18f, 0.16f));
            float ring = Fortress ? 12f : 9f;
            // The ring: stakes (stone at the Fortress), a section every 2.4 units.
            int sections = Mathf.RoundToInt(2f * Mathf.PI * ring / 2.4f);
            for (int i = 0; i < sections; i++)
            {
                float a = i * Mathf.PI * 2f / sections;
                var p = Middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
                var t = Piece("Wall", p, a * Mathf.Rad2Deg);
                if (Fortress)
                {
                    Shapes.Rounded("Wall", t, new Vector3(0f, 1.4f, 0f), new Vector3(0.9f, 2.8f, 2.5f), 0.06f, stone);
                    Shapes.Rounded("Merlon", t, new Vector3(0f, 3f, 0.6f), new Vector3(0.9f, 0.5f, 0.8f), 0.05f, stone);
                }
                else
                    for (int k = 0; k < 3; k++)
                        Shapes.Rounded("Stake", t, new Vector3(0f, 1f, -0.8f + k * 0.8f), new Vector3(0.35f, 2f + (k % 2) * 0.3f, 0.35f), 0.08f, stake);
                parts.Add(new Part { kind = PartKind.Wall, pos = p, radius = 1.3f, hp = (Fortress ? 380f : 100f) * strength, maxHp = (Fortress ? 380f : 100f) * strength, root = t });
            }
            // Watchtowers on the ring.
            int towers = Fortress ? 6 : 2 + Mathf.FloorToInt(strength);
            for (int i = 0; i < towers; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / towers;
                var p = Middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ring - 1.6f);
                var t = Piece("Tower", p, 0f);
                foreach (float x in new[] { -0.6f, 0.6f }) foreach (float z in new[] { -0.6f, 0.6f })
                    Shapes.Rounded("Leg", t, new Vector3(x, 1.6f, z), new Vector3(0.22f, 3.2f, 0.22f), 0.04f, stake);
                Shapes.Rounded("Deck", t, new Vector3(0f, 3.3f, 0f), new Vector3(1.9f, 0.25f, 1.9f), 0.04f, stake);
                Shapes.Rounded("Roof", t, new Vector3(0f, 4.4f, 0f), new Vector3(2.1f, 0.3f, 2.1f), 0.04f, cloth);
                parts.Add(new Part { kind = PartKind.Tower, pos = p, radius = 1.2f, hp = 160f * strength, maxHp = 160f * strength, root = t });
            }
            // Tents and the chief's hut.
            int tents = 3 + Mathf.FloorToInt(strength);
            for (int i = 0; i < tents; i++)
            {
                float a = i * Mathf.PI * 2f / tents + 0.4f;
                var p = Middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ring * 0.5f);
                var t = Piece("Tent", p, a * Mathf.Rad2Deg);
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Rounded("Canvas", t, new Vector3(s * 0.7f, 0.9f, 0f), new Vector3(0.1f, 2.2f, 2.6f), 0.03f, cloth).transform.localRotation = Quaternion.Euler(0f, 0f, s * 38f);
                parts.Add(new Part { kind = PartKind.Tent, pos = p, radius = 1.4f, hp = 80f * strength, maxHp = 80f * strength, root = t });
            }
            var hut = Piece("Hut", Middle, 0f);
            Shapes.Rounded("Walls", hut, new Vector3(0f, 1.3f, 0f), new Vector3(3.6f, 2.6f, 3.2f), 0.05f, dark);
            Shapes.Rounded("Roof", hut, new Vector3(0f, 2.9f, 0f), new Vector3(4f, 0.4f, 3.6f), 0.05f, cloth);
            Shapes.Rounded("Banner", hut, new Vector3(0f, 4.2f, 0f), new Vector3(0.1f, 2f, 0.1f), 0.02f, dark);
            Shapes.Rounded("Flag", hut, new Vector3(0.5f, 4.8f, 0f), new Vector3(1f, 0.6f, 0.04f), 0.02f, Mat(new Color(0.15f, 0.1f, 0.1f)));
            parts.Add(new Part { kind = PartKind.Hut, pos = Middle, radius = 2.2f, hp = 250f * strength, maxHp = 250f * strength, root = hut });
            // The bandits, round the fires inside.
            int n = Mathf.RoundToInt(4 * strength) + 2;
            var rng = new System.Random(Region.x * 31 + Region.y * 7);
            for (int i = 0; i < n; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var p = Middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (2.5f + (float)rng.NextDouble() * (ring - 4.5f));
                bool archer = rng.NextDouble() < 0.35;
                AddFoe(p, archer, false);
            }
            if (strength >= 1.25f) AddFoe(Middle + new Vector2(0f, -3f), false, true);
        }

        private Transform Piece(string name, Vector2 p, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.position = OwWorld.World(p);
            t.rotation = Quaternion.Euler(0f, -yaw, 0f);
            return t;
        }

        private void AddFoe(Vector2 p, bool archer, bool chief)
        {
            float mult = chief ? (Fortress ? 2.5f : 1.6f) : 1f;
            var f = new Foe
            {
                pos = p, home = p, chief = chief, archer = archer,
                hp = (archer ? 45f : 60f) * strength * mult * (chief ? 3f : 1f), damage = (archer ? 7f : 6f) * strength * (chief ? 2f : 1f),
                range = archer ? 7f : chief ? 2f : 1.4f, speed = chief ? 2.4f : archer ? 2.6f : 3.1f, cd = 1f,
            };
            f.maxHp = f.hp;
            var t = new GameObject(chief ? "Chief" : "Bandit").transform;
            t.SetParent(transform, false);
            f.root = t;
            f.bot = HumanoidBot.Build(t, chief ? new Color(1f, 0.15f, 0.1f) : archer ? new Color(0.45f, 0.6f, 0.25f) : new Color(0.85f, 0.5f, 0.2f), chief ? 3 : 0);
            f.bot.transform.localScale = Vector3.one * (chief ? (Fortress ? 1.7f : 1.35f) : 0.8f);
            var bar = new GameObject("Bar").transform;
            bar.SetParent(t, false);
            bar.localPosition = new Vector3(0f, chief ? 3.6f : 2.3f, 0f);
            Shapes.Rounded("Back", bar, Vector3.zero, new Vector3(chief ? 2f : 1f, 0.16f, 0.04f), 0.05f, barBack);
            f.fill = new GameObject("Fill").transform;
            f.fill.SetParent(bar, false);
            float w = chief ? 1.92f : 0.92f;
            f.fill.localPosition = new Vector3(-w * 0.5f, 0f, -0.03f);
            Shapes.Rounded("Fill", f.fill, new Vector3(w * 0.5f, 0f, 0f), new Vector3(w, 0.1f, 0.03f), 0.04f, barFill);
            t.position = OwWorld.World(p);
            foes.Add(f);
        }

        // ---------- The operation ----------

        /// <summary>foi attacks: its soldiers (at most 12 shown) come along.</summary>
        public void Begin()
        {
            if (Started) return;
            Started = true;
            int shown = 0;
            var near = foiPos();
            for (int g = 0; g < Army.Groups && shown < 12; g++)
            {
                var kind = Army.KindOf(g);
                var st = Army.StatsOf(g);
                for (int k = 0; k < save.soldiers[g] && shown < 12; k++, shown++)
                {
                    var model = SoldierModel.Build(transform, kind, Army.Heavy(kind) ? new Color(0.25f, 0.5f, 1f) : new Color(0.45f, 0.65f, 1f));
                    model.transform.localScale = Vector3.one * 0.62f * Army.SizeFor(g, st);
                    var p = near + new Vector2((shown % 4 - 1.5f) * 1.2f, -2f - (shown / 4) * 1.2f);
                    allies.Add(new Ally { group = g, ranged = Army.Family(kind) == 2, pos = p, hp = st.hp * 2f, damage = st.hit * 3f * (save.hungry ? 0.6f : 1f), root = model.transform, model = model });
                }
            }
            AudioManager.PlaySfx(Sfx.Warning, 1f, 1f);
        }

        /// <summary>A cannon where foi stands (paid by the caller). False when four already stand.</summary>
        public bool PlaceCannon(Vector2 at, float facing)
        {
            if (Cannons >= MaxCannons) return false;
            var t = new GameObject("Cannon").transform;
            t.SetParent(transform, false);
            t.position = OwWorld.World(at);
            t.rotation = Quaternion.Euler(0f, facing, 0f);
            var wood = Mat(new Color(0.5f, 0.34f, 0.2f));
            var iron = Mat(new Color(0.2f, 0.2f, 0.24f));
            Shapes.Rounded("Base", t, new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.4f, 1.4f), 0.06f, wood);
            Shapes.Primitive(PrimitiveType.Cylinder, "Barrel", t, new Vector3(0f, 0.8f, 0.3f), new Vector3(0.45f, 0.75f, 0.45f), iron).transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            foreach (float x in new[] { -0.65f, 0.65f })
                Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", t, new Vector3(x, 0.45f, 0f), new Vector3(0.8f, 0.08f, 0.8f), wood).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cannons.Add(new Cannon { pos = at, root = t, cd = 1f });
            AudioManager.PlaySfx(Sfx.Coin, 0.8f, 0.9f);
            if (!Started) Begin();
            return true;
        }

        public void AddBlockers(List<Vector3> into)
        {
            foreach (var p in parts) if (!p.dead && p.kind != PartKind.Tent) into.Add(new Vector3(p.pos.x, p.pos.y, p.radius * 0.8f));
        }

        public bool FoeNear(Vector2 at, float r)
        {
            foreach (var f in foes) if (!f.dead && (f.pos - at).magnitude < r) return true;
            foreach (var p in parts) if (!p.dead && (p.pos - at).magnitude < r + p.radius) return true;
            return false;
        }

        /// <summary>foi's blow: bandits and camp pieces within reach in front of it.</summary>
        public int Strike(Vector2 at, Vector2 facing, float damage, float reach = 2.6f)
        {
            if (!Started) Begin();
            int hits = 0;
            foreach (var f in foes)
            {
                if (f.dead) continue;
                var d = f.pos - at;
                if (d.magnitude > reach || (d.sqrMagnitude > 0.5f && Vector2.Dot(d.normalized, facing.normalized) < -0.2f)) continue;
                HurtFoe(f, damage);
                hits++;
            }
            if (hits == 0)
                foreach (var p in parts)
                {
                    if (p.dead || (p.pos - at).magnitude > reach + p.radius) continue;
                    HurtPart(p, damage);
                    hits++;
                    break;
                }
            return hits;
        }

        // ---------- Each frame ----------

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var foi = foiPos();
            // The towers wake when foi comes near (the operation is on).
            if (!Started && (foi - Middle).magnitude < (Fortress ? 17f : 13f)) Begin();
            UpdateShells(dt);
            if (!Started) return;
            UpdateTowers(dt, foi);
            UpdateFoes(dt, foi);
            UpdateAllies(dt, foi);
            UpdateCannons(dt);
            if (Left == 0) Win();
        }

        private void UpdateTowers(float dt, Vector2 foi)
        {
            foreach (var p in parts)
            {
                if (p.dead || p.kind != PartKind.Tower) continue;
                p.cd -= dt;
                if (p.cd > 0f) continue;
                Vector2 target; bool hasTarget = false; Ally ally = null; Cannon cannon = null; bool atFoi = false;
                float best = 9.5f;
                target = foi;
                if ((foi - p.pos).magnitude < best) { best = (foi - p.pos).magnitude; atFoi = true; hasTarget = true; }
                foreach (var a in allies) if (!a.dead && (a.pos - p.pos).magnitude < best) { best = (a.pos - p.pos).magnitude; ally = a; atFoi = false; cannon = null; target = a.pos; hasTarget = true; }
                foreach (var c in cannons) if (!c.dead && (c.pos - p.pos).magnitude < best) { best = (c.pos - p.pos).magnitude; cannon = c; ally = null; atFoi = false; target = c.pos; hasTarget = true; }
                if (!hasTarget) continue;
                p.cd = 1.6f;
                Tracer.Create(OwWorld.World(p.pos) + Vector3.up * 3.6f, OwWorld.World(target) + Vector3.up * 1f, new Color(1f, 0.7f, 0.3f));
                float dmg = 10f * strength;
                if (atFoi) hurtFoi(dmg); else if (ally != null) HurtAlly(ally, dmg); else if (cannon != null) HurtCannon(cannon, dmg);
            }
        }

        private void UpdateFoes(float dt, Vector2 foi)
        {
            foreach (var f in foes)
            {
                if (f.dead) continue;
                f.cd -= dt;
                // The nearest of foi, its soldiers and its cannons (within reach of the camp).
                Vector2 goal = foi; Ally ally = null; Cannon cannon = null;
                float best = (foi - f.pos).magnitude;
                foreach (var a in allies) if (!a.dead && (a.pos - f.pos).magnitude < best) { best = (a.pos - f.pos).magnitude; ally = a; cannon = null; goal = a.pos; }
                foreach (var c in cannons) if (!c.dead && (c.pos - f.pos).magnitude < best) { best = (c.pos - f.pos).magnitude; cannon = c; ally = null; goal = c.pos; }
                if ((goal - Middle).magnitude > 30f) goal = f.home; // never far from the camp
                var gap = goal - f.pos;
                if (gap.magnitude > f.range) { f.pos += gap.normalized * f.speed * dt; f.bot.SetRaise(0f); }
                else if (f.cd <= 0f && goal != f.home)
                {
                    f.cd = f.archer ? 1.4f : f.chief ? 1.3f : 0.9f;
                    f.bot.SetRaise(1f);
                    if (f.archer) Tracer.Create(OwWorld.World(f.pos) + Vector3.up * 1.2f, OwWorld.World(goal) + Vector3.up * 1f, new Color(1f, 0.7f, 0.3f));
                    if (ally != null) HurtAlly(ally, f.damage); else if (cannon != null) HurtCannon(cannon, f.damage); else hurtFoi(f.damage);
                    if (f.chief) Shockwave.Create(OwWorld.World(f.pos), 2.4f, new Color(1f, 0.3f, 0.2f));
                }
                f.root.position = OwWorld.World(f.pos);
                if (gap.sqrMagnitude > 0.01f) f.root.rotation = Quaternion.Slerp(f.root.rotation, Quaternion.LookRotation(new Vector3(gap.x, 0f, gap.y)), dt * 8f);
                var cam = Camera.main;
                if (cam != null) f.fill.parent.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            }
        }

        private void UpdateAllies(float dt, Vector2 foi)
        {
            int i = 0;
            foreach (var a in allies)
            {
                if (a.dead) continue;
                a.cd -= dt;
                Foe target = null; float best = 14f;
                foreach (var f in foes) if (!f.dead && (f.pos - a.pos).magnitude < best) { best = (f.pos - a.pos).magnitude; target = f; }
                Part part = null;
                if (target == null) foreach (var p in parts) if (!p.dead && (p.pos - a.pos).magnitude < 8f) { part = p; break; }
                Vector2 goal = target != null ? target.pos : part != null ? part.pos : foi + new Vector2((i % 4 - 1.5f) * 1.2f, -2f - (i / 4) * 1.2f);
                float reach = target != null || part != null ? (a.ranged ? 6f : 1.3f + (part != null ? part.radius : 0f)) : 0.3f;
                var gap = goal - a.pos;
                if (gap.magnitude > reach) { a.pos += gap.normalized * 3.8f * dt; a.model.SetRaise(0f); }
                else if (a.cd <= 0f && (target != null || part != null))
                {
                    a.cd = a.ranged ? 1.3f : 0.9f;
                    a.model.SetRaise(1f);
                    if (a.ranged) Tracer.Create(a.root.position + Vector3.up * 0.8f, OwWorld.World(goal) + Vector3.up * 1f, new Color(0.7f, 0.85f, 1f));
                    if (target != null) HurtFoe(target, a.damage); else HurtPart(part, a.damage);
                }
                a.root.position = OwWorld.World(a.pos);
                if (gap.sqrMagnitude > 0.01f) a.root.rotation = Quaternion.Slerp(a.root.rotation, Quaternion.LookRotation(new Vector3(gap.x, 0f, gap.y)), dt * 8f);
                i++;
            }
        }

        private void UpdateCannons(float dt)
        {
            foreach (var c in cannons)
            {
                if (c.dead) continue;
                c.cd -= dt;
                if (c.cd > 0f) continue;
                // Walls, towers and huts first (cannons are for them), then the bandits.
                Vector2? aim = null; float best = 15f;
                foreach (var p in parts) if (!p.dead && (p.pos - c.pos).magnitude < best) { best = (p.pos - c.pos).magnitude; aim = p.pos; }
                if (aim == null) foreach (var f in foes) if (!f.dead && (f.pos - c.pos).magnitude < best) { best = (f.pos - c.pos).magnitude; aim = f.pos; }
                if (aim == null) continue;
                c.cd = 2.6f;
                var to = aim.Value;
                c.root.rotation = Quaternion.LookRotation(new Vector3(to.x - c.pos.x, 0f, to.y - c.pos.y));
                ThrownRock.Create(c.root.position + Vector3.up * 1f, OwWorld.World(to) + Vector3.up * 0.8f, 0.7f, new Color(0.15f, 0.14f, 0.16f));
                shells.Add(new Shell { at = to, t = 0.7f, damage = 70f, splash = 2.2f });
                AudioManager.PlaySfx(Sfx.Impact, 0.8f, 0.5f);
            }
        }

        private void UpdateShells(float dt)
        {
            for (int i = shells.Count - 1; i >= 0; i--)
            {
                var s = shells[i];
                s.t -= dt;
                if (s.t > 0f) continue;
                shells.RemoveAt(i);
                var w = OwWorld.World(s.at);
                fx?.Burst(w + Vector3.up * 0.4f, new Color(1f, 0.55f, 0.2f), new Color(3f, 1.4f, 0.4f), 36, 7f);
                Shockwave.Create(w, s.splash * 1.4f, new Color(1f, 0.5f, 0.2f));
                AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
                foreach (var p in parts) if (!p.dead && (p.pos - s.at).magnitude < s.splash + p.radius) HurtPart(p, s.damage);
                foreach (var f in foes) if (!f.dead && (f.pos - s.at).magnitude < s.splash) HurtFoe(f, s.damage * 0.6f);
            }
        }

        // ---------- Hits ----------

        private void HurtFoe(Foe f, float damage)
        {
            if (f.dead) return;
            f.hp -= damage;
            f.bot.Flash();
            f.fill.localScale = new Vector3(Mathf.Max(0.001f, f.hp / f.maxHp), 1f, 1f);
            if (f.hp > 0f) return;
            f.dead = true;
            fx?.Burst(f.root.position + Vector3.up * 0.8f, new Color(1f, 0.5f, 0.3f), new Color(2.4f, 0.8f, 0.3f), f.chief ? 60 : 24, f.chief ? 8f : 5f);
            Destroy(f.root.gameObject);
        }

        private void HurtPart(Part p, float damage)
        {
            if (p.dead) return;
            p.hp -= damage;
            if (p.hp > 0f) return;
            p.dead = true;
            fx?.Burst(OwWorld.World(p.pos) + Vector3.up * 1f, new Color(0.5f, 0.35f, 0.2f), new Color(1f, 0.6f, 0.3f), 30, 6f);
            fx?.Dust(OwWorld.World(p.pos), new Color(0.45f, 0.38f, 0.32f), 24, 4f);
            AudioManager.PlaySfx(Sfx.Impact, 0.9f, 0.7f);
            Destroy(p.root.gameObject);
        }

        private void HurtAlly(Ally a, float damage)
        {
            a.hp -= damage;
            a.model.Flash();
            if (a.hp > 0f) return;
            a.dead = true;
            a.root.gameObject.SetActive(false);
            save.soldiers[a.group] = Mathf.Max(0, save.soldiers[a.group] - 1);
        }

        private void HurtCannon(Cannon c, float damage)
        {
            c.hp -= damage;
            if (c.hp > 0f) return;
            c.dead = true;
            fx?.Burst(c.root.position + Vector3.up * 0.6f, new Color(0.3f, 0.3f, 0.35f), new Color(1f, 0.6f, 0.3f), 20, 5f);
            Destroy(c.root.gameObject);
        }

        private bool over;

        private void Win()
        {
            if (over) return;
            over = true;
            // The camp falls: what still stands comes down.
            foreach (var p in parts) if (!p.dead) HurtPart(p, 1e9f);
            AudioManager.PlaySfx(Sfx.Win, 1f, 1f);
            Ended?.Invoke(true);
        }

        /// <summary>foi is down: the operation is lost (the camp stays, whole again next time).</summary>
        public void Lose()
        {
            if (over) return;
            over = true;
            Ended?.Invoke(false);
        }
    }
}
