using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The battle arenas fought in landscape over foi's shoulder (TPS): the phone turns sideways when an arena starts,
    /// the left thumb moves foi (the stick's way turned by the camera, to the nearest floor direction, step after step
    /// while held), the right thumb turns the camera round foi, and the buttons strike what is ahead, spin and jump.
    /// Everything else (robots, crates, floor rules, gifts) is the arena as before. Back to portrait when it ends.
    /// On a computer: WASD moves, the arrow keys (or a drag on the right) turn, space strikes, E spins, Q jumps.
    /// </summary>
    public partial class GameManager
    {
        private const float TpsDistance = 5.2f, TpsFov = 58f, TpsTurnSpeed = 240f, TpsPitchSpeed = 120f;
        private TpsControls tps;
        private readonly System.Collections.Generic.List<(Vector3 at, bool big)> mapEnemies = new System.Collections.Generic.List<(Vector3 at, bool big)>();
        private bool tpsOn, tpsStrike, tpsSpin, tpsJump;
        private float tpsYaw = 45f, tpsPitch = 27f;
        /// <summary>How far to the right of foi the camera sits (a touch, so foi stands a little left of the middle).</summary>
        private float tpsSide = 0.6f;

        private bool TpsActive => tpsOn && State == GameState.Playing;

        /// <summary>This floor is fought over the shoulder like the arenas (sideways): switched on by the player, for this floor only.</summary>
        private bool levelTps;
        private bool LevelTps => levelTps;

        private void EnterTps()
        {
            if (tps == null)
            {
                tps = TpsControls.Create();
                tps.Strike += () => tpsStrike = true;
                tps.Spin += () => tpsSpin = true;
                tps.Jump += () => tpsJump = true;
            }
            tpsOn = true;
            tpsStrike = tpsSpin = tpsJump = false;
            tpsYaw = 45f; // the usual view's way (looking along +x +z)
            tpsPitch = 27f;
            tps.Show(true);
            tps.Minimap.Build(grid, arenaRun ? BattleArenas.LastBuildings : new System.Collections.Generic.List<BattleArenas.Building>(), arenaRun ? BattleArenas.LastPosts : new System.Collections.Generic.List<BattleArenas.Post>());
            robot.WalkStyle = true; // the arenas: walking and running, not hopping
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        private void ExitTps()
        {
            if (!tpsOn) return;
            tpsOn = false;
            robot.WalkStyle = false;
            ClearGunPosts();
            ClearTrail();
            trailRun = false;
            if (tps != null) tps.Show(false);
            Screen.orientation = ScreenOrientation.Portrait;
        }

        /// <summary>Portrait everywhere but the arenas: the app allows landscape (for them), the game holds portrait.</summary>
        private static void LockPortrait()
        {
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        /// <summary>A tile foi may walk onto in the arenas: solid floor, no robot or crate on it, no fire.</summary>
        private bool FreeWalkable(GridPos p) => grid.IsStandable(p) && !grid.IsGap(p) && !grid.IsLava(p);

        private Vector3 TpsForward => Quaternion.Euler(0f, tpsYaw, 0f) * Vector3.forward;

        /// <summary>This frame's command from the TPS controls (and keys), the camera turned by the right thumb.</summary>
        private InputCommand TpsCommand()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateArenaGifts();
            UpdateTrail(Time.deltaTime);
            UpdateArenaExit();
            TrackAllies();
            hunt.Enemies(mapEnemies);
            tps.Minimap.Tick(dt, robot.transform.position, Gunning ? gunYaw : tpsYaw, mapEnemies);
            var look = tps.TakeLook();
            bool onGun = manned != null;
            if (!onGun)
            {
                tpsYaw += look.x * 260f;
                tpsPitch = Mathf.Clamp(tpsPitch - look.y * 140f, 6f, 55f);
            }
            var move = tps.Move;
            bool fireHeld = tps.StrikeHeld;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                var km = new Vector2((k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f), (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
                if (km.sqrMagnitude > 0f) move = km.normalized;
                float turn = ((k.rightArrowKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed ? 1f : 0f)) * dt;
                float tilt = ((k.downArrowKey.isPressed ? 1f : 0f) - (k.upArrowKey.isPressed ? 1f : 0f)) * dt;
                if (onGun) look += new Vector2(turn * 0.45f, -tilt * 0.6f); // aiming the gun
                else
                {
                    tpsYaw += turn * TpsTurnSpeed;
                    tpsPitch = Mathf.Clamp(tpsPitch + tilt * TpsPitchSpeed, 6f, 55f);
                }
                if (k.spaceKey.isPressed) fireHeld = true;
                if (k.spaceKey.wasPressedThisFrame) tpsStrike = true;
                if (k.eKey.wasPressedThisFrame) tpsSpin = true;
                if (k.qKey.wasPressedThisFrame) tpsJump = true;
            }
#endif
            var command = default(InputCommand);
            // The gun posts: climbing onto one, and aiming and firing while up there (the strike button fires).
            bool down = onGun && tpsJump;
            UpdateGunPosts(dt, onGun ? look : Vector2.zero, onGun && (fireHeld || tpsStrike), down);
            if (manned != null || onGun) { tpsStrike = tpsSpin = tpsJump = false; return command; }
            if (tpsStrike) { tpsStrike = false; ArenaAttack(); }
            bool aiming = gun != null && manned == null;
            tps.SetAim(aiming);
            if (aiming)
            {
                var aim = AimRay();
                var at = cameraRig.Cam.WorldToScreenPoint(aim.GetPoint(8f));
                if (at.z > 0f) tps.PlaceCrosshair(at);
            }
            if (tpsSpin) { tpsSpin = false; command.spin = true; return command; }
            if (tpsJump) { tpsJump = false; command.jump = true; return command; }
            var fwd = TpsForward;
            var right = new Vector3(fwd.z, 0f, -fwd.x);
            var want = right * move.x + fwd * move.y;
            bool inGlass = glassCell != null && !glassCell.Broken;
            if (!inGlass)
            {
                // Free walking and running: any way the stick points, as fast as it is pushed (a walk part-way, a run
                // all the way out, never a dash); walls, buildings and robots stop foi, which slides along them.
                float push = move.magnitude;
                float speed = push < 0.25f ? 0f : Mathf.Lerp(2.4f, 6.2f, Mathf.InverseLerp(0.25f, 1f, push));
                var vel = want.sqrMagnitude > 0.0001f ? want.normalized * speed : Vector3.zero;
                robot.FreeStep(vel, Time.deltaTime, arenaRun ? (System.Func<GridPos, bool>)FreeWalkable : FloorWalkable);
                return command;
            }
            if (move.magnitude < 0.3f) return command;
            // Still in the glass cell: the first push breaks out (a step the nearest floor way).
            float best = float.MinValue;
            foreach (var d in DirectionExtensions.All)
            {
                var o = d.ToOffset();
                float dot = Vector3.Dot(want, new Vector3(o.x, 0f, o.y));
                if (dot > best) { best = dot; command.move = d; }
            }
            command.held = true;
            command.pace = Mathf.Clamp01(move.magnitude);
            return command;
        }

        /// <summary>
        /// A strike from a button or the space bar: the best target in reach, those ahead first (ahead of the camera in
        /// the arenas' TPS, the way foi faces otherwise), else a crate ahead, else a swing at the air.
        /// </summary>
        private void KeyStrike() { var o = robot.Facing.ToOffset(); KeyStrike(new Vector3(o.x, 0f, o.y)); }

        private void KeyStrike(Vector3 fwd)
        {
            if (level == null || level.mission != MissionType.Hunt || !robot.IsAlive) return;
            if (hunt.BestInReach(robot.Position, StrikeReach, fwd, -1.1f, out var at))
            {
                if (hunt.IsFighter(at)) lastStrikeTap = Time.time;
                if (!TryStrikeNow(at, true)) pendingStrike = (at, true, Time.time);
                return;
            }
            var ahead = robot.Position + new GridPos(Mathf.RoundToInt(fwd.x), Mathf.RoundToInt(fwd.z));
            if (hazards.HasLanded(ahead)) { if (!TryStrikeNow(ahead, false)) pendingStrike = (ahead, false, Time.time); return; }
            if (strikeCooldown <= 0f && robot.Strike(robot.transform.position + fwd * 1.5f))
            {
                // Nothing in reach: a swing at the air all the same (the blade flashes, a whoosh), so every tap answers.
                strikeCooldown = 0.25f;
                var w = weapon ?? Armory.Equipped;
                if (heldWeapon != null) heldWeapon.Swing();
                SlashFx.Create(robot.transform.position, robot.transform.position + fwd * 1.6f, WeaponModels.GlowOf(w), 1f);
                AudioManager.PlaySfx(Sfx.Hop, 0.5f, 1.6f);
            }
        }

        /// <summary>
        /// The handheld gun in the arenas: a shot straight through the crosshair in the middle of the screen (the camera
        /// sits over foi's right shoulder, so it passes beside foi), out to the gun's range; what it meets takes the
        /// gun's blow. foi turns to face where it fires.
        /// </summary>
        /// <summary>
        /// The gun's aim: from foi's right shoulder, the way the camera faces, nearly level (the camera looks down on
        /// foi, the gun does not): dragging the view up or down tilts it. The crosshair is drawn where it points.
        /// </summary>
        private Ray AimRay()
        {
            var origin = robot.transform.position + Vector3.up * 0.75f + Quaternion.Euler(0f, tpsYaw, 0f) * Vector3.right * 0.25f;
            var dir = Quaternion.Euler(tpsPitch - 24f, tpsYaw, 0f) * Vector3.forward;
            return new Ray(origin, dir);
        }

        /// <summary>
        /// A tap in the arenas: a robot close ahead takes the blade, anything further the gun (when foi carries one);
        /// the model in hand follows.
        /// </summary>
        private void ArenaAttack()
        {
            if (gun == null || hunt.BestInReach(robot.Position, StrikeReach, TpsForward, 0.2f, out _))
            {
                ShowHeld(weapon ?? Armory.Equipped);
                KeyStrike(TpsForward);
                return;
            }
            ShowHeld(gun);
            FireHandGun();
        }

        private void FireHandGun()
        {
            var w = gun;
            if (w == null) return;
            if (strikeCooldown > 0f || !robot.IsAlive || level == null || level.mission != MissionType.Hunt) return;
            strikeCooldown = Armory.Cooldown(w);
            var ray = AimRay();
            int damage = Challenge.RankBoost(Armory.Damage(w)) * (superLeft > 0f ? 2 : 1); // the rank reached adds its strength
            bool hit = hunt.ShootBlow(ray, w.reach + 1f, damage, out var point);
            var look = point - robot.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) robot.ArenaFace(look);
            if (heldWeapon != null) heldWeapon.Swing();
            GunShotFx(point, w);
            cameraRig.Shake(0.05f + 0.003f * damage);
            if (hit) lastStrikeTap = Time.time;
        }

        /// <summary>The camera over foi's shoulder: behind it by the camera's turn, a little above, looking past it.</summary>
        private void TpsPose(out Vector3 pos, out Quaternion rot)
        {
            if (Gunning) { GunPose(out pos, out rot); return; }
            var focus = robot.transform.position + Vector3.up * 0.8f; // foi is its tunnel size: look a little lower
            var dir = Quaternion.Euler(tpsPitch, tpsYaw, 0f) * Vector3.forward;
            pos = focus - dir * TpsDistance + Quaternion.Euler(0f, tpsYaw, 0f) * Vector3.right * tpsSide; // a touch to the right
            rot = Quaternion.LookRotation(dir);
            // A building of the arena city between foi and the camera: the camera comes in in front of it (quickly), and
            // eases back out once the view is clear, so foi is never hidden behind a wall.
            float full = Vector3.Distance(focus, pos), allowed = full;
            // On a floor the ramparts round its edge do the same: the camera never goes out behind them low down.
            bool city = arenaRun && SquashBot.Visual.ArenaDecor.BuildingTop.Count > 0;
            if (city || !arenaRun)
                for (float d = 0.6f; d <= full; d += 0.25f)
                {
                    var p = Vector3.Lerp(focus, pos, d / full);
                    var tile = new GridPos(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
                    bool blocked = city ? SquashBot.Visual.ArenaDecor.BuildingTop.TryGetValue(tile, out float top) && p.y < top
                        : grid != null && IsRampart(tile) && p.y < GridView.ToWorld(robot.Position).y + 2.6f;
                    if (blocked) { allowed = Mathf.Max(0.9f, d - 0.5f); break; }
                }
            float dt = Time.unscaledDeltaTime;
            tpsCamDist = allowed < tpsCamDist ? Mathf.Lerp(tpsCamDist, allowed, 1f - Mathf.Exp(-18f * dt)) : Mathf.MoveTowards(tpsCamDist, allowed, dt * 3f);
            pos = focus + (pos - focus).normalized * tpsCamDist;
        }

        private float tpsCamDist = 6f;

        /// <summary>A rampart tile: off the floor but right beside it.</summary>
        private bool IsRampart(GridPos t)
        {
            if (grid.Exists(t)) return false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (grid.Exists(t + new GridPos(dx, dy))) return true;
            return false;
        }

        /// <summary>
        /// The floors' crowd made tougher (user asked): more robots standing at once, a few more of them, and from the
        /// third world on one more blow each.
        /// </summary>
        private void Toughen(LevelData l)
        {
            // Fewer robots on the floor at once (2 to 5); the crowd grows floor by floor through a world (the first floor
            // of a world four fifths of the old count, the tenth about a third more).
            int world = LevelCatalog.WorldOf(levelIndex), inWorld = levelIndex % LevelCatalog.LevelsPerWorld;
            if (l.robotsAtOnce > 0) l.robotsAtOnce = Mathf.Clamp(Mathf.RoundToInt(l.robotsAtOnce * 0.75f) + inWorld / 4, 2, 5);
            l.robots = Mathf.CeilToInt(l.robots * 0.65f * (0.8f + 0.06f * inWorld));
            l.elites = Mathf.CeilToInt(l.elites * 1.6f) + (world >= 1 ? 1 : 0);
            l.brutes = Mathf.CeilToInt(l.brutes * 1.5f) + (world >= 1 ? 1 : 0);
            // The robots' armour is their own now (the roster's share of a blow), not the floor's.
            l.armor = 0;
            // vanG: the world's strongest, 100 health points in the first world up to 1500 in the last, more every floor.
            l.monsterHp = EnemyRoster.VanGHp(world, inWorld);
            l.monsterAttack *= Mathf.Lerp(0.9f, 0.75f, inWorld / 9f);
        }
    }
}
