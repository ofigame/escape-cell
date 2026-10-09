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
    /// monster — when it is within reach (two tiles; farther taps step towards it),
    /// and the floor is won when the monster that rises after the crowd falls. The camera stays close behind the
    /// robot (a fixed angle, so swipes always mean the same directions).
    /// </summary>
    public partial class GameManager
    {
        /// <summary>How far a blow reaches (diagonals count as one step), for every weapon and the super skill alike.</summary>
        private const int StrikeReach = 2;
        private HuntSystem hunt;
        private float strikeCooldown;
        private WeaponDef weapon;
        private HeldWeapon heldWeapon;
        private float lastArmorNote = -10f;

        /// <summary>Back to the plain robot outside the core loop (normal size, empty-handed).</summary>
        private void ClearHuntDress()
        {
            ClearFifi();
            if (robot != null) robot.transform.localScale = Vector3.one;
            Robot.HopScale = 1f;
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
            hunt.SleepingHit += OnSleepingHit;
            InitFifi();
            hunt.Armored += p =>
            {
                // Too weak a weapon for this floor's armour: say so (not on every blow).
                if (Time.unscaledTime - lastArmorNote < 2.5f) return;
                lastArmorNote = Time.unscaledTime;
                FloatAt(GridView.ToWorld(p) + Vector3.up * 1.1f, Loc.F("float.armored", level.armor + 1), new Color(0.75f, 0.85f, 1f));
            };
            hunt.MonsterAppeared += () =>
            {
                OpenCage();
                FloatAt(robot.transform.position + Vector3.up * 0.8f, Loc.T("float.monster"), Palette.UiRed);
                AudioManager.PlayMusic(MusicTheme.Monster);
            };
            hunt.MonsterDefeated += OnHuntWon;
            hunt.CreateMonster = (p, hp) =>
            {
                MonsterLook(out var kind, out var tint);
                var m = Monster.Create(kind, GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY, hp, tint, robot.transform, 1);
                GuardianLooks.DressGuardian(m.Body, World);
                NameTag(m);
                return m;
            };
        }

        private void SetupHunt()
        {
            HazardVisuals.CrateTier = Mathf.Clamp(World / 5, 0, 4); // the crates grow sturdier every five worlds
            hunt.Avoid = p => hazards.IsThreatened(p);
            // The robot and the guards stand big on the floor and grow with the campaign (back to normal size in the
            // tunnels, see ClearHuntDress); the robot carries its weapon.
            float grow = Mathf.Clamp01(World / 12f);
            robot.transform.localScale = Vector3.one * Mathf.Lerp(1.65f, 2.17f, grow);
            Robot.HopScale = 0.6f; // quick and even: each step follows the finger at once, and a run of steps flows on
            hunt.EnemyScale = Mathf.Lerp(1.6f, 2.03f, grow);
            adHealUsed = false;
            weapon = Armory.Equipped;
            if (heldWeapon != null) Destroy(heldWeapon.gameObject);
            heldWeapon = HeldWeapon.Attach(robot.Visual, weapon);
            // The tunnel gate stands from the start in the middle of the north edge, the monster asleep in front of it.
            huntGate = NorthGate();
            hunt.MonsterSpot = GateGuardSpot(huntGate);
            if (gateView != null) Destroy(gateView.gameObject);
            gateView = TunnelGate.Create(GridView.ToWorld(huntGate) + Vector3.up * GridView.SurfaceY, WorldTheme.Current.accent);
            var guardSpot = hunt.MonsterSpot.Value;
            var gateTile = huntGate;
            hazards.IsProtected = p => p == gateTile || p == guardSpot || grid.IsCage(p) || grid.IsBridge(p);
            SetupCage();
            hunt.Begin(grid, level, World, levelIndex * 31 + 7);
            SetupPotions();
            SetupFifi();
        }

        private void UpdateHunt(float dt)
        {
            strikeCooldown -= dt;
            UpdatePendingStrike();
            UpdateFifi(dt);
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
            if (fifi != null && fifiUp) fifi.Cheer();
            OpenBridge();
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
            // Lost to armour the weapon in hand can't get through: point at the weapon that can, right away.
            if (level.armor > 0 && Armory.Equipped.damage <= level.armor)
            {
                WeaponDef needed = null;
                foreach (var w in Armory.All)
                    if (w.damage > level.armor && Armory.Unlocked(w) && (needed == null || w.price < needed.price)) needed = w;
                if (needed != null)
                {
                    ui.Tip.ShowWeapon(needed, Loc.F("tip.why.armor", level.armor + 1), () => { ShowShop(); ui.Shop.ShowWeapons(); });
                    return;
                }
            }
            int fails = PlayerPrefs.GetInt(FailKey(levelIndex), 0);
            if (fails < 2 || fails % 2 != 0) return;
            bool crushed = reason != null && reason.StartsWith(Loc.T("lose.block"));
            var current = Armory.Equipped;

            WeaponDef better = null;
            foreach (var w in Armory.All)
            {
                if (Armory.Owned(w) || !Armory.Unlocked(w) || Armory.Power(w) <= Armory.Power(current)) continue;
                bool crowded = level.robots + level.brutes >= 4;
                bool fits = crowded ? w.cooldown < current.cooldown * 0.85f || w.damage > current.damage : w.damage > current.damage || w.cooldown < current.cooldown * 0.8f;
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

        // ---------- The weapon bag (mid-level) ----------

        /// <summary>The bag: the game waits while another weapon is picked.</summary>
        private void OpenBag()
        {
            if (State != GameState.Playing || level == null || level.mission != MissionType.Hunt) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            cameraRig.SetMenuFocus(true);
            ui.WeaponBag.Show();
        }

        private void TakeWeapon(WeaponDef w)
        {
            Armory.Equip(w);
            weapon = w;
            if (heldWeapon != null) Destroy(heldWeapon.gameObject);
            heldWeapon = HeldWeapon.Attach(robot.Visual, w);
            heldWeapon.Swing();
            CloseBag();
            FloatAt(robot.transform.position + Vector3.up * 0.6f, Loc.T("weapon." + w.id), WeaponModels.Glow(w.tier));
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.3f);
        }

        private void CloseBag()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = slowMoLeft > 0f ? SlowMoScale : 1f;
            cameraRig.SetMenuFocus(false);
        }

        private string HuntHud() =>
            hunt.MonsterAwake || hunt.MonsterDown
                ? Loc.F("hud.huntBoss", hunt.MonsterHpLeft, hunt.MonsterHpTotal)
                : Loc.F("hud.hunt", hunt.Left, hunt.Total);

        /// <summary>
        /// A tap on the floor: strike the robot, bug, monster or crate on (or next to) the tapped tile if it is within
        /// reach; a tap on something farther takes a step towards it. False when nothing strikeable was tapped (the tap
        /// then counts for a double-tap jump as usual).
        /// </summary>
        private bool TapStrike(Vector2 screen)
        {
            // The target is picked on the screen first (robots are tall: anywhere on their body counts), then by the tile
            // the finger points at on the floor.
            bool crowd = hunt.PickOnScreen(cameraRig.Cam, screen, Screen.height * 0.07f, out var at);
            bool onFloor = TryScreenToGrid(screen, out var cell);
            if (!crowd && !onFloor) return false;
            if (!crowd) crowd = hunt.TargetNear(cell, out at);
            bool crate = false;
            if (!crowd)
            {
                if (hazards.HasLanded(cell)) { at = cell; crate = true; }
                else
                    foreach (var d in DirectionExtensions.All)
                        if (hazards.HasLanded(cell + d.ToOffset())) { at = cell + d.ToOffset(); crate = true; break; }
            }
            if (!crowd && !crate) return false;

            // Two tiles away (diagonals count as one) is in reach for every weapon; three is not.
            bool inReach = HuntSystem.Chebyshev(at, robot.Position) <= StrikeReach;
            TapMarker.Create(GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY, inReach ? new Color(1f, 0.82f, 0.3f) : new Color(0.6f, 0.9f, 1f));
            if (!inReach)
            {
                // Too far: a step towards it.
                int dx = at.x - robot.Position.x, dy = at.y - robot.Position.y;
                var dir = Mathf.Abs(dx) >= Mathf.Abs(dy) ? (dx > 0 ? Direction.PlusX : Direction.MinusX) : (dy > 0 ? Direction.PlusY : Direction.MinusY);
                robot.TryMove(dir);
                return true;
            }
            if (crowd && hunt.IsFighter(at)) lastStrikeTap = Time.time; // the fight view comes in while foi attacks a robot or vanG (not bugs)
            if (!TryStrikeNow(at, crowd)) pendingStrike = (at, crowd, Time.time); // mid-hop or between swings: strike as soon as foi can
            return true;
        }

        /// <summary>A tapped target waiting for foi to land or for the weapon to come round (dropped after a moment).</summary>
        private (GridPos at, bool crowd, float time)? pendingStrike;
        private float lastStrikeTap = -10f;

        private void UpdatePendingStrike()
        {
            if (!pendingStrike.HasValue) return;
            var (at, crowd, time) = pendingStrike.Value;
            // The target stepped on meanwhile: follow it to its new tile.
            if (crowd && !hunt.HasTargetAt(at) && hunt.TargetNear(at, out var moved)) at = moved;
            if (Time.time - time > 0.35f || HuntSystem.Chebyshev(at, robot.Position) > StrikeReach) { pendingStrike = null; return; }
            if (TryStrikeNow(at, crowd)) pendingStrike = null;
        }

        /// <summary>
        /// The blow itself, when foi is free to swing: it turns to face the target at once (behind it too) and
        /// strikes. False when it can't yet (mid-hop, or the weapon is still coming round).
        /// </summary>
        private bool TryStrikeNow(GridPos at, bool crowd)
        {
            var w = weapon ?? Armory.Equipped;
            if (strikeCooldown > 0f || !robot.Strike(GridView.ToWorld(at))) return false;
            strikeCooldown = w.cooldown;
            int damage = w.damage * (superLeft > 0f ? 2 : 1);
            if (heldWeapon != null) heldWeapon.Swing();
            // A blow you can see: a crescent of the weapon's colour sweeping through the target.
            SlashFx.Create(robot.transform.position, GridView.ToWorld(at), WeaponModels.Glow(w.tier), 1f + 0.15f * w.damage);
            LeanTowardsBlow(GridView.ToWorld(at));
            cameraRig.Shake(0.15f + 0.08f * damage);
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
        /// <summary>The camera's lean towards where the robot is heading (degrees about the vertical), eased slowly.</summary>
        private float camYaw, camYawVelocity;
        /// <summary>How far the camera has pulled back for the walk to the exit (1 = playing distance).</summary>
        private float camPull = 1f, camPullVelocity;
        /// <summary>A lean towards the side a blow went to (degrees), held for a moment after each strike.</summary>
        private float strikeLean, strikeLeanLeft;
        /// <summary>0 = the usual view .. 1 = the low, close fight view; and how long since the last enemy was near.</summary>
        private float combatBlend, combatBlendVelocity;
        /// <summary>Seconds the fight view stays after the last attack.</summary>
        private const float FightViewHold = 1.3f;

        /// <summary>
        /// After a blow the camera turns a little towards it: right when the target stood to the right or straight
        /// ahead, left when it stood to the left.
        /// </summary>
        private void LeanTowardsBlow(Vector3 target)
        {
            var forward = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(1f, 0f, 1f).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            var to = target - robot.transform.position;
            to.y = 0f;
            strikeLean = Vector3.Dot(to.normalized, right) >= -0.15f ? StrikeLean : -StrikeLean;
            strikeLeanLeft = 1.3f;
        }

        private void UpdateCloseCamera()
        {
            bool walking = roadPhase == RoadPhase.Walk && roadBeacon != null;
            bool want = (State == GameState.Playing || State == GameState.Paused) && level != null && level.mission == MissionType.Hunt && !runner.Active
                        && (roadPhase == RoadPhase.None || walking) && !previewing && !ArenaActive && robot != null;
            if (!want)
            {
                if (closeCamOn)
                {
                    closeCamOn = false;
                    cameraRig.EndChase();
                }
                camYaw = camYawVelocity = 0f;
                camPull = 1f;
                camPullVelocity = 0f;
                combatBlend = combatBlendVelocity = 0f;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            var target = robot.transform.position;
            target.y = GridView.ToWorld(robot.Position).y * 0.85f; // rises with the floor's steps
            float pullGoal = 1f;
            if (walking)
            {
                // On the way to the exit: pull back and frame the robot and the exit together, so the way out is easy
                // to find even on the widest floors.
                var exit = roadBeacon.transform.position;
                exit.y = 0f;
                target = Vector3.Lerp(target, exit, 0.5f);
                float apart = Vector3.Distance(robot.transform.position, exit);
                pullGoal = Mathf.Clamp(1.45f + apart * 0.09f, 1.45f, 3f);
            }
            closeCamFocus = closeCamOn ? Vector3.Lerp(closeCamFocus, target, 1f - Mathf.Exp((walking ? -3f : -11f) * dt)) : target;
            closeCamOn = true;
            camPull = Mathf.SmoothDamp(camPull, pullGoal, ref camPullVelocity, 0.9f, Mathf.Infinity, dt);

            // The fight view: while foi is attacking a robot or vanG (a tap on one in the last moment) the camera glides
            // lower and closer; when the attacks stop it rises again. Never for bugs, and only if the setting is on.
            bool fight = !walking && SaveData.CombatCamera && Time.time - lastStrikeTap < FightViewHold;
            float fightGoal = fight ? 1f : 0f;
            combatBlend = Mathf.SmoothDamp(combatBlend, fightGoal, ref combatBlendVelocity, 0.45f, Mathf.Infinity, dt);

            // A small lean towards the way the robot faces, settling slowly like a slow-motion pan (never a snap).
            float yawGoal = 0f;
            if (!walking)
            {
                var o = robot.Facing.ToOffset();
                float angle = Vector3.SignedAngle(new Vector3(1f, 0f, 1f), new Vector3(o.x, 0f, o.y), Vector3.up);
                yawGoal = Mathf.Abs(angle) > 120f ? 0f : Mathf.Clamp(angle * 0.16f, -CamLean, CamLean);
            }
            bool striking = strikeLeanLeft > 0f;
            if (striking) { strikeLeanLeft -= dt; yawGoal = Mathf.Clamp(yawGoal + strikeLean, -CamLean - StrikeLean, CamLean + StrikeLean); }
            camYaw = Mathf.SmoothDamp(camYaw, yawGoal, ref camYawVelocity, striking ? 0.45f : 1.1f, striking ? 60f : 25f, dt);

            var baseOffset = Vector3.Lerp(CloseCamOffset, FightCamOffset, combatBlend);
            var offset = Quaternion.Euler(0f, camYaw, 0f) * baseOffset * (CamDistanceScale[SaveData.CameraDistance] * camPull * Mathf.Lerp(1f, FightCamCloser, combatBlend));
            var pos = closeCamFocus + offset;
            // Low down, it looks a little higher, over foi towards the fight.
            cameraRig.Chase(pos, Quaternion.LookRotation(closeCamFocus + Vector3.up * Mathf.Lerp(0.35f, 0.9f, combatBlend) - pos), Mathf.Lerp(CloseCamFov, FightCamFov, combatBlend));
        }

        private static readonly Vector3 CloseCamOffset = new Vector3(-2.7f, 3.9f, -2.7f);
        /// <summary>The fight view: the same heading, much lower (about 30 degrees down instead of 46).</summary>
        private static readonly Vector3 FightCamOffset = new Vector3(-2.7f, 2.2f, -2.7f);
        /// <summary>How much closer the fight view sits, and its slightly wider lens.</summary>
        private const float FightCamCloser = 0.8f, FightCamFov = 54f;
        /// <summary>The settings' camera distances (near, medium, far, farthest), as multiples of the nearest view.</summary>
        private static readonly float[] CamDistanceScale = { 2.45f, 2.9f, 4.0f, 4.8f };
        private const float CloseCamFov = 50f;
        /// <summary>The most the camera leans towards the robot's heading, in degrees.</summary>
        private const float CamLean = 14f;
        /// <summary>How far the camera turns towards a blow, in degrees.</summary>
        private const float StrikeLean = 12f;
    }
}
