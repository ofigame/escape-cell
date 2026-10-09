using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Fifi on the floor: it rolls along a tile behind foi, zaps the nearest robot, tower or awake vanG within two
    /// tiles (bugs when nothing bigger is near), and has its own health — slams, crates, vanG and tower bolts on its
    /// tile hurt it, and the robots now and then go for it. Down to nothing, it falls out of the fight until the next
    /// floor, or until the player carries on after a loss (it comes back with foi). The workshop levels it up.
    /// </summary>
    public partial class GameManager
    {
        private Buddy fifi;
        private GridPos fifiTile;
        private GridPos fifiFollowedFrom;
        private int fifiHp;
        private bool fifiUp;
        private float fifiCooldown;
        /// <summary>After a hit Fifi can't be hurt again for a moment (like foi).</summary>
        private float fifiHurtAt = -10f;
        private const float FifiHurtGrace = 0.9f;

        private static readonly Color FifiZap = new Color(0.45f, 1f, 0.75f);

        private void InitFifi()
        {
            hunt.CompanionHurt += HurtFifi;
        }

        /// <summary>Fifi joins foi at the start of a floor (next to it), at full health.</summary>
        private void SetupFifi()
        {
            ClearFifi();
            var start = FifiSpotNear(robot.Position);
            if (!start.HasValue) return;
            fifi = Buddy.Create(GridView.ToWorld(start.Value) + Vector3.up * GridView.SurfaceY);
            fifi.transform.localScale = Vector3.one * 1.6f;
            fifiTile = start.Value;
            fifiFollowedFrom = robot.Position;
            fifiHp = FifiUpgrades.MaxHealth;
            fifiUp = true;
            fifiCooldown = 1f;
            fifi.SetHealth(1f);
            hunt.CompanionTile = fifiTile;
        }

        private void ClearFifi()
        {
            if (fifi != null) Destroy(fifi.gameObject);
            fifi = null;
            fifiUp = false;
            if (hunt != null) hunt.CompanionTile = null;
        }

        /// <summary>A free tile by foi for Fifi: behind foi if it can, else any free side.</summary>
        private GridPos? FifiSpotNear(GridPos around)
        {
            var o = robot.Facing.ToOffset();
            var behind = around + new GridPos(-o.x, -o.y);
            if (FifiCanStand(behind)) return behind;
            foreach (var d in DirectionExtensions.All)
                if (FifiCanStand(around + d.ToOffset())) return around + d.ToOffset();
            foreach (var diag in new[] { new GridPos(1, 1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(-1, -1) })
                if (FifiCanStand(around + diag)) return around + diag;
            return null;
        }

        private bool FifiCanStand(GridPos p) =>
            grid.IsStandable(p) && !grid.IsCage(p) && !grid.IsBridge(p) && p != robot.Position && FloorRelief.StepOk(p, p);

        private void UpdateFifi(float dt)
        {
            if (!fifiUp || fifi == null || State != GameState.Playing) return;
            // Follow: when foi has moved on, Fifi rolls to a tile next to it again.
            if (robot.Position != fifiFollowedFrom && !robot.IsHopping)
            {
                fifiFollowedFrom = robot.Position;
                if (HuntSystem.Chebyshev(fifiTile, robot.Position) > 1 || fifiTile == robot.Position || !grid.IsStandable(fifiTile))
                {
                    var spot = FifiSpotNear(robot.Position);
                    if (spot.HasValue)
                    {
                        fifiTile = spot.Value;
                        fifi.HopTo(GridView.ToWorld(fifiTile) + Vector3.up * GridView.SurfaceY, 0.22f);
                        hunt.CompanionTile = fifiTile;
                    }
                }
            }
            // Zap the nearest enemy in reach.
            fifiCooldown -= dt;
            if (fifiCooldown > 0f || fifi.Hopping) return;
            if (!hunt.CompanionTarget(fifiTile, FifiUpgrades.Range, out var at)) { fifiCooldown = 0.3f; return; }
            fifiCooldown = FifiUpgrades.ZapInterval;
            var from = fifi.transform.position + Vector3.up * 0.55f * fifi.transform.localScale.y;
            ZapFx.Create(from, GridView.ToWorld(at) + Vector3.up * 0.6f, FifiZap);
            AudioManager.PlaySfx(Sfx.Shield, 0.45f, 1.8f);
            hunt.Strike(at, FifiUpgrades.ZapDamage);
        }

        /// <summary>Fifi was hit (points of its health).</summary>
        private void HurtFifi(int points)
        {
            if (!fifiUp || fifi == null || Time.time - fifiHurtAt < FifiHurtGrace) return;
            fifiHurtAt = Time.time;
            fifiHp -= points;
            fifi.Daze(0.4f);
            fifi.SetHealth(fifiHp / (float)FifiUpgrades.MaxHealth);
            if (fifiHp > 0) return;
            // Down: out of the fight for this floor.
            fifiUp = false;
            hunt.CompanionTile = null;
            var at = fifi.transform.position + Vector3.up * 0.5f;
            fx.Burst(at, new Color(0.6f, 0.9f, 0.8f), new Color(0.6f, 1.6f, 1.2f), 40, 5f);
            fx.Dust(fifi.transform.position, new Color(0.5f, 0.5f, 0.55f), 16, 3f);
            FloatAt(at + Vector3.up * 0.4f, Loc.T("float.fifiDown"), new Color(0.6f, 0.9f, 1f));
            AudioManager.PlaySfx(Sfx.Fall, 0.8f, 1.3f);
            fifi.gameObject.SetActive(false);
        }

        /// <summary>The player carries on after a loss: Fifi comes back too, at full health.</summary>
        private void ReviveFifi()
        {
            if (fifi == null || level == null || level.mission != MissionType.Hunt) return;
            var spot = FifiSpotNear(robot.Position);
            if (!spot.HasValue) return;
            fifi.gameObject.SetActive(true);
            fifiTile = spot.Value;
            fifi.Place(GridView.ToWorld(fifiTile) + Vector3.up * GridView.SurfaceY);
            fifiFollowedFrom = robot.Position;
            fifiHp = FifiUpgrades.MaxHealth;
            fifiUp = true;
            fifi.SetHealth(1f);
            hunt.CompanionTile = fifiTile;
        }
    }
}
