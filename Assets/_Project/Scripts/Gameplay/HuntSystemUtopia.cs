using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The perfect city's crowd (level 101 on): guards behind energy shields that take a blow or two before the guard
    /// itself can be hurt, small repair drones that hover round the towers and mend them now and then (strike the
    /// drone down first, one blow is enough), and on every tenth floor the reactor core in the middle of the floor:
    /// a big tower that shoots a cross of five tiles. When the reactor falls, the whole floor lights up.
    /// </summary>
    public partial class HuntSystem
    {
        // ---------- Energy shields ----------

        /// <summary>A guard's shield took a blow (where; true when it broke).</summary>
        public event Action<GridPos, bool> ShieldHit;

        private static readonly Color ShieldColour = new Color(0.45f, 0.85f, 1f);

        private void AddShield(Guard g)
        {
            g.shield = Mathf.Max(1, level.shieldHits);
            g.bubble = new GameObject("Shield").transform;
            g.bubble.SetParent(g.root, false);
            float top = g.bar != null ? g.bar.localPosition.y - 0.28f : 1.2f;
            g.bubble.localPosition = new Vector3(0f, top * 0.5f, 0f);
            g.bubbleMat = MaterialFactory.CreateTransparent(new Color(ShieldColour.r, ShieldColour.g, ShieldColour.b, 0.35f), ShieldColour * 1.6f);
            Shapes.Primitive(PrimitiveType.Sphere, "Bubble", g.bubble, Vector3.zero, new Vector3(1f, 1.15f, 1f) * Mathf.Max(1.2f, top * 1.05f), g.bubbleMat);
        }

        private void HitShield(Guard g, GridPos p)
        {
            g.shield--;
            g.shieldFlash = 1f;
            g.windup = -1f;
            ClearTelegraph(g);
            bool broke = g.shield <= 0;
            var at = g.bubble != null ? g.bubble.position : At(p) + Vector3.up * 0.6f;
            fx.Burst(at, ShieldColour, ShieldColour * 2.4f, broke ? 40 : 18, broke ? 6f : 3.5f);
            Shockwave.Create(At(p), broke ? 1.8f : 1f, ShieldColour);
            AudioManager.PlaySfx(Sfx.Shield, 0.9f, broke ? 0.7f : 1.3f);
            rig.Shake(broke ? 0.45f : 0.2f);
            if (broke && g.bubble != null)
            {
                Destroy(g.bubble.gameObject);
                g.bubble = null;
            }
            ShieldHit?.Invoke(p, broke);
        }

        private void UpdateShield(Guard g, float dt)
        {
            if (g.bubble == null) return;
            g.shieldFlash = Mathf.Max(0f, g.shieldFlash - dt * 3f);
            float pulse = 0.85f + Mathf.Sin(Time.time * 4f + g.pos.x) * 0.15f;
            // Cracked shields glow weaker and flicker.
            float strength = g.shield / (float)Mathf.Max(1, level.shieldHits);
            float flicker = strength < 1f ? 0.7f + 0.3f * Mathf.Sin(Time.time * 23f) : 1f;
            float a = (0.24f + 0.16f * strength) * pulse * flicker + g.shieldFlash * 0.4f;
            MaterialFactory.SetColors(g.bubbleMat, new Color(ShieldColour.r, ShieldColour.g, ShieldColour.b, a), ShieldColour * (0.8f + strength * 0.6f + g.shieldFlash * 2f));
        }

        // ---------- Repair drones ----------

        private class Drone
        {
            public Tower home;
            public GridPos pos;
            public Transform root, rotor;
            public Vector3 from, to;
            public float moveT = 1f, wander, repair;
            public bool dead;
        }

        private readonly List<Drone> drones = new List<Drone>();

        /// <summary>Seconds between a drone's repairs (one point of the tower's health each).</summary>
        private float RepairInterval => Mathf.Lerp(4.5f, 3f, level.score / 100f);

        private static readonly Color RepairColour = new Color(0.45f, 1f, 0.6f);

        private void AddDrone(Tower home)
        {
            var spots = DroneSpots(home);
            if (spots.Count == 0) return;
            var d = new Drone { home = home, pos = spots[rng.Next(spots.Count)], wander = 1f + (float)rng.NextDouble() * 2f, repair = RepairInterval * (0.6f + (float)rng.NextDouble() * 0.6f) };
            d.root = new GameObject("RepairDrone").transform;
            d.root.SetParent(transform, false);
            var pearl = MaterialFactory.Create(new Color(0.94f, 0.95f, 0.97f), new Color(0.08f, 0.08f, 0.09f));
            var gold = MaterialFactory.Create(new Color(0.95f, 0.78f, 0.4f), new Color(0.35f, 0.24f, 0.06f));
            var glow = MaterialFactory.Create(RepairColour, RepairColour * 2.4f);
            var body = new GameObject("Body").transform;
            body.SetParent(d.root, false);
            body.localScale = Vector3.one * 1.3f;
            Shapes.Primitive(PrimitiveType.Sphere, "Hull", body, Vector3.zero, new Vector3(0.36f, 0.24f, 0.36f), pearl);
            Shapes.Primitive(PrimitiveType.Cylinder, "Band", body, Vector3.zero, new Vector3(0.38f, 0.03f, 0.38f), gold);
            Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(0f, 0f, 0.16f), Vector3.one * 0.1f, glow);
            Shapes.Primitive(PrimitiveType.Sphere, "Light", body, new Vector3(0f, -0.12f, 0f), new Vector3(0.14f, 0.06f, 0.14f), glow);
            Shapes.Rounded("Wrench", body, new Vector3(0.16f, -0.1f, 0.06f), new Vector3(0.04f, 0.16f, 0.04f), 0.015f, gold).transform.localRotation = Quaternion.Euler(30f, 0f, 20f);
            d.rotor = new GameObject("Rotor").transform;
            d.rotor.SetParent(body, false);
            d.rotor.localPosition = new Vector3(0f, 0.16f, 0f);
            foreach (float r in new[] { 0f, 90f })
                Shapes.Rounded("Blade", d.rotor, Vector3.zero, new Vector3(0.5f, 0.015f, 0.05f), 0.01f, gold).transform.localRotation = Quaternion.Euler(0f, r, 0f);
            d.from = d.to = DroneAt(d.pos, 0f);
            d.root.position = d.to;
            drones.Add(d);
        }

        /// <summary>Where a drone floats: a floor tile next to its tower.</summary>
        private Vector3 DroneAt(GridPos p, float time) => At(p) + Vector3.up * (1.1f + Mathf.Sin(time * 3f + p.x * 1.7f) * 0.12f);

        private List<GridPos> DroneSpots(Tower home)
        {
            var list = new List<GridPos>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                {
                    var p = home.pos + new GridPos(x, y);
                    if ((x != 0 || y != 0) && grid.IsFloor(p) && !grid.IsSafe(p) && !drones.Exists(o => !o.dead && o.pos == p)) list.Add(p);
                }
            return list;
        }

        private void UpdateDrones(float dt)
        {
            foreach (var d in drones)
            {
                if (d.dead) continue;
                d.rotor.localRotation = Quaternion.Euler(0f, Time.time * 1100f, 0f);
                if (d.moveT < 1f)
                {
                    d.moveT = Mathf.Min(1f, d.moveT + dt / 0.6f);
                    d.root.position = Vector3.Lerp(d.from, DroneAt(d.pos, Time.time), Mathf.SmoothStep(0f, 1f, d.moveT));
                }
                else d.root.position = DroneAt(d.pos, Time.time);
                var look = d.home.root.position - d.root.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) d.root.rotation = Quaternion.Slerp(d.root.rotation, Quaternion.LookRotation(look), dt * 5f);

                // Drift round the tower now and then.
                d.wander -= dt;
                if (d.wander <= 0f)
                {
                    d.wander = 1.6f + (float)rng.NextDouble() * 1.8f;
                    var spots = DroneSpots(d.home);
                    if (spots.Count > 0)
                    {
                        d.from = d.root.position;
                        d.pos = spots[rng.Next(spots.Count)];
                        d.moveT = 0f;
                    }
                }

                // Mend the tower: a green beam, a point of health back.
                if (d.home.hp >= d.home.maxHp) { d.repair = Mathf.Max(d.repair, 0.8f); continue; }
                d.repair -= dt;
                if (d.repair > 0f) continue;
                d.repair = RepairInterval;
                var home = d.home;
                TowerBolt.Fire(d.root.position, home.root.position + Vector3.up * 1f, RepairColour, 0.3f, () =>
                {
                    if (!running || home.dead) return;
                    home.hp = Mathf.Min(home.maxHp, home.hp + 1);
                    fx.Burst(home.root.position + Vector3.up * 1f, RepairColour, RepairColour * 2f, 12, 2.5f);
                    AudioManager.PlaySfx(Sfx.Shield, 0.4f, 1.8f);
                    TowerRepaired?.Invoke(home.pos);
                });
            }
        }

        /// <summary>A drone mended a tower (its tile).</summary>
        public event Action<GridPos> TowerRepaired;

        private bool DroneAt(GridPos p)
        {
            foreach (var d in drones) if (!d.dead && d.pos == p) return true;
            return false;
        }

        /// <summary>A blow on a tile: a drone there falls (one blow is enough, armour or not).</summary>
        private bool StrikeDrone(GridPos p)
        {
            foreach (var d in drones)
            {
                if (d.dead || d.pos != p) continue;
                KillDrone(d);
                Finished?.Invoke(p, false);
                return true;
            }
            return false;
        }

        private void KillDrone(Drone d)
        {
            d.dead = true;
            fx.Burst(d.root.position, RepairColour, RepairColour * 2f, 26, 4.5f);
            fx.Burst(d.root.position, new Color(0.95f, 0.8f, 0.45f), new Color(1.6f, 1.2f, 0.5f), 14, 3.5f);
            Shockwave.Create(At(d.pos), 0.9f, RepairColour);
            AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1.5f);
            Destroy(d.root.gameObject);
        }

        private void KillDronesOf(Tower t)
        {
            foreach (var d in drones) if (!d.dead && d.home == t) KillDrone(d);
        }

        private void StopDrones()
        {
            foreach (var d in drones) if (d.root != null) Destroy(d.root.gameObject);
            drones.Clear();
        }

        private void PickDrones(Action<Vector3, float, GridPos, float> tryBody)
        {
            foreach (var d in drones)
                if (!d.dead) tryBody(At(d.pos) + Vector3.up * 0.7f, 0.8f, d.pos, 0.9f);
        }

        // ---------- The reactor core ----------

        /// <summary>The reactor fell (its tile): the game lights the floor up.</summary>
        public event Action<GridPos> ReactorDown;

        private int ReactorHp => level.towerHp * 3 + 6;
        private const int ReactorRange = 9;
        private float ReactorHitShare => Mathf.Lerp(0.16f, 0.26f, level.score / 100f);

        private void ReactorFell(Tower t)
        {
            fx.Burst(At(t.pos) + Vector3.up * 1.4f, new Color(0.45f, 0.95f, 1f), new Color(1f, 2.6f, 3f), 90, 10f);
            Shockwave.Create(At(t.pos), 5f, new Color(0.5f, 0.95f, 1f));
            rig.Shake(1.2f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.4f);
            ReactorDown?.Invoke(t.pos);
        }
    }
}
