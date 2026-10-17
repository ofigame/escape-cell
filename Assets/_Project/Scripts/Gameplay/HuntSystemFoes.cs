using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The floors' robots fight so it shows: a red charge gathers at a robot's chest while it winds up (it follows foi
    /// for the first half, then commits), it lunges into the blow with a slash, and from the fifth level some carry
    /// guns — they keep their distance, a red aiming line shows where they shoot, and a tracer flies a moment later
    /// (step off the tile and it misses). Plus the crowd counts foi's crowd skills look for.
    /// </summary>
    public partial class HuntSystem
    {
        /// <summary>How far a gunner shoots from (it keeps about this far off).</summary>
        private const int GunReach = 4;

        private Material chargeMat, aimMat, gunMat;
        private static readonly Color ChargeColour = new Color(1f, 0.25f, 0.18f);

        /// <summary>The floors' robots get the new ways of fighting (not the challenge's soldiers, rival foi or squads).</summary>
        private bool FloorFoe(Guard g) => !ChallengeBattle && g.soldier == null && g != challengeFoe && !g.squad && g.mon == null && !g.ally;

        /// <summary>From the fifth level about one plain robot in four carries a gun.</summary>
        private void MaybeArm(Guard g)
        {
            if (ChallengeBattle || g.brute || g.rank >= 99 || g.bot == null) return;
            if (world == 0 && RosterFloor < 4) return;
            if (rng.NextDouble() > 0.25) return;
            g.gunner = true;
            if (gunMat == null) gunMat = MaterialFactory.Create(new Color(0.16f, 0.16f, 0.2f), new Color(0.6f, 0.08f, 0.05f));
            float h = Mathf.Max(0.4f, FoiHeight > 0.05f ? FoiHeight : 0.8f);
            var gun = Shapes.Rounded("Gun", g.root, new Vector3(0.2f, h * 0.55f, 0.22f), new Vector3(0.09f, 0.1f, 0.42f), 0.03f, gunMat);
            Shapes.Rounded("Muzzle", gun.transform, new Vector3(0f, 0f, 0.22f), new Vector3(0.06f, 0.06f, 0.06f), 0.02f, AimMat);
        }

        private Material AimMat => aimMat != null ? aimMat : aimMat = MaterialFactory.CreateTransparent(new Color(1f, 0.2f, 0.15f, 0.5f), new Color(2.2f, 0.3f, 0.2f));
        private Material ChargeMat => chargeMat != null ? chargeMat : chargeMat = MaterialFactory.CreateTransparent(new Color(1f, 0.3f, 0.2f, 0.6f), new Color(2.6f, 0.5f, 0.3f));

        private float ChestY => Mathf.Max(0.35f, (FoiHeight > 0.05f ? FoiHeight : 0.8f) * 0.6f);

        /// <summary>A wind-up's look: the red charge swelling at the chest, a gunner's aiming line growing brighter.</summary>
        private void ShowCharge(Guard g, float k)
        {
            if (!FloorFoe(g)) return;
            if (g.charge == null)
            {
                g.charge = Shapes.Primitive(PrimitiveType.Sphere, "Charge", g.root, new Vector3(0f, ChestY, 0.18f), Vector3.zero, ChargeMat).transform;
            }
            g.charge.gameObject.SetActive(true);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 30f);
            g.charge.localScale = Vector3.one * Mathf.Lerp(0.05f, g.brute ? 0.5f : 0.32f, k) * pulse;
            if (!g.gunner) return;
            if (g.aimLine == null) g.aimLine = Shapes.Primitive(PrimitiveType.Cube, "Aim", g.root, Vector3.zero, Vector3.one, AimMat).transform;
            g.aimLine.gameObject.SetActive(true);
            var from = Muzzle(g);
            var to = At(g.target) + Vector3.up * 0.35f;
            var d = to - from;
            g.aimLine.position = from + d * 0.5f;
            if (d.sqrMagnitude > 0.0001f) g.aimLine.rotation = Quaternion.LookRotation(d);
            float w = Mathf.Lerp(0.015f, 0.05f, k);
            g.aimLine.localScale = new Vector3(w, w, d.magnitude);
        }

        /// <summary>Not winding up: no charge, no aiming line, nothing being followed.</summary>
        private void IdleCharge(Guard g)
        {
            g.tracking = false;
            if ((g.charge != null && g.charge.gameObject.activeSelf) || (g.aimLine != null && g.aimLine.gameObject.activeSelf)) HideCharge(g);
        }

        private void HideCharge(Guard g)
        {
            if (g.charge != null) g.charge.gameObject.SetActive(false);
            if (g.aimLine != null) g.aimLine.gameObject.SetActive(false);
        }

        private Vector3 Muzzle(Guard g) => g.root.position + g.root.rotation * new Vector3(0.2f, ChestY * 0.92f, 0.5f);

        /// <summary>For the first half of a wind-up at foi the robot follows it (the red tiles move with foi); then it commits.</summary>
        private void TrackFoi(Guard g, float k)
        {
            if (!g.tracking || k > 0.5f || !FloorFoe(g) || g.target == robot.Position) return;
            if (Chebyshev(robot.Position, g.pos) > ReachOf(g) + 1) return;
            foreach (var t in g.area) view.SetWarning(t, 0f);
            g.target = robot.Position;
            g.area.Clear();
            g.area.Add(g.target);
            if (g.brute)
                foreach (var d in DirectionExtensions.All)
                    if (grid.IsFloor(g.target + d.ToOffset())) g.area.Add(g.target + d.ToOffset());
        }

        /// <summary>
        /// The blow lands: a melee robot lunges and slashes (its hit lands at once, as ever); a gunner fires a tracer that
        /// hits only if foi is still on the tile when it arrives. True when the hit is the gunner's (it lands later).
        /// </summary>
        private bool LandBlow(Guard g)
        {
            if (!FloorFoe(g)) return false;
            if (g.charge != null)
            {
                fx.Burst(g.charge.position, ChargeColour, new Color(2.6f, 0.5f, 0.3f), g.brute ? 16 : 8, 3f);
                HideCharge(g);
            }
            var to = At(g.target);
            if (g.gunner)
            {
                var from = Muzzle(g);
                var target = g.target;
                float share = ShareOf(g);
                fx.Burst(from, new Color(1f, 0.6f, 0.3f), new Color(3f, 1.2f, 0.4f), 6, 2.5f);
                AudioManager.PlaySfx(Sfx.Blocked, 0.5f, 1.8f);
                TowerBolt.Fire(from, to + Vector3.up * 0.35f, ChargeColour, 0.18f, () =>
                {
                    if (!running) return;
                    fx.Burst(to + Vector3.up * 0.3f, ChargeColour, new Color(2.6f, 0.5f, 0.3f), 10, 3f);
                    if (robot.Position == target) Hit?.Invoke(target, share);
                });
                return true;
            }
            // The lunge: half a tile into the blow and back; a slash across the target.
            var dir = to - g.to;
            dir.y = 0f;
            g.lungeDir = dir.sqrMagnitude > 0.001f ? dir.normalized : g.root.forward;
            g.lunge = 1f;
            SlashFx.Create(g.root.position + Vector3.up * ChestY, to, ChargeColour, g.brute ? 1.6f : 1f);
            return false;
        }

        /// <summary>The lunge's movement (on top of the robot standing on its tile).</summary>
        private void UpdateLunge(Guard g, float dt)
        {
            if (g.lunge <= 0f) return;
            g.lunge = Mathf.Max(0f, g.lunge - dt * 4.5f);
            if (g.moveT >= 1f) g.root.position = g.to + g.lungeDir * Mathf.Sin(g.lunge * Mathf.PI) * 0.45f;
        }

        // ---------- What foi's crowd skills look for ----------

        /// <summary>Fighting enemies (robots, towers, an awake vanG) within <paramref name="radius"/> of a tile.</summary>
        public int CrowdAround(GridPos at, int radius)
        {
            if (!running) return 0;
            int n = 0;
            foreach (var g in guards) if (!g.dead && !g.ally && Chebyshev(g.pos, at) <= radius) n++;
            foreach (var t in towers) if (!t.dead && Chebyshev(t.pos, at) <= radius) n++;
            if (MonsterUp && MonsterAwake && Chebyshev(monsterPos, at) <= radius) n += 2;
            return n;
        }

        /// <summary>The fighting enemies' tiles within a radius (for the skills and the ultimate), nearest first.</summary>
        public List<GridPos> FightersAround(GridPos at, int radius) => TargetsNear(at, radius, 64);
    }
}
