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
        private int TowerDesign(int n)
        {
            int stage = world < 6 ? 0 : world < 11 ? 1 : world < 18 ? 2 : 3;
            int stageStart = stage switch { 0 => 1, 1 => 6, 2 => 11, _ => 18 };
            int unlocked = Mathf.Clamp(world - stageStart + 1, 1, 5);
            int pick = ((unlocked - 1 - n) % unlocked + unlocked) % unlocked;
            return stage * 5 + pick;
        }

        private void AddTower(GridPos p)
        {
            var t = new Tower { pos = p, hp = level.towerHp, maxHp = Mathf.Max(1, level.towerHp), cooldown = 1.5f + (float)rng.NextDouble() };
            t.root = new GameObject("GuardTower").transform;
            t.root.SetParent(transform, false);
            var accent = Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.55f, 1f), 0.75f, 1f);
            t.model = TowerModel.Build(t.root, TowerDesign(towers.Count), accent);
            t.model.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.4f, Mathf.Clamp01(world / 12f));
            t.root.position = At(p);
            grid.SetOccupied(p, true);
            // Its health bar over the top.
            float top = 0f;
            foreach (var r in t.root.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y - t.root.position.y);
            t.bar = new GameObject("HealthBar").transform;
            t.bar.SetParent(t.root, false);
            t.bar.localPosition = new Vector3(0f, top + 0.3f, 0f);
            const float w = 1f;
            Shapes.Rounded("Back", t.bar, Vector3.zero, new Vector3(w + 0.08f, 0.15f, 0.03f), 0.05f, MaterialFactory.Create(new Color(0.08f, 0.06f, 0.12f), Color.black));
            t.barMat = MaterialFactory.Create(new Color(1f, 0.75f, 0.3f), new Color(1.5f, 1f, 0.4f));
            t.barFill = new GameObject("Fill").transform;
            t.barFill.SetParent(t.bar, false);
            t.barFill.localPosition = new Vector3(-w * 0.5f, 0f, -0.025f);
            Shapes.Rounded("Fill", t.barFill, new Vector3(w * 0.5f, 0f, 0f), new Vector3(w, 0.09f, 0.02f), 0.04f, t.barMat);
            towers.Add(t);
        }

        private void StopTowers()
        {
            foreach (var t in towers)
            {
                if (t.root != null) Destroy(t.root.gameObject);
                if (!t.dead && grid != null) grid.SetOccupied(t.pos, false);
                if (t.windup >= 0f) view?.SetWarning(t.target, 0f);
            }
            towers.Clear();
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
                    view.SetWarning(t.target, 0.35f + 0.65f * w);
                    if (w < 1f) continue;
                    // Fire: the bolt flies to the marked tile; whoever still stands there is hit.
                    t.windup = -1f;
                    t.model.SetCharge(0f);
                    t.model.Flash();
                    var target = t.target;
                    var colour = t.model.BoltColour;
                    AudioManager.PlaySfx(Sfx.Blocked, 0.7f, 1.6f);
                    TowerBolt.Fire(t.model.Muzzle.position, At(target) + Vector3.up * 0.3f, colour, 0.32f, () =>
                    {
                        if (view != null) view.SetWarning(target, 0f);
                        if (!running) return;
                        Shockwave.Create(At(target), 0.9f, colour);
                        fx.Burst(At(target) + Vector3.up * 0.3f, colour, colour * 2f, 14, 3f);
                        if (robot.Position == target) Hit?.Invoke(target, TowerHitShare);
                        if (CompanionTile.HasValue && CompanionTile.Value == target) CompanionHurt?.Invoke(1);
                    });
                    t.cooldown = TowerReload * (0.85f + (float)rng.NextDouble() * 0.3f);
                    continue;
                }
                t.cooldown -= dt;
                if (t.cooldown > 0f) continue;
                int d = Chebyshev(t.pos, robot.Position);
                // Out of sight, in the blind spot right next to it, or on a healing island: no shot (it looks again soon).
                if (d > TowerRange || d <= TowerBlindSpot || grid.IsSafe(robot.Position)) { t.cooldown = 0.4f; continue; }
                t.windup = 0f;
                t.target = robot.Position;
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
            if (t.windup >= 0f) view.SetWarning(t.target, 0f);
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
