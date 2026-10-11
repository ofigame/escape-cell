using System;
using System.Collections;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The player's defences at work on a floor: each one wakes only while foi is in its stage, picks the nearest bug,
    /// robot or enemy tower in reach and fires — archers loose arrows, cannons lob shells that burst over the tiles
    /// round the target, towers shoot bolts (the tier-3 plasma tower a searing beam). Floor towers stand in the robots'
    /// way and are worn down by the ones next to them; a fallen one is gone for this try only.
    /// </summary>
    public class DefenseSystem : MonoBehaviour
    {
        private class Active
        {
            public Defenses.Placement placement;
            public DefenseSlots.Slot slot;
            public DefenseModel model;
            public int tier, hp, maxHp;
            public float cooldown, wear;
            public bool dead;
        }

        private readonly List<Active> active = new List<Active>();
        private HuntSystem hunt;
        private FxSystem fx;
        private GridModel grid;
        /// <summary>Strikes a tile for a defence (the game halves the coins for what it fells).</summary>
        public Action<GridPos, int> Strike;
        /// <summary>The fortress stage foi is in now.</summary>
        public Func<int> RobotStage;
        /// <summary>Defences only fight while this is true (the level is being played, not set up).</summary>
        public Func<bool> Running;

        public int Count => active.Count;

        public void Init(HuntSystem hunt, FxSystem fx)
        {
            this.hunt = hunt;
            this.fx = fx;
        }

        public void Clear()
        {
            foreach (var a in active) if (a.model != null) Destroy(a.model.gameObject);
            active.Clear();
            grid = null;
        }

        public void Spawn(GridModel grid, Defenses.Placement pl, DefenseSlots.Slot slot, int tier)
        {
            this.grid = grid;
            var m = DefenseModel.Build(pl.kind, tier, transform);
            m.transform.position = slot.world;
            // Facing into the stage: cannons away from their rampart, archers off their corner, towers north.
            var face = pl.kind == DefenseKind.Cannon ? new Vector3(-slot.dir.x, 0f, -slot.dir.y)
                : pl.kind == DefenseKind.Archer ? new Vector3(-slot.dir.x, 0f, -slot.dir.y) : Vector3.forward;
            m.transform.rotation = Quaternion.LookRotation(face);
            var a = new Active { placement = pl, slot = slot, model = m, tier = tier, cooldown = 1f + UnityEngine.Random.value };
            if (pl.kind == DefenseKind.Tower)
            {
                a.maxHp = a.hp = 10 + tier * 8;
                grid.SetOccupied(slot.tile, true);
            }
            active.Add(a);
        }

        public bool Remove(Defenses.Placement pl)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (!active[i].placement.SameSpot(pl)) continue;
                if (active[i].model != null) Destroy(active[i].model.gameObject);
                if (pl.kind == DefenseKind.Tower && grid != null) grid.SetOccupied(active[i].slot.tile, false);
                active.RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool Has(Defenses.Placement pl) => active.Exists(a => a.placement.SameSpot(pl));

        public int CountIn(int stage, DefenseKind kind) => active.FindAll(a => a.slot.stage == stage && a.placement.kind == kind).Count;

        // ---------- Stats by kind and tier (tier 0 = the simple archers of the first levels) ----------

        private static float Range(DefenseKind k, int t) => k == DefenseKind.Archer ? 3.5f + t * 0.5f : k == DefenseKind.Cannon ? 5.5f + t * 0.5f : 3.5f + t * 0.5f;
        private static float Interval(DefenseKind k, int t) => k == DefenseKind.Archer ? new[] { 1.6f, 1.2f, 0.9f, 0.7f }[t]
            : k == DefenseKind.Cannon ? new[] { 3.4f, 3.2f, 2.6f, 2.0f }[t] : new[] { 1.2f, 1.0f, 0.7f, 0.45f }[t];
        private static int Damage(DefenseKind k, int t) => k == DefenseKind.Archer ? (t >= 2 ? 2 : 1)
            : k == DefenseKind.Cannon ? new[] { 2, 2, 3, 5 }[t] : new[] { 1, 2, 3, 6 }[t];

        private void Update()
        {
            if (hunt == null || grid == null || active.Count == 0) return;
            bool live = hunt.Live && (Running == null || Running());
            int here = RobotStage != null ? RobotStage() : 0;
            float dt = Time.deltaTime;
            foreach (var a in active)
            {
                if (a.dead) continue;
                if (a.placement.kind == DefenseKind.Tower)
                {
                    grid.SetOccupied(a.slot.tile, true); // stands in the way, whoever stepped off beside it
                    a.model.SetHealth(a.hp / (float)a.maxHp);
                }
                if (!live || a.slot.stage != here) continue;
                if (a.placement.kind == DefenseKind.Tower) Wear(a, dt);
                if (a.dead) continue;
                a.cooldown -= dt;
                if (a.cooldown > 0f) continue;
                if (!hunt.NearestEnemy(a.slot.world, Range(a.placement.kind, a.tier), a.slot.stage, out var target, out var at))
                {
                    a.cooldown = 0.25f;
                    continue;
                }
                a.cooldown = Interval(a.placement.kind, a.tier);
                a.model.Aim(at);
                a.model.Fire();
                StartCoroutine(Shot(a, target, at));
            }
        }

        /// <summary>Robots beside a floor tower hit it; when it falls it is gone for this try.</summary>
        private void Wear(Active a, float dt)
        {
            a.wear += dt;
            if (a.wear < 1.2f) return;
            a.wear = 0f;
            int hits = hunt.PressureAt(a.slot.tile);
            if (hits <= 0) return;
            a.hp -= hits;
            a.model.SetHealth(a.hp / (float)a.maxHp);
            fx.Burst(a.model.transform.position + Vector3.up * 1f, new Color(0.6f, 0.8f, 1f), new Color(0.8f, 1.6f, 2.4f), 6, 2f);
            if (a.hp > 0) return;
            a.dead = true;
            grid.SetOccupied(a.slot.tile, false);
            Shockwave.Create(a.model.transform.position, 1.6f, new Color(0.5f, 0.8f, 1f));
            fx.Burst(a.model.transform.position + Vector3.up * 0.8f, new Color(0.6f, 0.8f, 1f), new Color(1f, 2f, 3f), 30, 5f);
            AudioManager.PlaySfx(Sfx.Impact, 0.8f, 1.1f);
            a.model.gameObject.SetActive(false);
        }

        private IEnumerator Shot(Active a, GridPos target, Vector3 at)
        {
            var kind = a.placement.kind;
            int dmg = Damage(kind, a.tier);
            var from = a.model.Muzzle;
            if (kind == DefenseKind.Tower && a.tier >= 3)
            {
                // The plasma beam: instant, searing, a burst where it lands.
                var beam = Beam(from, at, new Color(0.6f, 0.95f, 1f), 0.12f);
                AudioManager.PlaySfx(Sfx.Shield, 0.5f, 1.8f);
                Strike?.Invoke(target, dmg);
                fx.Burst(at, new Color(0.6f, 0.95f, 1f), new Color(1f, 2.4f, 3.2f), 14, 3.5f);
                for (float t = 0f; t < 0.15f; t += Time.deltaTime) yield return null;
                Destroy(beam);
                yield break;
            }
            float flight = kind == DefenseKind.Cannon ? 0.65f : kind == DefenseKind.Archer ? 0.35f : 0.18f;
            float arc = kind == DefenseKind.Cannon ? 2.2f : kind == DefenseKind.Archer ? 0.6f : 0f;
            var shot = Projectile(kind, a.tier);
            shot.position = from;
            AudioManager.PlaySfx(kind == DefenseKind.Cannon ? Sfx.Impact : Sfx.Hop, kind == DefenseKind.Cannon ? 0.5f : 0.35f, kind == DefenseKind.Cannon ? 0.7f : 2f);
            var prev = from;
            for (float t = 0f; t < 1f; t += Time.deltaTime / flight)
            {
                var p = Vector3.Lerp(from, at, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arc;
                if (shot == null) yield break;
                shot.position = p;
                if ((p - prev).sqrMagnitude > 0.0001f) shot.rotation = Quaternion.LookRotation(p - prev);
                prev = p;
                yield return null;
            }
            if (shot != null) Destroy(shot.gameObject);
            if (kind == DefenseKind.Cannon)
            {
                // The shell bursts over the target and the tiles round it.
                Shockwave.Create(at, 1.8f, new Color(1f, 0.6f, 0.3f));
                fx.Burst(at, new Color(1f, 0.6f, 0.3f), new Color(2.4f, 1.2f, 0.4f), 26, 4.5f);
                AudioManager.PlaySfx(Sfx.Impact, 0.8f, 0.8f);
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        Strike?.Invoke(target + new GridPos(x, y), x == 0 && y == 0 ? dmg : Mathf.Max(1, dmg - 1));
            }
            else
            {
                fx.Burst(at, new Color(0.7f, 0.9f, 1f), new Color(0.8f, 1.8f, 2.6f), 6, 2f);
                Strike?.Invoke(target, dmg);
            }
        }

        private Transform Projectile(DefenseKind kind, int tier)
        {
            var t = new GameObject("Shot").transform;
            t.SetParent(transform, false);
            switch (kind)
            {
                case DefenseKind.Archer:
                    Shapes.Rounded("Arrow", t, Vector3.zero, new Vector3(0.03f, 0.03f, 0.42f), 0.01f,
                        tier >= 2 ? MaterialFactory.Create(new Color(0.6f, 0.95f, 1f), new Color(0.8f, 2.2f, 3f)) : MaterialFactory.Create(new Color(0.75f, 0.6f, 0.4f), Color.black));
                    break;
                case DefenseKind.Cannon:
                    Shapes.Primitive(PrimitiveType.Sphere, "Shell", t, Vector3.zero, Vector3.one * (0.18f + tier * 0.03f),
                        MaterialFactory.Create(new Color(0.2f, 0.2f, 0.24f), tier >= 2 ? new Color(1.6f, 0.6f, 0.2f) : Color.black));
                    break;
                default:
                    Shapes.Primitive(PrimitiveType.Sphere, "Bolt", t, Vector3.zero, Vector3.one * 0.16f, MaterialFactory.Create(new Color(0.6f, 0.95f, 1f), new Color(1f, 2.6f, 3.4f)));
                    break;
            }
            return t;
        }

        private GameObject Beam(Vector3 from, Vector3 to, Color c, float width)
        {
            var mid = (from + to) * 0.5f;
            var len = Vector3.Distance(from, to);
            var go = Shapes.Rounded("Beam", transform, Vector3.zero, new Vector3(width, width, len), width * 0.45f, MaterialFactory.Create(c, c * 3f));
            go.transform.position = mid;
            go.transform.rotation = Quaternion.LookRotation(to - from);
            return go;
        }
    }
}
