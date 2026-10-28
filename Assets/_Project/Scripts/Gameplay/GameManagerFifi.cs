using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Fifi, the helper who comes when it is needed: not at foi's side all the time, it teleports in a blow or two after foi
    /// starts a fight (a column of light, Fifi forming out of it), with a pulse that zaps whatever stands next to it. Then it rolls along a tile behind foi and zaps the nearest robot, tower or awake
    /// vanG within two tiles (bugs when nothing bigger is near), as long as the fight goes on; when things calm down it
    /// flies off again and can be called again a little later. It has its own health — slams, crates, vanG and tower
    /// bolts on its tile hurt it, and the robots now and then go for it. Down to nothing, it is out until the next
    /// floor, or until the player carries on after a loss (then it drops right in). The workshop levels it up.
    /// </summary>
    public partial class GameManager
    {
        private Buddy fifi;
        private GridPos fifiTile;
        private GridPos fifiFollowedFrom;
        private int fifiHp;
        /// <summary>Fifi is on the floor, fighting (not away in the sky, not down).</summary>
        private bool fifiUp;
        /// <summary>Knocked out on this floor: it doesn't come again until the next one (or a carry-on).</summary>
        private bool fifiDown;
        private float fifiCooldown;
        /// <summary>After a hit Fifi can't be hurt again for a moment (like foi).</summary>
        private float fifiHurtAt = -10f;
        private const float FifiHurtGrace = 0.9f;
        /// <summary>When Fifi may next be called down, how long it stays, and its drop (0..1 while falling, -1 otherwise).</summary>
        private float fifiReadyAt, fifiStayLeft, fifiStayed, fifiDropT = -1f, fifiLeaveT = -1f;
        private Vector3 fifiDropFrom, fifiDropTo;

        private const float FifiStay = 12f, FifiMaxStay = 28f, FifiRecall = 10f;
        /// <summary>Blows on robots since Fifi last left, how many it waits for before it comes, and the last one.</summary>
        private int fifiBlows, fifiCallAfter = 2;
        private float lastFightBlowAt = -10f;

        private static readonly Color FifiZap = new Color(0.45f, 1f, 0.75f);

        private void InitFifi()
        {
            hunt.CompanionHurt += HurtFifi;
        }

        /// <summary>Fifi's size: as tall as foi.</summary>
        private float fifiSize = 1f;

        /// <summary>A new floor: Fifi waits in the sky at full health, ready to be called down soon.</summary>
        /// <summary>Fifi, the companion, is out of the game (kept in code, switched off): it never comes to a floor.</summary>
        private const bool FifiEnabled = false;

        private void SetupFifi()
        {
            ClearFifi();
            if (!FifiEnabled) return;
            fifi = Buddy.Create(GridView.ToWorld(robot.Position) + Vector3.up * 30f);
            // As tall as foi (measured, like the robots).
            fifi.transform.localScale = Vector3.one;
            float tall = 0f;
            foreach (var r in fifi.GetComponentsInChildren<Renderer>()) if (!(r is ParticleSystemRenderer)) tall = Mathf.Max(tall, r.bounds.max.y - fifi.transform.position.y);
            fifiSize = tall > 0.05f ? Mathf.Clamp(foiBaseHeight * HeroScale.y / tall, 0.4f, 2.5f) : 1f; // as tall as foi stands
            fifi.transform.localScale = Vector3.one * fifiSize;
            fifi.gameObject.SetActive(false);
            fifiHp = FifiUpgrades.MaxHealth;
            fifiUp = fifiDown = false;
            fifiDropT = fifiLeaveT = -1f;
            fifiReadyAt = Time.time;
            fifiBlows = 0;
            fifiCallAfter = Random.Range(1, 3);
            fifi.SetHealth(FifiBar);
        }

        private void ClearFifi()
        {
            if (fifi != null) Destroy(fifi.gameObject);
            fifi = null;
            fifiUp = false;
            fifiDropT = fifiLeaveT = -1f;
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

        /// <summary>foi struck a robot, a tower or vanG: after one or two such blows Fifi teleports in to help.</summary>
        private void NoteFightBlow()
        {
            fifiBlows++;
            lastFightBlowAt = Time.time;
        }

        /// <summary>Is the fight still on (worth Fifi staying)?</summary>
        private bool FoiNeedsHelp() =>
            Time.time - lastFightBlowAt < 5f || hunt.Danger(robot.Position) >= 0.5f || (hunt.MonsterUp && hunt.MonsterAwake);

        private void UpdateFifi(float dt)
        {
            if (fifi == null || State != GameState.Playing || roadPhase != RoadPhase.None) return;
            if (fifiDropT >= 0f) { UpdateFifiDrop(dt); return; }
            if (fifiLeaveT >= 0f) { UpdateFifiLeave(dt); return; }
            if (!fifiUp)
            {
                // Away: it teleports in once foi has started a fight (a blow or two in).
                if (!fifiDown && Time.time >= fifiReadyAt && fifiBlows >= fifiCallAfter) DropFifi();
                return;
            }

            fifi.SetHealth(FifiBar);
            // It stays while the fight goes on, then flies off.
            fifiStayLeft -= dt;
            fifiStayed += dt;
            if (fifiStayLeft <= 0f)
            {
                if (FoiNeedsHelp() && fifiStayed < FifiMaxStay) fifiStayLeft = 4f;
                else { LeaveFifi(); return; }
            }

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
            FifiZapAt(at);
        }

        /// <summary>A zap you can't miss: a jagged bolt, a flash and a ring where it lands.</summary>
        private void FifiZapAt(GridPos at)
        {
            var from = fifi.transform.position + Vector3.up * 0.55f * fifi.transform.localScale.y;
            var to = GridView.ToWorld(at) + Vector3.up * 0.6f;
            ZapFx.Create(from, to, FifiZap);
            fx.Burst(to, FifiZap, FifiZap * 2.6f, 18, 4f);
            Shockwave.Create(GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY, 0.9f, FifiZap);
            fifi.Cheer();
            AudioManager.PlaySfx(Sfx.Shield, 0.55f, 1.8f);
            hunt.Strike(at, FifiUpgrades.ZapDamage * 4);
        }

        /// <summary>Fifi comes down from the sky onto a tile by foi.</summary>
        private void DropFifi()
        {
            var spot = FifiSpotNear(robot.Position);
            if (!spot.HasValue) { fifiReadyAt = Time.time + 1f; return; }
            fifiTile = spot.Value;
            fifiDropTo = GridView.ToWorld(fifiTile) + Vector3.up * GridView.SurfaceY;
            fifi.gameObject.SetActive(true);
            fifi.Place(fifiDropTo);
            fifi.transform.localScale = Vector3.zero;
            fifiDropT = 0f;
            TeleportBeam.Create(fifiDropTo, FifiZap);
            fx.Burst(fifiDropTo + Vector3.up * 0.5f, FifiZap, FifiZap * 2.6f, 30, 4f);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.5f);
        }

        private void UpdateFifiDrop(float dt)
        {
            fifiDropT = Mathf.Min(1f, fifiDropT + dt / 0.35f);
            // It forms out of the light: grows from nothing with a little overshoot, spinning.
            float s = fifiDropT < 0.8f ? Mathf.Lerp(0f, 1.15f, fifiDropT / 0.8f) : Mathf.Lerp(1.15f, 1f, (fifiDropT - 0.8f) / 0.2f);
            fifi.transform.localScale = Vector3.one * fifiSize * s;
            fifi.transform.Rotate(0f, dt * 900f, 0f);
            if (fifiDropT < 1f) return;
            fifiDropT = -1f;
            fifi.Place(fifiDropTo);
            fifi.transform.localScale = Vector3.one * fifiSize;
            fifi.transform.rotation = Quaternion.LookRotation(GridView.ToWorld(robot.Position) - GridView.ToWorld(fifiTile));
            fifiUp = true;
            fifiFollowedFrom = robot.Position;
            fifiStayLeft = FifiStay;
            fifiStayed = 0f;
            fifiCooldown = 0.4f;
            fifi.SetHealth(FifiBar);
            hunt.CompanionTile = fifiTile;
            // The arrival: a pulse of energy that zaps everything right around it.
            Shockwave.Create(fifiDropTo, 2.2f, FifiZap);
            fx.Burst(fifiDropTo + Vector3.up * 0.3f, FifiZap, FifiZap * 2.4f, 40, 6f);
            cameraRig.Shake(0.3f);
            AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1.6f);
            Haptics.Medium();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if (x != 0 || y != 0) hunt.Strike(fifiTile + new GridPos(x, y), FifiUpgrades.ZapDamage * 4);
            FloatAt(fifiDropTo + Vector3.up * 0.9f, Loc.T("float.fifiHere"), FifiZap);
        }

        /// <summary>The fight is over: Fifi flies back up into the sky.</summary>
        private void LeaveFifi()
        {
            fifiUp = false;
            hunt.CompanionTile = null;
            fifiLeaveT = 0f;
            fifiDropFrom = fifi.transform.position;
            fifi.Cheer();
            TeleportBeam.Create(fifiDropFrom, FifiZap);
            AudioManager.PlaySfx(Sfx.Shield, 0.6f, 1.9f);
        }

        private void UpdateFifiLeave(float dt)
        {
            fifiLeaveT = Mathf.Min(1f, fifiLeaveT + dt / 0.35f);
            fifi.transform.localScale = Vector3.one * fifiSize * (1f - fifiLeaveT); // shrinks into the light
            fifi.transform.Rotate(0f, dt * 900f, 0f);
            if (fifiLeaveT < 1f) return;
            fifiLeaveT = -1f;
            fifi.gameObject.SetActive(false);
            fifi.transform.localScale = Vector3.one * fifiSize;
            fifiReadyAt = Time.time + FifiRecall;
            fifiBlows = 0;
            fifiCallAfter = Random.Range(1, 3);
        }

        /// <summary>
        /// Fifi was hit: it only reels for a moment. Fifi has no health of its own any more — on hard floors its bar
        /// follows foi's and it goes down when foi does (see FifiBar, Lose and StageRespawn).
        /// </summary>
        private void HurtFifi(int points)
        {
            if (!fifiUp || fifi == null || Time.time - fifiHurtAt < FifiHurtGrace) return;
            fifiHurtAt = Time.time;
            fifi.Daze(0.4f);
        }

        /// <summary>Fifi's bar: foi's own health on hard floors, none elsewhere.</summary>
        private float FifiBar => level != null && level.hard ? health : -1f;

        /// <summary>Fifi goes down: out of the fight for this floor (or until foi comes back, see ReviveFifi).</summary>
        private void KnockOutFifi()
        {
            if (fifi == null || !fifi.gameObject.activeSelf) { fifiUp = false; fifiDown = true; hunt.CompanionTile = null; return; }
            fifiUp = false;
            fifiDown = true;
            hunt.CompanionTile = null;
            var at = fifi.transform.position + Vector3.up * 0.5f;
            fx.Burst(at, new Color(0.6f, 0.9f, 0.8f), new Color(0.6f, 1.6f, 1.2f), 40, 5f);
            fx.Dust(fifi.transform.position, new Color(0.5f, 0.5f, 0.55f), 16, 3f);
            FloatAt(at + Vector3.up * 0.4f, Loc.T("float.fifiDown"), UI.UiKit.Style.cyan);
            AudioManager.PlaySfx(Sfx.Fall, 0.8f, 1.3f);
            fifi.gameObject.SetActive(false);
        }

        /// <summary>The player carries on after a loss: Fifi comes back too, at full health, dropping right in.</summary>
        private void ReviveFifi()
        {
            if (fifi == null || level == null || level.mission != MissionType.Hunt) return;
            fifiHp = FifiUpgrades.MaxHealth;
            fifiDown = false;
            fifi.SetHealth(FifiBar);
            if (fifiUp || fifiDropT >= 0f) return;
            fifiLeaveT = -1f;
            DropFifi();
        }
    }
}
