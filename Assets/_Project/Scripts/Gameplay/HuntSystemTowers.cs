using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Guard towers (from the second world on): they stand on a tile and shoot at foi from afar — a red mark on foi's
    /// tile, then a bolt — but can't hit anything right next to them (their blind spot): walk up close and knock them
    /// down. They count with the crowd that has to be beaten before vanG wakes. Their build grows with the worlds,
    /// from a stone crossbow tower to a floating plasma spire.
    /// </summary>
    public partial class HuntSystem
    {
        private class Tower
        {
            public GridPos pos;
            public int hp, maxHp;
            public float cooldown, windup = -1f;
            public GridPos target;
            public Transform root, bar, barFill;
            public Material barMat;
            public TowerModel model;
            public bool dead;
            /// <summary>The perfect city's reactor core: the floor's boss tower (see HuntSystemUtopia).</summary>
            public bool reactor;
            /// <summary>The tiles its shot will hit: foi's tile (and, for the reactor, the four around it).</summary>
            public readonly List<GridPos> area = new List<GridPos>();
        }

        private readonly List<Tower> towers = new List<Tower>();

        /// <summary>How far a tower sees and shoots (diagonals count as one step).</summary>
        private const int TowerRange = 7;
        /// <summary>Right next to a tower (this close or closer) it can't aim: its blind spot.</summary>
        private const int TowerBlindSpot = 1;

        private float TowerHitShare => Mathf.Lerp(0.12f, 0.24f, level.score / 100f);
        private float TowerWind => Mathf.Lerp(1.1f, 0.65f, level.score / 100f);
        private float TowerReload => Mathf.Lerp(2.8f, 1.5f, level.score / 100f);

        private int TowersLeft
        {
            get
            {
                int n = 0;
                foreach (var t in towers) if (!t.dead) n++;
                return n;
            }
        }

        /// <summary>
        /// Which of the twenty builds the n-th tower of this floor gets: the stage follows the world (underdeveloped
        /// in worlds 1-5, developed 6-10, highly developed 11-17, ultra 18+), each world of a stage unlocks one more of
        /// its five builds, the floor's first tower is the newest one and the rest cycle back through the older ones.
        /// </summary>
        private int TowerDesign(int n) => TowerModel.DesignFor(world, n);

        /// <summary>
        /// Where the floor's towers stand, in order rather than at random: first the four corners of the floor (a tile
        /// in from the edge, so their blind spot can be reached), then the middle of each side, then the quarter
        /// points, each at least four tiles from the others and away from vanG's cage. Falls back on any far tile.
        /// </summary>
        private List<GridPos> TowerSpots(List<GridPos> far, int count)
        {
            var chosen = new List<GridPos>();
            if (count <= 0 || far.Count == 0) return chosen;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsFloor(p) || grid.IsCage(p) || grid.IsBridge(p) || grid.IsSafe(p)) continue;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            (float x, float y)[] pattern =
            {
                (0.08f, 0.08f), (0.92f, 0.08f), (0.08f, 0.92f), (0.92f, 0.92f),
                (0.5f, 0.08f), (0.08f, 0.5f), (0.92f, 0.5f), (0.5f, 0.92f),
                (0.3f, 0.3f), (0.7f, 0.3f), (0.3f, 0.7f), (0.7f, 0.7f),
            };
            bool Clear(GridPos p)
            {
                foreach (var c in chosen) if (Chebyshev(c, p) < 4) return false;
                foreach (var t in towers) if (Chebyshev(t.pos, p) < 4) return false;
                foreach (var c in grid.CageTiles) if (Chebyshev(c, p) < 3) return false;
                return true;
            }
            foreach (var (fx, fy) in pattern)
            {
                if (chosen.Count >= count) break;
                var target = new Vector2(Mathf.Lerp(minX, maxX, fx), Mathf.Lerp(minY, maxY, fy));
                GridPos? best = null;
                float bestD = 4.5f;
                foreach (var p in far)
                {
                    float d = Vector2.Distance(target, new Vector2(p.x, p.y));
                    if (d < bestD && Clear(p)) { bestD = d; best = p; }
                }
                if (best.HasValue) chosen.Add(best.Value);
            }
            // Not enough room in the pattern: the farthest-apart of what is left.
            while (chosen.Count < count)
            {
                GridPos? best = null;
                int bestGap = -1;
                foreach (var p in far)
                {
                    if (chosen.Contains(p)) continue;
                    int gap = int.MaxValue;
                    foreach (var c in chosen) gap = Mathf.Min(gap, Chebyshev(c, p));
                    foreach (var t in towers) gap = Mathf.Min(gap, Chebyshev(t.pos, p));
                    if (gap > bestGap) { bestGap = gap; best = p; }
                }
                if (!best.HasValue || bestGap < 2) break;
                chosen.Add(best.Value);
            }
            return chosen;
        }

        private void AddTower(GridPos p, bool reactor = false)
        {
            int hp = reactor ? ReactorHp : level.towerHp;
            var t = new Tower { pos = p, hp = hp, maxHp = Mathf.Max(1, hp), cooldown = 1.5f + (float)rng.NextDouble(), reactor = reactor };
            t.root = new GameObject("GuardTower").transform;
            t.root.SetParent(transform, false);
            var accent = Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.55f, 1f), 0.75f, 1f);
            int built = 0;
            foreach (var o in towers) if (!o.reactor) built++;
            t.model = TowerModel.Build(t.root, reactor ? TowerModel.Reactor : TowerDesign(built), accent);
            t.model.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.4f, Mathf.Clamp01(world / 12f)) * (reactor ? 1.25f : 1f);
            t.root.position = At(p);
            grid.SetOccupied(p, true);
            // Its health bar over the top.
            float top = 0f;
            foreach (var r in t.root.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y - t.root.position.y);
            t.bar = new GameObject("HealthBar").transform;
            t.bar.SetParent(t.root, false);
            t.bar.localPosition = new Vector3(0f, top + 0.3f, 0f);
            float w = reactor ? 1.6f : 1f;
            Shapes.Rounded("Back", t.bar, Vector3.zero, new Vector3(w + 0.08f, 0.15f, 0.03f), 0.05f, MaterialFactory.Create(new Color(0.08f, 0.06f, 0.12f), Color.black));
            t.barMat = MaterialFactory.Create(new Color(1f, 0.75f, 0.3f), new Color(1.5f, 1f, 0.4f));
            t.barFill = new GameObject("Fill").transform;
            t.barFill.SetParent(t.bar, false);
            t.barFill.localPosition = new Vector3(-w * 0.5f, 0f, -0.025f);
            Shapes.Rounded("Fill", t.barFill, new Vector3(w * 0.5f, 0f, 0f), new Vector3(w, 0.09f, 0.02f), 0.04f, t.barMat);
            towers.Add(t);
            if (level.repairDrones) for (int i = 0; i < (reactor ? 2 : 1); i++) AddDrone(t);
        }

        private void StopTowers()
        {
            foreach (var t in towers)
            {
                if (t.root != null) Destroy(t.root.gameObject);
                if (!t.dead && grid != null) grid.SetOccupied(t.pos, false);
                if (t.windup >= 0f) foreach (var a in t.area) view?.SetWarning(a, 0f);
            }
            towers.Clear();
            StopDrones();
        }

        private void UpdateTowers(float dt)
        {
            var cam = Camera.main;
            foreach (var t in towers)
            {
                if (t.dead) continue;
                if (cam != null) t.bar.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
                float k = Mathf.Clamp01(t.hp / (float)t.maxHp);
                t.barFill.localScale = new Vector3(Mathf.Max(0.001f, k), 1f, 1f);
                t.model.Aim(robot.transform.position, dt);
                if (t.windup >= 0f)
                {
                    t.windup += dt;
                    float w = t.windup / TowerWind;
                    t.model.SetCharge(w);
                    foreach (var a in t.area) view.SetWarning(a, 0.35f + 0.65f * w);
                    if (w < 1f) continue;
                    // Fire: the bolt flies to the marked tile; whoever still stands there is hit.
                    t.windup = -1f;
                    t.model.SetCharge(0f);
                    t.model.Flash();
                    var colour = t.model.BoltColour;
                    float share = t.reactor ? ReactorHitShare : TowerHitShare;
                    AudioManager.PlaySfx(Sfx.Blocked, 0.7f, t.reactor ? 1.1f : 1.6f);
                    if (t.reactor) rig.Shake(0.4f);
                    foreach (var a in t.area)
                    {
                        var target = a;
                        TowerBolt.Fire(t.model.Muzzle.position, At(target) + Vector3.up * 0.3f, colour, 0.32f, () =>
                        {
                            if (view != null) view.SetWarning(target, 0f);
                            if (!running) return;
                            Shockwave.Create(At(target), 0.9f, colour);
                            fx.Burst(At(target) + Vector3.up * 0.3f, colour, colour * 2f, 14, 3f);
                            if (robot.Position == target) Hit?.Invoke(target, share);
                            if (CompanionTile.HasValue && CompanionTile.Value == target) CompanionHurt?.Invoke(t.reactor ? 2 : 1);
                        });
                    }
                    t.area.Clear();
                    t.cooldown = TowerReload * (t.reactor ? 0.8f : 1f) * (0.85f + (float)rng.NextDouble() * 0.3f);
                    continue;
                }
                t.cooldown -= dt;
                if (t.cooldown > 0f) continue;
                int d = Chebyshev(t.pos, robot.Position);
                // Out of sight, in the blind spot right next to it, or on a healing island: no shot (it looks again soon).
                if (d > (t.reactor ? ReactorRange : TowerRange) || d <= TowerBlindSpot || grid.IsSafe(robot.Position)) { t.cooldown = 0.4f; continue; }
                t.windup = 0f;
                t.target = robot.Position;
                t.area.Clear();
                t.area.Add(t.target);
                // The reactor's shot spreads: a cross of five tiles round foi.
                if (t.reactor)
                    foreach (var dir in DirectionExtensions.All)
                        if (grid.IsFloor(t.target + dir.ToOffset()) && !grid.IsSafe(t.target + dir.ToOffset())) t.area.Add(t.target + dir.ToOffset());
            }
        }

        /// <summary>A blow on a tower's tile. True if a tower stood there.</summary>
        private bool StrikeTower(GridPos p, int damage)
        {
            foreach (var t in towers)
            {
                if (t.dead || t.pos != p) continue;
                t.hp -= ThroughArmor(p, damage);
                t.model.Flash();
                fx.Burst(At(p) + Vector3.up * 0.8f, Palette.UiGold, Palette.CoinGlow, 30, 6f);
                Shockwave.Create(At(p), 1.3f, new Color(1f, 0.85f, 0.45f));
                AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.7f);
                rig.Shake(0.3f);
                if (t.hp <= 0) KillTower(t);
                return true;
            }
            return false;
        }

        private void KillTower(Tower t)
        {
            t.dead = true;
            grid.SetOccupied(t.pos, false);
            if (t.windup >= 0f) foreach (var a in t.area) view.SetWarning(a, 0f);
            KillDronesOf(t);
            if (t.reactor) ReactorFell(t);
            fx.Burst(At(t.pos) + Vector3.up * 0.8f, new Color(0.7f, 0.65f, 0.6f), new Color(1.6f, 1.2f, 0.6f), 50, 7f);
            fx.Dust(At(t.pos), new Color(0.6f, 0.55f, 0.5f), 26, 5f);
            Shockwave.Create(At(t.pos), 2f, new Color(1f, 0.7f, 0.4f));
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
            rig.Shake(0.7f);
            Destroy(t.root.gameObject);
            Finished?.Invoke(t.pos, true);
            CheckCleared();
        }

        private bool TowerAt(GridPos p)
        {
            foreach (var t in towers) if (!t.dead && t.pos == p) return true;
            return false;
        }

        private bool TowerNear(GridPos p, int range)
        {
            foreach (var t in towers) if (!t.dead && Chebyshev(t.pos, p) <= range) return true;
            return false;
        }

        /// <summary>Screen picking for towers (see PickOnScreen).</summary>
        private void PickTowers(System.Action<Vector3, float, GridPos, float> tryBody)
        {
            foreach (var t in towers)
                if (!t.dead) tryBody(t.root.position, t.bar != null ? t.bar.localPosition.y : 1.6f, t.pos, 0.8f);
        }

        private void AddTowerTargets(List<GridPos> all)
        {
            foreach (var t in towers) if (!t.dead) all.Add(t.pos);
        }
    }
}
