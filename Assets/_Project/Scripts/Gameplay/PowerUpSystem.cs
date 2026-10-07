using System;
using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Skill pickups. More kinds unlock floor by floor and they show up more often later, so harder levels hand the
    /// player more tools to survive with: shield (level 4), rescue (floor 2), time freeze (floor 4), blast (floor 7),
    /// magnet (floor 10).
    /// </summary>
    public enum PowerUpType
    {
        Shield,
        Rescue,
        Freeze,
        Blast,
        Magnet,
        /// <summary>A heart: heals part of the health bar (health levels only).</summary>
        Heart,
        /// <summary>Super: a few seconds untouchable and fast (the hardest floors only, rare).</summary>
        Super
    }

    /// <summary>Spawns rare power-up pickups on free tiles (from the levels that enable them).</summary>
    public class PowerUpSystem : MonoBehaviour
    {
        /// <summary>On big platforms, pickups only appear this close to the robot (0 = anywhere).</summary>
        public int FocusRadius;

        private class Pickup
        {
            public PowerUpType type;
            public GridPos pos;
            public float lifeLeft;
            public GameObject go;
        }

        public event Action<PowerUpType, GridPos> Collected;
        /// <summary>The game wants hearts on the board (the health bar is not full).</summary>
        public Func<bool> WantsHeart;

        private readonly List<Pickup> pickups = new List<Pickup>();
        private GridModel grid;
        private LevelData level;
        private Robot robot;
        private HazardSystem hazards;
        private FxSystem fx;
        private Material coreMaterial;
        private Material haloMaterial;
        private Material[] typeCore, typeHalo;
        private int world;
        private float timer;
        private bool running;

        public void Init(Robot bot, HazardSystem hazardSystem, FxSystem fxSystem)
        {
            robot = bot;
            hazards = hazardSystem;
            fx = fxSystem;
            coreMaterial = MaterialFactory.Create(Palette.ShieldPickup, Palette.ShieldPickupGlow);
            haloMaterial = MaterialFactory.CreateTransparent(Palette.ShieldBubble, Palette.ShieldGlow * 0.6f);
            var colors = new[] { Palette.ShieldPickup, new Color(0.4f, 1f, 0.6f), new Color(0.55f, 0.85f, 1f), new Color(1f, 0.5f, 0.3f), new Color(1f, 0.35f, 0.4f), new Color(1f, 0.42f, 0.58f), new Color(1f, 0.82f, 0.3f) };
            typeCore = new Material[colors.Length];
            typeHalo = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                typeCore[i] = MaterialFactory.Create(colors[i], colors[i] * 1.8f);
                typeHalo[i] = MaterialFactory.CreateTransparent(new Color(colors[i].r, colors[i].g, colors[i].b, 0.3f), colors[i] * 0.8f);
            }
        }

        public void Begin(GridModel model, LevelData data, int worldIndex = 0)
        {
            Stop();
            grid = model;
            level = data;
            world = worldIndex;
            timer = data.powerUpInterval * 0.6f;
            running = data.powerUpInterval > 0f;
        }

        public void Freeze() => running = false;

        /// <summary>Picks up again after a Freeze (the player continued after losing).</summary>
        public void Resume() => running = grid != null;

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
            // Later floors keep two skills on the board at once.
            int maxOnBoard = world >= 8 ? 2 : 1;
            if (pickups.Count < maxOnBoard) Spawn(Pick());
        }

        /// <summary>A small white sign above the pickup so each skill can be told apart at a glance.</summary>
        private static void Icon(Transform parent, PowerUpType type)
        {
            var icon = new GameObject("Icon").transform;
            icon.SetParent(parent, false);
            icon.localPosition = new Vector3(0f, 0.55f, 0f);
            var white = MaterialFactory.Create(Color.white, new Color(1.6f, 1.6f, 1.6f));
            switch (type)
            {
                case PowerUpType.Shield:
                    for (int i = 0; i < 8; i++)
                        Shapes.Rounded("Ring", icon, new Vector3(Mathf.Cos(i * 0.785f) * 0.12f, Mathf.Sin(i * 0.785f) * 0.12f, 0f), new Vector3(0.06f, 0.06f, 0.04f), 0.02f, white);
                    break;
                case PowerUpType.Rescue:
                    Shapes.Rounded("V", icon, Vector3.zero, new Vector3(0.07f, 0.26f, 0.05f), 0.02f, white);
                    Shapes.Rounded("H", icon, Vector3.zero, new Vector3(0.26f, 0.07f, 0.05f), 0.02f, white);
                    break;
                case PowerUpType.Freeze:
                    for (int i = 0; i < 3; i++)
                        Shapes.Rounded("Arm", icon, Vector3.zero, new Vector3(0.05f, 0.3f, 0.04f), 0.02f, white).transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
                    break;
                case PowerUpType.Super:
                    for (int i = 0; i < 5; i++)
                        Shapes.Rounded("Ray", icon, Vector3.zero, new Vector3(0.07f, 0.34f, 0.05f), 0.03f, white).transform.localRotation = Quaternion.Euler(0f, 0f, i * 36f);
                    break;
                case PowerUpType.Heart:
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Lobe", icon, new Vector3(s * 0.06f, 0.02f, 0f), new Vector3(0.13f, 0.22f, 0.05f), 0.05f, white).transform.localRotation = Quaternion.Euler(0f, 0f, s * 40f);
                    break;
                case PowerUpType.Blast:
                    for (int i = 0; i < 4; i++)
                        Shapes.Rounded("Ray", icon, Vector3.zero, new Vector3(0.06f, 0.32f, 0.04f), 0.02f, white).transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                    break;
                default:
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Arm", icon, new Vector3(s * 0.09f, 0.03f, 0f), new Vector3(0.06f, 0.2f, 0.05f), 0.02f, white);
                    Shapes.Rounded("Bow", icon, new Vector3(0f, -0.08f, 0f), new Vector3(0.24f, 0.06f, 0.05f), 0.02f, white);
                    break;
            }
            icon.gameObject.AddComponent<Billboard>();
        }

        /// <summary>Turns a sign to face the camera.</summary>
        private class Billboard : MonoBehaviour
        {
            private void LateUpdate()
            {
                var cam = Camera.main;
                if (cam != null) transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            }
        }

        /// <summary>A skill the floor has unlocked; shields are always the most common.</summary>
        private PowerUpType Pick()
        {
            var pool = new List<PowerUpType> { PowerUpType.Shield, PowerUpType.Shield };
            if (robot.IsShielded) pool.Clear();
            if (world >= 1) pool.Add(PowerUpType.Rescue);
            if (world >= 3) { pool.Add(PowerUpType.Freeze); pool.Add(PowerUpType.Shield); }
            if (world >= 6) pool.Add(PowerUpType.Blast);
            if (world >= 9) pool.Add(PowerUpType.Magnet);
            if (pool.Count == 0) pool.Add(PowerUpType.Freeze);
            if (WantsHeart != null && WantsHeart()) { pool.Add(PowerUpType.Heart); pool.Add(PowerUpType.Heart); }
            // The super skill: only on the hardest floors, and only now and then.
            if (level.score >= 70 && UnityEngine.Random.value < 0.18f) return PowerUpType.Super;
            foreach (var p in pickups) if (pool.Count > 1) pool.Remove(p.type);
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        private void Spawn(PowerUpType type)
        {
            var options = new List<GridPos>();
            foreach (var p in grid.AllPositions())
            {
                if (FocusRadius > 0 && (Mathf.Abs(p.x - robot.Position.x) > FocusRadius || Mathf.Abs(p.y - robot.Position.y) > FocusRadius)) continue;
                if (!grid.IsStandable(p) || p.Manhattan(robot.Position) < 2 || hazards.IsThreatened(p)) continue;
                options.Add(p);
            }
            if (options.Count == 0) return;

            var pos = options[UnityEngine.Random.Range(0, options.Count)];
            var go = new GameObject("PowerUp " + type);
            go.transform.SetParent(transform, false);
            int t = (int)type;
            Shapes.Rounded("Core", go.transform, Vector3.zero, Vector3.one * 0.36f, 0.08f, typeCore[t]);
            Shapes.Primitive(PrimitiveType.Sphere, "Halo", go.transform, Vector3.zero, Vector3.one * 0.8f, typeHalo[t]);
            Icon(go.transform, type);
            fx.Burst(GridView.ToWorld(pos) + Vector3.up * 0.5f, Palette.ShieldPickup, Palette.ShieldPickupGlow, 8, 1.5f);
            pickups.Add(new Pickup { type = type, pos = pos, lifeLeft = level.powerUpLifetime, go = go });
        }
    }
}
