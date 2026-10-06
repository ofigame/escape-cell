using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// One surprise per level (when the level has one): a gold cart rolling across a row and spilling coins,
    /// a WARDEN alarm (faster waves, double coins), or a supply crate floating down on a parachute.
    /// </summary>
    public class LevelEvents : MonoBehaviour
    {
        /// <summary>The robot opened the supply crate.</summary>
        public event Action<GridPos> CrateOpened;
        /// <summary>The alarm started (true) or ended (false).</summary>
        public event Action<bool> AlarmChanged;
        /// <summary>The surprise begins (for a heads-up message).</summary>
        public event Action<LevelEvent> Started;

        private GridModel grid;
        private Robot robot;
        private CoinSystem coins;
        private HazardSystem hazards;
        private FxSystem fx;
        private CameraRig rig;
        private LevelEvent kind;
        private System.Random rng;
        private float startAt, time;
        private bool started, done, running;

        // Gold cart
        private Transform cart;
        private List<GridPos> path;
        private float cartPos;
        private int dropped = -1;

        // Alarm
        private float alarmLeft;
        public bool Alarm => alarmLeft > 0f;
        private const float AlarmSeconds = 6f;

        // Supply crate
        private Transform crate, chute;
        private GridPos crateAt;
        private float crateY;
        private bool crateLanded;

        public void Init(Robot bot, CoinSystem coinSystem, HazardSystem hazardSystem, FxSystem fxSystem, CameraRig cameraRig)
        {
            robot = bot;
            coins = coinSystem;
            hazards = hazardSystem;
            fx = fxSystem;
            rig = cameraRig;
        }

        public void Begin(GridModel model, LevelData level, int seed)
        {
            Stop();
            grid = model;
            kind = level.levelEvent;
            rng = new System.Random(seed * 17 + 3);
            startAt = 7f + (float)rng.NextDouble() * 7f;
            time = 0f;
            started = done = false;
            running = kind != LevelEvent.None;
        }

        public void Freeze() => running = false;
        public void Resume() => running = grid != null && kind != LevelEvent.None && !done;

        public void Stop()
        {
            running = false;
            if (cart != null) Destroy(cart.gameObject);
            if (crate != null) Destroy(crate.gameObject);
            cart = crate = chute = null;
            path = null;
            crateLanded = false;
            if (alarmLeft > 0f) EndAlarm();
        }

        private void Update()
        {
            if (!running || grid == null) return;
            float dt = Time.deltaTime;
            time += dt;
            if (!started && time >= startAt)
            {
                started = true;
                Started?.Invoke(kind);
                switch (kind)
                {
                    case LevelEvent.GoldCart: StartCart(); break;
                    case LevelEvent.Alarm: StartAlarm(); break;
                    case LevelEvent.SupplyCrate: StartCrate(); break;
                }
            }
            if (!started || done) return;
            switch (kind)
            {
                case LevelEvent.GoldCart: UpdateCart(dt); break;
                case LevelEvent.Alarm: UpdateAlarm(dt); break;
                case LevelEvent.SupplyCrate: UpdateCrate(dt); break;
            }
        }

        // ---------- Gold cart ----------

        private void StartCart()
        {
            // The longest row or column near the robot.
            path = null;
            for (int attempt = 0; attempt < 8 && path == null; attempt++)
            {
                bool row = rng.Next(2) == 0;
                int index = row ? Mathf.Clamp(robot.Position.y + rng.Next(-2, 3), 0, grid.Height - 1) : Mathf.Clamp(robot.Position.x + rng.Next(-2, 3), 0, grid.Width - 1);
                var line = new List<GridPos>();
                int n = row ? grid.Width : grid.Height;
                bool forward = rng.Next(2) == 0;
                for (int k = 0; k < n; k++)
                {
                    int c = forward ? k : n - 1 - k;
                    var p = row ? new GridPos(c, index) : new GridPos(index, c);
                    if (grid.Exists(p)) line.Add(p);
                }
                if (line.Count >= 3) path = line;
            }
            if (path == null) { done = true; return; }

            cart = new GameObject("GoldCart").transform;
            cart.SetParent(transform, false);
            var wood = MaterialFactory.Create(new Color(0.55f, 0.35f, 0.22f), Color.black);
            var metal = MaterialFactory.Create(new Color(0.35f, 0.33f, 0.42f), Color.black);
            var gold = MaterialFactory.Create(Palette.Coin, Palette.CoinGlow);
            Shapes.Rounded("Tub", cart, new Vector3(0f, 0.28f, 0f), new Vector3(0.62f, 0.3f, 0.5f), 0.06f, wood);
            Shapes.Rounded("Rim", cart, new Vector3(0f, 0.44f, 0f), new Vector3(0.66f, 0.05f, 0.54f), 0.02f, metal);
            Shapes.Rounded("Gold", cart, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.16f, 0.38f), 0.08f, gold);
            foreach (var w in new[] { new Vector2(-0.22f, -0.2f), new Vector2(0.22f, -0.2f), new Vector2(-0.22f, 0.2f), new Vector2(0.22f, 0.2f) })
                Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", cart, new Vector3(w.x, 0.12f, w.y), new Vector3(0.18f, 0.03f, 0.18f), metal)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var dir = new Vector3(path[1].x - path[0].x, 0f, path[1].y - path[0].y).normalized;
            cart.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up));
            cartPos = -1f;
            dropped = -1;
            AudioManager.PlaySfx(Sfx.Coin, 0.8f, 0.7f);
        }

        private void UpdateCart(float dt)
        {
            cartPos += dt * 1.8f;
            int k = Mathf.FloorToInt(cartPos);
            var a = path[Mathf.Clamp(k, 0, path.Count - 1)];
            var b = path[Mathf.Clamp(k + 1, 0, path.Count - 1)];
            float f = cartPos - k;
            var at = Vector3.Lerp(new Vector3(a.x, 0f, a.y), new Vector3(b.x, 0f, b.y), Mathf.Clamp01(f));
            if (cartPos < 0f) at -= new Vector3(path[1].x - path[0].x, 0f, path[1].y - path[0].y) * (-cartPos);
            cart.position = at + Vector3.up * (GridView.SurfaceY + Mathf.Abs(Mathf.Sin(time * 14f)) * 0.03f);

            // A coin spills onto every tile it rolls over.
            if (k > dropped && k >= 0 && k < path.Count)
            {
                dropped = k;
                coins.Drop(path[k], 4f);
                fx.Burst(cart.position + Vector3.up * 0.5f, Palette.Coin, Palette.CoinGlow, 6, 2f);
            }
            if (cartPos > path.Count + 1f)
            {
                Destroy(cart.gameObject);
                cart = null;
                done = true;
            }
        }

        // ---------- Alarm ----------

        private void StartAlarm()
        {
            alarmLeft = AlarmSeconds;
            hazards.PaceBoost = 1.6f;
            coins.SpawnBoost = 2.5f;
            rig.Shake(0.5f);
            AudioManager.PlaySfx(Sfx.Warning, 1f, 0.5f);
            AlarmChanged?.Invoke(true);
        }

        private void UpdateAlarm(float dt)
        {
            alarmLeft -= dt;
            if (alarmLeft <= 0f)
            {
                EndAlarm();
                done = true;
            }
        }

        private void EndAlarm()
        {
            alarmLeft = 0f;
            if (hazards != null) hazards.PaceBoost = 1f;
            if (coins != null) coins.SpawnBoost = 1f;
            AlarmChanged?.Invoke(false);
        }

        // ---------- Supply crate ----------

        private void StartCrate()
        {
            // A free tile a few steps from the robot: worth the trip, not across the whole platform.
            var options = new List<GridPos>();
            foreach (var p in grid.AllPositions())
            {
                int d = p.Manhattan(robot.Position);
                if (grid.IsStandable(p) && d >= 2 && d <= 4 && !hazards.IsThreatened(p)) options.Add(p);
            }
            if (options.Count == 0) { done = true; return; }
            crateAt = options[rng.Next(options.Count)];

            crate = new GameObject("SupplyCrate").transform;
            crate.SetParent(transform, false);
            var wood = MaterialFactory.Create(new Color(0.85f, 0.62f, 0.35f), Color.black);
            var band = MaterialFactory.Create(new Color(0.4f, 0.85f, 1f), new Color(0.4f, 1.4f, 1.8f));
            Shapes.Rounded("Box", crate, new Vector3(0f, 0.22f, 0f), new Vector3(0.44f, 0.44f, 0.44f), 0.06f, wood);
            Shapes.Rounded("Band", crate, new Vector3(0f, 0.22f, 0f), new Vector3(0.46f, 0.08f, 0.46f), 0.02f, band);
            Shapes.Rounded("Band2", crate, new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.46f, 0.46f), 0.02f, band);
            chute = new GameObject("Chute").transform;
            chute.SetParent(crate, false);
            var cloth = MaterialFactory.Create(new Color(1f, 0.45f, 0.5f), new Color(0.4f, 0.1f, 0.12f));
            Shapes.Primitive(PrimitiveType.Sphere, "Canopy", chute, new Vector3(0f, 1.25f, 0f), new Vector3(0.9f, 0.32f, 0.9f), cloth);
            var line = MaterialFactory.Create(Color.white, Color.black);
            foreach (var c in new[] { new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f), new Vector2(-0.3f, 0.3f), new Vector2(0.3f, 0.3f) })
            {
                var cord = Shapes.Rounded("Cord", chute, new Vector3(c.x * 0.6f, 0.82f, c.y * 0.6f), new Vector3(0.015f, 0.8f, 0.015f), 0.005f, line);
                cord.transform.localRotation = Quaternion.Euler(c.y * 30f, 0f, -c.x * 30f);
            }
            crateY = 5f;
            crateLanded = false;
            AudioManager.PlaySfx(Sfx.Shield, 0.6f, 0.8f);
        }

        private void UpdateCrate(float dt)
        {
            var ground = GridView.ToWorld(crateAt) + Vector3.up * GridView.SurfaceY;
            if (!crateLanded)
            {
                crateY = Mathf.MoveTowards(crateY, 0f, dt * 1.6f);
                crate.position = ground + Vector3.up * crateY + new Vector3(Mathf.Sin(time * 2f) * 0.08f * crateY / 5f, 0f, 0f);
                if (crateY <= 0f)
                {
                    crateLanded = true;
                    chute.gameObject.SetActive(false);
                    fx.Dust(ground + Vector3.up * 0.1f, new Color(0.85f, 0.62f, 0.35f), 10, 2f);
                }
                return;
            }
            crate.localScale = Vector3.one * (1f + Mathf.Sin(time * 5f) * 0.04f);
            // A block on the crate's tile smashes it open for nothing.
            if (grid.IsOccupied(crateAt) || grid.IsGap(crateAt))
            {
                fx.Burst(crate.position + Vector3.up * 0.2f, new Color(0.85f, 0.62f, 0.35f), Color.black, 14, 3f);
                Destroy(crate.gameObject);
                crate = null;
                done = true;
            }
        }

        /// <summary>The robot landed on <paramref name="p"/>: opens the crate when it is there.</summary>
        public void OnArrived(GridPos p)
        {
            if (crate == null || !crateLanded || p != crateAt) return;
            fx.Burst(crate.position + Vector3.up * 0.3f, Palette.UiGold, Palette.CoinGlow, 30, 5f);
            Destroy(crate.gameObject);
            crate = null;
            done = true;
            CrateOpened?.Invoke(p);
        }
    }
}
