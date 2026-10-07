using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The floor rules the scenario document adds for its later worlds. Like every hazard in the game each one reads
    /// warning → rule → counter-move: the blizzard closes the view in (the warnings still glow); mirror lasers draw
    /// their bent path before firing; cloud tiles pulse before they slide the robot on; lightning rings a tile, then
    /// strikes; glitch tiles pixelate, then swap places with whatever stands on them; gravity wells glow, then pull the
    /// robot a tile towards their centre (standing in the centre is fatal); the black hole's rim trembles before it
    /// grows over the tiles around it.
    /// </summary>
    public partial class FloorRules
    {
        private const float CloudEvery = 2f, LightningWarn = 1f, GlitchWarn = 1f, WellEvery = 2f;

        private readonly List<(GridPos pos, Direction dir, Transform view)> clouds = new List<(GridPos, Direction, Transform)>();
        private readonly List<GridPos> crystals = new List<GridPos>();
        private readonly List<(GridPos a, GridPos b)> glitchPairs = new List<(GridPos, GridPos)>();
        private readonly List<GridPos> wells = new List<GridPos>();
        private readonly List<(List<GridPos> path, float warn, float fire, List<Transform> bars, Material mat)> mirrorBeams =
            new List<(List<GridPos>, float, float, List<Transform>, Material)>();
        private readonly List<(GridPos pos, float left, Transform ring)> bolts = new List<(GridPos, float, Transform)>();
        private float cloudTimer, mirrorTimer, boltTimer, glitchTimer, glitchWarn = -1f, wellTimer, holeClock;
        private int glitchActive = -1;
        private GridPos holeCenter;
        private Transform holeView;
        private bool holeWide;
        private float holeHitCooldown;

        /// <summary>Sets up the scenario's newer rules (called from Begin).</summary>
        private void BeginWorldRules()
        {
            clouds.Clear();
            crystals.Clear();
            glitchPairs.Clear();
            wells.Clear();
            mirrorBeams.Clear();
            bolts.Clear();
            holeView = null;
            cloudTimer = CloudEvery;
            mirrorTimer = 2.5f;
            boltTimer = 2.5f;
            glitchTimer = 3f;
            glitchWarn = -1f;
            glitchActive = -1;
            wellTimer = WellEvery;
            holeClock = 0f;

            if (Has(FloorRule.Blizzard) && !Has(FloorRule.Dark)) view.Light = BlizzardLit;
            if (Has(FloorRule.MovingCloud)) PlaceClouds();
            if (Has(FloorRule.MirrorLaser)) PlaceCrystals();
            if (Has(FloorRule.Glitch)) PlaceGlitches();
            if (Has(FloorRule.GravityWell)) PlaceWells();
            if (Has(FloorRule.BlackHole)) PlaceHole();
        }

        private void UpdateWorldRules(float dt)
        {
            if (Has(FloorRule.MovingCloud)) UpdateClouds(dt);
            if (Has(FloorRule.MirrorLaser)) UpdateMirrorLasers(dt);
            if (Has(FloorRule.Lightning)) UpdateLightning(dt);
            if (Has(FloorRule.Glitch)) UpdateGlitches(dt);
            if (Has(FloorRule.GravityWell)) UpdateWells(dt);
            if (Has(FloorRule.BlackHole)) UpdateHole(dt);
        }

        private bool RobotHere(GridPos p) => robot.IsAlive && !robot.IsHovering && !robot.IsHopping && robot.Position == p;

        private Transform Decal(GridPos p, Color color, Color glow, float size, float height = 0.03f)
        {
            var t = Shapes.Primitive(PrimitiveType.Cylinder, "Decal", transform, GridView.ToWorld(p) + Vector3.up * (GridView.SurfaceY + height),
                new Vector3(size, 0.01f, size), MaterialFactory.CreateTransparent(color, glow)).transform;
            spawned.Add(t.gameObject);
            return t;
        }

        // ---------- Blizzard: the view closes in, a little wider than in the dark ----------

        private float BlizzardLit(GridPos p)
        {
            var r = robot.transform.position;
            float dist = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(r.x, r.z));
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.6f, 3.4f, dist)) * 0.85f;
        }

        // ---------- Moving clouds: pulse, then carry the robot a tile along their arrow ----------

        private void PlaceClouds()
        {
            var free = FreeTiles();
            int count = Mathf.Clamp(free.Count / 10, 3, 8);
            for (int i = 0; i < count && free.Count > 0; i++)
            {
                var p = free[rng.Next(free.Count)];
                free.Remove(p);
                var dir = DirectionExtensions.All[rng.Next(4)];
                var cloud = Decal(p, new Color(1f, 1f, 1f, 0.55f), new Color(0.6f, 0.7f, 0.9f), 0.9f);
                var o = dir.ToOffset();
                var arrow = Shapes.Rounded("Arrow", transform, GridView.ToWorld(p) + new Vector3(o.x * 0.28f, GridView.SurfaceY + 0.05f, o.y * 0.28f),
                    new Vector3(0.18f, 0.03f, 0.18f), 0.05f, MaterialFactory.Create(new Color(0.6f, 0.75f, 1f), new Color(0.5f, 0.8f, 1.6f)));
                spawned.Add(arrow);
                clouds.Add((p, dir, cloud));
            }
        }

        private void UpdateClouds(float dt)
        {
            cloudTimer -= dt;
            float pulse = cloudTimer < 0.5f ? 1f + Mathf.Sin(time * 30f) * 0.08f : 1f;
            foreach (var c in clouds) if (c.view != null) c.view.localScale = new Vector3(0.9f * pulse, 0.01f, 0.9f * pulse);
            if (cloudTimer > 0f) return;
            cloudTimer = CloudEvery;
            foreach (var c in clouds)
                if (RobotHere(c.pos)) { Push(c.dir, 1, 0.2f, 1.1f, Sfx.Hop); break; }
        }

        // ---------- Mirror lasers: a beam bent by a crystal, its path drawn before it fires ----------

        private void PlaceCrystals()
        {
            var free = FreeTiles();
            int count = Mathf.Clamp(free.Count / 25, 2, 4);
            var mat = MaterialFactory.Create(new Color(0.75f, 0.95f, 1f), new Color(0.6f, 1.6f, 2.2f));
            for (int i = 0; i < count && free.Count > 0; i++)
            {
                var p = free[rng.Next(free.Count)];
                free.Remove(p);
                crystals.Add(p);
                var t = Shapes.Primitive(PrimitiveType.Cube, "Crystal", transform, GridView.ToWorld(p) + new Vector3(0.36f, GridView.SurfaceY + 0.3f, 0.36f),
                    new Vector3(0.14f, 0.6f, 0.14f), mat).transform;
                t.rotation = Quaternion.Euler(0f, 45f, 12f);
                spawned.Add(t.gameObject);
            }
        }

        private void UpdateMirrorLasers(float dt)
        {
            mirrorTimer -= dt;
            if (mirrorTimer <= 0f && crystals.Count > 0)
            {
                mirrorTimer = Mathf.Lerp(5f, 3.2f, difficulty);
                FireMirrorBeam();
            }
            for (int i = mirrorBeams.Count - 1; i >= 0; i--)
            {
                var b = mirrorBeams[i];
                if (b.warn > 0f)
                {
                    b.warn -= dt;
                    float flicker = 0.3f + 0.3f * Mathf.Sin(time * 30f);
                    MaterialFactory.SetColors(b.mat, new Color(0.7f, 0.9f, 1f, flicker), new Color(0.6f, 1.2f, 2f) * flicker);
                    foreach (var p in b.path) view.SetWarning(p, flicker + 0.3f);
                    if (b.warn <= 0f)
                    {
                        MaterialFactory.SetColors(b.mat, new Color(0.85f, 0.95f, 1f, 0.95f), new Color(1.2f, 2.2f, 3f));
                        AudioManager.PlaySfx(Sfx.Impact, 0.7f, 1.9f);
                        rig.Shake(0.3f);
                        foreach (var p in b.path) if (RobotHere(p)) { Hit?.Invoke(p); break; }
                    }
                    mirrorBeams[i] = b;
                    continue;
                }
                b.fire -= dt;
                mirrorBeams[i] = b;
                if (b.fire > 0f) continue;
                foreach (var bar in b.bars) if (bar != null) Destroy(bar.gameObject);
                mirrorBeams.RemoveAt(i);
            }
        }

        /// <summary>A beam from the floor's edge along a row to a crystal, then bent along the crystal's column.</summary>
        private void FireMirrorBeam()
        {
            var c = crystals[rng.Next(crystals.Count)];
            bool fromLeft = rng.Next(2) == 0, up = rng.Next(2) == 0;
            var path = new List<GridPos>();
            for (int x = fromLeft ? 0 : grid.Width - 1; fromLeft ? x <= c.x : x >= c.x; x += fromLeft ? 1 : -1)
            {
                var p = new GridPos(x, c.y);
                if (grid.IsFloor(p)) path.Add(p);
            }
            for (int y = c.y + (up ? 1 : -1); y >= 0 && y < grid.Height; y += up ? 1 : -1)
            {
                var p = new GridPos(c.x, y);
                if (grid.IsFloor(p)) path.Add(p);
            }
            var mat = MaterialFactory.CreateTransparent(new Color(0.7f, 0.9f, 1f, 0.3f), new Color(0.6f, 1.2f, 2f));
            var bars = new List<Transform>();
            var a = GridView.ToWorld(new GridPos(fromLeft ? 0 : grid.Width - 1, c.y)) + Vector3.up * 0.45f;
            var m = GridView.ToWorld(c) + Vector3.up * 0.45f;
            var e = GridView.ToWorld(new GridPos(c.x, up ? grid.Height - 1 : 0)) + Vector3.up * 0.45f;
            foreach (var (from, to) in new[] { (a, m), (m, e) })
            {
                var bar = Shapes.Primitive(PrimitiveType.Cylinder, "MirrorBeam", transform, (from + to) * 0.5f,
                    new Vector3(0.08f, Mathf.Max(0.3f, Vector3.Distance(from, to) * 0.5f + 0.5f), 0.08f), mat).transform;
                bar.rotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
                bars.Add(bar);
                spawned.Add(bar.gameObject);
            }
            mirrorBeams.Add((path, 1.4f, 0.4f, bars, mat));
            AudioManager.PlaySfx(Sfx.Warning, 0.5f, 1.4f);
        }

        // ---------- Lightning: a yellow ring for a second, then the strike ----------

        private void UpdateLightning(float dt)
        {
            boltTimer -= dt;
            if (boltTimer <= 0f)
            {
                boltTimer = Mathf.Lerp(3.2f, 1.8f, difficulty);
                var target = rng.NextDouble() < 0.55 ? robot.Position : RandomFreeNear(robot.Position, 3);
                if (grid.IsFloor(target) && (isProtected == null || !isProtected(target)))
                {
                    var ring = Decal(target, new Color(1f, 0.9f, 0.3f, 0.7f), new Color(2.4f, 2f, 0.5f), 0.95f);
                    bolts.Add((target, LightningWarn, ring));
                    AudioManager.PlaySfx(Sfx.Warning, 0.4f, 1.8f);
                }
            }
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var b = bolts[i];
                b.left -= dt;
                if (b.ring != null) b.ring.localScale = new Vector3(0.95f, 0.01f, 0.95f) * (1f + 0.1f * Mathf.Sin(time * 25f));
                view.SetWarning(b.pos, 0.5f + 0.4f * Mathf.Sin(time * 25f));
                if (b.left > 0f) { bolts[i] = b; continue; }
                var at = GridView.ToWorld(b.pos);
                fx.Burst(at + Vector3.up * 0.4f, new Color(1f, 0.95f, 0.5f), new Color(2.6f, 2.4f, 0.8f), 26, 6f);
                var bolt = Shapes.Primitive(PrimitiveType.Cylinder, "Bolt", transform, at + Vector3.up * 3f, new Vector3(0.12f, 3f, 0.12f),
                    MaterialFactory.Create(Color.white, new Color(3f, 2.8f, 1.2f))).transform;
                Destroy(bolt.gameObject, 0.12f);
                AudioManager.PlaySfx(Sfx.Impact, 0.8f, 1.5f);
                rig.Shake(0.4f);
                if (RobotHere(b.pos)) Hit?.Invoke(b.pos);
                if (b.ring != null) Destroy(b.ring.gameObject);
                bolts.RemoveAt(i);
            }
        }

        private GridPos RandomFreeNear(GridPos c, int radius)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var p = new GridPos(c.x + rng.Next(-radius, radius + 1), c.y + rng.Next(-radius, radius + 1));
                if (grid.IsStandable(p)) return p;
            }
            return c;
        }

        // ---------- Glitch tiles: a pair pixelates, then they swap (with whatever stands on them) ----------

        private void PlaceGlitches()
        {
            var free = FreeTiles();
            int pairs = Mathf.Clamp(free.Count / 30, 2, 4);
            var colors = new[] { new Color(1f, 0.3f, 0.9f), new Color(0.3f, 1f, 0.9f), new Color(1f, 0.85f, 0.3f), new Color(0.6f, 0.5f, 1f) };
            for (int i = 0; i < pairs && free.Count >= 2; i++)
            {
                var a = free[rng.Next(free.Count)];
                free.Remove(a);
                GridPos b = a;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    var c = free[rng.Next(free.Count)];
                    if (c.Manhattan(a) >= 3) { b = c; break; }
                }
                if (b == a) break;
                free.Remove(b);
                glitchPairs.Add((a, b));
                var col = colors[i % colors.Length];
                foreach (var p in new[] { a, b }) Decal(p, new Color(col.r, col.g, col.b, 0.45f), col * 1.5f, 0.7f);
            }
        }

        private void UpdateGlitches(float dt)
        {
            if (glitchPairs.Count == 0) return;
            if (glitchActive < 0)
            {
                glitchTimer -= dt;
                if (glitchTimer > 0f) return;
                glitchActive = rng.Next(glitchPairs.Count);
                glitchWarn = GlitchWarn;
                AudioManager.PlaySfx(Sfx.Click, 0.5f, 2f);
                return;
            }
            glitchWarn -= dt;
            var (a, b) = glitchPairs[glitchActive];
            float flicker = Mathf.Repeat(time * 14f, 1f) < 0.5f ? 0.8f : 0.2f;
            view.SetWarning(a, flicker * 0.6f);
            view.SetWarning(b, flicker * 0.6f);
            if (glitchWarn > 0f) return;
            // The swap: a robot on one tile comes out on the other.
            if (RobotHere(a)) robot.RescueTo(b);
            else if (RobotHere(b)) robot.RescueTo(a);
            foreach (var p in new[] { a, b })
                fx.Burst(GridView.ToWorld(p) + Vector3.up * 0.3f, new Color(1f, 0.4f, 0.9f), new Color(1.8f, 0.6f, 1.8f), 12, 3f);
            AudioManager.PlaySfx(Sfx.Shield, 0.6f, 1.9f);
            glitchActive = -1;
            glitchTimer = Mathf.Lerp(4f, 2.6f, difficulty);
        }

        // ---------- Gravity wells: a pulse, then a pull of one tile towards the centre ----------

        private void PlaceWells()
        {
            var free = FreeTiles();
            var start = grid.StartSpot ?? grid.CenterFloor();
            free.RemoveAll(p => p.Manhattan(start) < 3);
            int count = Mathf.Clamp(free.Count / 40, 1, 2);
            for (int i = 0; i < count && free.Count > 0; i++)
            {
                var p = free[rng.Next(free.Count)];
                free.RemoveAll(q => q.Manhattan(p) < 5);
                wells.Add(p);
                Decal(p, new Color(0.1f, 0.05f, 0.2f, 0.9f), new Color(0.5f, 0.2f, 1f), 0.8f, 0.035f);
                foreach (float s in new[] { 1.6f, 2.8f, 4.4f })
                    Decal(p, new Color(0.7f, 0.4f, 1f, 0.18f), new Color(0.8f, 0.4f, 1.6f), s, 0.02f);
            }
        }

        private void UpdateWells(float dt)
        {
            wellTimer -= dt;
            if (wellTimer > 0f) return;
            wellTimer = WellEvery;
            foreach (var w in wells)
            {
                if (RobotHere(w)) { Hit?.Invoke(w); return; }
                var r = robot.Position;
                int dist = r.Manhattan(w);
                if (dist == 0 || dist > 2 || robot.IsHopping || robot.IsHovering) continue;
                // One tile towards the centre, along the longer axis.
                int dx = w.x - r.x, dy = w.y - r.y;
                var dir = Mathf.Abs(dx) >= Mathf.Abs(dy) ? (dx > 0 ? Direction.PlusX : Direction.MinusX) : (dy > 0 ? Direction.PlusY : Direction.MinusY);
                fx.Burst(GridView.ToWorld(w) + Vector3.up * 0.3f, new Color(0.7f, 0.4f, 1f), new Color(1.2f, 0.5f, 2f), 14, 3f);
                Push(dir, 1, 0.15f, 1.3f, Sfx.Hop);
                return;
            }
        }

        // ---------- The black hole: small, then its rim trembles, then it grows over the tiles around it ----------

        private void PlaceHole()
        {
            holeCenter = grid.CenterFloor();
            var t = Shapes.Primitive(PrimitiveType.Cylinder, "BlackHole", transform, GridView.ToWorld(holeCenter) + Vector3.up * (GridView.SurfaceY + 0.04f),
                new Vector3(0.9f, 0.01f, 0.9f), MaterialFactory.Create(new Color(0.03f, 0.02f, 0.06f), Color.black)).transform;
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", t, new Vector3(0f, -0.5f, 0f), new Vector3(1.15f, 0.5f, 1.15f),
                MaterialFactory.Create(new Color(1f, 0.6f, 0.3f), new Color(2.2f, 1f, 0.4f)));
            spawned.Add(t.gameObject);
            holeView = t;
            holeWide = false;
        }

        private void UpdateHole(float dt)
        {
            holeClock += dt;
            const float small = 4f, warn = 1f, wide = 3f;
            float t = Mathf.Repeat(holeClock, small + warn + wide);
            bool wideNow = t >= small + warn;
            bool warning = t >= small && !wideNow;
            float target = wideNow ? 2.9f : 0.9f;
            if (holeView != null)
            {
                float s = Mathf.MoveTowards(holeView.localScale.x, target, dt * 6f) + (warning ? Mathf.Sin(time * 40f) * 0.04f : 0f);
                holeView.localScale = new Vector3(s, 0.01f, s);
            }
            if (warning)
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        view.SetWarning(new GridPos(holeCenter.x + x, holeCenter.y + y), 0.4f + 0.3f * Mathf.Sin(time * 30f));
            if (wideNow && !holeWide) AudioManager.PlaySfx(Sfx.Impact, 0.6f, 0.5f);
            holeWide = wideNow;
            var r = robot.Position;
            int reach = wideNow ? 1 : 0;
            // One hit per stay in the hole (a shield takes it, then the robot has a moment to climb out).
            holeHitCooldown -= dt;
            if (holeHitCooldown <= 0f && Mathf.Abs(r.x - holeCenter.x) <= reach && Mathf.Abs(r.y - holeCenter.y) <= reach && RobotHere(r))
            {
                holeHitCooldown = 1.5f;
                Hit?.Invoke(r);
            }
        }
    }
}
