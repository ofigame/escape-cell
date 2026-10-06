using System;
using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    public enum PowerUpType
    {
        Shield
    }

    /// <summary>Spawns rare power-up pickups on free tiles (from the levels that enable them).</summary>
    public class PowerUpSystem : MonoBehaviour
    {
        private class Pickup
        {
            public PowerUpType type;
            public GridPos pos;
            public float lifeLeft;
            public GameObject go;
        }

        public event Action<PowerUpType, GridPos> Collected;

        private readonly List<Pickup> pickups = new List<Pickup>();
        private GridModel grid;
        private LevelData level;
        private Robot robot;
        private HazardSystem hazards;
        private FxSystem fx;
        private Material coreMaterial;
        private Material haloMaterial;
        private float timer;
        private bool running;

        public void Init(Robot bot, HazardSystem hazardSystem, FxSystem fxSystem)
        {
            robot = bot;
            hazards = hazardSystem;
            fx = fxSystem;
            coreMaterial = MaterialFactory.Create(Palette.ShieldPickup, Palette.ShieldPickupGlow);
            haloMaterial = MaterialFactory.CreateTransparent(Palette.ShieldBubble, Palette.ShieldGlow * 0.6f);
        }

        public void Begin(GridModel model, LevelData data)
        {
            Stop();
            grid = model;
            level = data;
            timer = data.powerUpInterval * 0.6f;
            running = data.powerUpInterval > 0f;
        }

        public void Freeze() => running = false;

        public void Stop()
        {
            running = false;
            foreach (var p in pickups) Destroy(p.go);
            pickups.Clear();
        }

        public bool TryCollect(GridPos p)
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                if (pickups[i].pos != p) continue;
                var pickup = pickups[i];
                fx.Burst(pickup.go.transform.position, Palette.ShieldPickup, Palette.ShieldPickupGlow, 18, 3.5f);
                Destroy(pickup.go);
                pickups.RemoveAt(i);
                Collected?.Invoke(pickup.type, p);
                return true;
            }
            return false;
        }

        public void Smash(GridPos p)
        {
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                if (pickups[i].pos != p) continue;
                Destroy(pickups[i].go);
                pickups.RemoveAt(i);
            }
        }

        private void Update()
        {
            if (grid == null) return;
            float dt = Time.deltaTime;

            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var p = pickups[i];
                if (running) p.lifeLeft -= dt;
                if (p.lifeLeft <= 0f || grid.IsGap(p.pos))
                {
                    Destroy(p.go);
                    pickups.RemoveAt(i);
                    continue;
                }

                var tr = p.go.transform;
                tr.position = GridView.ToWorld(p.pos) + Vector3.up * (0.5f + Mathf.Sin(Time.time * 3f) * 0.07f);
                tr.GetChild(0).rotation = Quaternion.Euler(45f, Time.time * 120f, 45f);
                float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.06f;
                tr.GetChild(1).localScale = Vector3.one * 0.75f * pulse;
                p.go.SetActive(p.lifeLeft > 1.5f || Mathf.Repeat(p.lifeLeft, 0.25f) > 0.1f);
            }

            if (!running) return;
            timer -= dt;
            if (timer > 0f) return;
            timer = level.powerUpInterval;
            if (pickups.Count == 0 && !robot.IsShielded) Spawn(PowerUpType.Shield);
        }

        private void Spawn(PowerUpType type)
        {
            var options = new List<GridPos>();
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsStandable(p) || p.Manhattan(robot.Position) < 2 || hazards.IsThreatened(p)) continue;
                options.Add(p);
            }
            if (options.Count == 0) return;

            var pos = options[UnityEngine.Random.Range(0, options.Count)];
            var go = new GameObject("PowerUp " + type);
            go.transform.SetParent(transform, false);
            Shapes.Rounded("Core", go.transform, Vector3.zero, Vector3.one * 0.32f, 0.08f, coreMaterial);
            Shapes.Primitive(PrimitiveType.Sphere, "Halo", go.transform, Vector3.zero, Vector3.one * 0.75f, haloMaterial);
            fx.Burst(GridView.ToWorld(pos) + Vector3.up * 0.5f, Palette.ShieldPickup, Palette.ShieldPickupGlow, 8, 1.5f);
            pickups.Add(new Pickup { type = type, pos = pos, lifeLeft = level.powerUpLifetime, go = go });
        }
    }
}
