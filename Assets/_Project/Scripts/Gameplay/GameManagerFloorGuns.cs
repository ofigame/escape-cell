using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Guns on the floors: a tap on a robot (or vanG, a tower, a bug) beyond the blade's reach but within the gun's range
    /// fires the gun foi carries — one ball of energy a tap, or a burst for an automatic. Each floor starts with the gun's
    /// energy balls full (<see cref="Armory.Ammo"/>); when they run out, the ammo pill buys a refill for gold. The blade
    /// does everything close.
    /// </summary>
    public partial class GameManager
    {
        private WeaponDef floorGun;
        private int gunAmmo;
        private float gunReadyAt;

        /// <summary>Gold for a refill of the gun's energy balls (more for a better gun).</summary>
        private int AmmoPrice => floorGun == null ? 0 : 60 + 40 * floorGun.tier;

        private void SetupFloorGun()
        {
            bool floor = !arenaRun && !challengeRun && level != null && level.mission == MissionType.Hunt;
            floorGun = floor ? Armory.EquippedGun : null;
            gunAmmo = floorGun != null ? Armory.Ammo(floorGun) : 0;
            gunReadyAt = 0f;
            RefreshAmmo();
        }

        /// <summary>A gun picked from the bag on a floor: in foi's hand for the far targets, full of energy balls.</summary>
        private void TakeFloorGun(WeaponDef w)
        {
            bool fresh = floorGun != w;
            floorGun = w;
            if (fresh) gunAmmo = Mathf.Max(gunAmmo, Armory.Ammo(w));
            RefreshAmmo();
        }

        private void RefreshAmmo() => ui.SetAmmo(floorGun != null && State == GameState.Playing, gunAmmo, AmmoPrice);

        /// <summary>The ammo pill tapped with no energy balls left: a refill for gold.</summary>
        private void BuyAmmo()
        {
            if (floorGun == null || gunAmmo > 0) return;
            if (!Shop.Spend(AmmoPrice)) { AudioManager.PlaySfx(Sfx.Bump, 0.6f); FloatAt(robot.transform.position + Vector3.up, Loc.T("gun.noGold"), Palette.UiRed); return; }
            gunAmmo = Armory.Ammo(floorGun);
            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
            RefreshAmmo();
        }

        /// <summary>A far target tapped: fires the gun at it if it can (in range, balls left, ready). False: it did not.</summary>
        private bool TryFireFloorGun(GridPos at)
        {
            if (floorGun == null || HuntSystem.Chebyshev(at, robot.Position) > floorGun.reach) return false;
            if (glassCell != null && !glassCell.Broken) return false; // still in its glass cell
            if (gunAmmo <= 0)
            {
                FloatAt(robot.transform.position + Vector3.up * 1.1f, Loc.F("gun.empty", AmmoPrice), Palette.UiGold);
                return false;
            }
            if (Time.time < gunReadyAt) return true; // still cooling: the tap is spent on waiting
            gunReadyAt = Time.time + Armory.Cooldown(floorGun);
            ShowHeld(floorGun);
            // Face the target.
            var way = GridView.ToWorld(at) - robot.transform.position;
            way.y = 0f;
            if (way.sqrMagnitude > 0.01f) robot.Visual.rotation = Quaternion.LookRotation(way);
            StartCoroutine(Burst(at, floorGun.auto ? floorGun.burst : 1));
            return true;
        }

        private IEnumerator Burst(GridPos at, int shots)
        {
            var w = floorGun;
            for (int i = 0; i < shots && gunAmmo > 0 && State == GameState.Playing; i++)
            {
                gunAmmo--;
                RefreshAmmo();
                var target = at;
                if (!hunt.HasTargetAt(target) && hunt.TargetNear(target, out var moved)) target = moved; // it stepped on
                var muzzle = heldWeapon != null ? heldWeapon.transform.position + heldWeapon.transform.forward * 0.45f : robot.transform.position + Vector3.up * 0.6f;
                var end = GridView.ToWorld(target) + Vector3.up * 0.6f;
                int damage = Challenge.RankBoost(Armory.Damage(w)) * (superLeft > 0f ? 2 : 1);
                var glow = WeaponModels.Glow(w.tier);
                if (heldWeapon != null) heldWeapon.Swing();
                AudioManager.PlaySfx(Sfx.Impact, 0.4f, 1.8f + 0.08f * w.tier + Random.Range(-0.05f, 0.05f));
                EnergyBall.Fly(transform, muzzle, end, glow, w.auto ? 0.14f : 0.2f + 0.02f * w.tier, fx, () =>
                {
                    if (State != GameState.Playing) return;
                    var hitAt = target;
                    if (!hunt.HasTargetAt(hitAt) && hunt.TargetNear(hitAt, out var now)) hitAt = now;
                    hunt.Strike(hitAt, damage);
                });
                if (i + 1 < shots) yield return new WaitForSeconds(0.09f);
            }
        }
    }
}
