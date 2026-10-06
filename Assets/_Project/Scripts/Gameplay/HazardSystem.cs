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
    /// Warns, drops, holds and removes whatever falls from the sky: the world's own block (crate, ice cube, meteor...)
    /// and, from world 2, bombs whose blast covers a plus shape. Every wave is checked for an escape route.
    /// </summary>
    public class HazardSystem : MonoBehaviour
    {
        private const float FallDuration = 0.22f;
        private const float DropHeight = 9f;
        private const float MinSolidFraction = 0.6f;
        private const float BlockRestY = GridView.SurfaceY + 0.43f;
        private const float BombRestY = GridView.SurfaceY + 0.36f;

        private enum Phase { Warning, Falling, Landed }
        private enum Kind { Block, Bomb }

        private class Hazard
        {
            public Kind kind;
            public GridPos pos;                 // where it falls
            public List<GridPos> area;          // tiles it hits (just pos for a block, a plus for a bomb)
            public Phase phase;
            public float timeToImpact;
            public float lingerLeft;
            public readonly List<GameObject> markers = new List<GameObject>();
            public GameObject body;
            public bool shattered;
        }

        private class Repair
        {
            public GridPos pos;
            public float timeLeft;
            public bool fire;
        }

        /// <summary>Raised for every tile hit, the moment the hazard lands (once per tile of a bomb's blast).</summary>
        public event Action<GridPos> Impact;

        /// <summary>Raised when a tile breaks into a hole.</summary>
        public event Action<GridPos> TileBroken;

        public bool AnyWarningActive { get; private set; }

        private readonly List<Hazard> hazards = new List<Hazard>();
        private readonly List<Repair> repairs = new List<Repair>();

        private GridModel grid;
        private GridView gridView;
        private Robot robot;
        private FxSystem fx;
        private CameraRig cameraRig;
        private LevelData level;
        private Func<float> progress;
        private int world;

        private bool running;
        private float spawnTimer;
        private int lastWarningSfxFrame = -1;

        private Material markerMaterial;
        private Material bombMarkerMaterial;
        private Material beamMaterial;

        private static readonly Color BombGlow = new Color(2.4f, 1.1f, 0.2f);

        public void Init(GridView view, Robot bot, FxSystem fxSystem, CameraRig rig)
        {
            gridView = view;
            robot = bot;
            fx = fxSystem;
            cameraRig = rig;
            markerMaterial = MaterialFactory.Create(Palette.Marker, Palette.MarkerGlow);
            bombMarkerMaterial = MaterialFactory.Create(new Color(1f, 0.75f, 0.4f), BombGlow);
            beamMaterial = MaterialFactory.Create(Palette.Marker, Palette.MarkerGlow * 0.3f);
        }

        public void Begin(GridModel model, LevelData data, Func<float> missionProgress, int worldIndex)
        {
            Stop();
            grid = model;
            level = data;
            progress = missionProgress;
            world = worldIndex;
            spawnTimer = 1.0f; // short breather before the first wave
            running = true;
        }

        /// <summary>Stops spawning; hazards already in the air finish their fall.</summary>
        public void Freeze() => running = false;

        public void Stop()
        {
            running = false;
            foreach (var h in hazards) DestroyVisuals(h);
            hazards.Clear();
            repairs.Clear();
            AnyWarningActive = false;
        }

        /// <summary>Smash the landed block on <paramref name="p"/> (armored robot hit by it, or hopping into it).</summary>
        public bool Shatter(GridPos p)
        {
            bool any = false;
            foreach (var h in hazards)
            {
                if (h.kind != Kind.Block || h.pos != p || h.phase != Phase.Landed || h.shattered) continue;
                any = true;
                h.shattered = true;
                h.body.SetActive(false);
                grid.SetOccupied(p, false);
                var at = GridView.ToWorld(p) + Vector3.up * BlockRestY;
                fx.Burst(at, Palette.Block, Palette.BlockGlow, 26, 6f);
                fx.Burst(at, Palette.ShieldPickup, Palette.ShieldPickupGlow, 14, 4f);
            }
            return any;
        }

        public bool IsThreatened(GridPos p)
        {
            foreach (var h in hazards)
                if (h.area.Contains(p)) return true;
            return false;
        }

        private float PaceMultiplier => 1f + level.rampUp * Mathf.Clamp01(progress());
        private float CurrentWarning => level.warningTime / (1f + (PaceMultiplier - 1f) * 0.5f);

        private void Update()
        {
            if (grid == null) return;
            float dt = Time.deltaTime;

            if (running)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    SpawnWave();
                    spawnTimer = level.spawnInterval / PaceMultiplier;
                }
                UpdateRepairs(dt);
            }

            AnyWarningActive = false;
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                if (UpdateHazard(hazards[i], dt))
                {
                    DestroyVisuals(hazards[i]);
                    hazards.RemoveAt(i);
                }
            }
        }

        // ---------- Spawning ----------

        private void SpawnWave()
        {
            var taken = new HashSet<GridPos>();
            foreach (var h in hazards) taken.UnionWith(h.area);

            var candidates = new List<GridPos>();
            foreach (var p in grid.AllPositions())
                if (grid.IsStandable(p) && !taken.Contains(p))
                    candidates.Add(p);

            float warning = CurrentWarning;
            int maxSteps = Mathf.Clamp(Mathf.FloorToInt((warning - 0.35f) / 0.25f), 1, 3);

            var pending = new HashSet<GridPos>();
            foreach (var h in hazards)
                if (h.phase != Phase.Landed) pending.UnionWith(h.area);

            if (level.lineWaveChance > 0f && UnityEngine.Random.value < level.lineWaveChance
                && TrySpawnLine(candidates, pending, warning, maxSteps))
                return;

            if (level.bombChance > 0f && UnityEngine.Random.value < level.bombChance
                && TrySpawnBomb(candidates, taken, pending, warning, maxSteps))
                return;

            for (int count = Mathf.Min(level.blocksPerWave, candidates.Count); count > 0; count--)
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    var chosen = PickTargets(candidates, count);
                    var danger = new HashSet<GridPos>(pending);
                    danger.UnionWith(chosen);

                    if (!SafetyChecker.HasEscape(grid, robot.Position, danger, maxSteps)) continue;

                    foreach (var p in chosen) CreateBlock(p, warning);
                    return;
                }
            }
            // No fair wave possible this tick: skip it.
        }

        /// <summary>A whole row or column comes down at once, minus one or two gaps to dodge through.</summary>
        private bool TrySpawnLine(List<GridPos> candidates, HashSet<GridPos> pending, float warning, int maxSteps)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                bool row = UnityEngine.Random.value < 0.5f;
                int index = UnityEngine.Random.Range(0, row ? grid.Height : grid.Width);
                var line = candidates.FindAll(p => row ? p.y == index : p.x == index);
                if (line.Count < 3) continue;

                int gaps = line.Count >= 5 ? 2 : 1;
                for (int g = 0; g < gaps; g++) line.RemoveAt(UnityEngine.Random.Range(0, line.Count));

                var danger = new HashSet<GridPos>(pending);
                danger.UnionWith(line);
                if (!SafetyChecker.HasEscape(grid, robot.Position, danger, maxSteps)) continue;

                // A sweeping line gets a little extra warning so it reads as a pattern, not a wall.
                foreach (var p in line) CreateBlock(p, warning + 0.15f);
                return true;
            }
            return false;
        }

        /// <summary>One bomb whose blast covers its tile and the four neighbours. Often aimed near the robot.</summary>
        private bool TrySpawnBomb(List<GridPos> candidates, HashSet<GridPos> taken, HashSet<GridPos> pending, float warning, int maxSteps)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                GridPos center = attempt < 3 && candidates.Contains(robot.Position)
                    ? robot.Position + DirectionExtensions.All[UnityEngine.Random.Range(0, 4)].ToOffset()
                    : candidates[UnityEngine.Random.Range(0, candidates.Count)];
                if (!candidates.Contains(center)) continue;

                var area = new List<GridPos> { center };
                foreach (var d in DirectionExtensions.All)
                {
                    var n = center + d.ToOffset();
                    if (grid.InBounds(n) && grid.GetTile(n) == TileState.Solid && !taken.Contains(n)) area.Add(n);
                }

                var danger = new HashSet<GridPos>(pending);
                danger.UnionWith(area);
                if (!SafetyChecker.HasEscape(grid, robot.Position, danger, maxSteps)) continue;

                // Bombs fuse a touch longer: the blast is bigger, so reading it needs a moment more.
                CreateBomb(center, area, warning + 0.2f);
                return true;
            }
            return false;
        }

        private List<GridPos> PickTargets(List<GridPos> candidates, int count)
        {
            var pool = new List<GridPos>(candidates);
            var chosen = new List<GridPos>();

            if (UnityEngine.Random.value < level.aimAtPlayerChance && pool.Remove(robot.Position))
                chosen.Add(robot.Position);

            while (chosen.Count < count && pool.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                chosen.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return chosen;
        }

        private GameObject CreateMarker(GridPos p, Material material, bool beam)
        {
            var marker = new GameObject("Warning");
            marker.transform.position = GridView.ToWorld(p) + Vector3.up * (GridView.SurfaceY + 0.01f);
            marker.transform.rotation = Quaternion.Euler(0f, 45f, 0f); // '+' reads upright on screen
            Shapes.Rounded("PlusA", marker.transform, Vector3.zero, new Vector3(0.5f, 0.02f, 0.13f), 0.01f, material);
            Shapes.Rounded("PlusB", marker.transform, Vector3.zero, new Vector3(0.13f, 0.02f, 0.5f), 0.01f, material);
            if (beam)
                Shapes.Cube("Beam", marker.transform, new Vector3(0f, DropHeight * 0.5f, 0f), new Vector3(0.03f, DropHeight, 0.03f), beamMaterial);
            return marker;
        }

        private void PlayWarningBeep()
        {
            if (lastWarningSfxFrame == Time.frameCount) return; // one beep per wave, not per block
            lastWarningSfxFrame = Time.frameCount;
            AudioManager.PlaySfx(Sfx.Warning, 0.45f, 1f, 0.05f);
        }

        private void CreateBlock(GridPos p, float warning)
        {
            PlayWarningBeep();
            var h = new Hazard
            {
                kind = Kind.Block,
                pos = p,
                area = new List<GridPos> { p },
                phase = Phase.Warning,
                timeToImpact = warning,
                body = HazardVisuals.Block(world),
            };
            h.markers.Add(CreateMarker(p, markerMaterial, true));
            h.body.SetActive(false);
            hazards.Add(h);
        }

        private void CreateBomb(GridPos center, List<GridPos> area, float warning)
        {
            PlayWarningBeep();
            var h = new Hazard
            {
                kind = Kind.Bomb,
                pos = center,
                area = area,
                phase = Phase.Warning,
                timeToImpact = warning,
                body = HazardVisuals.Bomb(),
            };
            foreach (var p in area) h.markers.Add(CreateMarker(p, bombMarkerMaterial, p == center));
            h.body.SetActive(false);
            hazards.Add(h);
        }

        // ---------- Updating ----------

        /// <returns>true when the hazard is finished and can be removed.</returns>
        private bool UpdateHazard(Hazard h, float dt)
        {
            var at = GridView.ToWorld(h.pos);
            float restY = h.kind == Kind.Bomb ? BombRestY : BlockRestY;

            switch (h.phase)
            {
                case Phase.Warning:
                case Phase.Falling:
                {
                    h.timeToImpact -= dt;
                    AnyWarningActive = true;

                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 18f);
                    foreach (var p in h.area) gridView.SetWarning(p, pulse);

                    if (h.timeToImpact <= FallDuration)
                    {
                        h.phase = Phase.Falling;
                        h.body.SetActive(true);
                        float t = 1f - Mathf.Clamp01(h.timeToImpact / FallDuration);
                        h.body.transform.position = at + Vector3.up * (restY + DropHeight * (1f - t * t));
                        if (h.kind == Kind.Bomb) h.body.transform.Rotate(0f, 0f, 400f * dt);
                    }

                    if (h.timeToImpact <= 0f)
                    {
                        if (h.kind == Kind.Bomb) Explode(h, at);
                        else Land(h, at);
                    }
                    return false;
                }
                case Phase.Landed:
                {
                    if (h.shattered || h.kind == Kind.Bomb) return true;
                    h.lingerLeft -= dt;
                    var tr = h.body.transform;
                    if (h.lingerLeft < 0.25f)
                    {
                        float s = Mathf.Clamp01(h.lingerLeft / 0.25f);
                        tr.localScale = new Vector3(s, s, s);
                    }
                    else
                    {
                        // settle bounce
                        tr.localScale = Vector3.Lerp(tr.localScale, Vector3.one, dt * 14f);
                    }

                    if (h.lingerLeft > 0f) return false;

                    grid.SetOccupied(h.pos, false);
                    if (!TryBreakTile(h.pos))
                        fx.Burst(at + Vector3.up * 0.3f, Palette.Block, Palette.BlockGlow, 6, 2f);
                    return true;
                }
            }
            return true;
        }

        private void Land(Hazard h, Vector3 at)
        {
            h.phase = Phase.Landed;
            h.lingerLeft = level.blockLinger;
            foreach (var m in h.markers) m.SetActive(false);
            h.body.transform.position = at + Vector3.up * BlockRestY;
            h.body.transform.localScale = new Vector3(1.22f, 0.72f, 1.22f);
            grid.SetOccupied(h.pos, true);

            fx.Burst(at + Vector3.up * 0.15f, Palette.Block, Palette.BlockGlow, 16, 4.5f);
            cameraRig.Shake(0.5f);
            AudioManager.PlaySfx(Sfx.Impact, 0.8f, 1f, 0.1f);
            if (h.pos.Manhattan(robot.Position) <= 1) Haptics.Pulse(22, 0.5f); // feel the near misses, not every distant block
            Impact?.Invoke(h.pos);
        }

        private void Explode(Hazard h, Vector3 at)
        {
            h.phase = Phase.Landed;
            foreach (var m in h.markers) m.SetActive(false);
            h.body.SetActive(false);

            foreach (var p in h.area)
            {
                var cell = GridView.ToWorld(p) + Vector3.up * 0.2f;
                fx.Burst(cell, new Color(1f, 0.6f, 0.2f), BombGlow, p == h.pos ? 26 : 12, p == h.pos ? 6f : 4f);
            }
            cameraRig.Shake(0.9f);
            cameraRig.Punch(0.8f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.75f);
            AudioManager.PlaySfx(Sfx.Squash, 0.5f, 1.4f);
            Haptics.Pulse(30, 0.6f);

            foreach (var p in h.area) Impact?.Invoke(p);
            TryBreakTile(h.pos);
        }

        private bool TryBreakTile(GridPos p)
        {
            if (!level.breakTiles || !running || !CanBreakTile() || grid.GetTile(p) != TileState.Solid) return false;

            // Some tiles catch fire for a few seconds instead of breaking into a hole.
            bool fire = level.fireChance > 0f && UnityEngine.Random.value < level.fireChance;
            grid.SetTile(p, fire ? TileState.Fire : TileState.Broken);
            if (fire) gridView.Ignite(p);
            else gridView.Break(p);
            TileBroken?.Invoke(p);
            repairs.Add(new Repair { pos = p, timeLeft = fire ? level.fireDuration : level.tileRepairTime, fire = fire });
            return true;
        }

        private bool CanBreakTile()
        {
            int minSolid = Mathf.CeilToInt(grid.TileCount * MinSolidFraction);
            return grid.SolidCount() > minSolid;
        }

        private void UpdateRepairs(float dt)
        {
            for (int i = repairs.Count - 1; i >= 0; i--)
            {
                repairs[i].timeLeft -= dt;
                if (repairs[i].timeLeft > 0f) continue;

                grid.SetTile(repairs[i].pos, TileState.Solid);
                if (repairs[i].fire) gridView.Extinguish(repairs[i].pos);
                else gridView.Repair(repairs[i].pos);
                repairs.RemoveAt(i);
            }
        }

        private static void DestroyVisuals(Hazard h)
        {
            foreach (var m in h.markers)
                if (m != null) Destroy(m);
            if (h.body != null) Destroy(h.body);
        }
    }
}
