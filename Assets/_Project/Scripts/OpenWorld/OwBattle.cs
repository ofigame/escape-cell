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
    /// The farm's fights: the soldiers standing guard (they come out when bandits do), the towers and cannons firing,
    /// and a raid — bandits walking in from the wild side: raiders go for the crops and the barn, archers shoot from
    /// afar, bombers run at the walls and blow up, siege carts batter walls and buildings. Won when every bandit is
    /// down; lost if foi falls or the bandits are still there when the time runs out (they loot and leave).
    /// </summary>
    public class OwBattle : MonoBehaviour
    {
        private class Bandit
        {
            public BanditDef def;
            public Vector2 pos;
            public float hp, maxHp, cd;
            public Transform root, bar, fill;
            public HumanoidBot bot;
            public int defenceTarget = -1;
            public bool dead;
        }

        private class Soldier
        {
            public int group;
            public bool ranged;
            public Vector2 pos, home;
            public float hp, maxHp, damage, cd;
            public Transform root;
            public SoldierModel model;
            public bool dead;
        }

        private class Shell { public Vector2 at; public float t, damage, splash; }

        private OwSave save;
        private OwFarm farm;
        private FxSystem fx;
        private Func<Vector2> foiPos;
        private Action<float> hurtFoi;
        private readonly List<Bandit> bandits = new List<Bandit>();
        private readonly List<Soldier> soldiers = new List<Soldier>();
        private readonly List<Shell> shells = new List<Shell>();
        private readonly float[] towerCd = new float[256];
        private Material barBack, barFill;
        private System.Random rng = new System.Random();

        public bool Raiding { get; private set; }
        public float TimeLeft { get; private set; }
        public int Left { get { int n = 0; foreach (var b in bandits) if (!b.dead) n++; return n; } }
        public int Kills { get; private set; }
        public long Reward { get; private set; }
        public int Stolen { get; private set; }
        /// <summary>The raid is over: won (true) or lost.</summary>
        public event Action<bool> Ended;

        public static OwBattle Create(Transform parent, OwSave save, OwFarm farm, FxSystem fx, Func<Vector2> foiPos, Action<float> hurtFoi)
        {
            var go = new GameObject("OwBattle");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<OwBattle>();
            b.save = save; b.farm = farm; b.fx = fx; b.foiPos = foiPos; b.hurtFoi = hurtFoi;
            b.barBack = MaterialFactory.Create(new Color(0.08f, 0.06f, 0.1f), Color.black);
            b.barFill = MaterialFactory.Create(new Color(1f, 0.35f, 0.3f), new Color(1.6f, 0.4f, 0.3f));
            b.Garrison();
            return b;
        }

        private void OnDestroy()
        {
            if (barBack != null) Destroy(barBack);
            if (barFill != null) Destroy(barFill);
        }

        // ---------- The garrison ----------

        /// <summary>The soldiers at their posts in rows before the house (at most 24 shown; the rest still count in the
        /// offline battles).</summary>
        public void Garrison()
        {
            foreach (var s in soldiers) if (s.root != null) Destroy(s.root.gameObject);
            soldiers.Clear();
            int shown = 0;
            var front = farm.HouseAt + new Vector2(0f, -4f);
            for (int g = 0; g < Army.Groups; g++)
            {
                var kind = Army.KindOf(g);
                var st = Army.StatsOf(g);
                float hungry = save.hungry ? 0.6f : 1f;
                for (int k = 0; k < save.soldiers[g] && shown < 24; k++, shown++)
                {
                    var home = front + new Vector2((shown % 8 - 3.5f) * 1.2f, -(shown / 8) * 1.3f);
                    var model = SoldierModel.Build(transform, kind, Army.Heavy(kind) ? new Color(0.25f, 0.5f, 1f) : new Color(0.45f, 0.65f, 1f));
                    model.transform.localScale = Vector3.one * 0.62f * Army.SizeFor(g, st);
                    soldiers.Add(new Soldier
                    {
                        group = g, ranged = Army.Family(kind) == 2, pos = home, home = home,
                        hp = st.hp * 2f, maxHp = st.hp * 2f, damage = st.hit * 3f * hungry, root = model.transform, model = model,
                    });
                }
            }
            foreach (var s in soldiers) Place(s.root, s.pos, Vector2.down);
        }

        private static void Place(Transform t, Vector2 p, Vector2 face)
        {
            t.position = OwWorld.World(p);
            if (face.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(new Vector3(face.x, 0f, face.y));
        }

        // ---------- A raid ----------

        /// <summary>How many bandits come, by what the farm is worth.</summary>
        public int RaidSize => SizeOf(save);

        /// <summary>How many bandits come: by what the farm is worth, and more with bandit regions round it.</summary>
        public static int SizeOf(OwSave s) => Mathf.Clamp(Mathf.RoundToInt((4 + s.FarmValue() / 1500f) * (1f + 0.15f * (s.regions != null ? OwRegions.BanditNeighbours(s) : 0))), 4, 28);

        public void StartRaid()
        {
            if (Raiding) return;
            Raiding = true;
            TimeLeft = OwBattleData.RaidSeconds;
            Kills = 0; Reward = 0; Stolen = 0;
            int n = RaidSize;
            var dir = (OwWorld.FarmCentre - OwWorld.Centre).normalized;
            var side = new Vector2(-dir.y, dir.x);
            var spawn = OwWorld.FarmCentre + dir * (save.House.land * 0.5f + 28f);
            float scale = 1f + save.house * 0.15f;
            for (int i = 0; i < n; i++)
            {
                double r = rng.NextDouble();
                var kind = i == 0 && n >= 16 ? BanditKind.Chief : r < 0.5 ? BanditKind.Raider : r < 0.75 ? BanditKind.Archer : r < 0.9 || n < 8 ? BanditKind.Bomber : BanditKind.SiegeCart;
                var def = OwBattleData.Of(kind);
                var at = spawn + side * ((float)rng.NextDouble() * 24f - 12f) + dir * ((float)rng.NextDouble() * 8f);
                var b = new Bandit { def = def, pos = at, hp = def.hp * scale, maxHp = def.hp * scale, cd = 1f };
                Model(b);
                bandits.Add(b);
            }
            AudioManager.PlaySfx(Sfx.Warning, 1f, 0.8f);
        }

        private void Model(Bandit b)
        {
            var t = new GameObject("Bandit " + b.def.kind).transform;
            t.SetParent(transform, false);
            b.root = t;
            if (b.def.kind == BanditKind.SiegeCart)
            {
                var wood = MaterialFor(new Color(0.42f, 0.28f, 0.16f));
                Shapes.Rounded("Body", t, new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 1f, 2.6f), 0.1f, wood);
                Shapes.Rounded("Roof", t, new Vector3(0f, 1.6f, 0f), new Vector3(1.8f, 0.3f, 2.8f), 0.1f, MaterialFor(new Color(0.5f, 0.2f, 0.12f)));
                Shapes.Rounded("Ram", t, new Vector3(0f, 0.9f, 1.8f), new Vector3(0.4f, 0.4f, 1.6f), 0.15f, MaterialFor(new Color(0.3f, 0.3f, 0.32f)));
                foreach (float x in new[] { -0.9f, 0.9f }) foreach (float z in new[] { -0.8f, 0.8f })
                    Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", t, new Vector3(x, 0.4f, z), new Vector3(0.8f, 0.08f, 0.8f), wood).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                var accent = b.def.kind == BanditKind.Archer ? new Color(0.45f, 0.6f, 0.25f) : b.def.kind == BanditKind.Bomber ? new Color(0.9f, 0.25f, 0.15f) : b.def.kind == BanditKind.Chief ? new Color(1f, 0.15f, 0.1f) : new Color(0.85f, 0.5f, 0.2f);
                b.bot = HumanoidBot.Build(t, accent, b.def.kind == BanditKind.Chief ? 3 : 0);
                b.bot.transform.localScale = Vector3.one * (b.def.kind == BanditKind.Chief ? 1.35f : 0.8f);
                if (b.def.kind == BanditKind.Bomber)
                    Shapes.Primitive(PrimitiveType.Sphere, "Bomb", t, new Vector3(0f, 1.2f, -0.35f), Vector3.one * 0.55f, MaterialFor(new Color(0.12f, 0.1f, 0.12f)));
            }
            b.bar = new GameObject("Bar").transform;
            b.bar.SetParent(t, false);
            b.bar.localPosition = new Vector3(0f, b.def.kind == BanditKind.Chief ? 3.2f : 2.3f, 0f);
            Shapes.Rounded("Back", b.bar, Vector3.zero, new Vector3(1f, 0.14f, 0.04f), 0.05f, barBack);
            b.fill = new GameObject("Fill").transform;
            b.fill.SetParent(b.bar, false);
            b.fill.localPosition = new Vector3(-0.46f, 0f, -0.03f);
            Shapes.Rounded("Fill", b.fill, new Vector3(0.46f, 0f, 0f), new Vector3(0.92f, 0.09f, 0.03f), 0.04f, barFill);
            Place(t, b.pos, Vector2.zero);
        }

        private readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        private Material MaterialFor(Color c)
        {
            if (!mats.TryGetValue(c, out var m)) { mats[c] = m = MaterialFactory.Create(c, Color.black); OwnedMaterials.Own(gameObject, m); }
            return m;
        }

        // ---------- Each frame ----------

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UpdateShells(dt);
            UpdateSoldiers(dt);
            if (!Raiding) return;
            TimeLeft -= dt;
            UpdateBandits(dt);
            UpdateTowers(dt);
            if (Left == 0) Finish(true);
            else if (TimeLeft <= 0f) Finish(false);
        }

        private bool Blocks(DefenceState d) => d.hp > 0 && OwBattleData.Of((DefenceKind)d.kind).blocks;

        private Vector2 DefenceAt(DefenceState d) => new Vector2(d.x, d.z);

        private void UpdateBandits(float dt)
        {
            var foi = foiPos();
            foreach (var b in bandits)
            {
                if (b.dead) continue;
                b.cd -= dt;
                // What it goes for.
                Vector2 goal; int defence = -1; Soldier sol = null; bool atFoi = false; int plot = -1; bool barn = false;
                float near = 1e9f;
                // Anyone close enough to strike first: foi or a soldier.
                if ((foi - b.pos).magnitude < Mathf.Max(b.def.range, 2.2f)) { atFoi = true; near = (foi - b.pos).magnitude; }
                foreach (var s in soldiers) if (!s.dead) { float d = (s.pos - b.pos).magnitude; if (d < near && d < Mathf.Max(b.def.range, 2.2f)) { near = d; sol = s; atFoi = false; } }
                goal = foi;
                if (!atFoi && sol == null)
                {
                    switch (b.def.kind)
                    {
                        case BanditKind.Raider:
                        case BanditKind.Chief:
                            near = 1e9f;
                            for (int i = 0; i < save.plots.Count; i++) if (save.plots[i].crop >= 0) { var c = OwFarm.CellCentre(new Vector2Int(save.plots[i].x, save.plots[i].z)); float d = (c - b.pos).sqrMagnitude; if (d < near) { near = d; plot = i; goal = c; } }
                            { float d = (farm.BarnAt - b.pos).sqrMagnitude; if (d < near || plot < 0) { near = d; plot = -1; barn = true; goal = farm.BarnAt; } }
                            break;
                        case BanditKind.Archer:
                            near = 1e9f;
                            for (int i = 0; i < save.defences.Count; i++) { var d = save.defences[i]; if (d.hp <= 0 || OwBattleData.Of((DefenceKind)d.kind).damage <= 0) continue; float dd = (DefenceAt(d) - b.pos).sqrMagnitude; if (dd < near) { near = dd; defence = i; goal = DefenceAt(d); } }
                            foreach (var s in soldiers) if (!s.dead) { float dd = (s.pos - b.pos).sqrMagnitude; if (dd < near) { near = dd; sol = s; defence = -1; goal = s.pos; } }
                            if ((foi - b.pos).sqrMagnitude < near) { sol = null; defence = -1; goal = foi; atFoi = true; }
                            break;
                        default: // bombers and carts: the nearest defence, else the house
                            near = 1e9f;
                            for (int i = 0; i < save.defences.Count; i++) { var d = save.defences[i]; if (d.hp <= 0) continue; float dd = (DefenceAt(d) - b.pos).sqrMagnitude; if (dd < near) { near = dd; defence = i; goal = DefenceAt(d); } }
                            if (defence < 0) { goal = farm.BarnAt; barn = true; }
                            break;
                    }
                }
                else if (sol != null) goal = sol.pos;
                // A wall or fence in the way is broken first.
                var toGoal = goal - b.pos;
                for (int i = 0; i < save.defences.Count; i++)
                {
                    var d = save.defences[i];
                    if (!Blocks(d)) continue;
                    var dp = DefenceAt(d) - b.pos;
                    float r = OwBattleData.Of((DefenceKind)d.kind).radius + 0.8f;
                    if (dp.magnitude < r + 0.4f && Vector2.Dot(dp, toGoal) > 0f) { defence = i; goal = DefenceAt(d); atFoi = false; sol = null; plot = -1; barn = false; break; }
                }
                // Walk on, or strike.
                float reach = defence >= 0 ? OwBattleData.Of((DefenceKind)save.defences[defence].kind).radius + b.def.range : plot >= 0 ? 1.2f : barn ? 2.6f : b.def.range;
                var gap = goal - b.pos;
                if (gap.magnitude > reach)
                {
                    b.pos += gap.normalized * b.def.speed * dt;
                    if (b.bot != null) b.bot.SetRaise(0f);
                }
                else if (b.cd <= 0f)
                {
                    b.cd = b.def.cooldown;
                    if (b.bot != null) b.bot.SetRaise(1f);
                    Strike(b, defence, sol, atFoi, plot, barn, goal);
                }
                b.root.position = OwWorld.World(b.pos);
                if (gap.sqrMagnitude > 0.01f) b.root.rotation = Quaternion.Slerp(b.root.rotation, Quaternion.LookRotation(new Vector3(gap.x, 0f, gap.y)), dt * 8f);
                var cam = Camera.main != null ? Camera.main : null;
                if (cam != null) b.bar.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            }
        }

        private void Strike(Bandit b, int defence, Soldier sol, bool atFoi, int plot, bool barn, Vector2 goal)
        {
            var from = OwWorld.World(b.pos) + Vector3.up * 1.2f;
            var to = OwWorld.World(goal) + Vector3.up * 1f;
            if (b.def.range > 3f) Tracer.Create(from, to, new Color(1f, 0.7f, 0.3f));
            if (b.def.kind == BanditKind.Bomber)
            {
                // It blows up where it stands: walls, towers, soldiers and foi round it.
                Explode(b.pos, b.def.splash, b.def.damage, true);
                Kill(b, false);
                return;
            }
            if (defence >= 0) HurtDefence(defence, b.def.damage * (b.def.kind == BanditKind.SiegeCart ? 2f : 1f));
            else if (sol != null) HurtSoldier(sol, b.def.damage);
            else if (atFoi) hurtFoi(b.def.damage);
            else if (plot >= 0)
            {
                // Raiders pull up the crop.
                save.plots[plot].crop = -1;
                fx?.Burst(to, new Color(0.45f, 0.78f, 0.3f), new Color(0.6f, 1f, 0.4f), 10, 3f);
            }
            else if (barn)
            {
                // Out of the barn: a few things carried off.
                for (int k = 0; k < 3; k++)
                {
                    int i = rng.Next(save.items.Length);
                    if (save.items[i] > 0) { save.items[i]--; Stolen++; }
                }
                fx?.Burst(to, new Color(1f, 0.8f, 0.4f), new Color(1.6f, 1.2f, 0.5f), 8, 3f);
            }
            AudioManager.PlaySfx(Sfx.Impact, 0.5f, 1.2f);
        }

        private void HurtDefence(int i, float damage)
        {
            var d = save.defences[i];
            if (d.hp <= 0) return;
            d.hp = Mathf.Max(0, d.hp - Mathf.RoundToInt(damage));
            farm.DefenceHit(i, d.hp <= 0);
        }

        private void HurtSoldier(Soldier s, float damage)
        {
            s.hp -= damage;
            s.model?.Flash();
            if (s.hp > 0f) return;
            s.dead = true;
            fx?.Burst(s.root.position + Vector3.up * 0.5f, new Color(0.4f, 0.6f, 1f), new Color(0.6f, 0.9f, 1.6f), 16, 4f);
            s.root.gameObject.SetActive(false);
            // A fallen soldier is gone from the farm (bought again with money).
            save.soldiers[s.group] = Mathf.Max(0, save.soldiers[s.group] - 1);
        }

        private void Explode(Vector2 at, float r, float damage, bool hurtsOwn)
        {
            var w = OwWorld.World(at);
            fx?.Burst(w + Vector3.up * 0.4f, new Color(1f, 0.55f, 0.2f), new Color(3f, 1.4f, 0.4f), 40, 7f);
            Shockwave.Create(w, r * 1.4f, new Color(1f, 0.5f, 0.2f));
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
            if (hurtsOwn)
            {
                for (int i = 0; i < save.defences.Count; i++) if ((DefenceAt(save.defences[i]) - at).magnitude < r + 1f) HurtDefence(i, damage);
                foreach (var s in soldiers) if (!s.dead && (s.pos - at).magnitude < r) HurtSoldier(s, damage * 0.5f);
                if ((foiPos() - at).magnitude < r) hurtFoi(damage * 0.4f);
            }
            else foreach (var b in bandits) if (!b.dead && (b.pos - at).magnitude < r) Hurt(b, damage);
        }

        private void UpdateTowers(float dt)
        {
            bool watch = save.defences.Exists(d => d.kind == (int)DefenceKind.Watchtower && d.hp > 0);
            for (int i = 0; i < save.defences.Count && i < towerCd.Length; i++)
            {
                var d = save.defences[i];
                var def = OwBattleData.Of((DefenceKind)d.kind);
                if (def.damage <= 0 || d.hp <= 0) continue;
                towerCd[i] -= dt;
                if (towerCd[i] > 0f) continue;
                float range = def.range + (watch ? 2f : 0f);
                var at = DefenceAt(d);
                Bandit best = null; float bd = range;
                foreach (var b in bandits) if (!b.dead) { float dd = (b.pos - at).magnitude; if (dd < bd) { bd = dd; best = b; } }
                if (best == null) continue;
                towerCd[i] = def.cooldown;
                float dmg = def.damage * OwBattleData.LevelScale(d.level);
                var top = OwWorld.World(at) + Vector3.up * (d.kind == (int)DefenceKind.ArcherTower || d.kind == (int)DefenceKind.FireTower ? 3.2f : 1f);
                var aim = OwWorld.World(best.pos) + Vector3.up * 1f;
                switch ((DefenceKind)d.kind)
                {
                    case DefenceKind.ArcherTower:
                        Tracer.Create(top, aim, new Color(0.9f, 0.85f, 0.6f));
                        Hurt(best, dmg);
                        break;
                    case DefenceKind.FireTower:
                        ZapFx.Create(top, aim, new Color(1f, 0.5f, 0.15f));
                        fx?.Burst(aim, new Color(1f, 0.5f, 0.15f), new Color(3f, 1.2f, 0.3f), 14, 3f);
                        foreach (var b in bandits) if (!b.dead && (b.pos - best.pos).magnitude < def.splash) Hurt(b, dmg);
                        break;
                    default: // cannons: a shell in an arc, a blast where it lands
                        ThrownRock.Create(top, aim, 0.7f, new Color(0.15f, 0.14f, 0.16f));
                        shells.Add(new Shell { at = best.pos, t = 0.7f, damage = dmg, splash = def.splash });
                        AudioManager.PlaySfx(Sfx.Impact, 0.7f, 0.5f);
                        break;
                }
            }
        }

        private void UpdateShells(float dt)
        {
            for (int i = shells.Count - 1; i >= 0; i--)
            {
                shells[i].t -= dt;
                if (shells[i].t > 0f) continue;
                Explode(shells[i].at, shells[i].splash, shells[i].damage, false);
                shells.RemoveAt(i);
            }
        }

        private void UpdateSoldiers(float dt)
        {
            foreach (var s in soldiers)
            {
                if (s.dead) continue;
                // No raid and home: nothing to do (the land's height is not asked for two dozen soldiers every frame).
                if (!Raiding && (s.home - s.pos).sqrMagnitude < 0.04f) continue;
                s.cd -= dt;
                Bandit best = null; float bd = 26f;
                if (Raiding) foreach (var b in bandits) if (!b.dead) { float d = (b.pos - s.pos).magnitude; if (d < bd && (b.pos - OwWorld.FarmCentre).magnitude < 40f) { bd = d; best = b; } }
                var goal = best != null ? best.pos : s.home;
                float reach = best == null ? 0.2f : s.ranged ? 6f : 1.3f;
                var gap = goal - s.pos;
                if (gap.magnitude > reach) { s.pos += gap.normalized * 3.6f * dt; s.model.SetRaise(0f); }
                else if (best != null && s.cd <= 0f)
                {
                    s.cd = s.ranged ? 1.3f : 0.9f;
                    s.model.SetRaise(1f);
                    if (s.ranged) Tracer.Create(s.root.position + Vector3.up * 0.8f, best.root.position + Vector3.up * 1f, new Color(0.7f, 0.85f, 1f));
                    Hurt(best, s.damage);
                }
                s.root.position = OwWorld.World(s.pos);
                var face = best != null ? best.pos - s.pos : gap.sqrMagnitude > 0.01f ? gap : Vector2.down;
                if (face.sqrMagnitude > 0.01f) s.root.rotation = Quaternion.Slerp(s.root.rotation, Quaternion.LookRotation(new Vector3(face.x, 0f, face.y)), dt * 8f);
            }
        }

        private void Hurt(Bandit b, float damage)
        {
            if (b.dead) return;
            b.hp -= damage;
            b.bot?.Flash();
            b.fill.localScale = new Vector3(Mathf.Max(0.001f, b.hp / b.maxHp), 1f, 1f);
            if (b.hp <= 0f) Kill(b, true);
        }

        private void Kill(Bandit b, bool byUs)
        {
            b.dead = true;
            if (byUs) { Kills++; Reward += b.def.reward; }
            fx?.Burst(b.root.position + Vector3.up * 0.8f, new Color(1f, 0.5f, 0.3f), new Color(2.4f, 0.8f, 0.3f), 24, 5f);
            Destroy(b.root.gameObject);
        }

        /// <summary>foi's blow: every bandit within reach in front of it (a wide arc).</summary>
        public int Strike(Vector2 at, Vector2 facing, float damage, float reach = 2.6f)
        {
            int hits = 0;
            foreach (var b in bandits)
            {
                if (b.dead) continue;
                var d = b.pos - at;
                if (d.magnitude > reach || (d.sqrMagnitude > 0.5f && Vector2.Dot(d.normalized, facing.normalized) < -0.2f)) continue;
                Hurt(b, damage);
                hits++;
            }
            return hits;
        }

        public bool BanditNear(Vector2 at, float r) { foreach (var b in bandits) if (!b.dead && (b.pos - at).magnitude < r) return true; return false; }

        /// <summary>foi is down: what is left of the raid loots and goes.</summary>
        public void FoiDown() { if (Raiding) Finish(false); }

        private void Finish(bool won)
        {
            if (!Raiding) return;
            Raiding = false;
            foreach (var b in bandits) if (!b.dead && b.root != null) Destroy(b.root.gameObject);
            bandits.Clear();
            shells.Clear();
            foreach (var s in soldiers) if (!s.dead) { s.pos = s.home; s.hp = s.maxHp; Place(s.root, s.home, Vector2.down); }
            Ended?.Invoke(won);
        }

        // ---------- While away ----------

        /// <summary>
        /// A raid that came while the game was closed, fought out by the farm alone: the towers' and cannons' fire, the
        /// soldiers, the walls' strength against the bandits' health and blows. Returns won, bandits beaten.
        /// </summary>
        public static (bool won, int kills, int count) Simulate(OwSave save, int seed)
        {
            var r = new System.Random(seed);
            int n = SizeOf(save);
            float scale = 1f + save.house * 0.15f;
            float attack = n * (55f * 0.6f + 9f * 5f) * scale;
            float defence = 0f;
            foreach (var d in save.defences)
            {
                if (d.hp <= 0) continue;
                var def = OwBattleData.Of((DefenceKind)d.kind);
                if (def.damage > 0) defence += def.damage / def.cooldown * OwBattleData.LevelScale(d.level) * (def.splash > 0f ? 2f : 1f) * 20f;
                else defence += d.hp * 0.08f;
            }
            for (int g = 0; g < Army.Groups; g++) defence += save.soldiers[g] * Army.StatsOf(g).hit * 3f * (save.hungry ? 0.6f : 1f) * 10f;
            float luck = 0.8f + (float)r.NextDouble() * 0.4f;
            bool won = defence * luck >= attack;
            int kills = won ? n : Mathf.Clamp(Mathf.RoundToInt(n * defence * luck / Mathf.Max(1f, attack)), 0, n - 1);
            return (won, kills, n);
        }
    }
}
