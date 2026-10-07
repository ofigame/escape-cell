using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>Spawns short-lived coins on free tiles; the robot collects them by landing on them.</summary>
    public class CoinSystem : MonoBehaviour
    {
        /// <summary>On big platforms, pickups only appear this close to the robot (0 = anywhere).</summary>
        public int FocusRadius;

        private class Coin
        {
            public GridPos pos;
            public float lifeLeft;
            public float age;
            public GameObject go;
            /// <summary>Coin rain: the gold ring on the tile where the coin is about to land.</summary>
            public GameObject ring;
        }

        public event Action<GridPos> Collected;

        private readonly List<Coin> coins = new List<Coin>();
        private GridModel grid;
        private LevelData level;
        private Robot robot;
        private HazardSystem hazards;
        private FxSystem fx;
        private Material coinMaterial;
        private Material rimMaterial;
        private float timer;
        private bool running;

        public void Init(Robot bot, HazardSystem hazardSystem, FxSystem fxSystem)
        {
            robot = bot;
            hazards = hazardSystem;
            fx = fxSystem;
            coinMaterial = MaterialFactory.Create(Palette.Coin, Palette.CoinGlow);
            rimMaterial = MaterialFactory.Create(Palette.CoinRim, Palette.CoinGlow * 0.4f);
        }

        public void Begin(GridModel model, LevelData data)
        {
            Stop();
            grid = model;
            level = data;
            timer = 0.6f;
            running = true;
        }

        public void Freeze() => running = false;

        /// <summary>Picks up again after a Freeze (the player continued after losing).</summary>
        public void Resume() => running = grid != null;

        public void Stop()
        {
            running = false;
            foreach (var c in coins) DestroyCoin(c);
            coins.Clear();
        }

        public bool TryCollect(GridPos p)
        {
            for (int i = 0; i < coins.Count; i++)
            {
                if (coins[i].pos != p) continue;
                fx.Burst(coins[i].go.transform.position, Palette.Coin, Palette.CoinGlow, 10, 3f);
                DestroyCoin(coins[i]);
                coins.RemoveAt(i);
                AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1f, 0.04f);
                Haptics.Pulse(18, 0.4f);
                Collected?.Invoke(p);
                return true;
            }
            return false;
        }

        /// <summary>Coin magnet: coins within <paramref name="range"/> tiles of the robot are pulled in too.</summary>
        public void CollectNear(GridPos p, int range)
        {
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                if (coins[i].pos == p || coins[i].pos.Manhattan(p) > range) continue;
                var c = coins[i];
                fx.Burst(c.go.transform.position, Palette.Coin, Palette.CoinGlow, 8, 2.5f);
                DestroyCoin(c);
                coins.RemoveAt(i);
                AudioManager.PlaySfx(Sfx.Coin, 0.6f, 1.15f, 0.04f);
                Collected?.Invoke(c.pos);
            }
        }

        /// <summary>A block landed here: any coin on the tile is lost.</summary>
        /// <summary>Where coins lie right now.</summary>
        public IEnumerable<GridPos> Positions
        {
            get { foreach (var c in coins) yield return c.pos; }
        }

        /// <summary>Someone else (Kuzgun) takes the coin on <paramref name="p"/>: it vanishes with a sparkle. True if there was one.</summary>
        public bool Take(GridPos p)
        {
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                if (coins[i].pos != p) continue;
                fx.Burst(coins[i].go.transform.position, Palette.Coin, Palette.CoinGlow, 10, 3f);
                DestroyCoin(coins[i]);
                coins.RemoveAt(i);
                return true;
            }
            return false;
        }

        public void Smash(GridPos p)
        {
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                if (coins[i].pos != p) continue;
                DestroyCoin(coins[i]);
                coins.RemoveAt(i);
            }
        }

        private void Update()
        {
            if (grid == null) return;
            float dt = Time.deltaTime;

            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var c = coins[i];
                if (running) c.lifeLeft -= dt;
                if (c.lifeLeft <= 0f || grid.IsGap(c.pos))
                {
                    DestroyCoin(c);
                    coins.RemoveAt(i);
                    continue;
                }

                var tr = c.go.transform;
                // Pop in with a little overshoot when the coin appears.
                c.age += dt;
                float pop = c.age < 0.32f ? EaseOutBack(c.age / 0.32f) : 1f;
                tr.localScale = Vector3.one * pop;
                // Face the camera (cylinder axis toward the viewer) and wobble gently, so the coin always reads as a coin.
                var cam = Camera.main;
                var facing = cam != null ? Quaternion.FromToRotation(Vector3.up, -cam.transform.forward) : Quaternion.Euler(90f, 0f, 0f);
                tr.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * 3f + c.pos.y) * 25f, Vector3.up) * facing;
                // Falls into place; in a coin rain from high up, its landing tile ringed in gold first.
                bool rain = level.mission == MissionType.CoinRain;
                float fall = rain ? RainFall : 0.25f;
                float drop = Mathf.Pow(1f - Mathf.Clamp01(c.age / fall), 2f) * (rain ? 4.5f : 1.4f);
                if (c.ring != null && c.age >= fall) { Destroy(c.ring); c.ring = null; }
                tr.position = GridView.ToWorld(c.pos) + Vector3.up * (0.4f + drop + Mathf.Sin(Time.time * 4f + c.pos.x) * 0.06f);
                // blink when about to vanish
                c.go.SetActive(c.lifeLeft > 1.5f || Mathf.Repeat(c.lifeLeft, 0.25f) > 0.1f);
            }

            if (!running) return;
            timer -= dt;
            if (timer > 0f) return;
            timer = level.coinInterval / Mathf.Max(0.1f, SpawnBoost);
            if (coins.Count < level.maxCoins) SpawnCoin();
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 2.2f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        /// <summary>Drops a coin on a tile right away (gold cart, supply crate), ignoring the usual limit.</summary>
        public void Drop(GridPos p, float lifetime)
        {
            if (grid == null || !grid.IsStandable(p) || coins.Exists(c => c.pos == p)) return;
            var go = new GameObject("Coin");
            go.transform.SetParent(transform, false);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", go.transform, Vector3.zero, new Vector3(0.46f, 0.035f, 0.46f), rimMaterial);
            Shapes.Primitive(PrimitiveType.Cylinder, "Face", go.transform, Vector3.zero, new Vector3(0.36f, 0.045f, 0.36f), coinMaterial);
            coins.Add(new Coin { pos = p, lifeLeft = lifetime, go = go });
        }

        /// <summary>WARDEN alarm: coins come faster while it lasts.</summary>
        public float SpawnBoost = 1f;

        /// <summary>Coin rain: how long a coin takes to fall (its landing ring shows meanwhile).</summary>
        private const float RainFall = 0.6f;

        private void DestroyCoin(Coin c)
        {
            if (c.ring != null) Destroy(c.ring);
            Destroy(c.go);
        }
        private Material ringMaterial;

        private void SpawnCoin()
        {
            var options = new List<GridPos>();
            foreach (var p in grid.AllPositions())
            {
                if (FocusRadius > 0 && (Mathf.Abs(p.x - robot.Position.x) > FocusRadius || Mathf.Abs(p.y - robot.Position.y) > FocusRadius)) continue;
                if (!grid.IsStandable(p) || p == robot.Position || hazards.IsThreatened(p)) continue;
                if (coins.Exists(c => c.pos == p)) continue;
                options.Add(p);
            }
            if (options.Count == 0) return;

            var pos = options[UnityEngine.Random.Range(0, options.Count)];
            var go = new GameObject("Coin");
            go.transform.SetParent(transform, false);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", go.transform, Vector3.zero, new Vector3(0.46f, 0.035f, 0.46f), rimMaterial);
            Shapes.Primitive(PrimitiveType.Cylinder, "Face", go.transform, Vector3.zero, new Vector3(0.36f, 0.045f, 0.36f), coinMaterial);
            GameObject ring = null;
            if (level.mission == MissionType.CoinRain)
            {
                ring = Shapes.Primitive(PrimitiveType.Cylinder, "LandingRing", transform, GridView.ToWorld(pos) + Vector3.up * (GridView.SurfaceY + 0.03f),
                    new Vector3(0.7f, 0.01f, 0.7f), ringMaterial ?? (ringMaterial = MaterialFactory.CreateTransparent(new Color(1f, 0.85f, 0.3f, 0.6f), new Color(2f, 1.5f, 0.3f))));
            }
            coins.Add(new Coin { pos = pos, lifeLeft = level.coinLifetime + (ring != null ? RainFall : 0f), go = go, ring = ring });
        }
    }
}
