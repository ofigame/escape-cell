using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The core loop (<see cref="MissionType.Hunt"/>): crates fall, bugs and guard robots roam (see
    /// <see cref="HuntSystem"/>), the robot strikes whatever is tapped — a robot, a bug, a crate on the floor, the
    /// monster — when it is within reach (two tiles; four while the super skill runs; farther taps step towards it),
    /// and the floor is won when the monster that rises after the crowd falls. The camera stays close behind the
    /// robot (a fixed angle, so swipes always mean the same directions).
    /// </summary>
    public partial class GameManager
    {
        private const int StrikeReach = 2, SuperStrikeReach = 4;
        private HuntSystem hunt;
        private float strikeCooldown;
        private WeaponDef weapon;
        private HeldWeapon heldWeapon;

        /// <summary>Back to the plain robot outside the core loop (normal size, empty-handed).</summary>
        private void ClearHuntDress()
        {
            if (robot != null) robot.transform.localScale = Vector3.one;
            if (heldWeapon != null) Destroy(heldWeapon.gameObject);
            heldWeapon = null;
        }

        private void InitHunt()
        {
            hunt = new GameObject("Hunt").AddComponent<HuntSystem>();
            hunt.Init(gridView, robot, fx, cameraRig);
            hunt.Hit += (p, share) =>
            {
                // A robot's or the monster's blow: its own share of health, not a crate's.
                hitShareOverride = share;
                OnBlockImpact(p);
                hitShareOverride = -1f;
            };
            hunt.Finished += OnHuntFinished;
            hunt.MonsterAppeared += () =>
            {
                FloatAt(robot.transform.position + Vector3.up * 0.8f, Loc.T("float.monster"), Palette.UiRed);
                AudioManager.PlayMusic(MusicTheme.Monster);
            };
            hunt.MonsterDefeated += OnHuntWon;
            hunt.CreateMonster = (p, hp) =>
            {
                MonsterLook(out var kind, out var tint);
                var m = Monster.Create(kind, GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY, hp, tint, robot.transform, 1);
                GuardianLooks.DressGuardian(m.Body, World);
                return m;
            };
        }

        private void SetupHunt()
        {
            HazardVisuals.CrateTier = Mathf.Clamp(World / 5, 0, 4); // the crates grow sturdier every five worlds
            hunt.Avoid = p => hazards.IsThreatened(p);
            // The robot and the guards grow with the campaign, up to twice their old size; the robot carries its weapon.
            float grow = Mathf.Clamp01(World / 12f);
            robot.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 2f, grow);
            hunt.EnemyScale = Mathf.Lerp(1.45f, 1.85f, grow);
            weapon = Armory.Equipped;
            if (heldWeapon != null) Destroy(heldWeapon.gameObject);
            heldWeapon = HeldWeapon.Attach(robot.Visual, weapon);
            hunt.Begin(grid, level, World, levelIndex * 31 + 7);
        }

        private void UpdateHunt(float dt)
        {
            strikeCooldown -= dt;
        }

        private void OnHuntFinished(GridPos p, bool robotKind)
        {
            int reward = robotKind ? 3 : 1;
            coinsThisRun += reward;
            comboTimer = ComboWindow;
            FloatAt(GridView.ToWorld(p) + Vector3.up * 0.7f, "+" + reward, Palette.UiGold);
        }

        private void OnHuntWon()
        {
            slowMoLeft = 0.8f;
            cameraRig.Punch(1f);
            Win();
        }


        /// <summary>
        /// After the second loss in a row on a floor (and every other one after), a card suggests one thing from the shop
        /// that fits why it was lost and what this floor holds: crushed by crates → armour or a shield upgrade (or a
        /// freezing/slowing tool); beaten by robots, enforcers or the monster → the next stronger weapon open by now
        /// (a spear when the floor is crowded). Only items open at this level and not yet owned are suggested.
        /// </summary>
        private void MaybeShowTip(string reason)
        {
            int fails = PlayerPrefs.GetInt(FailKey(levelIndex), 0);
            if (fails < 2 || fails % 2 != 0) return;
            bool crushed = reason != null && reason.StartsWith(Loc.T("lose.block"));
            var current = Armory.Equipped;

            WeaponDef better = null;
            foreach (var w in Armory.All)
            {
                if (Armory.Owned(w) || !Armory.Unlocked(w) || Armory.Power(w) <= Armory.Power(current)) continue;
                bool crowded = level.robots + level.brutes >= 4;
                bool fits = crowded ? w.reach >= 3 || w.damage > current.damage : w.damage > current.damage || w.cooldown < current.cooldown * 0.8f;
                if (!fits) continue;
                if (better == null || w.price < better.price) better = w;
            }

            Upgrade? upgrade = null;
            foreach (var u in new[] { Upgrade.Armor, Upgrade.Shield })
                if (Shop.Unlocked(u) && !Shop.IsMaxed(u)) { upgrade = u; break; }
            Tool? tool = null;
            foreach (var t in new[] { Tool.Freeze, Tool.SlowMo })
                if (Tools.Unlocked(t) && !Tools.Owned(t)) { tool = t; break; }

            void GoShop(int shelf)
            {
                ShowShop();
                if (shelf == 0) ui.Shop.ShowWeapons();
                else ui.Shop.ShowShelf(shelf == 2);
            }

            if (crushed && upgrade.HasValue)
            {
                var u = upgrade.Value;
                ui.Tip.ShowIcon(icon => ShopScreen.DrawUpgrade(icon, u), ShopScreen.UpgradeColor(u), Loc.T("shop." + u), Loc.T("tip.why.crates"), Shop.NextPrice(u), () => GoShop(1));
            }
            else if (crushed && tool.HasValue)
            {
                var t = tool.Value;
                ui.Tip.ShowIcon(icon => ToolButton.DrawIcon(icon, t), Palette.UiGold, Loc.T("tool." + t), Loc.T("tip.why.crates"), Tools.NextPrice(t), () => GoShop(2));
            }
            else if (better != null)
            {
                string why = Loc.T(level.brutes > 0 ? "tip.why.brutes" : hunt.MonsterUp ? "tip.why.monster" : "tip.why.robots");
                ui.Tip.ShowWeapon(better, why, () => GoShop(0));
            }
            else if (upgrade.HasValue)
            {
                var u = upgrade.Value;
                ui.Tip.ShowIcon(icon => ShopScreen.DrawUpgrade(icon, u), ShopScreen.UpgradeColor(u), Loc.T("shop." + u), Loc.T("tip.why.robots"), Shop.NextPrice(u), () => GoShop(1));
            }
        }

        private string HuntHud() =>
            hunt.MonsterUp || hunt.MonsterDown
                ? Loc.F("hud.huntBoss", hunt.MonsterHpLeft, hunt.MonsterHpTotal)
                : Loc.F("hud.hunt", hunt.Left, hunt.Total);

        /// <summary>
        /// A tap on the floor: strike the robot, bug, monster or crate on (or next to) the tapped tile if it is within
        /// reach; a tap on something farther takes a step towards it. False when nothing strikeable was tapped (the tap
        /// then counts for a double-tap jump as usual).
        /// </summary>
        private bool TapStrike(Vector2 screen)
        {
            if (!TryScreenToGrid(screen, out var cell)) return false;
            bool crowd = hunt.TargetNear(cell, out var at);
            bool crate = false;
            if (!crowd)
            {
                if (hazards.HasLanded(cell)) { at = cell; crate = true; }
                else
                    foreach (var d in DirectionExtensions.All)
                        if (hazards.HasLanded(cell + d.ToOffset())) { at = cell + d.ToOffset(); crate = true; break; }
            }
            if (!crowd && !crate) return false;

            var w = weapon ?? Armory.Equipped;
            int reach = w.reach + (superLeft > 0f ? SuperStrikeReach - StrikeReach : 0);
            if (HuntSystem.Chebyshev(at, robot.Position) > reach)
            {
                // Too far: a step towards it.
                int dx = at.x - robot.Position.x, dy = at.y - robot.Position.y;
                var dir = Mathf.Abs(dx) >= Mathf.Abs(dy) ? (dx > 0 ? Direction.PlusX : Direction.MinusX) : (dy > 0 ? Direction.PlusY : Direction.MinusY);
                robot.TryMove(dir);
                return true;
            }
            if (strikeCooldown > 0f || !robot.Strike(GridView.ToWorld(at))) return true;
            strikeCooldown = w.cooldown;
            int damage = w.damage * (superLeft > 0f ? 2 : 1);
            if (heldWeapon != null) heldWeapon.Swing();
            if (crowd) hunt.Strike(at, damage);
            else if (hazards.Shatter(at))
            {
                AudioManager.PlaySfx(Sfx.Blocked, 0.9f, 1.1f);
                cameraRig.Shake(0.25f);
            }
            return true;
        }

        /// <summary>The close third-person view of the core loop: low and near, behind the robot at the floor's fixed angle.</summary>
        private Vector3 closeCamFocus;
        private bool closeCamOn;

        private void UpdateCloseCamera()
        {
            bool want = State == GameState.Playing && level != null && level.mission == MissionType.Hunt && !runner.Active
                        && roadPhase == RoadPhase.None && !previewing && !ArenaActive && robot != null;
            if (!want)
            {
                if (closeCamOn)
                {
                    closeCamOn = false;
                    cameraRig.EndChase();
                }
                return;
            }
            var target = robot.transform.position;
            target.y = 0f;
            closeCamFocus = closeCamOn ? Vector3.Lerp(closeCamFocus, target, 1f - Mathf.Exp(-8f * Time.deltaTime)) : target;
            closeCamOn = true;
            var pos = closeCamFocus + CloseCamOffset * CamDistanceScale[SaveData.CameraDistance];
            cameraRig.Chase(pos, Quaternion.LookRotation(closeCamFocus + Vector3.up * 0.35f - pos), CloseCamFov);
        }

        private static readonly Vector3 CloseCamOffset = new Vector3(-2.7f, 3.9f, -2.7f);
        /// <summary>The settings' camera distances (near, medium, far, farthest), as multiples of the nearest view.</summary>
        private static readonly float[] CamDistanceScale = { 2.45f, 2.9f, 3.4f, 4.0f };
        private const float CloseCamFov = 50f;
    }
}
