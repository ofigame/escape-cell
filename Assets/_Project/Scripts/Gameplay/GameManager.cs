using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Monetization;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    public enum GameState
    {
        Menu,
        Map,
        Playing,
        Paused,
        Result
    }

    /// <summary>Entry point: builds the world from code and runs the menu → map → level → result loop.</summary>
    public class GameManager : MonoBehaviour
    {
        private const float CloseCallWindow = 0.3f;
        private const float SlowMoScale = 0.35f;
        private const float SlowMoDuration = 0.45f; // real seconds
        private const int CloseCallsForArmor = 2;
        private const float ArmorDuration = 5f;
        private const float SuperArmorDuration = 10f;
        private const int ArmorsForSuper = 3;
        private const int CoinsPerRescue = 5;
        private const int MaxRescues = 3;
        private const float HoverDuration = 2f;
        private const float HoverCooldown = 8f;
        private const int FailsForAssist = 3;

        // Features unlock as the player progresses (world index, 0-based).
        private const int RescueFromWorld = 1;
        private const int FireFromWorld = 2;
        private const int HoverFromWorld = 3;

        [SerializeField] private LevelSet levelSet;

        public GameState State { get; private set; }

        private CameraRig cameraRig;
        private UIController ui;
        private AudioManager audioManager;
        private FxSystem fx;
        private GridView gridView;
        private Robot robot;
        private HazardSystem hazards;
        private CoinSystem coins;
        private PowerUpSystem powerUps;
        private InputReader input;

        private GridModel grid;
        private LevelData level;
        private int levelIndex;
        private int coinsThisRun;
        private float elapsed;
        private float slowMoLeft;
        private int themeWorld = -1;
        private int closeCalls;
        private bool jumpHintShown;
        private int pendingLevel = -1; // the level the player tried to start without lives
        private int armorsThisLevel;
        private int rescues;
        private int coinsTowardRescue;
        private bool hovering;
        private float hoverLeft;
        private float hoverCooldown;
        private GridPos hoverTarget;
        private GameObject hoverMarker;
        private ExitPortal portal;
        private GridPos doorPos;
        private readonly HashSet<GridPos> painted = new HashSet<GridPos>();

        private int World => LevelCatalog.WorldOf(levelIndex);
        private bool RescueEnabled => World >= RescueFromWorld;
        private bool HoverEnabled => World >= HoverFromWorld;

        private int LevelCount => levelSet.levels.Count;
        private int NextLevel => Mathf.Clamp(SaveData.UnlockedLevel, 0, LevelCount - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // Lets any scene (even an empty one) run the game.
            if (FindAnyObjectByType<GameManager>() == null)
                new GameObject("SquashBot").AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (IconRenderer.TryRun())
            {
                enabled = false; // icon rendering mode: no game, just the icon scene
                return;
            }

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (levelSet == null || levelSet.levels.Count == 0) levelSet = LevelSet.LoadOrDefault();

            cameraRig = CameraRig.Create(SaveData.PerspectiveView ? ViewMode.Perspective : ViewMode.Isometric);
            input = new InputReader(cameraRig.Cam);
            audioManager = AudioManager.Create();
            EnsureLight();

            fx = new GameObject("FX").AddComponent<FxSystem>();
            gridView = new GameObject("Grid").AddComponent<GridView>();
            robot = Robot.Create(null);
            robot.Arrived += OnRobotArrived;
            robot.BlockSmasher = SmashBlock;

            hazards = new GameObject("Hazards").AddComponent<HazardSystem>();
            hazards.Init(gridView, robot, fx, cameraRig);
            hazards.Impact += OnBlockImpact;
            hazards.TileBroken += OnTileBroken;

            coins = new GameObject("Coins").AddComponent<CoinSystem>();
            coins.Init(robot, hazards, fx);
            coins.Collected += OnCoinCollected;

            powerUps = new GameObject("PowerUps").AddComponent<PowerUpSystem>();
            powerUps.Init(robot, hazards, fx);
            powerUps.Collected += OnPowerUpCollected;

            CreateUi();
            ShowMenu();
            SplashScreen.Show(); // OFIGAME studio logo over the menu, fading out
        }

        private void CreateUi()
        {
            if (ui != null) Destroy(ui.gameObject);
            ui = UIController.Create(LevelCount);
            ui.PlayPressed += () => ShowMap();
            ui.LevelChosen += StartLevel;
            ui.RetryPressed += () => StartLevel(levelIndex);
            ui.NextPressed += () => ShowMap(animateFrom: levelIndex);
            ui.MapPressed += () => ShowMap();
            ui.MenuPressed += ShowMenu;
            ui.PausePressed += Pause;
            ui.ResumePressed += Resume;
            ui.SettingToggled += OnSettingToggled;
            ui.WatchAdPressed += WatchAdForLife;
        }

        private static void EnsureLight()
        {
            var light = FindAnyObjectByType<Light>();
            if (light == null) light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = Color.white;
            // From behind the camera's left shoulder: front faces bright, left faces softer, tops evenly lit.
            light.transform.rotation = Quaternion.Euler(50f, 20f, 0f);
            light.shadows = LightShadows.None;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        /// <summary>Switch the backdrop, platform colors and lighting to the world <paramref name="levelIdx"/> belongs to.</summary>
        private void ApplyTheme(int levelIdx)
        {
            int world = LevelCatalog.WorldOf(levelIdx);
            if (world == themeWorld) return;
            themeWorld = world;
            WorldTheme.SetCurrent(world);
            robot.ApplyWorld(world);
            RenderSettings.ambientLight = Palette.Ambient * 0.8f;
            cameraRig.RefreshTheme();
        }

        // ---------- Flow ----------

        private void ResetRun()
        {
            StopAllCoroutines();
            hovering = false;
            if (hoverMarker != null) hoverMarker.SetActive(false);
            if (portal != null) Destroy(portal.gameObject);
            portal = null;
            painted.Clear();
            Time.timeScale = 1f;
            slowMoLeft = 0f;
            hazards.Stop();
            coins.Stop();
            powerUps.Stop();
        }

        /// <summary>The next level's platform idles behind the menu while the camera slowly orbits it.</summary>
        private void ShowBackdrop(int levelIdx)
        {
            ApplyTheme(levelIdx);
            var preview = levelSet.levels[levelIdx];
            grid = new GridModel(preview.gridWidth, preview.gridHeight);
            gridView.Build(grid, fx);
            cameraRig.Frame(grid.Width, grid.Height);
            cameraRig.SetStyle(CameraStyle.MenuOrbit);
            cameraRig.SetMenuFocus(true);
            robot.Spawn(grid, grid.Center);
        }

        private void ShowMenu()
        {
            ResetRun();
            State = GameState.Menu;
            ShowBackdrop(NextLevel);
            ui.ShowMenu(NextLevel, SaveData.Coins, LevelCatalog.WorldName(NextLevel), MissionText(levelSet.levels[NextLevel]));
        }

        private void ShowMap(int animateFrom = -1)
        {
            if (State != GameState.Menu) ShowBackdrop(NextLevel);
            ResetRun();
            State = GameState.Map;
            ui.ShowMap(SaveData.UnlockedLevel, SaveData.Coins, NextLevel, animateFrom);
        }

        private void StartLevel(int index)
        {
            // Each attempt costs a life (unlocking a new level refills them). Without lives, offer the ad instead.
            if (!Lives.TryConsume())
            {
                pendingLevel = index;
                ui.ShowNoLives();
                return;
            }

            ResetRun();
            closeCalls = 0;
            jumpHintShown = false;
            levelIndex = Mathf.Clamp(index, 0, LevelCount - 1);
            level = levelSet.levels[levelIndex].Clone();
            coinsThisRun = 0;
            elapsed = 0f;
            armorsThisLevel = 0;
            rescues = 0;
            coinsTowardRescue = 0;
            hovering = false;
            hoverCooldown = 0f;
            bool assisted = ApplyAssist(level);
            input.HoldEnabled = HoverEnabled;

            ApplyTheme(levelIndex);
            grid = new GridModel(level.gridWidth, level.gridHeight);
            gridView.Build(grid, fx);
            cameraRig.Frame(grid.Width, grid.Height);
            cameraRig.SetStyle(CameraStyle.Gameplay);
            cameraRig.SetMenuFocus(false);
            cameraRig.PlayIntro();
            robot.Spawn(grid, grid.Center);

            SetupMission();
            hazards.Begin(grid, level, MissionProgress, LevelCatalog.WorldOf(levelIndex));
            coins.Begin(grid, level);
            powerUps.Begin(grid, level);

            State = GameState.Playing;
            ui.ShowHud(levelIndex);
            ShowLevelIntro(assisted);
            RefreshHud();
        }

        private void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            cameraRig.SetMenuFocus(true);
            ui.ShowPause();
        }

        private void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = slowMoLeft > 0f ? SlowMoScale : 1f;
            cameraRig.SetMenuFocus(false);
            ui.HidePause();
        }

        /// <summary>Losing focus (a phone call, the home button) pauses the run.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        private void OnSettingToggled(SettingKind kind)
        {
            switch (kind)
            {
                case SettingKind.Sound:
                    SaveData.Sound = !SaveData.Sound;
                    break;
                case SettingKind.Music:
                    SaveData.Music = !SaveData.Music;
                    audioManager.RefreshMusic();
                    break;
                case SettingKind.Vibration:
                    SaveData.Vibration = !SaveData.Vibration;
                    Haptics.Medium(); // feel it right away when turned on
                    break;
                case SettingKind.Camera:
                    SaveData.PerspectiveView = !SaveData.PerspectiveView;
                    cameraRig.SetMode(SaveData.PerspectiveView ? ViewMode.Perspective : ViewMode.Isometric);
                    break;
                case SettingKind.Language:
                    Loc.Set(Loc.Current == Language.Turkish ? Language.English : Language.Turkish);
                    // Every label is baked at build time, so rebuild the UI in the new language.
                    CreateUi();
                    ShowMenu();
                    ui.ShowSettings();
                    return;
            }
            ui.RefreshSettings();
        }

        private void WatchAdForLife()
        {
            if (!Ads.Rewarded.IsReady) return;
            Ads.Rewarded.Show(rewarded =>
            {
                if (!rewarded) return;
                Lives.Add(1);
                ui.HideNoLives();
                AudioManager.PlaySfx(Sfx.Shield);
                // Jump straight into the level the player wanted to play.
                if (pendingLevel >= 0) StartLevel(pendingLevel);
                pendingLevel = -1;
            });
        }

        /// <summary>Armored robot hopping into a landed block: smash it and take the tile.</summary>
        private bool SmashBlock(GridPos p)
        {
            if (State != GameState.Playing || !hazards.Shatter(p)) return false;
            cameraRig.Shake(0.7f);
            cameraRig.Punch(0.6f);
            AudioManager.PlaySfx(Sfx.Blocked);
            Haptics.Medium();
            FloatAt(GridView.ToWorld(p), Loc.T("float.smash"), Palette.UiCyan);
            return true;
        }

        /// <summary>The robot ended up on a hole or fire: a rescue charge saves it, otherwise it falls or burns.</summary>
        private void StepIntoGap(GridPos p)
        {
            if (TryRescue(p, crushed: false)) return;

            if (grid.GetTile(p) == TileState.Fire)
            {
                robot.Squash();
                fx.Burst(robot.transform.position + Vector3.up * 0.3f, new Color(1f, 0.5f, 0.2f), new Color(2.4f, 0.8f, 0.1f), 26, 4f);
                AudioManager.PlaySfx(Sfx.Squash, 0.8f, 1.3f);
                Haptics.Death();
                Lose(Loc.T("lose.fire"));
                return;
            }

            robot.FallIntoHole();
            AudioManager.PlaySfx(Sfx.Fall);
            Haptics.Death();
            Lose(Loc.T("lose.fall"));
        }

        /// <summary>Spend a rescue charge instead of dying: smash the block, or bounce out of the hole/fire.</summary>
        private bool TryRescue(GridPos p, bool crushed)
        {
            if (!RescueEnabled || rescues <= 0) return false;
            rescues--;

            if (crushed) hazards.Shatter(p);
            else robot.RescueTo(SafeTileNear(robot.LastLeftTile, p));

            robot.GiveShield(1.5f); // a moment to get out of trouble
            cameraRig.Shake(0.8f);
            cameraRig.Punch(0.8f);
            fx.Burst(robot.transform.position + Vector3.up * 0.4f, Palette.UiCyan, Palette.ShieldPickupGlow, 24, 5f);
            AudioManager.PlaySfx(Sfx.Blocked);
            Haptics.Medium();
            FloatAt(GridView.ToWorld(p), Loc.T("float.rescued"), Palette.UiCyan);
            return true;
        }

        /// <summary>Armor from a pickup or two close calls; every third one in a level is a long "super" armor.</summary>
        private void GiveArmor(float seconds, GridPos at, string label, Color color)
        {
            armorsThisLevel++;
            bool super = armorsThisLevel % ArmorsForSuper == 0;
            robot.GiveShield(super ? SuperArmorDuration : seconds);
            FloatAt(GridView.ToWorld(at), super ? Loc.T("float.superArmor") : label, super ? Palette.UiGold : color);
            AudioManager.PlaySfx(Sfx.Shield, 1f, super ? 0.85f : 1f);
            Haptics.Medium();
            if (super) cameraRig.Punch(1.2f);
        }

        /// <summary><paramref name="preferred"/> if it is safe, otherwise the standable tile closest to <paramref name="near"/>.</summary>
        private GridPos SafeTileNear(GridPos preferred, GridPos near)
        {
            if (grid.InBounds(preferred) && grid.IsStandable(preferred) && !hazards.IsThreatened(preferred)) return preferred;
            GridPos best = preferred;
            int bestScore = int.MaxValue;
            foreach (var t in grid.AllPositions())
            {
                if (!grid.IsStandable(t)) continue;
                int score = t.Manhattan(near) * 10 + (hazards.IsThreatened(t) ? 100 : 0);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best;
        }

        // ---------- Hover escape ----------

        private void UpdateHover(InputCommand command)
        {
            if (hoverCooldown > 0f && !hovering) hoverCooldown -= Time.deltaTime;

            if (!hovering)
            {
                if (command.holdStart && HoverEnabled && hoverCooldown <= 0f && robot.IsAlive && !robot.IsHopping)
                {
                    hovering = true;
                    hoverLeft = HoverDuration;
                    hoverTarget = robot.Position;
                    robot.StartHover();
                    ShowHoverMarker(true);
                    AudioManager.PlaySfx(Sfx.Shield, 0.7f, 1.4f);
                    Haptics.Medium();
                }
                return;
            }

            if (!robot.IsAlive) { EndHover(); return; }
            if (command.holdPosition.HasValue && TryScreenToGrid(command.holdPosition.Value, out var cell)) hoverTarget = cell;

            hoverLeft -= Time.deltaTime;
            hoverMarker.transform.position = GridView.ToWorld(hoverTarget) + Vector3.up * (GridView.SurfaceY + 0.03f);
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.06f;
            hoverMarker.transform.localScale = new Vector3(pulse, 1f, pulse);

            if (command.holdEnd || hoverLeft <= 0f) EndHover();
        }

        private void EndHover()
        {
            hovering = false;
            hoverCooldown = HoverCooldown;
            ShowHoverMarker(false);
            if (!robot.IsAlive) return;
            var target = grid.IsStandable(hoverTarget) ? hoverTarget : SafeTileNear(robot.Position, hoverTarget);
            robot.EndHover(target);
            AudioManager.PlaySfx(Sfx.Hop, 0.7f, 0.8f);
        }

        private bool TryScreenToGrid(Vector2 screen, out GridPos cell)
        {
            var ray = cameraRig.Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, GridView.SurfaceY, 0f));
            cell = default;
            if (!plane.Raycast(ray, out float distance)) return false;
            var hit = ray.GetPoint(distance);
            cell = new GridPos(Mathf.Clamp(Mathf.RoundToInt(hit.x), 0, grid.Width - 1), Mathf.Clamp(Mathf.RoundToInt(hit.z), 0, grid.Height - 1));
            return true;
        }

        private void ShowHoverMarker(bool on)
        {
            if (hoverMarker == null)
            {
                hoverMarker = Shapes.Rounded("HoverTarget", null, Vector3.zero, new Vector3(0.9f, 0.03f, 0.9f), 0.015f,
                    MaterialFactory.CreateTransparent(new Color(0.6f, 0.95f, 1f, 0.45f), new Color(0.4f, 1.6f, 2f)));
            }
            hoverMarker.SetActive(on);
        }

        // ---------- Difficulty help ----------

        private static string FailKey(int index) => "sb_fails_" + index;

        /// <summary>After several failed attempts in a row, the level eases off a little so nobody gets stuck.</summary>
        private bool ApplyAssist(LevelData data)
        {
            if (PlayerPrefs.GetInt(FailKey(levelIndex), 0) < FailsForAssist) return false;
            data.warningTime *= 1.15f;
            data.spawnInterval *= 1.12f;
            data.blocksPerWave = Mathf.Max(1, data.blocksPerWave - 1);
            data.bombChance *= 0.5f;
            data.lineWaveChance *= 0.5f;
            data.rampUp *= 0.6f;
            return true;
        }

        /// <summary>Level banner: a newly unlocked feature gets introduced once, otherwise the mission.</summary>
        private void ShowLevelIntro(bool assisted)
        {
            string feature = null;
            if (World >= HoverFromWorld && !Seen("hover")) feature = "feature.hover";
            else if (World >= FireFromWorld && !Seen("fire")) feature = "feature.fire";
            else if (World >= RescueFromWorld && !Seen("rescue")) feature = "feature.rescue";

            string missionKey = "mission." + level.mission;
            if (feature == null && level.mission != MissionType.CollectCoins && level.mission != MissionType.Survive && !Seen(missionKey))
            {
                PlayerPrefs.SetInt("sb_seen_feature." + missionKey, 1);
                ui.ShowIntro(Loc.T("feature.newMission"), MissionText(level, upper: true));
                return;
            }

            if (feature != null)
            {
                PlayerPrefs.SetInt("sb_seen_" + feature, 1);
                ui.ShowIntro(Loc.T("feature.new"), Loc.T(feature));
            }
            else if (assisted)
            {
                ui.ShowIntro(Loc.T("assist.title"), MissionText(level, upper: true));
            }
            else
            {
                ui.ShowIntro(Loc.F("level", levelIndex + 1), MissionText(level, upper: true));
            }

            bool Seen(string key) => PlayerPrefs.GetInt("sb_seen_feature." + key, 0) == 1;
        }

        private void Update()
        {
            if (State == GameState.Paused) return;

            if (slowMoLeft > 0f)
            {
                slowMoLeft -= Time.unscaledDeltaTime;
                Time.timeScale = slowMoLeft > 0f ? SlowMoScale : 1f;
            }

            if (State != GameState.Playing) return;

            elapsed += Time.deltaTime;

            var command = input.Poll(robot.transform.position);
            UpdateHover(command);
            if (!hovering)
            {
                if (command.jump) robot.TryJump();
                else if (command.move.HasValue) robot.TryMove(command.move.Value);
            }

            ui.SetWarning(hazards.AnyWarningActive);
            ui.SetShield(robot.ShieldLeft, Mathf.Max(level.shieldDuration, ArmorDuration));

            ui.SetRescues(RescueEnabled, rescues, coinsTowardRescue / (float)CoinsPerRescue);
            ui.SetHover(HoverEnabled, hoverCooldown);

            // Armor lets the robot stand over a hole or fire; once it wears off, gravity (or heat) wins.
            if (robot.IsAlive && !robot.IsHopping && !robot.IsHovering && !robot.IsShielded && grid.IsGap(robot.Position))
            {
                StepIntoGap(robot.Position);
                return;
            }
            RefreshHud();

            switch (level.mission)
            {
                case MissionType.Survive:
                case MissionType.CoinRain:
                    if (elapsed >= level.surviveSeconds) Win();
                    break;
                case MissionType.Exit:
                    if (!portal.IsOpen && elapsed >= level.exitDelay) OpenDoor();
                    break;
            }
        }

        // ---------- Events ----------

        private void OnRobotArrived(GridPos p)
        {
            if (State != GameState.Playing) return;

            if (grid.IsGap(p) && !robot.IsShielded)
            {
                StepIntoGap(p);
                return;
            }

            // Every landing has weight: the tile dips and a little dust puffs out.
            gridView.Bounce(p, 0.6f);
            fx.Dust(GridView.ToWorld(p) + Vector3.up * 0.06f, Palette.TileTop, 6, 1.2f);

            coins.TryCollect(p);
            powerUps.TryCollect(p);

            if (level.mission == MissionType.Paint) PaintTile(p);
            else if (level.mission == MissionType.Exit && portal.IsOpen && p == doorPos) Escape();
        }

        private void OnBlockImpact(GridPos p)
        {
            coins.Smash(p);
            powerUps.Smash(p);
            if (State != GameState.Playing) return;
            cameraRig.Punch(0.5f);

            if (robot.IsAlive && !robot.IsHovering && robot.Position == p)
            {
                if (robot.IsShielded)
                {
                    hazards.Shatter(p);
                    cameraRig.Shake(0.9f);
                    AudioManager.PlaySfx(Sfx.Blocked);
                    Haptics.Medium();
                    FloatAt(GridView.ToWorld(p), Loc.T("float.blocked"), Palette.UiCyan);
                    return;
                }

                if (TryRescue(p, crushed: true)) return;

                robot.Squash();
                cameraRig.Shake(1.2f);
                cameraRig.Focus(robot.transform.position, 1.2f);
                fx.Burst(robot.transform.position + Vector3.up * 0.3f, Palette.RobotBody, Palette.RobotEye, 24, 5f);
                AudioManager.PlaySfx(Sfx.Squash);
                Haptics.Death();
                Lose(Loc.T("lose.block"));
                return;
            }

            if (robot.IsAlive && robot.Position.Manhattan(p) == 1) robot.Flinch(GridView.ToWorld(p));

            if (robot.LastLeftTile == p && Time.time - robot.LastLeftTime < CloseCallWindow)
            {
                AudioManager.PlaySfx(Sfx.CloseCall);
                closeCalls++;
                if (closeCalls >= CloseCallsForArmor && !robot.IsShielded)
                {
                    // Two narrow escapes earn a few seconds of armor.
                    closeCalls = 0;
                    GiveArmor(ArmorDuration, robot.Position, Loc.T("float.armor"), Palette.UiGold);
                }
                else
                {
                    FloatAt(GridView.ToWorld(p), Loc.F("float.closeCount", Mathf.Min(closeCalls, CloseCallsForArmor), CloseCallsForArmor), Palette.UiCyan);
                }
                slowMoLeft = SlowMoDuration;
                cameraRig.Focus(GridView.ToWorld(p), SlowMoDuration + 0.2f);
            }
        }

        /// <summary>The first holes of a level teach the jump (for the player's first few levels with holes).</summary>
        private void OnTileBroken(GridPos p)
        {
            const string key = "sb_jump_hints";
            if (jumpHintShown || State != GameState.Playing || PlayerPrefs.GetInt(key, 0) >= 3) return;
            jumpHintShown = true;
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);
            ui.ShowIntro(Loc.T("hint.title"), Loc.T("hint.jump"));
        }

        private void OnCoinCollected(GridPos p)
        {
            coinsThisRun++;
            FloatAt(GridView.ToWorld(p), "+1", Palette.UiGold);
            ui.FlyCoin(cameraRig.Cam.WorldToScreenPoint(GridView.ToWorld(p) + Vector3.up * 0.45f));

            // Every few coins bank a rescue charge.
            if (RescueEnabled && rescues < MaxRescues && ++coinsTowardRescue >= CoinsPerRescue)
            {
                coinsTowardRescue = 0;
                rescues++;
                FloatAt(GridView.ToWorld(p) + Vector3.up * 0.5f, Loc.T("float.rescueGain"), Palette.UiCyan);
                AudioManager.PlaySfx(Sfx.Shield, 0.6f, 1.2f);
            }

            if (level.mission == MissionType.CollectCoins && coinsThisRun >= level.coinTarget)
                Win();
        }

        private void OnPowerUpCollected(PowerUpType type, GridPos p)
        {
            switch (type)
            {
                case PowerUpType.Shield:
                    GiveArmor(level.shieldDuration, p, Loc.T("float.shield"), Palette.UiCyan);
                    cameraRig.Punch(1f);
                    break;
            }
        }

        private void FloatAt(Vector3 world, string text, Color color)
        {
            ui.Float(cameraRig.Cam.WorldToScreenPoint(world + Vector3.up * 0.9f), text, color);
        }

        // ---------- Win / lose ----------

        private void Win() => Win(escaped: false);

        private void Win(bool escaped)
        {
            State = GameState.Result;
            slowMoLeft = 0f;
            Time.timeScale = 1f;
            hazards.Freeze();
            coins.Freeze();
            powerUps.Freeze();
            if (hovering) { hovering = false; ShowHoverMarker(false); }
            if (!escaped) robot.Cheer();
            cameraRig.SetStyle(CameraStyle.Victory);
            fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.ShieldPickup, Palette.ShieldPickupGlow, 30, 6f);
            fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 20, 5f);
            AudioManager.PlaySfx(Sfx.Win);
            Haptics.Medium();

            SaveData.Coins += coinsThisRun;
            PlayerPrefs.DeleteKey(FailKey(levelIndex));

            // Only beating the newest level (unlocking the next one) refills lives; replays don't.
            bool unlockedNew = levelIndex >= SaveData.UnlockedLevel;
            if (unlockedNew) Lives.Refill();
            if (levelIndex + 1 > SaveData.UnlockedLevel && levelIndex + 1 < LevelCount)
                SaveData.UnlockedLevel = levelIndex + 1;

            bool hasNext = levelIndex + 1 < LevelCount;
            bool newWorld = unlockedNew && hasNext && (levelIndex + 1) % LevelCatalog.LevelsPerWorld == 0;
            string subtitle = Loc.T(newWorld ? "result.newWorld" : hasNext ? "result.next" : "result.allDone");
            StartCoroutine(ShowResultDelayed(true, subtitle, coinsThisRun, hasNext));
        }

        private void Lose(string reason)
        {
            State = GameState.Result;
            hazards.Freeze();
            coins.Freeze();
            powerUps.Freeze();

            PlayerPrefs.SetInt(FailKey(levelIndex), PlayerPrefs.GetInt(FailKey(levelIndex), 0) + 1);
            if (hovering) { hovering = false; ShowHoverMarker(false); }

            // Coins picked up are kept even on a loss, so every run feels worth it.
            SaveData.Coins += coinsThisRun;
            StartCoroutine(ShowResultDelayed(false, reason + "\n" + Loc.F("lives.left", Lives.Count), coinsThisRun, false));
        }

        private IEnumerator ShowResultDelayed(bool won, string subtitle, int earned, bool hasNext)
        {
            ui.SetWarning(false);
            ui.SetShield(0f, 1f);
            yield return new WaitForSecondsRealtime(won ? 1.4f : 1.2f);
            slowMoLeft = 0f;
            Time.timeScale = 1f;
            if (!won) AudioManager.PlaySfx(Sfx.Lose, 0.8f);
            cameraRig.SetMenuFocus(true);
            ui.ShowResult(won, subtitle, earned, hasNext);
        }

        // ---------- Mission ----------

        /// <summary>Per-mission setup: the exit door far from the robot, or the first painted tile.</summary>
        private void SetupMission()
        {
            hazards.IsProtected = null;
            if (level.mission == MissionType.Exit)
            {
                // The door goes on one of the tiles farthest from the robot, so reaching it is a little journey.
                int best = -1;
                var options = new List<GridPos>();
                foreach (var t in grid.AllPositions())
                {
                    int d = t.Manhattan(robot.Position);
                    if (d > best) { best = d; options.Clear(); }
                    if (d == best) options.Add(t);
                }
                doorPos = options[Random.Range(0, options.Count)];
                portal = ExitPortal.Create(GridView.ToWorld(doorPos) + Vector3.up * GridView.SurfaceY);
                hazards.IsProtected = p => p == doorPos;
            }
            else if (level.mission == MissionType.Paint)
            {
                PaintTile(robot.Position);
            }
        }

        private void OpenDoor()
        {
            portal.Open();
            cameraRig.Punch(0.8f);
            AudioManager.PlaySfx(Sfx.Shield, 1f, 0.8f);
            Haptics.Medium();
            FloatAt(GridView.ToWorld(doorPos), Loc.T("hud.exitOpen"), Palette.UiCyan);
            if (robot.IsAlive && !robot.IsHopping && robot.Position == doorPos) Escape();
        }

        private void Escape()
        {
            robot.EscapeInto();
            fx.Burst(GridView.ToWorld(doorPos) + Vector3.up * 0.5f, Palette.UiCyan, Palette.ShieldPickupGlow, 30, 4f);
            FloatAt(GridView.ToWorld(doorPos), Loc.T("float.escaped"), Palette.UiCyan);
            Win(escaped: true);
        }

        private void PaintTile(GridPos p)
        {
            if (!grid.InBounds(p) || grid.GetTile(p) != TileState.Solid || !painted.Add(p)) return;
            gridView.Paint(p);
            if (State == GameState.Playing)
            {
                // Each new tile sings a little higher as the platform fills up.
                AudioManager.PlaySfx(Sfx.Click, 0.8f, 0.9f + painted.Count / (float)grid.TileCount * 0.8f);
                Haptics.Light();
            }
            if (painted.Count >= grid.TileCount && State == GameState.Playing) Win();
        }

        private float MissionProgress()
        {
            if (level == null) return 0f;
            switch (level.mission)
            {
                case MissionType.CollectCoins: return (float)coinsThisRun / level.coinTarget;
                case MissionType.Exit: return portal != null && portal.IsOpen ? 1f : elapsed / level.exitDelay;
                case MissionType.Paint: return grid == null ? 0f : painted.Count / (float)grid.TileCount;
                default: return elapsed / level.surviveSeconds;
            }
        }

        private static string MissionText(LevelData data, bool upper = false)
        {
            string suffix = upper ? ".up" : "";
            switch (data.mission)
            {
                case MissionType.CollectCoins: return Loc.F("mission.collect" + suffix, data.coinTarget);
                case MissionType.Exit: return Loc.T("mission.exit" + suffix);
                case MissionType.Paint: return Loc.T("mission.paint" + suffix);
                case MissionType.CoinRain: return Loc.T("mission.rain" + suffix);
                default: return Loc.F("mission.survive" + suffix, data.surviveSeconds.ToString("0", CultureInfo.InvariantCulture));
            }
        }

        private void RefreshHud()
        {
            string Seconds(float s) => Mathf.Max(0f, s).ToString("0.0", CultureInfo.InvariantCulture);
            string text;
            switch (level.mission)
            {
                case MissionType.CollectCoins: text = Loc.F("hud.coins", coinsThisRun, level.coinTarget); break;
                case MissionType.Exit:
                    text = portal != null && portal.IsOpen
                        ? Loc.T("hud.exitOpen")
                        : Loc.F("hud.exitWait", Mathf.Max(1, Mathf.CeilToInt(level.exitDelay - elapsed)));
                    break;
                case MissionType.Paint: text = Loc.F("hud.paint", painted.Count, grid.TileCount); break;
                case MissionType.CoinRain: text = Loc.F("hud.rain", Seconds(level.surviveSeconds - elapsed)); break;
                default: text = Loc.F("hud.survive", Seconds(level.surviveSeconds - elapsed)); break;
            }
            ui.SetMission(text, MissionProgress(), coinsThisRun);
        }
    }
}
