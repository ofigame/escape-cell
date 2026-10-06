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
        private const int CloseCallsForArmor = 3;
        private const float ArmorDuration = 5f;
        private const float SuperArmorDuration = 10f;
        private const int ArmorsForSuper = 3;
        private const int CoinsPerRescue = 8;
        private const int MaxRescues = 3;
        private const float HoverCooldown = 8f;
        private const int FailsForAssist = 5;

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
        private DuctRunner runner;

        private GridModel grid;
        private LevelData level;
        private int levelIndex;
        private int coinsThisRun;
        private int comboBonus;  // extra coins from combos (missions and stars count pickups, not these)
        private int comboStreak;
        private float comboTimer;
        private const float ComboWindow = 6f;
        private const int ComboStep = 5;
        private int ComboMultiplier => comboStreak >= ComboStep * 2 ? 3 : comboStreak >= ComboStep ? 2 : 1;
        private int Earned => coinsThisRun + comboBonus;
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
        /// <summary>Something to reach: a key (Exit missions) or a lit button (the boss fight).</summary>
        private class Objective
        {
            public GridPos pos, home;
            public KeyPickup key;
            public BossButton button;
            public GameObject View => key != null ? key.gameObject : button != null ? button.gameObject : null;
        }

        private readonly List<Objective> objectives = new List<Objective>();
        private int objectivesDone, objectivesTotal;
        private bool spotKeys; // keys wait on the layout's own spots (all at once) instead of appearing far away one by one
        private WardenBoss warden;

        // Journey hazards: tiles crumbling behind the robot, and the wave eating the platform from the start.
        private readonly List<(GridPos pos, float left)> collapses = new List<(GridPos, float)>();
        private float chaseFront;
        private int chaseRow;
        private const float CollapseDelay = 0.7f;
        private const float CollapseRepair = 7f;
        private const float ChaseGraceRows = 3f;
        private const int FollowWindow = 6;
        private readonly HashSet<GridPos> painted = new HashSet<GridPos>();
        private bool bonusRun;
        private bool pendingEnding;
        private bool continued;   // the one ad-continue of this attempt is used
        private bool doubled;     // the 2x coins ad of this result is used
        private StarRules.Goals starGoals;

        // A rare surprise bonus round after beating a new level, on top of the ones stars unlock.
        private const float SurpriseBonusChance = 0.07f;
        private const int SurpriseFromLevel = 4;
        private const float TunnelChance = 0.7f;
        private const int TunnelExitBonus = 10;

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

            runner = new GameObject("DuctRunner").AddComponent<DuctRunner>();
            runner.Init(robot, cameraRig, input, fx);
            runner.CoinCollected += OnTunnelCoin;
            runner.Finished += OnTunnelFinished;

            CreateUi();
            ShowMenu();
            SplashScreen.Show(); // OFIGAME studio logo over the menu, fading out
        }

        private void CreateUi()
        {
            if (ui != null) Destroy(ui.gameObject);
            ui = UIController.Create(LevelCount);
            ui.PlayPressed += () => ShowMap();
            ui.LevelChosen += ShowPrelevel;
            ui.RetryPressed += () => ShowPrelevel(levelIndex);
            ui.PrelevelPlay += (index, shield, rescue) =>
            {
                ui.HidePrelevel();
                StartLevel(index, shield, rescue);
            };
            ui.GaragePressed += ShowGarage;
            ui.ShopPressed += ShowShop;
            ui.DailyPressed += ClaimDaily;
            ui.Garage.PreviewChanged += outfit => robot.ApplyOutfit(outfit);
            ui.Garage.DancePreview += robot.Cheer;
            ui.NextPressed += () =>
            {
                if (bonusRun) ShowMap();
                else ShowMap(animateFrom: levelIndex);
            };
            ui.BonusPressed += StartBonus;
            ui.ContinuePressed += WatchAdToContinue;
            ui.DoublePressed += WatchAdToDouble;
            ui.StoryPressed += world => PlayStory(world, world * LevelCatalog.LevelsPerWorld, () => ShowMap());
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
            robot.ApplyOutfit(Cosmetics.Outfit());
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
            foreach (var o in objectives)
                if (o.View != null) Destroy(o.View);
            objectives.Clear();
            objectivesDone = objectivesTotal = 0;
            if (warden != null) Destroy(warden.gameObject);
            warden = null;
            collapses.Clear();
            painted.Clear();
            Time.timeScale = 1f;
            slowMoLeft = 0f;
            hazards.Stop();
            coins.Stop();
            powerUps.Stop();
            runner.Stop();
            gridView.gameObject.SetActive(true);
        }

        /// <summary>The next level's platform idles behind the menu while the camera slowly orbits it.</summary>
        private void ShowBackdrop(int levelIdx)
        {
            ApplyTheme(levelIdx);
            var preview = levelSet.levels[levelIdx];
            grid = BuildGrid(preview);
            gridView.Build(grid, fx, preview.lowWalls);
            robot.ApplyOutfit(Cosmetics.Outfit());
            cameraRig.Frame(grid.Width, grid.Height);
            cameraRig.SetStyle(CameraStyle.MenuOrbit);
            cameraRig.SetMenuFocus(true);
            robot.Spawn(grid, grid.CenterFloor());
        }

        private void ShowMenu()
        {
            ResetRun();
            State = GameState.Menu;
            ShowBackdrop(NextLevel);
            ui.ShowMenu(NextLevel, SaveData.Coins, LevelCatalog.WorldName(NextLevel), MissionText(levelSet.levels[NextLevel]));
            // The robot is the star of the menu: crisp, close and in the middle of the screen.
            cameraRig.SetMenuFocus(false);
            cameraRig.Showcase(robot.transform, 0.7f);
        }

        private void ShowMap(int animateFrom = -1)
        {
            if (State != GameState.Menu) ShowBackdrop(NextLevel);
            ResetRun();
            State = GameState.Map;
            ui.ShowMap(SaveData.UnlockedLevel, SaveData.Coins, NextLevel, animateFrom);
            cameraRig.SetMenuFocus(true);
            cameraRig.Showcase(null, 0f);
        }

        /// <summary>The card before a level: its mission, best stars and boosts to take along.</summary>
        private void ShowPrelevel(int index)
        {
            index = Mathf.Clamp(index, 0, LevelCount - 1);
            ui.ShowPrelevel(index, LevelCatalog.WorldName(index), MissionText(levelSet.levels[index]), Progress.Stars(index),
                LevelCatalog.WorldOf(index) >= RescueFromWorld);
        }

        private void ShowGarage()
        {
            if (State != GameState.Menu) ShowMenu();
            ui.ShowGarage(LevelCatalog.WorldOf(SaveData.UnlockedLevel));
            cameraRig.Showcase(robot.transform, 1f);
            cameraRig.SetMenuFocus(false);
        }

        private void ShowShop()
        {
            if (State != GameState.Menu) ShowMenu();
            ui.ShowShop();
            cameraRig.Showcase(null, 0f);
            cameraRig.SetMenuFocus(true);
        }

        private void ClaimDaily()
        {
            int got = DailyChest.Claim();
            if (got <= 0) return;
            fx.Burst(robot.transform.position + Vector3.up * 0.8f, Palette.Coin, Palette.CoinGlow, 40, 6f);
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f);
            Haptics.Medium();
            robot.Cheer();
            FloatAt(robot.transform.position + Vector3.up * 0.4f, Loc.F("daily.got", got), Palette.UiGold);
            ui.RefreshMenuCoins();
        }

        private void StartLevel(int index, bool boostShield = false, bool boostRescue = false)
        {
            // A new floor opens with its story scene (once; the map banner replays it).
            int floor = LevelCatalog.WorldOf(index);
            if (index % LevelCatalog.LevelsPerWorld == 0 && !Story.Seen(floor))
            {
                PlayStory(floor, index, () => StartLevel(index, boostShield, boostRescue));
                return;
            }

            // Each attempt costs a life (unlocking a new level refills them). Without lives, offer the ad instead.
            if (!Lives.TryConsume())
            {
                pendingLevel = index;
                ui.ShowNoLives();
                return;
            }

            bonusRun = false;
            levelIndex = Mathf.Clamp(index, 0, LevelCount - 1);
            level = levelSet.levels[levelIndex].Clone();
            bool assisted = ApplyAssist(level);
            BeginRun();

            // Boosts picked on the before-level card are spent now.
            if (boostShield && Shop.TryUse(Boost.StartShield))
                GiveArmor(ArmorDuration, robot.Position, Loc.T("float.shield"), Palette.UiCyan);
            if (boostRescue && RescueEnabled && Shop.TryUse(Boost.ExtraRescue))
                rescues++;

            ShowLevelIntro(assisted);
            RefreshHud();
        }

        /// <summary>A story scene over the level's blurred platform, then <paramref name="done"/>.</summary>
        private void PlayStory(int scene, int themeLevel, System.Action done)
        {
            ResetRun();
            State = GameState.Map;
            ShowBackdrop(Mathf.Clamp(themeLevel, 0, LevelCount - 1));
            Story.MarkSeen(scene);
            ui.ShowStory(scene, LevelCatalog.WorldOf(themeLevel), done);
        }

        /// <summary>A bonus treasure vault: free (no life), nothing to lose, themed like the world the player is in.</summary>
        private void StartBonus()
        {
            if (Progress.BonusTokens <= 0) return;
            Progress.BonusTokens--;
            bonusRun = true;
            levelIndex = NextLevel;
            if (Random.value < TunnelChance)
            {
                StartTunnel();
                return;
            }
            level = LevelCatalog.Treasure(Random.Range(0, 100000));
            BeginRun();
            ui.ShowIntro(Loc.T("level.bonus"), MissionText(level, upper: true));
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.2f);
            RefreshHud();
        }

        /// <summary>The escape tunnel bonus: a third-person run down an air duct, themed like the current world.</summary>
        private void StartTunnel()
        {
            ResetRun();
            level = LevelCatalog.Treasure(0);
            level.mission = MissionType.Tunnel;
            coinsThisRun = 0;
            elapsed = 0f;
            ApplyTheme(levelIndex);
            gridView.gameObject.SetActive(false);
            cameraRig.SetStyle(CameraStyle.Gameplay);
            cameraRig.SetMenuFocus(false);
            runner.Begin(Random.Range(0, 100000));

            State = GameState.Playing;
            ui.ShowHud(-1);
            ui.ShowIntro(Loc.T("level.bonus"), MissionText(level, upper: true));
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.2f);
            RefreshHud();
        }

        private void OnTunnelCoin(Vector3 at)
        {
            coinsThisRun++;
            ui.FlyCoin(cameraRig.Cam.WorldToScreenPoint(at));
        }

        private void OnTunnelFinished(bool reachedExit)
        {
            if (State != GameState.Playing) return;
            State = GameState.Result;
            if (reachedExit)
            {
                coinsThisRun += TunnelExitBonus;
                FloatAt(robot.transform.position, "+" + TunnelExitBonus, Palette.UiGold);
            }
            SaveData.Coins += coinsThisRun;
            ShowBonusResult(Loc.T(reachedExit ? "result.tunnelOut" : "result.tunnelCrash"));
        }

        /// <summary>Builds the platform for <see cref="level"/> and starts play.</summary>
        /// <summary>The platform for a level; a layout sets its own size.</summary>
        private static GridModel BuildGrid(LevelData data)
        {
            bool shaped = data.layout != null && data.layout.Length > 0;
            int w = shaped ? data.layout[0].Length : data.gridWidth;
            int h = shaped ? data.layout.Length : data.gridHeight;
            return new GridModel(w, h, data.layout);
        }

        private void BeginRun()
        {
            ResetRun();
            continued = false;
            closeCalls = 0;
            jumpHintShown = false;
            coinsThisRun = 0;
            comboBonus = 0;
            comboStreak = 0;
            comboTimer = 0f;
            elapsed = 0f;
            armorsThisLevel = 0;
            rescues = 0;
            coinsTowardRescue = 0;
            hovering = false;
            hoverCooldown = 0f;
            input.HoldEnabled = HoverEnabled;

            ApplyTheme(levelIndex);
            grid = BuildGrid(level);
            gridView.Build(grid, fx, level.lowWalls);
            cameraRig.Frame(grid.Width, grid.Height);
            cameraRig.SetStyle(CameraStyle.Gameplay);
            cameraRig.SetMenuFocus(false);
            cameraRig.PlayIntro();
            robot.Spawn(grid, grid.StartSpot ?? grid.CenterFloor());

            // Long journeys don't fit the screen: the camera rides along and hazards and pickups stay near the robot.
            bool big = grid.Width > FollowWindow || grid.Height > FollowWindow;
            if (big) cameraRig.Follow(robot.transform, FollowWindow, FollowWindow);
            hazards.FocusRadius = big ? 3 : 0;
            coins.FocusRadius = big ? 4 : 0;
            powerUps.FocusRadius = big ? 4 : 0;

            SetupMission();
            hazards.Begin(grid, level, MissionProgress, LevelCatalog.WorldOf(levelIndex));
            coins.Begin(grid, level);
            powerUps.Begin(grid, level);

            starGoals = StarRules.For(level, grid.FloorCount);
            State = GameState.Playing;
            ui.ShowHud(bonusRun ? -1 : levelIndex);
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
                case SettingKind.TestMode:
                    SaveData.TestMode = !SaveData.TestMode;
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

        /// <summary>Lost? Watch an ad and carry on from a safe tile nearby, once per attempt.</summary>
        private void WatchAdToContinue()
        {
            if (continued || State != GameState.Result || !Ads.Rewarded.IsReady) return;
            Ads.Rewarded.Show(rewarded =>
            {
                if (rewarded && State == GameState.Result) Revive();
            });
        }

        private void Revive()
        {
            continued = true;
            StopAllCoroutines();
            // The loss already banked the coins and counted a fail; this run goes on instead.
            SaveData.Coins -= Earned;
            PlayerPrefs.SetInt(FailKey(levelIndex), Mathf.Max(0, PlayerPrefs.GetInt(FailKey(levelIndex), 0) - 1));

            var at = SafeTileNear(robot.Position, robot.Position);
            robot.Spawn(grid, at);
            robot.GiveShield(2.5f);
            hazards.Resume();
            coins.Resume();
            powerUps.Resume();
            State = GameState.Playing;
            cameraRig.SetMenuFocus(false);
            cameraRig.SetStyle(CameraStyle.Gameplay);
            ui.ShowHud(levelIndex);
            fx.Burst(GridView.ToWorld(at) + Vector3.up * 0.5f, Palette.ShieldPickup, Palette.ShieldPickupGlow, 30, 5f);
            FloatAt(GridView.ToWorld(at), Loc.T("float.revive"), Palette.UiCyan);
            AudioManager.PlaySfx(Sfx.Shield, 1f, 1.1f);
            Haptics.Medium();
            RefreshHud();
        }

        /// <summary>After a result: watch an ad to get the run's coins once more.</summary>
        private void WatchAdToDouble()
        {
            if (doubled || State != GameState.Result || !Ads.Rewarded.IsReady) return;
            int earned = Earned;
            Ads.Rewarded.Show(rewarded =>
            {
                if (!rewarded || doubled) return;
                doubled = true;
                SaveData.Coins += earned;
                ui.ShowDoubled(earned * 2);
            });
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
            BreakCombo();

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
            robot.GiveShield((super ? SuperArmorDuration : seconds) + Shop.ShieldBonusSeconds, Shop.ShieldHits);
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
                    hoverLeft = Shop.HoverSeconds;
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
            string journey = level.chaseSpeed > 0f ? "chase" : level.collapseBehind ? "collapse" : level.lowWalls ? "maze"
                : level.mission == MissionType.Exit && grid.KeySpots.Count > 0 ? "journey" : null;
            if (journey != null && !Seen(journey)) feature = "feature." + journey;
            else if (World >= HoverFromWorld && !Seen("hover")) feature = "feature.hover";
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

            if (runner.Active)
            {
                // The tunnel runs itself (input, robot, camera); just keep the HUD current.
                elapsed += Time.deltaTime;
                RefreshHud();
                return;
            }

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
            UpdateJourney(Time.deltaTime);
            comboTimer -= Time.deltaTime;
            ui.SetCombo(comboTimer > 0f ? ComboMultiplier : 1, comboTimer / ComboWindow);

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
                    if (elapsed >= level.surviveSeconds) Win();
                    break;
                case MissionType.CoinRain:
                    // The clock is the enemy here: miss the target and the round is lost.
                    // Reaching the target early doesn't end the round: extra coins earn extra stars.
                    if (elapsed >= level.surviveSeconds)
                    {
                        if (coinsThisRun >= level.coinTarget) Win();
                        else TimeUp();
                    }
                    break;
                case MissionType.Treasure:
                    if (elapsed >= level.surviveSeconds) Win();
                    break;
                case MissionType.Exit:
                    // A block, a hole or fire took the key's tile: it hops somewhere else.
                    UpdateObjectives();
                    break;
                case MissionType.Boss:
                    UpdateObjectives();
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
            if (robot.TrailColor.HasValue)
            {
                var trail = robot.TrailColor.Value;
                fx.Burst(GridView.ToWorld(p) + Vector3.up * 0.2f, trail * 0.45f, trail, 7, 1.8f);
            }

            coins.TryCollect(p);
            if (Shop.MagnetRange > 0) coins.CollectNear(p, Shop.MagnetRange);
            powerUps.TryCollect(p);

            // Collapsing paths: the tile just left crumbles a moment later.
            var left = robot.LastLeftTile;
            if (level.collapseBehind && left != p && grid.IsFloor(left) && !collapses.Exists(c => c.pos == left))
                collapses.Add((left, CollapseDelay));

            if (level.mission == MissionType.Paint) PaintTile(p);
            else if (level.mission == MissionType.Exit || level.mission == MissionType.Boss)
            {
                var reached = objectives.Find(o => o.pos == p);
                if (reached != null) CompleteObjective(reached);
                else if (portal != null && portal.IsOpen && p == doorPos) Escape();
            }
        }

        /// <summary>Crumbling tiles and the chasing wave, ticking every frame of a journey level.</summary>
        private void UpdateJourney(float dt)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 22f);
            for (int i = collapses.Count - 1; i >= 0; i--)
            {
                var (pos, timeLeft) = collapses[i];
                timeLeft -= dt;
                if (timeLeft <= 0f)
                {
                    hazards.Collapse(pos, CollapseRepair);
                    AudioManager.PlaySfx(Sfx.Fall, 0.25f, 1.4f, 0.1f);
                    collapses.RemoveAt(i);
                }
                else
                {
                    gridView.SetWarning(pos, pulse);
                    collapses[i] = (pos, timeLeft);
                }
            }

            if (level.chaseSpeed <= 0f) return;
            chaseFront += dt * level.chaseSpeed;
            int row = Mathf.FloorToInt(chaseFront);
            while (chaseRow < row && chaseRow < grid.Height - 1)
            {
                chaseRow++;
                for (int x = 0; x < grid.Width; x++) hazards.Collapse(new GridPos(x, chaseRow), 0f);
                cameraRig.Shake(0.25f);
                AudioManager.PlaySfx(Sfx.Impact, 0.35f, 0.7f, 0.1f);
            }
            // The next row to go glows red.
            int next = chaseRow + 1;
            if (next >= 0 && next < grid.Height && chaseFront > next - 1.5f)
                for (int x = 0; x < grid.Width; x++)
                {
                    var p = new GridPos(x, next);
                    if (grid.IsFloor(p)) gridView.SetWarning(p, pulse);
                }
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
                    // The shield takes the hit; upgraded shields crack and hold until their last hit.
                    bool broke = robot.AbsorbHit();
                    BreakCombo();
                    hazards.Shatter(p);
                    cameraRig.Shake(0.9f);
                    AudioManager.PlaySfx(Sfx.Blocked, 1f, broke ? 0.8f : 1.2f);
                    Haptics.Medium();
                    if (broke) fx.Burst(robot.transform.position + Vector3.up * 0.4f, Palette.ShieldPickup, Palette.ShieldPickupGlow, 24, 5f);
                    FloatAt(GridView.ToWorld(p), broke ? Loc.T("float.shieldBroke") : robot.ShieldHits < Shop.ShieldHits ? Loc.F("float.shieldCrack", robot.ShieldHits) : Loc.T("float.blocked"), broke ? Palette.UiRed : Palette.UiCyan);
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

            // Combo: coins picked up one after another (without taking a hit) count double, then triple.
            if (comboTimer <= 0f) comboStreak = 0;
            comboStreak++;
            comboTimer = ComboWindow;
            int multiplier = ComboMultiplier;
            comboBonus += multiplier - 1;
            FloatAt(GridView.ToWorld(p), "+" + multiplier, multiplier > 1 ? Palette.UiGold : Palette.UiGold);
            if (comboStreak == ComboStep || comboStreak == ComboStep * 2)
            {
                FloatAt(GridView.ToWorld(p) + Vector3.up * 0.5f, Loc.F("float.combo", multiplier), Palette.UiGold);
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1.5f);
                cameraRig.Punch(0.4f);
            }
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
            else if (level.mission == MissionType.CoinRain && coinsThisRun == level.coinTarget)
                FloatAt(GridView.ToWorld(p) + Vector3.up * 0.5f, Loc.T("float.target"), Palette.UiCyan);
        }

        /// <summary>Taking a hit ends the coin streak.</summary>
        private void BreakCombo()
        {
            comboStreak = 0;
            comboTimer = 0f;
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

            SaveData.Coins += Earned;
            if (bonusRun)
            {
                ShowBonusResult();
                return;
            }
            PlayerPrefs.DeleteKey(FailKey(levelIndex));

            // Only beating the newest level (unlocking the next one) refills lives; replays don't.
            bool unlockedNew = levelIndex >= SaveData.UnlockedLevel;
            if (unlockedNew) Lives.Refill();
            if (levelIndex + 1 > SaveData.UnlockedLevel && levelIndex + 1 < LevelCount)
                SaveData.UnlockedLevel = levelIndex + 1;

            // Stars: how well the level went. New stars fill the bonus meter.
            int stars = StarRules.Evaluate(starGoals, coinsThisRun, elapsed);
            int unlockedBonus = Progress.Award(levelIndex, stars, out _);
            bool surprise = false;
            if (unlockedBonus == 0 && unlockedNew && levelIndex + 1 >= SurpriseFromLevel && Progress.BonusTokens == 0 && Random.value < SurpriseBonusChance)
            {
                Progress.BonusTokens++;
                surprise = true;
            }

            pendingEnding = levelIndex == LevelCount - 1 && !Story.Seen(Story.Ending);
            bool hasNext = levelIndex + 1 < LevelCount;
            bool newWorld = unlockedNew && hasNext && (levelIndex + 1) % LevelCatalog.LevelsPerWorld == 0;
            string note = surprise ? Loc.T("bonus.surprise")
                : unlockedBonus > 0 ? Loc.T("bonus.ready")
                : stars < 3 ? StarHint(stars)
                : Loc.T("star.max");

            StartCoroutine(ShowResultDelayed(new UIController.ResultInfo
            {
                won = true,
                hasNext = hasNext,
                canDouble = true,
                subtitle = Loc.T(newWorld ? "result.newWorld" : hasNext ? "result.next" : "result.allDone"),
                note = note,
                coins = Earned,
                stars = stars,
                bonusAvailable = Progress.BonusTokens > 0,
                meter = unlockedBonus > 0 || surprise ? 1f : Progress.Meter / (float)Progress.StarsPerBonus,
                meterText = Loc.F("bonus.meter", unlockedBonus > 0 || surprise ? Progress.StarsPerBonus : Progress.Meter, Progress.StarsPerBonus),
            }));
        }

        /// <summary>What the next star asks for, e.g. "Next star: 14 coins".</summary>
        private string StarHint(int stars)
        {
            float goal = stars >= 2 ? starGoals.three : starGoals.two;
            return starGoals.timed
                ? Loc.F("star.time", goal.ToString("0", CultureInfo.InvariantCulture))
                : Loc.F("star.coins", goal.ToString("0", CultureInfo.InvariantCulture));
        }

        /// <summary>A bonus round always ends well: the coins are kept, nothing else changes.</summary>
        private void ShowBonusResult(string subtitle = null)
        {
            StartCoroutine(ShowResultDelayed(new UIController.ResultInfo
            {
                won = true,
                bonusRound = true,
                canDouble = true,
                hasNext = true,
                subtitle = subtitle ?? Loc.T("result.bonusSub"),
                coins = Earned,
                bonusAvailable = Progress.BonusTokens > 0,
                meter = Progress.Meter / (float)Progress.StarsPerBonus,
                meterText = Loc.F("bonus.meter", Progress.Meter, Progress.StarsPerBonus),
            }));
        }

        private void Lose(string reason)
        {
            State = GameState.Result;
            hazards.Freeze();
            coins.Freeze();
            powerUps.Freeze();
            if (hovering) { hovering = false; ShowHoverMarker(false); }

            // Coins picked up are kept even on a loss, so every run feels worth it.
            SaveData.Coins += Earned;
            if (bonusRun)
            {
                ShowBonusResult();
                return;
            }

            PlayerPrefs.SetInt(FailKey(levelIndex), PlayerPrefs.GetInt(FailKey(levelIndex), 0) + 1);
            StartCoroutine(ShowResultDelayed(new UIController.ResultInfo
            {
                won = false,
                note = Story.LoseQuip(),
                canContinue = !continued && Ads.Rewarded.IsReady,
                subtitle = reason + "\n" + Loc.F("lives.left", Lives.Count),
                coins = Earned,
                meter = Progress.Meter / (float)Progress.StarsPerBonus,
                meterText = Loc.F("bonus.meter", Progress.Meter, Progress.StarsPerBonus),
            }));
        }

        private IEnumerator ShowResultDelayed(UIController.ResultInfo info)
        {
            // The coin goal picked in the shop or garage, measured against the coins after this run.
            if (Goal.TryGet(out string goalName, out int goalPrice) && goalPrice > 0)
            {
                int coinsNow = SaveData.Coins;
                info.goalProgress = Mathf.Clamp01(coinsNow / (float)goalPrice);
                info.goalText = coinsNow >= goalPrice ? Loc.F("goal.ready", goalName) : Loc.F("goal.left", goalPrice - coinsNow, goalName);
            }
            doubled = false;
            ui.SetWarning(false);
            ui.SetShield(0f, 1f);
            yield return new WaitForSecondsRealtime(info.won ? 1.4f : 1.2f);
            slowMoLeft = 0f;
            Time.timeScale = 1f;
            if (!info.won) AudioManager.PlaySfx(Sfx.Lose, 0.8f);
            cameraRig.SetMenuFocus(true);
            if (pendingEnding)
            {
                // The very last level: the escape scene on the roof comes before the result card.
                pendingEnding = false;
                Story.MarkSeen(Story.Ending);
                ui.ShowStory(Story.Ending, World, () => ui.ShowResult(info));
                yield break;
            }
            ui.ShowResult(info);
        }

        // ---------- Mission ----------

        /// <summary>Per-mission setup: the exit door and its keys, WARDEN and its buttons, or the first painted tile.</summary>
        private void SetupMission()
        {
            hazards.IsProtected = null;
            objectivesDone = 0;
            chaseFront = -ChaseGraceRows;
            chaseRow = -1;

            if (level.mission == MissionType.Exit)
            {
                // Journey layouts mark the door; otherwise it goes on one of the tiles farthest from the robot.
                doorPos = grid.DoorSpot ?? FarTile(avoidDoor: false) ?? robot.Position;
                portal = ExitPortal.Create(GridView.ToWorld(doorPos) + Vector3.up * GridView.SurfaceY);
                hazards.IsProtected = p => p == doorPos;

                spotKeys = grid.KeySpots.Count > 0;
                if (spotKeys)
                {
                    objectivesTotal = grid.KeySpots.Count;
                    foreach (var spot in grid.KeySpots) AddObjective(spot);
                }
                else
                {
                    objectivesTotal = Mathf.Max(1, level.keys);
                    AddFarObjective();
                }
            }
            else if (level.mission == MissionType.Boss)
            {
                spotKeys = false;
                objectivesTotal = Mathf.Max(1, level.keys);
                var corner = GridView.ToWorld(new GridPos(grid.Width - 1, grid.Height - 1));
                warden = WardenBoss.Create(corner + new Vector3(0.9f, 2.3f, 0.9f), objectivesTotal, robot.transform);
                AddFarObjective();
            }
            else if (level.mission == MissionType.Paint)
            {
                PaintTile(robot.Position);
            }
        }

        /// <summary>A free tile far from the robot (never the door), or null if none is free right now.</summary>
        private GridPos? FarTile(bool avoidDoor = true)
        {
            int best = -1;
            var scored = new List<(GridPos pos, int d)>();
            foreach (var t in grid.AllPositions())
            {
                if (!grid.IsStandable(t) || t == robot.Position || hazards.IsThreatened(t)) continue;
                if (avoidDoor && portal != null && t == doorPos) continue;
                if (objectives.Exists(o => o.pos == t)) continue;
                int d = t.Manhattan(robot.Position);
                scored.Add((t, d));
                best = Mathf.Max(best, d);
            }
            if (scored.Count == 0) return null;
            var options = scored.FindAll(s => s.d >= best - 1);
            return options[Random.Range(0, options.Count)].pos;
        }

        /// <summary>The free tile closest to <paramref name="home"/> (a key whose spot got hit moves next door).</summary>
        private GridPos? NearTile(GridPos home)
        {
            GridPos? best = null;
            int bestD = int.MaxValue;
            foreach (var t in grid.AllPositions())
            {
                if (!grid.IsStandable(t) || t == doorPos || t == robot.Position || hazards.IsThreatened(t)) continue;
                if (objectives.Exists(o => o.pos == t)) continue;
                int d = t.Manhattan(home);
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        private void AddFarObjective()
        {
            var p = FarTile();
            if (p.HasValue) AddObjective(p.Value);
            else objectives.Add(new Objective { pos = new GridPos(-99, -99), home = robot.Position }); // placed next frame
        }

        private void AddObjective(GridPos p)
        {
            var o = new Objective { pos = p, home = p };
            MakeView(o, GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY);
            objectives.Add(o);
        }

        private void MakeView(Objective o, Vector3 at)
        {
            if (level.mission == MissionType.Boss) o.button = BossButton.Create(at);
            else o.key = KeyPickup.Create(at);
        }

        /// <summary>Keys and buttons whose tile got hit, burned or broke hop to another tile.</summary>
        private void UpdateObjectives()
        {
            foreach (var o in objectives)
            {
                if (grid.InBounds(o.pos) && grid.IsStandable(o.pos)) continue;
                var target = spotKeys ? NearTile(o.home) : FarTile();
                if (!target.HasValue) continue;
                o.pos = target.Value;
                var at = GridView.ToWorld(o.pos) + Vector3.up * GridView.SurfaceY;
                if (o.View == null) MakeView(o, at);
                else if (o.key != null) o.key.MoveTo(at);
                else o.button.MoveTo(at);
                fx.Dust(at + Vector3.up * 0.1f, Palette.UiCyan, 8, 1.5f);
            }
        }

        private void CompleteObjective(Objective o)
        {
            objectives.Remove(o);
            if (o.View != null) Destroy(o.View);
            objectivesDone++;
            var at = GridView.ToWorld(o.pos);
            cameraRig.Punch(0.5f);
            Haptics.Medium();

            if (level.mission == MissionType.Boss)
            {
                // A hit on WARDEN: it flinches and loses a light; the last one brings it down.
                fx.Burst(at + Vector3.up * 0.3f, new Color(1f, 0.4f, 0.45f), new Color(2.2f, 0.35f, 0.4f), 22, 5f);
                AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.8f);
                cameraRig.Shake(0.8f);
                int remaining = objectivesTotal - objectivesDone;
                FloatAt(at, Loc.T("float.bossHit"), Palette.UiGold);
                if (remaining <= 0)
                {
                    warden.Defeat();
                    fx.Burst(warden.transform.position, Palette.UiGold, Palette.CoinGlow, 40, 7f);
                    AudioManager.PlaySfx(Sfx.Squash, 1f, 0.6f);
                    Win();
                }
                else
                {
                    warden.Hit(remaining);
                    AddFarObjective();
                }
                return;
            }

            fx.Burst(at + Vector3.up * 0.5f, Palette.UiCyan, Palette.ShieldPickupGlow, 18, 4f);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.3f);
            if (objectivesDone >= objectivesTotal)
            {
                FloatAt(at, Loc.T("float.key"), Palette.UiCyan);
                OpenDoor();
            }
            else
            {
                FloatAt(at, Loc.F("float.keyLeft", objectivesTotal - objectivesDone), Palette.UiCyan);
                if (!spotKeys) AddFarObjective();
            }
        }

        private void TimeUp()
        {
            AudioManager.PlaySfx(Sfx.Bump);
            Haptics.Death();
            Lose(Loc.T("lose.time"));
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
                AudioManager.PlaySfx(Sfx.Click, 0.8f, 0.9f + painted.Count / (float)grid.FloorCount * 0.8f);
                Haptics.Light();
            }
            if (painted.Count >= grid.FloorCount && State == GameState.Playing) Win();
        }

        private float MissionProgress()
        {
            if (level == null) return 0f;
            switch (level.mission)
            {
                case MissionType.CollectCoins:
                case MissionType.CoinRain: return (float)coinsThisRun / level.coinTarget;
                case MissionType.Tunnel: return runner.Progress;
                case MissionType.Exit: return portal != null && portal.IsOpen ? 1f : objectivesDone / (objectivesTotal + 1f);
                case MissionType.Boss: return objectivesDone / (float)Mathf.Max(1, objectivesTotal);
                case MissionType.Paint: return grid == null ? 0f : painted.Count / (float)grid.FloorCount;
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
                case MissionType.CoinRain: return Loc.F("mission.rain" + suffix, data.coinTarget);
                case MissionType.Treasure: return Loc.T("mission.treasure" + suffix);
                case MissionType.Tunnel: return Loc.T("mission.tunnel" + suffix);
                case MissionType.Boss: return Loc.T("mission.boss" + suffix);
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
                        : Loc.F("hud.exitWait", objectivesDone, objectivesTotal);
                    break;
                case MissionType.Paint: text = Loc.F("hud.paint", painted.Count, grid.FloorCount); break;
                case MissionType.CoinRain: text = Loc.F("hud.rain", coinsThisRun, level.coinTarget, Seconds(level.surviveSeconds - elapsed)); break;
                case MissionType.Treasure: text = Loc.F("hud.treasure", coinsThisRun, Seconds(level.surviveSeconds - elapsed)); break;
                case MissionType.Tunnel: text = Loc.F("hud.tunnel", coinsThisRun, Mathf.RoundToInt(runner.Progress * 100f)); break;
                case MissionType.Boss: text = Loc.F("hud.boss", objectivesDone, objectivesTotal); break;
                default: text = Loc.F("hud.survive", Seconds(level.surviveSeconds - elapsed)); break;
            }
            ui.SetMission(text, MissionProgress(), Earned);
        }
    }
}
