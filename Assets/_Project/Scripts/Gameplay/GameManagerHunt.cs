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
        /// <summary>How far foi strikes: two tiles (diagonals count as one) for every blade, the range of a gun.</summary>
        private int StrikeReach => 2;
        private HuntSystem hunt;
        private float strikeCooldown;
        private WeaponDef weapon;
        /// <summary>The gun foi carries in the battle arenas (null on the floors, or with none taken).</summary>
        private WeaponDef gun;
        private HeldWeapon heldWeapon;
        private float lastArmorNote = -10f;

        /// <summary>The hero's size on the floors and in the arenas (wider a quarter, taller two fifths); the tunnels keep 1.</summary>
        private static readonly Vector3 HeroScale = new Vector3(1.25f, 1.4f, 1.25f);
        private float foiBaseHeight = 0.875f;

        /// <summary>foi's height as it stands (its body, without the weapon it carries): the robots are sized against it.</summary>
        private float FoiHeight()
        {
            float top = 0f;
            foreach (var r in robot.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (heldWeapon != null && r.transform.IsChildOf(heldWeapon.transform)) continue;
                top = Mathf.Max(top, r.bounds.max.y - robot.transform.position.y);
            }
            return Mathf.Clamp(top, 0.6f, 2.5f);
        }

        /// <summary>Back to the plain robot outside the core loop (normal size, empty-handed).</summary>
        private void ClearHuntDress()
        {
            ClearFortressFloor();
            ClearFifi();
            if (ui != null) HideBombs();
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
            hunt.BugSquashed += OnBugSquashed;
            hunt.SquadFell += SoldierPool.Lose; // a soldier of the army fell (a duel's or a floor's): an hour away
            InitStages();
            InitDefense();
            InitFifi();
            InitUtopia();
            InitRewards();
            hunt.Armored += p =>
            {
                // Too weak a weapon for this floor's armour: say so (not on every blow, never for a defence's shot).
                if (defenseStrike || Time.unscaledTime - lastArmorNote < 2.5f) return;
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
            // The guards stand big on the floor and grow with the campaign; foi keeps its tunnel size everywhere, and
            // the big robots (warlords, enforcers) shrink by the same share foi used to grow, so they still tower over it
            // as before relative to the crowd. foi carries its weapon.
            float grow = Mathf.Clamp01(World / 12f);
            robot.transform.localScale = Vector3.one;
            foiBaseHeight = FoiHeight(); // the robots and Fifi are sized against foi's plain height
            hunt.FoiHeight = foiBaseHeight * HeroScale.y; // foi as it stands (the hero scale): no robot is shorter than that
            // The hero stands out in the crowd: a little bigger and a little taller than the robots round it.
            robot.transform.localScale = HeroScale;
            Robot.HopScale = 0.9f; // steady and readable: each step follows the finger, a run of steps flows on (0.6 felt too fast)
            hunt.EnemyScale = Mathf.Lerp(1.6f, 2.03f, grow);
            adHealUsed = false;
            weapon = Armory.Equipped;
            gun = arenaRun ? Armory.EquippedGun : null;
            SetupFloorGun();
            if (heldWeapon != null) Destroy(heldWeapon.gameObject);
            heldWeapon = null;
            ShowHeld(gun ?? weapon);
            // The tunnel gate stands from the start in the middle of the north edge, the monster asleep in front of it.
            huntGate = NorthGate();
            // The battle arenas have no cage and no tunnel: vanG rises in the courtyard when the crowd is beaten.
            hunt.MonsterSpot = arenaRun || challengeRun ? (GridPos?)null : GateGuardSpot(huntGate);
            if (gateView != null) Destroy(gateView.gameObject);
            gateView = arenaRun || challengeRun ? null : TunnelGate.Create(GridView.ToWorld(huntGate) + Vector3.up * GridView.SurfaceY, WorldTheme.Current.accent);
            var guardSpot = hunt.MonsterSpot ?? new GridPos(-99, -99);
            var gateTile = huntGate;
            hazards.IsProtected = p => p == gateTile || p == guardSpot || grid.IsCage(p) || grid.IsBridge(p) || grid.IsGate(p);
            SetupCage();
            hunt.NoMonster = trailRun || challengeRun;
            hunt.RedFoes = arenaRun || challengeRun; // the open world's roads: no vanG
            hunt.Begin(grid, level, World, levelIndex * 31 + 7);
            ResetScore();
            introPending = true; // the opening shot over the whole floor
            SetupPotions();
            SetupFifi();
            SetupBombs();
            SetupFortressFloor();
            SetupStageGates();
            SetupDefenses();
            ResetScore(); // again: the defences' robots count towards the par time too
        }

        private void UpdateHunt(float dt)
        {
            strikeCooldown -= dt;
            UpdatePendingStrike();
            UpdateFifi(dt);
            UpdateBridgeGuide();
        }

        private void OnHuntFinished(GridPos p, bool robotKind)
        {
            if (robotKind && !defenseStrike && !hunt.AllyKill) { hitStopLeft = 0.06f; cameraRig.Punch(0.35f); CountStreak(p); }
            // A robot: 3 gold, and more the longer the streak of kills (up to 11); a bug: 1.
            int reward = robotKind ? 3 + Mathf.Clamp(killStreak - 1, 0, 8) : 1;
            if (floorStand != null && robotKind) floorStand.Cheer(0.55f);
            if (defenseStrike) reward /= 2; // felled by a defence: half the coins
            coinsThisRun += reward;
            comboTimer = ComboWindow;
            FloatAt(GridView.ToWorld(p) + Vector3.up * 0.7f, "+" + reward, Palette.UiGold);
        }

        /// <summary>
        /// A squashed bug: one in three leaves a skill on its tile — a heart when foi is hurt, a shield, and later a freeze,
        /// a blast, now and then the super skill — the floors' only skills now that nothing falls from the sky.
        /// </summary>
        private void OnBugSquashed(GridPos p)
        {
            if (arenaRun || challengeRun || Random.value > 0.34f) return;
            var pool = new System.Collections.Generic.List<PowerUpType> { PowerUpType.Shield };
            if (HealthEnabled && health < 0.85f) { pool.Add(PowerUpType.Heart); pool.Add(PowerUpType.Heart); }
            if (World >= 2) pool.Add(PowerUpType.Freeze);
            if (World >= 5) pool.Add(PowerUpType.Blast);
            if (World >= 8 && Random.value < 0.15f) pool.Add(PowerUpType.Super);
            powerUps.Drop(pool[Random.Range(0, pool.Count)], p);
        }

        private void OnHuntWon()
        {
            if (floorStand != null) floorStand.Cheer(1f);
            QuestProgress(DailyGoal.Wins);
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
            if (level.armor > 0 && Armory.Damage(Armory.Equipped) <= level.armor)
            {
                WeaponDef needed = null;
                foreach (var w in Armory.All)
                    if (Armory.Damage(w) > level.armor && Armory.Unlocked(w) && (needed == null || w.price < needed.price)) needed = w;
                if (needed != null)
                {
                    ui.Tip.ShowWeapon(needed, Loc.F("tip.why.armor", level.armor + 1), () => { ShowShop(); ui.Shop.ShowWeapons(); });
                    return;
                }
            }
            int fails = PlayerPrefs.GetInt(FailKey(levelIndex), 0);
            if (fails < 1) return; // after every loss: what would help, from the shop
            bool crushed = reason != null && reason.StartsWith(Loc.T("lose.block"));
            var current = Armory.Equipped;

            WeaponDef better = null;
            foreach (var w in Armory.All)
            {
                if (w.kind == WeaponKind.Gun || Armory.Owned(w) || !Armory.Unlocked(w) || Armory.Power(w) <= Armory.Power(current)) continue;
                bool crowded = level.robots + level.brutes >= 4;
                bool fits = crowded ? Armory.Cooldown(w) < Armory.Cooldown(current) * 0.85f || Armory.Damage(w) > Armory.Damage(current) : Armory.Damage(w) > Armory.Damage(current) || Armory.Cooldown(w) < Armory.Cooldown(current) * 0.8f;
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
            ui.WeaponBag.Show(true); // guns on the floors too (fired at what is out of the blade's reach)
        }

        /// <summary>The weapon model in foi's hand (rebuilt only when it changes: the arenas switch blade and gun).</summary>
        private void ShowHeld(WeaponDef w)
        {
            if (heldWeapon != null && heldShown == w) return;
            if (heldWeapon != null) Destroy(heldWeapon.gameObject);
            heldWeapon = HeldWeapon.Attach(robot.Visual, w);
            heldShown = w;
        }

        private WeaponDef heldShown;

        private void TakeWeapon(WeaponDef w)
        {
            Armory.Equip(w);
            if (w.kind == WeaponKind.Gun) { if (arenaRun) gun = w; else TakeFloorGun(w); } else weapon = w;
            ShowHeld(w);
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
            bool tappedFoi = TapOnFoi(screen);
            if (!crowd && !onFloor && !tappedFoi) return false;
            if (!onFloor) cell = robot.Position; // only foi's body was touched
            if (!crowd) crowd = hunt.TargetNear(cell, out at);
            // A tap on foi itself (a robot right behind it is hidden by its body): the blow goes to the best target in
            // reach, the one behind foi (away from the camera) first.
            bool onSelf = tappedFoi || (onFloor && cell == robot.Position);
            if (onSelf && !(crowd && hunt.IsFighter(at) && HuntSystem.Chebyshev(at, robot.Position) <= StrikeReach))
            {
                var away = cameraRig.Cam.transform.forward;
                away.y = 0f;
                if (hunt.BestInReach(robot.Position, StrikeReach, away.normalized, -1f, out var hidden)) { at = hidden; crowd = true; }
            }
            // A tap on the floor towards a robot in reach that the finger missed: the swing still finds it.
            if (!crowd && onFloor && cell != robot.Position)
            {
                var towards = GridView.ToWorld(cell) - GridView.ToWorld(robot.Position);
                towards.y = 0f;
                if (HuntSystem.Chebyshev(cell, robot.Position) <= StrikeReach + 1 && hunt.BestInReach(robot.Position, StrikeReach, towards.normalized, 0.6f, out var inLine)) { at = inLine; crowd = true; }
            }
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
                // Out of the blade's reach: the gun, when foi carries one with energy balls left and the target is in its range.
                if (crowd && TryFireFloorGun(at)) return true;
                // Too far: a swing that way all the same (a tap is always a blow, never a step).
                var way = GridView.ToWorld(at) - GridView.ToWorld(robot.Position);
                way.y = 0f;
                KeyStrike(way.normalized);
                return true;
            }
            if (crowd && hunt.IsFighter(at)) lastStrikeTap = Time.time; // the fight view comes in while foi attacks a robot or vanG (not bugs)
            if (!TryStrikeNow(at, crowd)) pendingStrike = (at, crowd, Time.time); // mid-hop or between swings: strike as soon as foi can
            return true;
        }

        /// <summary>Did the finger land on foi's own body on the screen (feet to head)?</summary>
        private bool TapOnFoi(Vector2 screen)
        {
            var cam = cameraRig.Cam;
            var feet = robot.transform.position;
            var a = cam.WorldToScreenPoint(feet);
            var b = cam.WorldToScreenPoint(feet + Vector3.up * 1.1f * robot.transform.lossyScale.y);
            if (a.z <= 0f || b.z <= 0f) return false;
            var ab = (Vector2)(b - a);
            float t = ab.sqrMagnitude < 1f ? 0f : Mathf.Clamp01(Vector2.Dot(screen - (Vector2)a, ab) / ab.sqrMagnitude);
            float width = Mathf.Max(Screen.height * 0.045f, ab.magnitude * 0.35f);
            return Vector2.Distance(screen, (Vector2)a + ab * t) < width;
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
            if (heldShown != null && heldShown.kind == WeaponKind.Gun && !arenaRun) ShowHeld(w); // the blade back in hand
            strikeCooldown = Armory.Cooldown(w);
            if (crowd && hunt.IsFighter(at)) NoteFightBlow(); // a blow or two into a fight, Fifi teleports in
            int damage = Challenge.RankBoost(Armory.Damage(w)) * (superLeft > 0f ? 2 : 1); // the rank reached adds its strength
            if (w.kind == WeaponKind.Whirl)
            {
                WhirlStrike(w, damage);
                return true;
            }
            if (heldWeapon != null) heldWeapon.Swing();
            if (w.kind == WeaponKind.Gun) GunShotFx(GridView.ToWorld(at) + Vector3.up * 0.6f, w);
            // A blow you can see: a crescent of the weapon's colour sweeping through the target.
            else SlashFx.Create(robot.transform.position, GridView.ToWorld(at), WeaponModels.Glow(w.tier), 1f + 0.15f * Armory.Damage(w));
            LeanTowardsBlow(GridView.ToWorld(at));
            cameraRig.Shake(0.15f + 0.08f * damage);
            if (crowd)
            {
                // The target slipped off the tile mid-swing: the blow lands on whatever is in reach that way.
                if (!hunt.Strike(at, damage))
                {
                    var swing = GridView.ToWorld(at) - GridView.ToWorld(robot.Position);
                    swing.y = 0f;
                    if (hunt.BestInReach(robot.Position, StrikeReach, swing.normalized, 0.5f, out var other)) hunt.Strike(other, damage);
                }
            }
            else if (hazards.Shatter(at))
            {
                AudioManager.PlaySfx(Sfx.Blocked, 0.9f, 1.1f);
                cameraRig.Shake(0.25f);
            }
            return true;
        }

        /// <summary>A gun fires: a streak of light from its muzzle to <paramref name="to"/>, a flash, a crack.</summary>
        private void GunShotFx(Vector3 to, WeaponDef w)
        {
            var glow = WeaponModels.Glow(w.tier);
            var muzzle = heldWeapon != null ? heldWeapon.transform.position + heldWeapon.transform.forward * 0.55f : robot.transform.position + Vector3.up * 0.6f;
            Tracer.Create(muzzle, to, glow);
            if (w.id == "twinBlaster") Tracer.Create(muzzle + Vector3.up * 0.08f, to + Vector3.up * 0.08f, glow);
            fx.Burst(muzzle, glow, glow * 2f, 6, 2.5f);
            fx.Burst(to, glow, glow * 2f, 10, 3f);
            AudioManager.PlaySfx(Sfx.Impact, 0.45f, 1.7f + 0.1f * w.tier + Random.Range(-0.05f, 0.05f));
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
            if (challengeRun && chPhase != ChPhase.Off)
            {
                // The challenge's duel: filmed from the east, both foi in view.
                BoardPose(out var bpos, out var brot);
                cameraRig.Chase(bpos, brot, 46f);
                if (cameraRig.Cam.farClipPlane < 200f) cameraRig.Cam.farClipPlane = 200f;
                closeCamOn = true;
                closeCamFocus = robot.transform.position;
                return;
            }
            if (defenseEditing)
            {
                // Setting up defences: the stage on show from above (the set-up panels over the HUD).
                if (!ui.DefenseEditor.IsOpen) { ui.DefenseEditor.Show(); ui.SetHudHidden(true); RefreshEditor(); }
                UpdateEditorZoom();
                EditorPose(out var epos, out var erot);
                cameraRig.Chase(epos, erot, IntroFov);
                cameraRig.Cam.farClipPlane = Mathf.Clamp(editDist * 2.2f, 200f, 260f);
                SetWideViewShadows(false);
                closeCamOn = true;
                closeCamFocus = robot.transform.position;
                return;
            }
            var target = robot.transform.position;
            target.y = GridView.ToWorld(robot.Position).y * 0.85f; // rises with the floor's steps
            float pullGoal = 1f;
            // In a fight the view leads towards the way foi strikes, so the side it fights on is never cut off.
            if (!walking && combatBlend > 0.01f)
            {
                var lead = robot.Facing.ToOffset();
                target += new Vector3(lead.x, 0f, lead.y) * (FightLead * combatBlend);
            }
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
            var rot = Quaternion.LookRotation(closeCamFocus + Vector3.up * Mathf.Lerp(0.35f, 0.9f, combatBlend) - pos);
            float fov = Mathf.Lerp(CloseCamFov, FightCamFov, combatBlend);
            if (TpsActive) { TpsPose(out pos, out rot); fov = TpsFov; } // the battle arenas: over foi's shoulder
            if (introPending) { introPending = false; introStart = Time.time; }
            if (IntroRunning)
            {
                // The opening shot: the whole floor from high above (every stage of it), then gliding down to foi.
                float t = Time.time - introStart;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - IntroHold) / IntroGlide));
                IntroPose(out var ipos, out var irot);
                pos = Vector3.Lerp(ipos, pos, k);
                rot = Quaternion.Slerp(irot, rot, k);
                fov = Mathf.Lerp(IntroFov, fov, k);
                closeCamFocus = target;
                cameraRig.Cam.farClipPlane = Mathf.Clamp(Vector3.Distance(ipos, closeCamFocus) * 1.8f, 200f, 400f);
                SetWideViewShadows(k > 0.85f); // no shadows over the wide view (memory on phones)
            }
            else
            {
                if (cameraRig.Cam.farClipPlane > 200f) cameraRig.Cam.farClipPlane = 200f;
                SetWideViewShadows(true);
            }
            cameraRig.Chase(pos, rot, fov);
        }

        private bool wideShadowsOff;

        /// <summary>Real-time shadows off while the camera takes in a wide view (set-up, the opening shot), back after.</summary>
        private bool lowMemoryMode;

        private void SetWideViewShadows(bool on)
        {
            if (lowMemoryMode) on = false; // once the phone warned of low memory, shadows stay off
            if (on == !wideShadowsOff) return;
            wideShadowsOff = !on;
            var data = cameraRig.Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data != null) data.renderShadows = on;
        }

        /// <summary>The opening shot at the start of a floor (see UpdateCloseCamera): waiting to start, and since when.</summary>
        private bool introPending;
        private float introStart = -100f;
        private const float IntroHold = 1.1f, IntroGlide = 1.7f, IntroFov = 46f;
        public bool IntroRunning => Time.time - introStart < IntroHold + IntroGlide;

        /// <summary>Where the opening shot looks from: above the middle of the whole floor, far enough to see all of it.</summary>
        private void IntroPose(out Vector3 pos, out Quaternion rot)
        {
            var centre = new Vector3((grid.Width - 1) * 0.5f, FloorRelief.MaxHeight * FloorRelief.Step * 0.5f, (grid.Height - 1) * 0.5f);
            // The floor runs long to the north: seen from the south and high up, at the play view's heading.
            var dir = new Vector3(-0.32f, 1f, -0.55f).normalized;
            float span = Mathf.Max(grid.Width * 1.25f, grid.Height);
            float dist = span * 0.5f / Mathf.Tan(IntroFov * 0.5f * Mathf.Deg2Rad) + 4f;
            // Never further back than the biggest single stage needs (a whole 6-stage floor at once is too much for phones).
            var rects = level.stageBounds;
            if (rects != null && rects.Length >= 8)
            {
                float maxStage = 0f;
                for (int s = 0; s < rects.Length / 4; s++)
                    maxStage = Mathf.Max(maxStage, Mathf.Max((rects[s * 4 + 2] - rects[s * 4] + 3f) * 1.25f, rects[s * 4 + 3] - rects[s * 4 + 1] + 3f));
                float cap = Mathf.Min(70f, maxStage * 0.5f / Mathf.Tan(IntroFov * 0.5f * Mathf.Deg2Rad) + 6f);
                if (dist > cap)
                {
                    dist = cap;
                    centre = new Vector3(centre.x, centre.y, rects[1] + (rects[3] - rects[1]) * 0.5f + cap * 0.35f); // frame the first stages and what lies ahead
                }
            }
            pos = centre + dir * dist;
            rot = Quaternion.LookRotation(centre - pos);
        }

        private static readonly Vector3 CloseCamOffset = new Vector3(-2.7f, 3.9f, -2.7f);
        /// <summary>The fight view: the same heading, much lower (about 30 degrees down instead of 46).</summary>
        private static readonly Vector3 FightCamOffset = new Vector3(-2.7f, 3.1f, -2.7f); // steeper than before (user asked)
        /// <summary>How much closer the fight view sits, and its slightly wider lens.</summary>
        private const float FightCamCloser = 0.62f, FightCamFov = 56f; // the fight view sits close behind foi (user asked)
        /// <summary>How many tiles the fight view leads towards the way foi strikes.</summary>
        private const float FightLead = 1.4f;
        /// <summary>The settings' camera distances (near, medium, far, farthest), as multiples of the nearest view.</summary>
        private static readonly float[] CamDistanceScale = { 4.0f, 4.8f, 5.7f, 6.8f };
        private const float CloseCamFov = 50f;
        /// <summary>The most the camera leans towards the robot's heading, in degrees.</summary>
        private const float CamLean = 14f;
        /// <summary>How far the camera turns towards a blow, in degrees.</summary>
        private const float StrikeLean = 12f;
    }
}
