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
        private class Coin
        {
            public GridPos pos;
            public float lifeLeft;
            public GameObject go;
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

        public void Stop()
        {
            running = false;
            foreach (var c in coins) Destroy(c.go);
            coins.Clear();
        }

        public bool TryCollect(GridPos p)
        {
            for (int i = 0; i < coins.Count; i++)
            {
                if (coins[i].pos != p) continue;
                fx.Burst(coins[i].go.transform.position, Palette.Coin, Palette.CoinGlow, 10, 3f);
                Destroy(coins[i].go);
                coins.RemoveAt(i);
                AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1f, 0.04f);
                Haptics.Pulse(18, 0.4f);
                Collected?.Invoke(p);
                return true;
            }
            return false;
        }

        /// <summary>A block landed here: any coin on the tile is lost.</summary>
        public void Smash(GridPos p)
        {
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                if (coins[i].pos != p) continue;
                Destroy(coins[i].go);
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
                    Destroy(c.go);
                    coins.RemoveAt(i);
                    continue;
                }

                var tr = c.go.transform;
                // Face the camera (cylinder axis toward the viewer) and wobble gently, so the coin always reads as a coin.
                var cam = Camera.main;
                var facing = cam != null ? Quaternion.FromToRotation(Vector3.up, -cam.transform.forward) : Quaternion.Euler(90f, 0f, 0f);
                tr.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * 3f + c.pos.y) * 25f, Vector3.up) * facing;
                tr.position = GridView.ToWorld(c.pos) + Vector3.up * (0.4f + Mathf.Sin(Time.time * 4f + c.pos.x) * 0.06f);
                // blink when about to vanish
                c.go.SetActive(c.lifeLeft > 1.5f || Mathf.Repeat(c.lifeLeft, 0.25f) > 0.1f);
            }

            if (!running) return;
            timer -= dt;
            if (timer > 0f) return;
            timer = level.coinInterval;
            if (coins.Count < level.maxCoins) SpawnCoin();
        }

        private void SpawnCoin()
        {
            var options = new List<GridPos>();
            foreach (var p in grid.AllPositions())
            {
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
            coins.Add(new Coin { pos = pos, lifeLeft = level.coinLifetime, go = go });
        }
    }
}
