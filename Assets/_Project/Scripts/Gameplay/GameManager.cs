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
    public partial class GameManager : MonoBehaviour
    {
        private const float CloseCallWindow = 0.3f;
        private const float SlowMoScale = 0.35f;
        private const float SlowMoDuration = 0.45f; // real seconds
        private const int CloseCallsForArmor = 4;
        private const float DodgeStreakGap = 12f;
        private const float ArmorDuration = 6f;
        private const float SuperArmorDuration = 10f;
        private const int ArmorsForSuper = 3;
        private const int CoinsPerRescue = 8;
        private static int MaxRescues => Shop.MaxRescues;
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
        private FloorRules floorRules;
        private EnemySystem enemies;
        private LevelEvents levelEvents;
        private Weather weather;
        private bool dailyRun;      // a daily bonus game (not a level's bonus round)

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
        private int Earned => (coinsThisRun + comboBonus) * (doubleCoins ? 2 : 1);
        private bool doubleCoins; // the Double Coins boost is on for this run
        private float elapsed;
        private float slowMoLeft;
        private WorldTheme themeNow;
        private int closeCalls; // dodges in a row toward armor
        private float lastDodgeTime;
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
            public QuestItem item;
            public BossButton button;
            public GameObject View => key != null ? key.gameObject : button != null ? button.gameObject : item != null ? item.gameObject : null;
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
        // The window the camera keeps around the robot (fitted to the screen width, so on a tall phone it shows more rows
        // than columns). Every floor bigger than the first one gets the same close view as level 1, gliding with the robot;
        // dangers and pickups then stay within FocusRange of it, so nothing falls where the player can't see.
        private const int FollowWindow = 3;
        private const int FollowFrom = 3;
        private const int FocusRange = 3;
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
            weather = Weather.Create(fx);
            gridView = new GameObject("Grid").AddComponent<GridView>();
            robot = Robot.Create(null);
            robot.Arrived += OnRobotArrived;
            robot.BumpedInto += OnRobotBumped;
            robot.BlockSmasher = SmashBlock;

            hazards = new GameObject("Hazards").AddComponent<HazardSystem>();
            hazards.Init(gridView, robot, fx, cameraRig);
            hazards.Impact += OnBlockImpact;
            hazards.CalmChanged += calm =>
            {
                // The rhythm is felt: a breather is announced, and so is the next storm.
                if (State != GameState.Playing || roadPhase != RoadPhase.None) return;
                FloatAt(robot.transform.position + Vector3.up * 0.9f, Loc.T(calm ? "float.calm" : "float.storm"), calm ? new Color(0.55f, 1f, 0.75f) : Palette.UiRed);
                if (!calm) cameraRig.Shake(0.4f);
            };
            hazards.TileBroken += OnTileBroken;
            hazards.Dodged += OnDodged;

            coins = new GameObject("Coins").AddComponent<CoinSystem>();
            coins.Init(robot, hazards, fx);
            coins.Collected += OnCoinCollected;

            powerUps = new GameObject("PowerUps").AddComponent<PowerUpSystem>();
            powerUps.Init(robot, hazards, fx);
            powerUps.Collected += OnPowerUpCollected;
            powerUps.WantsHeart = WantsHeart;

            enemies = new GameObject("Enemies").AddComponent<EnemySystem>();
            enemies.Init(gridView, robot, hazards, fx);
            InitHunt();
            enemies.Hit += OnBlockImpact;
            enemies.Shove += dir => { if (State == GameState.Playing && robot.IsAlive && !robot.IsHopping) robot.Shove(dir, 1, 0.3f, 1.2f); };
            floorRules = new GameObject("FloorRules").AddComponent<FloorRules>();
            floorRules.Init(gridView, robot, hazards, fx, cameraRig);
            floorRules.Hit += OnBlockImpact;

            levelEvents = new GameObject("LevelEvents").AddComponent<LevelEvents>();
            levelEvents.Init(robot, coins, hazards, fx, cameraRig);
            levelEvents.CrateOpened += OnCrateOpened;
            levelEvents.AlarmChanged += on => ui.SetAlarm(on);
            levelEvents.Started += e => ui.ShowIntro(Loc.T("event.title"), Loc.T("event." + e));

            runner = new GameObject("DuctRunner").AddComponent<DuctRunner>();
            runner.Init(robot, cameraRig, input, fx);
            runner.CoinCollected += OnTunnelCoin;
            runner.Finished += OnTunnelFinished;
            runner.Arrived += OnRoadArrived;
            runner.RoadFailed += OnRoadFailed;
            runner.RiskTaken += () => FloatAt(robot.transform.position + Vector3.up * 0.5f, Loc.T("float.risk"), Palette.UiRed);
            runner.Notice += (key, at) => FloatAt(at, Loc.T(key), Palette.UiCyan);

            CreateUi();
            ShowMenu();
            // Closed on a road last time: the menu comes first, PLAY goes back onto the road.
            if (PendingRoad >= LevelCount) PlayerPrefs.DeleteKey(RoadLevelKey);
            FrameGovernor.Install(); // picture quality for this device (after the camera's post-processing exists)
            AdMob.Start(); // consent form where required, then AdMob (phones only)
            SplashScreen.Show(); // OFIGAME studio logo over the menu, fading out
        }

        private void CreateUi()
        {
            if (ui != null) Destroy(ui.gameObject);
            ui = UIController.Create(LevelCount);
            ui.PlayPressed += () =>
            {
                if (PendingRoad >= 0) ResumeRoad(PendingRoad); // a level won but its road not walked yet
                else ShowMap();
            };
            ui.LevelChosen += ShowPrelevel;
            ui.RetryPressed += () => ShowPrelevel(levelIndex);
            ui.PrelevelPlay += (index, boosts) =>
            {
                ui.HidePrelevel();
                StartLevel(index, boosts);
            };
            ui.Map.LevelOf = i => i >= 0 && i < LevelCount ? levelSet.levels[i] : null;
            ui.Map.RobotLook = t => robot.BuildLookalike(t);
            ui.GaragePressed += ShowGarage;
            ui.ShopPressed += ShowShop;
            ui.DailyPressed += ClaimDaily;
            ui.DailyBonusPressed += () => ui.DailyBonus.Show();
            ui.DailyBonus.PlayPressed += PlayDailyBonus;
            ui.DailyBonus.AdPressed += WatchAdForDailyBonus;
            ui.Garage.PreviewChanged += outfit => robot.ApplyOutfit(outfit);
            ui.Garage.DancePreview += robot.Cheer;
            ui.SkillBar.Pressed += OnSkillPressed;
            ui.NextPressed += () =>
            {
                if (dailyRun) ShowMenu();
                else if (bonusRun) ShowMap();
                else
                {
                    // Between levels an interstitial may come first (paced: see Ads.AfterLevel).
                    int won = levelIndex;
                    Ads.AfterLevel(won, () => ShowMap(animateFrom: won));
                }
            };
            ui.BonusPressed += StartBonus;
            ui.ContinuePressed += WatchAdToContinue;
            ui.ContinueCoinsPressed += BuyContinue;
            ui.DoublePressed += WatchAdToDouble;
            ui.StoryPressed += world => PlayStory(world, world * LevelCatalog.LevelsPerWorld, () => ShowMap());
            ui.MapPressed += () => ShowMap();
            ui.MenuPressed += ShowMenu;
            ui.PausePressed += Pause;
            ui.ResumePressed += Resume;
            ui.SettingToggled += OnSettingToggled;
            ui.Languages.Chosen += language =>
            {
                Loc.Set(language);
                // Every label is baked at build time, so rebuild the UI in the new language.
                CreateUi();
                ShowMenu();
                ui.ShowSettings();
            };
            ui.WatchAdPressed += WatchAdForLife;
            ui.ToolPressed += OnToolPressed;
            input.Ignore = p => ui != null && ui.IsOverTool(p);
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

        /// <summary>
        /// Switch the backdrop, platform colors, weather and lighting to the design <paramref name="levelIdx"/> is played
        /// in: its floor's first design for levels 1-5, the second for 6-10.
        /// </summary>
        private void ApplyTheme(int levelIdx)
        {
            int world = LevelCatalog.WorldOf(levelIdx);
            var theme = WorldTheme.ForLevel(levelIdx);
            if (theme == themeNow) return;
            themeNow = theme;
            WorldTheme.SetCurrent(theme);
            robot.ApplyWorld(world);
            robot.ApplyOutfit(Cosmetics.Outfit());
            RenderSettings.ambientLight = Palette.Ambient * 0.8f;
            cameraRig.RefreshTheme();
            weather.Apply(theme.weather, theme.accent);
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
            ClearQuest();
            ClearMonster();
            ClearThief();
            ClearEscort();
            ClearClones();
            DropBip();
            ClearAlly();
            ClearMarathon();
            ClearMonsterShield();
            skillFreezeLeft = skillMagnetLeft = 0f;
            doubleCoins = false;
            previewing = false;
            pendingIntro = null;
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
            roadPhase = RoadPhase.None;
            weather.SetVisible(true);
            weather.SetIntensity(0.45f);
            if (roadBeacon != null) Destroy(roadBeacon);
            gridView.gameObject.SetActive(true);
            floorRules.Stop();
            enemies.Stop();
            hunt.Stop();
            hazards.Hunting = false;
            levelEvents.Stop();
            cameraRig.FrameUpper(0f, 1f);
            cameraRig.Showcase(null, 0f); // the menu's close-up on the robot must never leak into a level
            HideTools();
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
            AudioManager.PlayMusic(MusicTheme.Menu);
            AudioManager.SetTension(0f);
            ResetRun();
            State = GameState.Menu;
            ShowBackdrop(NextLevel);
            ui.ShowMenu(NextLevel, SaveData.Coins, LevelCatalog.WorldName(NextLevel),
                PendingRoad >= 0 ? Loc.T("menu.roadPending") : MissionText(levelSet.levels[NextLevel]));
            if (lobby == null) lobby = LobbyStage.Create(t => robot.BuildLookalike(t));
            lobby.Open(WorldTheme.ForWorld(LevelCatalog.WorldOf(NextLevel)));
            // The robot is the star of the menu: crisp, close and in the middle of the screen.
            cameraRig.SetMenuFocus(false);
            cameraRig.Showcase(robot.transform, 0.7f);
        }

        // The menu's showroom and the 3D map have their own cameras; the game's camera only draws when neither is up.
        private LobbyStage lobby;

        private void LateUpdate()
        {
            if (ui == null) return;
            // The splash waits for the menu to stand (a few frames in, when the lobby has drawn).
            if (!SplashScreen.Ready && Time.frameCount > 5) SplashScreen.Ready = true;
            if (lobby != null && lobby.IsOpen && !ui.MenuVisible) lobby.Close();
            bool covered = (lobby != null && lobby.IsOpen) || ui.Map.IsOpen;
            if (cameraRig.Cam.enabled == covered) cameraRig.Cam.enabled = !covered;
            UpdateCloseCamera();
        }

        private void ShowMap(int animateFrom = -1)
        {
            // Out of the test cell (level 10): Bip opens the backpack workshop for the first time.
            if (WorkshopTalk.IntroDue && PendingRoad < 0)
            {
                ShowShop();
                return;
            }
            AudioManager.PlayMusic(MusicTheme.Menu);
            AudioManager.SetTension(0f);
            if (State != GameState.Menu) ShowBackdrop(NextLevel);
            ResetRun();
            State = GameState.Map;
            ui.ShowMap(SaveData.UnlockedLevel, SaveData.Coins, NextLevel, animateFrom);
            cameraRig.SetMenuFocus(false); // the 3D map has its own camera; the blur is for the menus over the floor
            cameraRig.Showcase(null, 0f);
        }

        /// <summary>The card before a level: its mission, best stars and boosts to take along.</summary>
        private void ShowPrelevel(int index)
        {
            index = Mathf.Clamp(index, 0, LevelCount - 1);
            if (index == PendingRoad)
            {
                ResumeRoad(index);
                return;
            }
            // Bought rescues need no slot here: they wait in reserve and step in by themselves (see TryRescue).
            var data = levelSet.levels[index];
            ui.ShowPrelevel(index, LevelCatalog.WorldName(index), MissionText(data), Progress.Stars(index), data.mission == MissionType.Monster, SuggestedBoost(data), Shop.BoostsAllowed(data));
        }

        /// <summary>The boost that helps most on this level: the hammer for a monster, double coins for coin hunts, else a shield.</summary>
        private static Boost SuggestedBoost(LevelData data)
        {
            switch (data.mission)
            {
                case MissionType.Monster: return Boost.StartHammer;
                case MissionType.Thief:
                case MissionType.CollectCoins:
                case MissionType.CoinRain: return Boost.DoubleCoins;
                default: return Boost.StartShield;
            }
        }

        private void ShowGarage()
        {
            if (State != GameState.Menu) ShowMenu();
            ui.ShowGarage(LevelCatalog.WorldOf(SaveData.UnlockedLevel));
            cameraRig.Showcase(robot.transform, 1f, 1.9f, 0.55f); // the whole robot, above the paint panel
            robot.FaceCamera();
            cameraRig.SetMenuFocus(false);
        }

        /// <summary>A daily bonus play: the day's next game (or a given one).</summary>
        private void PlayDailyBonus(BonusGame? chosen)
        {
            var game = chosen ?? DailyBonus.NextGame();
            if (!DailyBonus.Use())
            {
                ui.DailyBonus.Refresh();
                return;
            }
            ui.DailyBonus.Hide();
            bonusRun = true;
            dailyRun = true;
            levelIndex = NextLevel;
            StartBonusGame(game);
        }

        /// <summary>Out of daily plays: one more for an ad, once a day. No ad, no harm: nothing is used up.</summary>
        private void WatchAdForDailyBonus()
        {
            if (!DailyBonus.CanWatchAd) return;
            if (!Ads.Rewarded.IsReady)
            {
                ui.ShowIntro(Loc.T("daily.bonusTitle"), Loc.T("ad.unavailable"));
                return;
            }
            Ads.Rewarded.Show(rewarded =>
            {
                if (rewarded) DailyBonus.GrantAdPlay();
                ui.DailyBonus.Refresh();
                ui.RefreshDailyBonus();
            });
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

        private void StartLevel(int index, List<Boost> boosts = null)
        {
            // A new floor opens with its story scene (once; the map banner replays it).
            int floor = LevelCatalog.WorldOf(index);
            if (index % LevelCatalog.LevelsPerWorld == 0 && !Story.Seen(floor))
            {
                PlayStory(floor, index, () => StartLevel(index, boosts));
                return;
            }

            // Each attempt costs a life (unlocking a new level refills them). Without lives, offer the ad instead.
            if (!Lives.TryConsume())
            {
                pendingLevel = index;
                ui.ShowNoLives();
                return;
            }

            if (PendingRoad >= 0) PlayerPrefs.DeleteKey(RoadLevelKey); // another level was chosen: that road is given up
            bonusRun = false;
            dailyRun = false;
            levelIndex = Mathf.Clamp(index, 0, LevelCount - 1);
            level = levelSet.levels[levelIndex].Clone();
            bool assisted = ApplyAssist(level);
            BeginRun();

            // Boosts picked on the before-level card are spent now (never on boss or marathon levels).
            if (boosts != null && Shop.BoostsAllowed(level))
            {
                if (boosts.Contains(Boost.StartShield) && Shop.TryUse(Boost.StartShield))
                    GiveArmor(ArmorDuration, robot.Position, Loc.T("float.shield"), Palette.UiCyan);
                if (boosts.Contains(Boost.StartHammer) && level.mission == MissionType.Monster && Shop.TryUse(Boost.StartHammer))
                    FillHammerBag();
                if (boosts.Contains(Boost.DoubleCoins) && Shop.TryUse(Boost.DoubleCoins))
                {
                    doubleCoins = true;
                    FloatAt(robot.transform.position + Vector3.up * 0.6f, Loc.T("float.doubleCoins"), Palette.UiGold);
                }
                if (boosts.Contains(Boost.CoinMagnet) && Shop.TryUse(Boost.CoinMagnet))
                {
                    skillMagnetLeft = 99999f; // the whole level
                    FloatAt(robot.transform.position + Vector3.up * 0.9f, Loc.T("float.skillMagnet"), new Color(1f, 0.4f, 0.45f));
                }
            }

            ShowLevelIntro(assisted);
            RefreshHud();
            ui.SkillBar.Refresh(!bonusRun);
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
            dailyRun = false;
            levelIndex = NextLevel;
            var tunnels = new[] { BonusGame.Duct, BonusGame.Surf, BonusGame.Mine };
            StartBonusGame(Random.value < TunnelChance ? tunnels[Random.Range(0, tunnels.Length)] : BonusGame.Treasure);
        }

        /// <summary>Starts one of the bonus games: a tunnel run or the treasure room.</summary>
        private void StartBonusGame(BonusGame game)
        {
            DailyBonus.MarkSeen(game);
            if (game != BonusGame.Treasure)
            {
                StartTunnel((DuctRunner.Kind)(int)game);
                return;
            }
            level = LevelCatalog.Treasure(Random.Range(0, 100000));
            BeginRun();
            ui.ShowIntro(Loc.T("level.bonus"), MissionText(level, upper: true));
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.2f);
            RefreshHud();
        }

        /// <summary>The escape tunnel bonus: a third-person run down an air duct, themed like the current world.</summary>
        private void StartTunnel(DuctRunner.Kind tunnel)
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
            ui.ShowHud(-1); // the result card or map goes first, whatever the run setup does
            runner.Begin(Random.Range(0, 100000), tunnel);
            AudioManager.PlayMusic(MusicTheme.Tunnel);

            State = GameState.Playing;
            ui.ShowIntro(Loc.T("level.bonus"), Loc.T("tunnel." + tunnel));
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.2f);
            RefreshHud();
        }

        private void OnTunnelCoin(Vector3 at)
        {
            if (roadPhase == RoadPhase.None) coinsThisRun = runner.Coins;
            ui.FlyCoin(cameraRig.Cam.WorldToScreenPoint(at));
        }

        private void OnTunnelFinished(bool reachedExit, int bonus)
        {
            if (State != GameState.Playing) return;
            State = GameState.Result;
            coinsThisRun = runner.Coins;
            if (reachedExit && bonus > 0)
            {
                coinsThisRun += bonus;
                FloatAt(robot.transform.position, "+" + bonus, Palette.UiGold);
            }
            SaveData.Coins += coinsThisRun;
            ShowBonusResult(Loc.T(reachedExit ? "result.tunnelOut" : "result.tunnelCrash"));
        }

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
            BipReset();
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
            if (level.final) gridView.SetPaintColor(FinalGold);
            robot.Spawn(grid, grid.StartSpot ?? grid.CenterFloor());

            // Long journeys don't fit the screen: the camera rides along and hazards and pickups stay near the robot.
            bool big = grid.Width > FollowFrom || grid.Height > FollowFrom;
            if (big) cameraRig.Follow(robot.transform, FollowWindow, FollowWindow);
            hazards.FocusRadius = big ? FocusRange : 0;
            coins.FocusRadius = big ? FocusRange : 0;
            powerUps.FocusRadius = big ? FocusRange : 0;

            SetupMission();
            hazards.Begin(grid, level, MissionProgress);
            floorRules.Begin(grid, level, levelIndex, p => hazards.IsProtected != null && hazards.IsProtected(p));
            enemies.Begin(grid, level, levelIndex, p => hazards.IsProtected != null && hazards.IsProtected(p),
                p => level.mission == MissionType.Paint && painted.Contains(p), Unpaint);
            hazards.Hunting = (level.rules & FloorRule.Hunter) != 0;
            levelEvents.Begin(grid, level, levelIndex);
            coins.Begin(grid, level);
            powerUps.Begin(grid, level, LevelCatalog.WorldOf(levelIndex));

            starGoals = StarRules.For(level, grid.FloorCount);
            SetupTools();
            SetupHealth();
            State = GameState.Playing;
            AudioManager.PlayMusic(LevelMusic());
            ui.ShowHud(bonusRun ? -1 : levelIndex);
            StartMissionPreview();
            if (!bonusRun) StartCoroutine(HelperTip());
        }

        /// <summary>Once the level is on: the helper's tip for this cell, in the little speech bubble.</summary>
        private IEnumerator HelperTip()
        {
            while (previewing) yield return null;
            yield return new WaitForSeconds(0.8f);
            if (State != GameState.Playing || !level.cardGoal || !Loc.Has(CardKey("help"))) yield break;
            bipLast = Time.time;
            ui.Bip.Say(Loc.T(CardKey("help")), HelperName, HelperColor);
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
                case SettingKind.Graphics:
                    GraphicsQuality.Next();
                    break;
                case SettingKind.Camera:
                    SaveData.CameraDistance = (SaveData.CameraDistance + 1) % 4; // near → medium → far → farthest
                    break;
                case SettingKind.Language:
                    ui.Languages.Show();
                    return;
            }
            ui.RefreshSettings();
        }

        /// <summary>Carrying on for coins instead of an ad: a little more in later worlds.</summary>
        private int ContinuePrice => 60 + 10 * World;

        private void BuyContinue()
        {
            if (continued || State != GameState.Result) return;
            // The loss already banked this run's coins; the price comes out of what was there before.
            if (SaveData.Coins - Earned < ContinuePrice) return;
            Revive();
            SaveData.Coins -= ContinuePrice;
            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
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

        private const int ReviveSeconds = 15;

        private void Revive()
        {
            continued = true;
            StopAllCoroutines();
            // The loss already banked the coins and counted a fail; this run goes on instead.
            SaveData.Coins -= Earned;
            PlayerPrefs.SetInt(FailKey(levelIndex), Mathf.Max(0, PlayerPrefs.GetInt(FailKey(levelIndex), 0) - 1));

            // A run lost to the clock gets time back, or it would end again at once.
            bool timed = level.mission == MissionType.CoinRain;
            if (timed) elapsed = Mathf.Min(elapsed, level.surviveSeconds - ReviveSeconds);

            var at = SafeTileNear(robot.Position, robot.Position);
            robot.Spawn(grid, at);
            robot.GiveShield(2.5f);
            hazards.Resume();
            coins.Resume();
            powerUps.Resume();
            floorRules.Resume();
            enemies.Resume();
            hunt.Resume();
            levelEvents.Resume();
            State = GameState.Playing;
            cameraRig.SetMenuFocus(false);
            cameraRig.SetStyle(CameraStyle.Gameplay);
            ui.ShowHud(levelIndex);
            fx.Burst(GridView.ToWorld(at) + Vector3.up * 0.5f, Palette.ShieldPickup, Palette.ShieldPickupGlow, 30, 5f);
            FloatAt(GridView.ToWorld(at), Loc.T("float.revive"), Palette.UiCyan);
            if (timed) FloatAt(GridView.ToWorld(at) + Vector3.up * 0.6f, Loc.F("float.moreTime", ReviveSeconds), Palette.UiGold);
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
            if (robot.InArena) return;
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

            if (grid.GetTile(p) == TileState.Poison)
            {
                robot.Squash();
                fx.Burst(robot.transform.position + Vector3.up * 0.3f, new Color(0.5f, 1f, 0.4f), new Color(0.6f, 1.8f, 0.3f), 26, 4f);
                AudioManager.PlaySfx(Sfx.Squash, 0.8f, 0.8f);
                Haptics.Death();
                Lose(Loc.T("lose.poison"));
                return;
            }

            robot.FallIntoHole();
            AudioManager.PlaySfx(Sfx.Fall);
            Haptics.Death();
            Lose(Loc.T("lose.fall"));
        }

        /// <summary>
        /// Spend a rescue charge instead of dying: smash the block, or bounce out of the hole/fire. Out of charges, a
        /// rescue bought in the shop steps in by itself (in any world): it is only used up when it actually saves the robot.
        /// </summary>
        private bool TryRescue(GridPos p, bool crushed)
        {
            if (TryTakeHealthHit(p, crushed, crushed ? 1f : 0.75f)) return true;
            bool bought = false;
            if (RescueEnabled && rescues > 0) rescues--;
            else if (Shop.TryUse(Boost.ExtraRescue)) bought = true;
            else return false;
            BreakCombo();

            if (crushed) hazards.Shatter(p);
            else robot.RescueTo(SafeTileNear(robot.LastLeftTile, p));

            robot.GiveShield(1.5f); // a moment to get out of trouble
            cameraRig.Shake(0.8f);
            cameraRig.Punch(0.8f);
            fx.Burst(robot.transform.position + Vector3.up * 0.4f, Palette.UiCyan, Palette.ShieldPickupGlow, 24, 5f);
            AudioManager.PlaySfx(Sfx.Blocked);
            Haptics.Medium();
            FloatAt(GridView.ToWorld(p), Loc.T(bought ? "float.rescuedShop" : "float.rescued"), bought ? Palette.UiGold : Palette.UiCyan);
            BipSay("rescue");
            return true;
        }

        /// <summary>Armor from a pickup or four dodges in a row; every third one in a level is a long "super" armor.</summary>
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
            // While the camera shows the goal, its bubble speaks; the level banner follows right after.
            if (previewing)
            {
                pendingIntro = assisted;
                return;
            }
            string feature = null;
            // A floor rule met for the first time is introduced before anything else.
            string ruleFeature = null;
            foreach (FloorRule r in System.Enum.GetValues(typeof(FloorRule)))
                if (r != FloorRule.None && (level.rules & r) != 0 && !Seen("rule." + r)) { ruleFeature = "feature.rule." + r; break; }
            // So is a moving enemy met for the first time.
            if (ruleFeature == null)
            {
                int[] counts = { level.sweepers, level.erasers, level.drones, level.sandworms, level.crabs, level.turrets, level.penguins, level.springbots };
                for (int i = 0; i < counts.Length; i++)
                    if (counts[i] > 0 && !Seen("enemy." + (EnemyKind)i)) { ruleFeature = "feature.enemy." + (EnemyKind)i; break; }
            }
            string journey = level.chaseSpeed > 0f ? "chase" : level.collapseBehind ? "collapse" : level.lowWalls ? "maze"
                : level.mission == MissionType.Exit && grid.KeySpots.Count > 0 ? "journey" : null;
            if (trialSlot && !Seen("tools")) feature = "feature.tools";
            else if (HealthEnabled && !Seen("health")) feature = "feature.health";
            else if (ruleFeature != null) feature = ruleFeature;
            else if (journey != null && !Seen(journey)) feature = "feature." + journey;
            else if (World >= HoverFromWorld && !Seen("hover")) feature = "feature.hover";
            else if (World >= FireFromWorld && !Seen("fire")) feature = "feature.fire";
            else if (World >= RescueFromWorld && !Seen("rescue")) feature = "feature.rescue";

            if (briefedNow && feature == null && !assisted)
            {
                PlayerPrefs.SetInt("sb_seen_feature.mission." + level.mission, 1);
                return;
            }

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

            // Close-call slow motion and the slow-motion tool both bend time; the slower one wins.
            if (slowMoLeft > 0f) slowMoLeft -= Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Min(slowMoLeft > 0f ? SlowMoScale : 1f, toolSlowLeft > 0f && State == GameState.Playing ? Tools.SlowScale : 1f);

            if (State != GameState.Playing) return;

            if (roadPhase == RoadPhase.Walk)
            {
                UpdateRoadWalk();
                return;
            }

            if (runner.Active)
            {
                // The tunnel runs itself (input, robot, camera); just keep the HUD current.
                AudioManager.SetTension(0.55f + 0.4f * runner.Progress);
                weather.SetVisible(false);
                elapsed += Time.deltaTime;
                if (roadPhase == RoadPhase.None) coinsThisRun = runner.Coins;
                RefreshHud();
                return;
            }

            if (previewing)
            {
                RefreshHud();
                AudioManager.SetTension(0.05f);
                return;
            }

            UpdateBip(Time.deltaTime);
            UpdateAlly(Time.deltaTime);
            AudioManager.SetTension(Tension());
            // The sky builds from calm to storm as the mission nears its end.
            if (level.mission == MissionType.Hunt) weather.SetVisible(false); // nothing but crates falls in the core loop
            else weather.SetIntensity(Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(MissionProgress())));
            elapsed += Time.deltaTime;

            // In an arena fight the arena reads the touches itself.
            var command = ArenaActive ? default(InputCommand) : input.Poll(robot.transform.position);
            UpdateHover(command);
            if (!hovering)
            {
                if (command.tap.HasValue && level.mission == MissionType.Hunt && TapStrike(command.tap.Value)) input.CancelTap();
                else if (command.jump) robot.TryJump();
                else if (command.move.HasValue) robot.TryMove(command.move.Value);
            }

            ui.SetWarning(hazards.AnyWarningActive);
            ui.SetShield(robot.ShieldLeft, Mathf.Max(level.shieldDuration, ArmorDuration));
            ui.Skills.Set(SkillIcon.Super, superLeft, SuperSeconds);
            ui.Skills.Set(SkillIcon.Shield, superLeft > 0f ? 0f : robot.ShieldLeft, Mathf.Max(level.shieldDuration, ArmorDuration));
            ui.Skills.Set(SkillIcon.Freeze, skillFreezeLeft, 4f);
            ui.Skills.Set(SkillIcon.Magnet, skillMagnetLeft, 8f);
            ui.Skills.Set(SkillIcon.Hammer, charged ? 1f : 0f, -1f);

            ui.SetRescues(RescueEnabled, rescues, coinsTowardRescue / (float)CoinsPerRescue);
            ui.SetHover(HoverEnabled, hoverCooldown);
            UpdateJourney(Time.deltaTime);
            UpdateSkills(Time.deltaTime);
            UpdateHealth(Time.deltaTime);
            UpdateSuperSkill(Time.deltaTime);
            if (level.marathon) UpdateMarathon(Time.deltaTime);
            ui.Arrows.Set(cameraRig.Cam, Goals());
            comboTimer -= Time.deltaTime;
            ui.SetCombo(comboTimer > 0f ? ComboMultiplier : 1, comboTimer / ComboWindow);
            UpdateTools();

            // Armor lets the robot stand over a hole or fire; once it wears off, gravity (or heat) wins.
            if (robot.IsAlive && !robot.InArena && !robot.IsHopping && !robot.IsHovering && !robot.IsShielded && grid.IsGap(robot.Position))
            {
                StepIntoGap(robot.Position);
                return;
            }
            // A goal against the clock (coin hunts, roads, paint): out of time is out.
            if (level.timeLimit > 0f && elapsed >= level.timeLimit && level.mission != MissionType.CoinRain)
            {
                TimeUp();
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
                case MissionType.Quest:
                    UpdateObjectives();
                    break;
                case MissionType.Monster:
                    UpdateMonster(Time.deltaTime);
                    UpdateArena(Time.deltaTime);
                    UpdateMonsterShield(Time.deltaTime);
                    break;
                case MissionType.Thief:
                    UpdateThief(Time.deltaTime);
                    UpdateArena(Time.deltaTime);
                    break;
                case MissionType.Escort:
                    UpdateEscort(Time.deltaTime);
                    break;
                case MissionType.Clone:
                    UpdateClones(Time.deltaTime);
                    break;
                case MissionType.Hunt:
                    UpdateHunt(Time.deltaTime);
                    break;
            }
        }

        // ---------- Events ----------

        private void OnRobotArrived(GridPos p)
        {
            if (level != null && level.mission == MissionType.Monster && State == GameState.Playing) CheckArenaEntry(p);
            if (State != GameState.Playing) return;
            if (roadPhase == RoadPhase.Walk)
            {
                if (p == roadExit) TakeOverRoad();
                return;
            }

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
            if (robot.StepId != null) StepMark.Leave(GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY, robot.StepId, robot.StepColor);

            coins.TryCollect(p);
            if (Shop.MagnetRange > 0) coins.CollectNear(p, Shop.MagnetRange);
            powerUps.TryCollect(p);
            if (level.mission == MissionType.Hunt) hunt.OnRobotArrived(p);

            // Collapsing paths: the tile just left crumbles a moment later.
            var left = robot.LastLeftTile;
            if (level.collapseBehind && left != p && grid.IsFloor(left) && !collapses.Exists(c => c.pos == left))
                collapses.Add((left, CollapseDelay));

            if (level.mission == MissionType.Paint) PaintTile(p);
            else if (level.mission == MissionType.Monster)
            {
                if (orb != null && p == orbPos) PickUpOrb();
            }
            else if (level.mission == MissionType.Clone) ClonesFollow(p);
            else if (level.mission == MissionType.Quest)
            {
                var piece = objectives.Find(o => o.pos == p);
                if (piece != null) CompleteObjective(piece);
                else if (questReady && p == doorPos) FinishQuest();
            }
            else if (level.mission == MissionType.Exit || level.mission == MissionType.Boss)
            {
                var reached = objectives.Find(o => o.pos == p);
                if (reached != null) CompleteObjective(reached);
                else if (portal != null && portal.IsOpen && p == doorPos) Escape();
            }

            enemies.OnRobotArrived(p);
            levelEvents.OnArrived(p);
            // Floor rules last: ice, currents, trampolines and teleports may carry the robot on from here.
            if (State == GameState.Playing) floorRules.OnArrived(p, robot.LastLeftTile);
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
            if (level != null && level.mission == MissionType.Hunt) hunt.OnBlockLanded(p);
            if (State != GameState.Playing || roadPhase != RoadPhase.None) return; // the level is won: nothing on the floor can hurt now
            if (level.mission == MissionType.Clone) KnockClonesAt(p);
            if (level.mission == MissionType.Escort && p == buddyPos) DazeBuddy(p);
            cameraRig.Punch(0.5f);

            if (robot.InArena) return; // the robot is off the grid, fighting
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

            // A last-moment escape: a beat of slow motion (it also counts as a dodge, see OnDodged).
            if (robot.LastLeftTile == p && Time.time - robot.LastLeftTime < CloseCallWindow)
            {
                AudioManager.PlaySfx(Sfx.CloseCall);
                slowMoLeft = SlowMoDuration;
                RechargeTool();
                cameraRig.Focus(GridView.ToWorld(p), SlowMoDuration + 0.2f);
            }
        }

        /// <summary>
        /// The robot got out from under a block or bomb in time. Dodges in a row (each within
        /// <see cref="DodgeStreakGap"/> seconds of the last) earn armor to break out of a tight spot.
        /// </summary>
        private void OnDodged(GridPos p)
        {
            if (State != GameState.Playing || roadPhase != RoadPhase.None) return;
            if (Time.time - lastDodgeTime > DodgeStreakGap) closeCalls = 0;
            lastDodgeTime = Time.time;
            if (robot.IsShielded) return;
            closeCalls++;
            if (closeCalls >= CloseCallsForArmor)
            {
                closeCalls = 0;
                GiveArmor(ArmorDuration, robot.Position, Loc.T("float.armor"), Palette.UiGold);
                fx.Burst(robot.transform.position + Vector3.up * 0.5f, Palette.UiGold, Palette.CoinGlow, 24, 5f);
            }
            else FloatAt(GridView.ToWorld(p), Loc.F("float.closeCount", closeCalls, CloseCallsForArmor), Palette.UiCyan);
        }

        /// <summary>The first holes of a level teach the jump (for the player's first few levels with holes).</summary>
        private void OnTileBroken(GridPos p)
        {
            if (State == GameState.Playing && level != null && level.mission == MissionType.Escort && p == buddyPos) DazeBuddy(p);
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
            int multiplier = ComboMultiplier * (levelEvents.Alarm ? 2 : 1); // the WARDEN alarm doubles every coin
            comboBonus += multiplier - 1;
            FloatAt(GridView.ToWorld(p), "+" + multiplier, multiplier > 1 ? Palette.UiGold : Palette.UiGold);
            if (comboStreak == ComboStep || comboStreak == ComboStep * 2)
            {
                RechargeTool();
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

            if (level.mission == MissionType.Thief) CheckRace();
            if (level.mission == MissionType.CollectCoins && coinsThisRun >= level.coinTarget)
                Win();
            else if (level.mission == MissionType.CoinRain && coinsThisRun == level.coinTarget)
                FloatAt(GridView.ToWorld(p) + Vector3.up * 0.5f, Loc.T("float.target"), Palette.UiCyan);
        }

        /// <summary>The supply crate: a tool charge, a handful of coins or a short shield.</summary>
        private void OnCrateOpened(GridPos p)
        {
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.4f);
            Haptics.Medium();
            int roll = Random.Range(0, 100);
            bool hasTool = toolSlot[0].HasValue || toolSlot[1].HasValue;
            if (roll < 40 && hasTool)
            {
                for (int s = 0; s < 2; s++)
                    if (toolSlot[s].HasValue) { toolCharges[s]++; ui.PunchTool(s); break; }
                FloatAt(GridView.ToWorld(p), Loc.T("float.crateTool"), Palette.UiGold);
            }
            else if (roll < 75)
            {
                comboBonus += 8;
                FloatAt(GridView.ToWorld(p), "+8", Palette.UiGold);
                ui.FlyCoin(cameraRig.Cam.WorldToScreenPoint(GridView.ToWorld(p) + Vector3.up * 0.4f));
            }
            else
            {
                GiveArmor(4f, p, Loc.T("float.shield"), Palette.UiCyan);
            }
        }

        /// <summary>Taking a hit ends the coin streak.</summary>
        private void BreakCombo()
        {
            comboStreak = 0;
            comboTimer = 0f;
        }

        /// <summary>A skill fires: from the bag (the skill bar) or straight from the floor (rescues).</summary>
        private void ApplySkill(PowerUpType type, GridPos p)
        {
            switch (type)
            {
                case PowerUpType.Shield:
                    GiveArmor(level.shieldDuration, p, Loc.T("float.shield"), Palette.UiCyan);
                    cameraRig.Punch(1f);
                    break;
                case PowerUpType.Rescue:
                    rescues = Mathf.Min(MaxRescues, rescues + 1);
                    FloatAt(GridView.ToWorld(p), Loc.T("float.skillRescue"), new Color(0.4f, 1f, 0.6f));
                    AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.3f);
                    break;
                case PowerUpType.Freeze:
                    // Every block, warning and floor rule stops for a few seconds.
                    skillFreezeLeft = 4f;
                    hazards.Freeze();
                    floorRules.Freeze();
            enemies.Freeze();
            hunt.Freeze();
                    FloatAt(GridView.ToWorld(p), Loc.T("float.skillFreeze"), new Color(0.55f, 0.85f, 1f));
                    AudioManager.PlaySfx(Sfx.Shield, 0.9f, 0.7f);
                    break;
                case PowerUpType.Blast:
                    // Blocks around the robot burst.
                    for (int x = -2; x <= 2; x++)
                        for (int y = -2; y <= 2; y++)
                        {
                            var t = new GridPos(p.x + x, p.y + y);
                            if (grid.InBounds(t)) hazards.Shatter(t);
                        }
                    fx.Burst(GridView.ToWorld(p) + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.3f), new Color(3f, 1.2f, 0.3f), 50, 8f);
                    cameraRig.Shake(0.8f);
                    FloatAt(GridView.ToWorld(p), Loc.T("float.skillBlast"), new Color(1f, 0.6f, 0.3f));
                    AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.7f);
                    break;
                case PowerUpType.Heart:
                    Heal(p);
                    break;
                case PowerUpType.Super:
                    StartSuperSkill();
                    break;
                case PowerUpType.Magnet:
                    skillMagnetLeft = Mathf.Max(skillMagnetLeft, 8f);
                    FloatAt(GridView.ToWorld(p), Loc.T("float.skillMagnet"), new Color(1f, 0.4f, 0.45f));
                    AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.5f);
                    break;
            }
            Haptics.Medium();
        }

        private float skillFreezeLeft, skillMagnetLeft;

        /// <summary>Skills that last a while: the time freeze runs out, the magnet pulls coins in.</summary>
        private void UpdateSkills(float dt)
        {
            if (skillFreezeLeft > 0f)
            {
                skillFreezeLeft -= dt;
                if (skillFreezeLeft <= 0f && State == GameState.Playing)
                {
                    hazards.Resume();
                    floorRules.Resume();
                    enemies.Resume();
                    hunt.Resume();
                }
            }
            if (skillMagnetLeft > 0f)
            {
                skillMagnetLeft -= dt;
                coins.CollectNear(robot.Position, 2);
            }
        }

        private void FloatAt(Vector3 world, string text, Color color)
        {
            ui.Float(cameraRig.Cam.WorldToScreenPoint(world + Vector3.up * 0.9f), text, color);
        }

        // ---------- Tools ----------

        private readonly Tool?[] toolSlot = new Tool?[2];
        private readonly int[] toolCharges = new int[2];
        private readonly int[] toolLevel = new int[2];
        private bool trialSlot;           // slot 0 holds a free trial of a tool not bought yet
        private float toolSlowLeft, toolSlowTotal;
        private float bridgeLeft, bridgeTotal;

        /// <summary>Fills the bag for a new run: the equipped tools, or a one-off free trial in an empty bag.</summary>
        private void SetupTools()
        {
            trialSlot = false;
            toolSlowLeft = bridgeLeft = 0f;
            for (int s = 0; s < 2; s++)
            {
                toolSlot[s] = null;
                toolCharges[s] = 0;
            }
            if (bonusRun || levelIndex < Tools.FromLevel) return;

            for (int s = 0; s < 2; s++)
            {
                var t = Tools.Equipped(s);
                if (!t.HasValue) continue;
                toolSlot[s] = t;
                toolLevel[s] = Tools.Level(t.Value);
                toolCharges[s] = Tools.Charges(toolLevel[s]);
                // The first level with a tool in the bag: Bip says what its button does.
                if (PlayerPrefs.GetInt("sb_seen_toolhint." + t.Value, 0) == 0)
                {
                    PlayerPrefs.SetInt("sb_seen_toolhint." + t.Value, 1);
                    ui.Bip.Queue(Loc.F("bip.toolHint", Loc.T("tool." + t.Value), Loc.T("tool." + t.Value + ".desc")));
                }
            }

            if (!toolSlot[0].HasValue && !toolSlot[1].HasValue)
            {
                var trial = Tools.NextTrial();
                if (trial.HasValue)
                {
                    toolSlot[0] = trial;
                    toolLevel[0] = 1;
                    toolCharges[0] = 1;
                    trialSlot = true;
                }
            }
        }

        /// <summary>A close call or a combo tops up an empty tool (not trials).</summary>
        private void RechargeTool()
        {
            for (int s = 0; s < 2; s++)
            {
                if (!toolSlot[s].HasValue || (trialSlot && s == 0)) continue;
                if (toolCharges[s] >= Tools.Charges(toolLevel[s])) continue;
                toolCharges[s]++;
                ui.PunchTool(s);
                FloatAt(robot.transform.position + Vector3.up * 0.4f, Loc.T("float.toolCharge"), Palette.UiGold);
                return;
            }
        }

        private void OnToolPressed(int slot)
        {
            if (State != GameState.Playing || runner.Active || !toolSlot[slot].HasValue) return;
            if (toolCharges[slot] <= 0)
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.5f);
                return;
            }
            var tool = toolSlot[slot].Value;
            int lvl = toolLevel[slot];
            toolCharges[slot]--;
            ui.PunchTool(slot);
            Haptics.Medium();

            switch (tool)
            {
                case Tool.SlowMo:
                    toolSlowLeft = toolSlowTotal = Tools.SlowSeconds(lvl);
                    robot.TimeBoost = 1f / Tools.SlowScale;
                    AudioManager.PlaySfx(Sfx.Shield, 0.9f, 0.6f);
                    FloatAt(robot.transform.position, Loc.T("float.slow"), Palette.UiCyan);
                    break;

                case Tool.Bridge:
                {
                    // Mend every hole and fire nearby and keep those tiles whole for a while; poison is covered too.
                    float seconds = Tools.BridgeSeconds(lvl);
                    int radius = Tools.BridgeRadius(lvl);
                    foreach (var p in grid.AllPositions())
                    {
                        if (!grid.IsFloor(p) || p.Manhattan(robot.Position) > radius) continue;
                        if (grid.GetTile(p) == TileState.Poison) floorRules.Cover(p, seconds);
                        hazards.Shelter(p, seconds);
                        gridView.Bounce(p, 0.8f);
                    }
                    bridgeLeft = bridgeTotal = seconds;
                    fx.Burst(robot.transform.position + Vector3.up * 0.2f, Palette.UiCyan, Palette.ShieldPickupGlow, 30, 4f);
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 1.3f);
                    FloatAt(robot.transform.position, Loc.T("float.bridge"), Palette.UiCyan);
                    break;
                }

                case Tool.Freeze:
                {
                    // Bip's tinkered gardener coolant: every block, warning and floor rule holds still for a moment.
                    skillFreezeLeft = Mathf.Max(skillFreezeLeft, Tools.FreezeSeconds(lvl));
                    hazards.Freeze();
                    floorRules.Freeze();
                    enemies.Freeze();
                    hunt.Freeze();
                    fx.Burst(robot.transform.position + Vector3.up * 0.5f, new Color(0.7f, 0.9f, 1f), new Color(0.8f, 1.6f, 2.4f), 40, 6f);
                    AudioManager.PlaySfx(Sfx.Shield, 0.9f, 0.7f);
                    FloatAt(robot.transform.position, Loc.T("float.skillFreeze"), new Color(0.55f, 0.85f, 1f));
                    break;
                }

                case Tool.Blast:
                {
                    // The colour miners' powder: blocks around the robot burst (a plus, a square, a big square).
                    var at = robot.Position;
                    for (int x = -2; x <= 2; x++)
                        for (int y = -2; y <= 2; y++)
                        {
                            var t = new GridPos(at.x + x, at.y + y);
                            if (Tools.BlastHits(lvl, x, y) && grid.InBounds(t)) hazards.Shatter(t);
                        }
                    fx.Burst(robot.transform.position + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.3f), new Color(3f, 1.2f, 0.3f), 50, 8f);
                    cameraRig.Shake(0.8f);
                    AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.7f);
                    FloatAt(robot.transform.position, Loc.T("float.skillBlast"), new Color(1f, 0.6f, 0.3f));
                    break;
                }

                default:
                {
                    int hit = hazards.Emp();
                    if (Tools.EmpClearsRules(lvl)) floorRules.ClearActive();
                    cameraRig.Shake(1f);
                    cameraRig.Punch(1f);
                    fx.Burst(robot.transform.position + Vector3.up * 0.5f, Palette.ShieldPickup, Palette.ShieldPickupGlow, 50, 8f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
                    AudioManager.PlaySfx(Sfx.Blocked, 0.8f, 1.4f);
                    FloatAt(robot.transform.position, Loc.F("float.emp", hit), Palette.UiCyan);
                    break;
                }
            }

            if (trialSlot && slot == 0)
            {
                // The free trial is spent: from now on the tool is bought in the shop.
                Tools.MarkTrial(tool);
                FloatAt(robot.transform.position + Vector3.up * 0.6f, Loc.T("float.trialDone"), Palette.UiGold);
            }
        }

        /// <summary>Keeps the slow-motion tool and the HUD buttons up to date (real time, so slow motion can end).</summary>
        private void UpdateTools()
        {
            float dt = Time.unscaledDeltaTime;
            if (toolSlowLeft > 0f)
            {
                toolSlowLeft -= dt;
                if (toolSlowLeft <= 0f) robot.TimeBoost = 1f;
            }
            if (bridgeLeft > 0f) bridgeLeft -= Time.deltaTime;
            for (int s = 0; s < 2; s++)
            {
                float active = 0f;
                if (toolSlot[s] == Tool.SlowMo && toolSlowLeft > 0f) active = toolSlowLeft / toolSlowTotal;
                else if (toolSlot[s] == Tool.Bridge && bridgeLeft > 0f) active = bridgeLeft / bridgeTotal;
                else if (toolSlot[s] == Tool.Freeze && skillFreezeLeft > 0f) active = Mathf.Clamp01(skillFreezeLeft / Tools.FreezeSeconds(toolLevel[s]));
                ui.SetTool(s, toolSlot[s], toolCharges[s], trialSlot && s == 0, active);
            }
        }

        private void HideTools()
        {
            ui.SkillBar.Refresh(false);
            EndSuperSkill();
            ui.SetHealth(false, 0f, 0f);
            toolSlowLeft = 0f;
            robot.TimeBoost = 1f;
            for (int s = 0; s < 2; s++) ui.SetTool(s, null, 0, false, 0f);
        }

        // ---------- Win / lose ----------

        private void Win() => Win(escaped: false);

        private void Win(bool escaped)
        {
            EndArenaFight(false);
            State = GameState.Result;
            slowMoLeft = 0f;
            Time.timeScale = 1f;
            hazards.Freeze();
            coins.Freeze();
            powerUps.Freeze();
            floorRules.Freeze();
            enemies.Freeze();
            hunt.Freeze();
            levelEvents.Freeze();
            HideTools();
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

            // Stars: how well the level went.
            int stars = StarRules.Evaluate(starGoals, coinsThisRun, elapsed);

            // Every level leads on to the next by a road, and the level only counts as beaten when the robot arrives:
            // until then it is saved as "on the road", so quitting there brings the player back onto the road.
            if (RoadAhead)
            {
                PlayerPrefs.SetInt(RoadLevelKey, levelIndex);
                PlayerPrefs.SetInt(RoadStarsKey, stars);
                PlayerPrefs.SetInt(RoadCoinsKey, Earned);
                PlayerPrefs.Save();
                // Not right now: the win can come from inside the robot's own step (the last coin), which must finish first.
                StartCoroutine(OpenRoadSoon());
                return;
            }
            StartCoroutine(ShowResultDelayed(CommitWin(stars, Earned)));
        }

        /// <summary>The level is beaten for good: the next one opens, lives refill, stars are banked. Returns the result card.</summary>
        private UIController.ResultInfo CommitWin(int stars, int earned)
        {
            // Only beating the newest level (unlocking the next one) refills lives; replays don't.
            bool unlockedNew = levelIndex >= SaveData.UnlockedLevel;
            if (unlockedNew) Lives.Refill();
            if (levelIndex + 1 > SaveData.UnlockedLevel && levelIndex + 1 < LevelCount)
                SaveData.UnlockedLevel = levelIndex + 1;

            // New stars fill the bonus meter.
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
            // The card's hook (a teaser for the next cell) comes first; otherwise the bonus or star news.
            string note = Loc.Has(CardKey("hook")) ? Loc.T(CardKey("hook"))
                : surprise ? Loc.T("bonus.surprise")
                : unlockedBonus > 0 ? Loc.T("bonus.ready")
                : stars < 3 ? StarHint(stars)
                : Loc.T("star.max");

            var info = new UIController.ResultInfo
            {
                won = true,
                hasNext = hasNext,
                canDouble = true,
                subtitle = Loc.T(newWorld ? "result.newWorld" : hasNext ? "result.next" : "result.allDone"),
                note = note,
                coins = earned,
                stars = stars,
                bonusAvailable = Progress.BonusTokens > 0,
                meter = unlockedBonus > 0 || surprise ? 1f : Progress.Meter / (float)Progress.StarsPerBonus,
                meterText = Loc.F("bonus.meter", unlockedBonus > 0 || surprise ? Progress.StarsPerBonus : Progress.Meter, Progress.StarsPerBonus),
            };
            return info;
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
            EndArenaFight(false);
            if (roadPhase != RoadPhase.None) return; // already won, on the way to the next floor
            if (MarathonRespawn()) return; // a marathon past its checkpoint goes on from there
            State = GameState.Result;
            hazards.Freeze();
            coins.Freeze();
            powerUps.Freeze();
            floorRules.Freeze();
            enemies.Freeze();
            hunt.Freeze();
            levelEvents.Freeze();
            HideTools();
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
                canBuyContinue = !continued && SaveData.Coins - Earned >= ContinuePrice,
                continuePrice = ContinuePrice,
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
                // The very last road: vanG falls and nature wakes up (its own scene behind the lines) before the result card.
                pendingEnding = false;
                Story.MarkSeen(Story.Ending);
                var epilogue = EpilogueStage.Create(t => robot.BuildLookalike(t));
                AudioManager.PlayMusic(MusicTheme.Menu);
                ui.ShowStory(Story.Ending, World, () =>
                {
                    epilogue.Close();
                    ui.ShowResult(info);
                });
                yield break;
            }
            ui.ShowResult(info);
        }

        // ---------- Mission ----------

        /// <summary>Per-mission setup: the exit door and its keys, WARDEN and its buttons, or the first painted tile.</summary>
        private void SetupMission()
        {
            hazards.IsProtected = null;
            HazardVisuals.CrateTier = -1;
            objectivesDone = 0;
            chaseFront = -ChaseGraceRows;
            chaseRow = -1;
            if (level.mission == MissionType.Hunt)
            {
                SetupHunt();
                return;
            }

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
                GuardianLooks.DressAvatar(warden.Body, World); // vanG's avatar for this floor (Pres Kolu, Orman Biçici...)
                AddFarObjective();
            }
            else if (level.mission == MissionType.Quest)
            {
                SetupQuest();
            }
            else if (level.mission == MissionType.Monster)
            {
                SetupMonster();
            }
            else if (level.mission == MissionType.Thief)
            {
                SetupThief();
            }
            else if (level.mission == MissionType.Escort)
            {
                SetupEscort();
            }
            else if (level.mission == MissionType.Clone)
            {
                SetupClones();
            }
            else if (level.mission == MissionType.Paint)
            {
                PaintTile(robot.Position);
            }
            SetupAlly();
            if (level.marathon) SetupMarathon();
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
            else if (level.mission == MissionType.Quest) o.item = QuestItem.Create(level.quest, at);
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
                else if (o.item != null) o.item.MoveTo(at);
                else o.button.MoveTo(at);
                fx.Dust(at + Vector3.up * 0.1f, Palette.UiCyan, 8, 1.5f);
            }
        }

        private void CompleteObjective(Objective o)
        {
            objectives.Remove(o);
            if (o.item != null && o.item.StaysWhenCollected)
            {
                // Lanterns stay lit where they stand.
                o.item.Light();
                questLeftovers.Add(o.item.gameObject);
            }
            else if (o.View != null) Destroy(o.View);
            objectivesDone++;
            var at = GridView.ToWorld(o.pos);
            cameraRig.Punch(0.5f);
            Haptics.Medium();

            if (level.mission == MissionType.Quest)
            {
                QuestPieceFound(o.pos);
                return;
            }

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
            if (!RoadAhead) robot.EscapeInto(); // with a road ahead the robot stays, to walk out to it
            fx.Burst(GridView.ToWorld(doorPos) + Vector3.up * 0.5f, Palette.UiCyan, Palette.ShieldPickupGlow, 30, 4f);
            FloatAt(GridView.ToWorld(doorPos), Loc.T("float.escaped"), Palette.UiCyan);
            Win(escaped: true);
        }

        /// <summary>Tiles to paint: the card's number, or every tile of the floor.</summary>
        private int PaintGoal => level.paintTarget > 0 ? Mathf.Min(level.paintTarget, grid.FloorCount) : grid.FloorCount;

        /// <summary>A Silgi-bot rolled over a painted tile: it has to be painted again.</summary>
        private void Unpaint(GridPos p)
        {
            if (painted.Remove(p)) gridView.Unpaint(p);
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
            if (painted.Count >= PaintGoal && State == GameState.Playing) Win();
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
                case MissionType.Quest: return questReady ? 1f : objectivesDone / (objectivesTotal + 1f);
                case MissionType.Thief: return objectivesDone / (float)Mathf.Max(1, objectivesTotal);
                case MissionType.Escort: return EscortProgress();
                case MissionType.Clone: return objectivesDone / (float)Mathf.Max(1, objectivesTotal);
                case MissionType.Monster: return objectivesDone / (float)Mathf.Max(1, objectivesTotal);
                case MissionType.Hunt: return hunt.Progress;
                case MissionType.Paint: return grid == null ? 0f : painted.Count / (float)PaintGoal;
                default: return elapsed / level.surviveSeconds;
            }
        }

        private static string MissionText(LevelData data, bool upper = false)
        {
            string suffix = upper ? ".up" : "";
            // Scenario levels say their goal in the card's own words.
            string card = "lvl." + data.number + ".goal";
            if (data.cardGoal && data.number > 0 && Loc.Has(card)) return upper ? Loc.T(card).ToUpper(Loc.Culture) : Loc.T(card);
            switch (data.mission)
            {
                case MissionType.CollectCoins: return Loc.F("mission.collect" + suffix, data.coinTarget);
                case MissionType.Exit: return Loc.T("mission.exit" + suffix);
                case MissionType.Paint: return Loc.T((data.final ? "mission.final" : "mission.paint") + suffix);
                case MissionType.CoinRain: return Loc.F("mission.rain" + suffix, data.coinTarget);
                case MissionType.Treasure: return Loc.T("mission.treasure" + suffix);
                case MissionType.Tunnel: return Loc.T("mission.tunnel" + suffix);
                case MissionType.Boss: return Loc.T("mission.boss" + suffix);
                case MissionType.Quest: return Loc.F("quest.intro." + data.quest, data.keys);
                case MissionType.Monster: return Loc.F((data.cage ? "mission.cage" : data.guardsPrincess ? "mission.monsterPrincess" : "mission.monster") + suffix, data.keys);
                case MissionType.Thief: return Loc.F("mission.thief" + suffix, data.keys);
                case MissionType.Escort: return Loc.T("mission.escort" + suffix);
                case MissionType.Clone: return Loc.F("mission.clone" + suffix, data.keys);
                case MissionType.Hunt: return Loc.T("mission.hunt" + suffix);
                default: return Loc.F("mission.survive" + suffix, data.surviveSeconds.ToString("0", CultureInfo.InvariantCulture));
            }
        }

        // ---------- Music ----------

        /// <summary>The level's music: the monster or WARDEN theme for those fights, otherwise one of six world moods.</summary>
        private MusicTheme LevelMusic()
        {
            if (level.mission == MissionType.Monster) return MusicTheme.Monster;
            if (level.mission == MissionType.Boss || level.final) return MusicTheme.Boss;
            return MusicTheme.World0 + World % 6;
        }

        /// <summary>
        /// How tense the moment is, for the music: it builds with the mission's progress, jumps when a block is about to
        /// land on or next to the robot or the monster winds up a stomp, and WARDEN keeps it high.
        /// </summary>
        private float Tension()
        {
            float t = 0.2f + 0.35f * Mathf.Clamp01(MissionProgress());
            var p = robot.Position;
            bool danger = hazards.IsThreatened(p);
            foreach (var d in DirectionExtensions.All)
                if (!danger && hazards.IsThreatened(p + d.ToOffset())) danger = true;
            if (danger) t += 0.35f;
            if (stompWindup >= 0f) t += 0.3f;
            if (level.mission == MissionType.Boss) t = Mathf.Max(t, 0.6f);
            if (robot.ShieldLeft > 0f) t -= 0.1f;
            return Mathf.Clamp01(t);
        }

        // ---------- Roads between levels ----------

        // A beaten level opens a road to the next one: the floor is swept clean of every danger, a glowing exit
        // appears on the platform's far edge with the road already running out from it, and stepping onto the exit
        // hands the robot to the third-person runner without a cut. The road ends on the next floor's landing,
        // where the result card (then the map) waits.
        private enum RoadPhase { None, Walk, Run }
        private RoadPhase roadPhase;
        private GridPos roadExit;
        private GameObject roadBeacon;

        // A level won but whose road is not walked yet (-1: none), with the stars and coins it earned.
        private const string RoadLevelKey = "sb_road_level", RoadStarsKey = "sb_road_stars", RoadCoinsKey = "sb_road_coins";
        private static int PendingRoad => PlayerPrefs.GetInt(RoadLevelKey, -1);

        /// <summary>A road follows every level except bonus rounds and the very last level.</summary>
        private bool RoadAhead => !bonusRun && (levelIndex + 1 < LevelCount || level.final);

        /// <summary>
        /// Back onto the road of a level won earlier (the game was closed or left on the way): the platform stands
        /// swept clean, the robot is on its exit and the road starts at once.
        /// </summary>
        private void ResumeRoad(int index)
        {
            ResetRun();
            bonusRun = false;
            dailyRun = false;
            continued = false;
            doubleCoins = false;
            comboBonus = 0;
            levelIndex = Mathf.Clamp(index, 0, LevelCount - 1);
            level = levelSet.levels[levelIndex].Clone();
            coinsThisRun = PlayerPrefs.GetInt(RoadCoinsKey, 0);
            elapsed = 0f;
            ApplyTheme(levelIndex);
            grid = BuildGrid(level);
            gridView.Build(grid, fx, level.lowWalls);
            cameraRig.Frame(grid.Width, grid.Height);
            cameraRig.SetStyle(CameraStyle.Gameplay);
            cameraRig.SetMenuFocus(false);
            cameraRig.Showcase(null, 0f);
            robot.ApplyOutfit(Cosmetics.Outfit());
            robot.Spawn(grid, grid.StartSpot ?? grid.CenterFloor());
            if (grid.Width > FollowFrom || grid.Height > FollowFrom) cameraRig.Follow(robot.transform, FollowWindow, FollowWindow);
            starGoals = StarRules.For(level, grid.FloorCount);
            ui.ShowHud(levelIndex);
            OpenRoad();
            robot.Spawn(grid, roadExit);
            TakeOverRoad();
        }

        private IEnumerator OpenRoadSoon()
        {
            yield return new WaitForSeconds(0.5f);
            OpenRoad();
        }

        private void OpenRoad()
        {
            ClearDangers();
            // The victory dance froze the robot; the player walks it to the exit now.
            robot.Spawn(grid, robot.Position);
            roadExit = PickExit(out var outward);
            var origin = GridView.ToWorld(roadExit) + new Vector3(outward.x, 0f, outward.y);
            float heading = outward.y > 0 ? 0f : 90f;
            runner.PrepareRoad(levelIndex * 7919 + 13, origin, heading, levelIndex, LevelCatalog.WorldOf(levelIndex + 1));

            roadBeacon = new GameObject("RoadExit");
            roadBeacon.transform.position = GridView.ToWorld(roadExit) + Vector3.up * GridView.SurfaceY;
            roadBeacon.transform.rotation = Quaternion.Euler(0f, heading, 0f);
            var glow = MaterialFactory.CreateTransparent(new Color(0.5f, 1f, 0.7f, 0.5f), new Color(0.4f, 2f, 0.8f));
            var solid = MaterialFactory.Create(new Color(0.5f, 1f, 0.7f), new Color(0.4f, 2f, 0.8f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Ring", roadBeacon.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.95f, 0.01f, 0.95f), glow);
            for (int i = 0; i < 3; i++)
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Rounded("Arrow", roadBeacon.transform, new Vector3(s * 0.12f, 0.08f, -0.25f + i * 0.25f), new Vector3(0.07f, 0.04f, 0.3f), 0.02f, solid)
                        .transform.localRotation = Quaternion.Euler(0f, -s * 40f, 0f);

            roadPhase = RoadPhase.Walk;
            State = GameState.Playing;
            input.HoldEnabled = false;
            cameraRig.SetStyle(CameraStyle.Gameplay);
            ui.SetWarning(false);
            ui.SetHover(false, 0f);
            ui.SetRescues(false, 0, 0f);
            ui.SetShield(0f, 1f);
            ui.SetCombo(1, 0f);
            cameraRig.SetMenuFocus(false);
            ui.ShowIntro(Loc.T("road.title"), Loc.T("road.go"));
            RefreshHud();
            // The player walks the robot there; the road only starts when it steps onto the exit.
        }

        /// <summary>Mission done: every block, hole, fire, poison and rule effect disappears from the floor.</summary>
        private void ClearDangers()
        {
            hazards.Stop();
            floorRules.Stop();
            enemies.Stop();
            hunt.Stop();
            levelEvents.Stop();
            coins.Stop();
            powerUps.Stop();
            collapses.Clear();
            foreach (var o in objectives) if (o.View != null) Destroy(o.View);
            objectives.Clear();
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsFloor(p)) continue;
                var tile = grid.GetTile(p);
                if (tile == TileState.Fire) gridView.Extinguish(p);
                else if (tile != TileState.Solid) gridView.Repair(p);
                grid.SetTile(p, TileState.Solid);
                grid.SetOccupied(p, false);
                gridView.SetTint(p, null);
                gridView.SetWarning(p, 0f);
            }
            fx.Burst(robot.transform.position + Vector3.up * 0.5f, Palette.UiCyan, Palette.ShieldPickupGlow, 40, 7f);
            AudioManager.PlaySfx(Sfx.Shield, 1f, 1.2f);
        }

        /// <summary>
        /// The exit: a floor tile on the far edge (seen from the camera), with nothing beyond it, nearest the robot.
        /// <paramref name="outward"/> is the way the road leaves the platform.
        /// </summary>
        private GridPos PickExit(out Vector2Int outward)
        {
            // How many steps each tile is from the robot (walking, or leaping a one-tile gap like the robot does).
            var steps = new Dictionary<GridPos, int> { [robot.Position] = 0 };
            var queue = new Queue<GridPos>();
            queue.Enqueue(robot.Position);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in DirectionExtensions.All)
                {
                    var o = d.ToOffset();
                    var n = p + o;
                    if (!grid.IsFloor(n))
                    {
                        if (!grid.InBounds(n) || grid.IsWall(n)) continue;
                        n = n + o; // a gap: the robot leaps it
                    }
                    if (!grid.IsFloor(n) || steps.ContainsKey(n)) continue;
                    steps[n] = steps[p] + 1;
                    queue.Enqueue(n);
                }
            }

            // Candidates: reachable floor tiles on the far edges with nothing beyond them. The robot's own tile is
            // never chosen while there is another, and a couple of steps away is preferred, so the player walks there.
            GridPos best = robot.Position;
            outward = new Vector2Int(0, 1);
            int bestScore = int.MaxValue;
            foreach (var kv in steps)
            {
                var p = kv.Key;
                foreach (var dir in new[] { new Vector2Int(0, 1), new Vector2Int(1, 0) })
                {
                    bool clear = true;
                    for (var q = new GridPos(p.x + dir.x, p.y + dir.y); grid.InBounds(q); q = new GridPos(q.x + dir.x, q.y + dir.y))
                        if (grid.Exists(q)) { clear = false; break; }
                    if (!clear) continue;
                    int s = kv.Value;
                    int score = s == 0 ? 1000 : s == 1 ? 100 + s : s;
                    if (score < bestScore) { bestScore = score; best = p; outward = dir; }
                }
            }
            return best;
        }

        /// <summary>From this level's road on, vanG greets the robot in the tunnel instead of the control hint.</summary>
        private const int VanGRoadsFrom = 4;

        /// <summary>The golden paint of the finale (the First Observer's energy).</summary>
        private static readonly Color FinalGold = new Color(1f, 0.78f, 0.3f);

        private void TakeOverRoad()
        {
            if (roadPhase != RoadPhase.Walk) return;
            roadPhase = RoadPhase.Run;
            if (roadBeacon != null) Destroy(roadBeacon);
            AudioManager.PlayMusic(MusicTheme.Tunnel);
            runner.TakeOver();
            AudioManager.PlaySfx(Sfx.Hop, 0.8f, 1.2f);
            RideBip();
            // The first roads teach the controls; after that vanG speaks from the tunnel walls.
            string text = levelIndex < VanGRoadsFrom ? Loc.T("road.run") : Loc.T("story.warden") + ": " + Loc.T("road.vang." + DuctRunner.RoadTheme(levelIndex));
            if (level.final) ui.ShowIntro(Loc.T("road.finalTitle"), Loc.T("road.finalText"));
            else ui.ShowIntro(Loc.F("level", levelIndex + 2), text);
        }

        /// <summary>A crash on the road: it costs a life (while there are any) and the road starts again from the top.</summary>
        private void OnRoadFailed()
        {
            if (State != GameState.Playing || roadPhase != RoadPhase.Run) return;
            // A tunnel boost from Bip's counter takes the first crash instead of a life.
            bool boosted = Shop.TryUse(Boost.TunnelBoost);
            bool paid = !boosted && Lives.TryConsume();
            runner.RestartRoad();
            // Long roads start again from the last checkpoint passed.
            string again = Loc.T(runner.ResumeRow > 0 ? "road.failCheckpoint" : "road.failFree");
            if (boosted) again = Loc.T("road.boostSaved") + " · " + again;
            ui.ShowIntro(Loc.T("road.failTitle"), paid ? Loc.T("road.lifeLost") + " · " + again + "\n" + Loc.F("lives.left", Lives.Count) : again);
        }

        private void OnRoadArrived()
        {
            if (roadPhase != RoadPhase.Run) return;
            roadPhase = RoadPhase.None;
            State = GameState.Result;
            DropBip();
            SaveData.Coins += runner.Coins;
            // Only now is the level beaten: the next one opens and the stars are banked.
            var info = CommitWin(PlayerPrefs.GetInt(RoadStarsKey, 1), PlayerPrefs.GetInt(RoadCoinsKey, 0) + runner.Coins);
            PlayerPrefs.DeleteKey(RoadLevelKey);
            PlayerPrefs.Save();
            info.subtitle = Loc.T("road.arrived");
            if (level.final) ui.ShowIntro(Loc.T("road.finalDone"), "");
            else ui.ShowIntro(Loc.T("road.doneTitle"), Loc.T("road.doneText"));
            StartCoroutine(ShowResultDelayed(info));
        }

        private void UpdateRoadWalk()
        {
            var command = input.Poll(robot.transform.position);
            if (command.jump) robot.TryJump();
            else if (command.move.HasValue) robot.TryMove(command.move.Value);
            if (roadBeacon != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.12f;
                roadBeacon.transform.localScale = new Vector3(pulse, 1f, pulse);
            }
            RefreshHud();
        }

        // ---------- Quests ----------

        // A quest level tells a little story on a big floor: every piece (keys, cores, cages, lanterns, gems) lies out
        // at once, spread far apart, and the goal waits on the far side. Find them all in any order, then reach the goal
        // for the happy ending. The blocks keep the player moving; the challenge is the search.
        private QuestGoal questGoal;
        private bool questReady;
        private readonly List<GameObject> questLeftovers = new List<GameObject>();

        private void SetupQuest()
        {
            spotKeys = true; // pieces that get hit hop to a tile nearby, not across the map
            doorPos = FarTile(avoidDoor: false) ?? robot.Position;
            questGoal = QuestGoal.Create(level.quest, GridView.ToWorld(doorPos) + Vector3.up * GridView.SurfaceY, fx);
            questReady = false;
            objectivesTotal = Mathf.Max(1, level.keys);
            foreach (var p in SpreadTiles(objectivesTotal)) AddObjective(p);
            objectivesTotal = objectives.Count;
            hazards.IsProtected = p => p == doorPos || objectives.Exists(o => o.pos == p);
        }

        /// <summary>Tiles far from the robot, the goal and each other, so the pieces send the player all over the floor.</summary>
        private List<GridPos> SpreadTiles(int count)
        {
            var chosen = new List<GridPos>();
            var anchors = new List<GridPos> { robot.Position, doorPos };
            var free = new List<GridPos>();
            foreach (var t in grid.AllPositions())
                if (grid.IsStandable(t) && t != robot.Position && t != doorPos) free.Add(t);
            for (int n = 0; n < count && free.Count > 0; n++)
            {
                // Score = distance to the nearest piece or anchor; pick among the best few for variety.
                var scored = new List<(GridPos p, int d)>();
                foreach (var t in free)
                {
                    int d = int.MaxValue;
                    foreach (var a in anchors) d = Mathf.Min(d, t.Manhattan(a));
                    scored.Add((t, d));
                }
                scored.Sort((x, y) => y.d.CompareTo(x.d));
                var pick = scored[Random.Range(0, Mathf.Min(3, scored.Count))].p;
                chosen.Add(pick);
                anchors.Add(pick);
                free.Remove(pick);
            }
            return chosen;
        }

        private void QuestPieceFound(GridPos at)
        {
            int left = objectivesTotal - objectivesDone;
            string kind = level.quest.ToString();
            fx.Burst(GridView.ToWorld(at) + Vector3.up * 0.5f, Palette.UiGold, Palette.CoinGlow, 22, 4f);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.3f);
            if (left > 0)
            {
                FloatAt(GridView.ToWorld(at), Loc.F("quest.left." + kind, left), Palette.UiCyan);
                return;
            }
            // Every piece found: the goal wakes up and points the way.
            questReady = true;
            questGoal.Ready();
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.3f);
            ui.ShowIntro(Loc.T("quest.title." + kind), Loc.T("quest.go." + kind));
            cameraRig.Punch(0.8f);
            if (robot.Position == doorPos && !robot.IsHopping) FinishQuest();
        }

        private void FinishQuest()
        {
            if (!questReady || State != GameState.Playing) return;
            questReady = false;
            questGoal.Complete();
            cameraRig.Shake(0.6f);
            Haptics.Medium();
            FloatAt(GridView.ToWorld(doorPos) + Vector3.up * 0.5f, Loc.T("quest.done." + level.quest), Palette.UiGold);
            Win();
        }

        private void ClearQuest()
        {
            if (questGoal != null) Destroy(questGoal.gameObject);
            questGoal = null;
            questReady = false;
            foreach (var go in questLeftovers) if (go != null) Destroy(go);
            questLeftovers.Clear();
        }

        // ---------- Monster fights ----------

        // A monster holds one tile with a health bar over its head. Bumping into it bare-handed does nothing; the Thunder
        // Hammer lies somewhere else on the floor (the "orb" below). Pick it up (the robot shoulders it), run into the
        // monster: the hammer swings down with a lightning bolt, one hit, and a new hammer drops on another tile. Now and then the monster stomps: the tiles around it flash, and a
        // robot still standing there is knocked back a couple of tiles. Never a death, always a chase.
        private Monster monster;
        private GridPos monsterPos, orbPos;
        private ThunderHammer orb; // the Thunder Hammer lying on the floor
        private GameObject aura;
        private int hammerAmmo;
        private bool charged => hammerAmmo > 0;
        private int monsterHp;
        private float orbLeft, orbCheck;
        private const float OrbStay = 7f, OrbWarn = 2.5f;
        private GridPos princessPos;
        private float stompTimer, stompWindup = -1f;

        /// <summary>
        /// The tiles the robot can walk to right now (floor tiles, leaping a one-tile gap like the robot does). Walls,
        /// pillars, the monster and the princess block the way; falling blocks don't, they are gone in a moment.
        /// </summary>
        private HashSet<GridPos> ReachableTiles() => ReachableTiles(monsterPos, princessPos);

        /// <summary>The same, with two given tiles blocked instead (to try out where the monster and the princess could go).</summary>
        private HashSet<GridPos> ReachableTiles(GridPos blockA, GridPos blockB)
        {
            var seen = new HashSet<GridPos> { robot.Position };
            var queue = new Queue<GridPos>();
            queue.Enqueue(robot.Position);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in DirectionExtensions.All)
                {
                    var o = d.ToOffset();
                    var n = p + o;
                    if (!grid.IsFloor(n))
                    {
                        if (!grid.InBounds(n) || grid.IsWall(n)) continue;
                        n = n + o;
                    }
                    if (!grid.IsFloor(n) || n == blockA || n == blockB || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// <summary>How many sides of the monster the robot can reach to hit it.</summary>
        private int OpenSides(GridPos at, HashSet<GridPos> reach)
        {
            int open = 0;
            foreach (var d in DirectionExtensions.All)
                if (reach.Contains(at + d.ToOffset())) open++;
            return open;
        }

        /// <summary>This world's monster: its kind and colour (the briefing shows the same one).</summary>
        private void MonsterLook(out Monster.Kind kind, out Color tint)
        {
            if (level != null && level.cage) { kind = Monster.Kind.Cage; tint = new Color(1f, 0.8f, 0.35f); return; }
            if (GuardianLooks.Guardian(World, out kind, out tint)) return;
            kind = (Monster.Kind)(World % 4);
            tint = Color.Lerp(WorldTheme.Current.accent, new Color(0.55f, 0.85f, 0.4f), kind == Monster.Kind.Slime ? 0.5f : 0.15f);
        }

        private void SetupMonster()
        {
            var centre = new GridPos(grid.Width / 2, grid.Height / 2);
            var none = new GridPos(-99, -99);
            monsterPos = princessPos = none;
            var reach = ReachableTiles();

            // Near the middle, on a tile the robot can walk up to from three sides or more, and never on the only way
            // into a part of the floor (a monster on a bridge would cut the orb, the keys or the exit off). Each try
            // walks the floor again with the monster there, so only the closest few dozen tiles are tried.
            var tries = new List<GridPos>();
            foreach (var t in grid.AllPositions())
                if (grid.IsStandable(t) && t.Manhattan(robot.Position) >= 3 && reach.Contains(t) && OpenSides(t, reach) > 0) tries.Add(t);
            tries.Sort((a, b) => a.Manhattan(centre).CompareTo(b.Manhattan(centre)));
            if (tries.Count > 40) tries.RemoveRange(40, tries.Count - 40);
            monsterPos = tries.Count > 0 ? tries[0] : robot.Position;
            int best = int.MaxValue;
            foreach (var t in tries)
            {
                var without = ReachableTiles(t, none);
                int sides = OpenSides(t, without);
                if (sides == 0) continue;
                int lost = reach.Count - 1 - without.Count;
                int score = t.Manhattan(centre) + (sides < 3 ? 100 : 0) + lost * 50;
                if (score < best) { best = score; monsterPos = t; }
            }
            grid.SetOccupied(monsterPos, true);
            // Health in points: 10 per hit on the card (an upgraded hammer needs fewer blows).
            monsterHp = objectivesTotal = Mathf.Max(2, level.keys) * MonsterPointsPerHit;
            objectivesDone = 0;
            MonsterLook(out var kind, out var tint);
            monster = Monster.Create(kind, GridView.ToWorld(monsterPos) + Vector3.up * GridView.SurfaceY, monsterHp, tint, robot.transform, MonsterPointsPerHit);
            GuardianLooks.DressGuardian(monster.Body, World); // the floor's named guardian (Kütükbaş, Penguen Kral...)
            princessPos = new GridPos(-99, -99);
            if (level.guardsPrincess && !level.cage)
            {
                // Princess Lumi, frozen in ice right next to the monster: she is freed when it falls.
                // On the side that blocks the least: the monster stays reachable and no part of the floor is cut off.
                var open = ReachableTiles(monsterPos, none);
                int bestCut = int.MaxValue;
                foreach (var d in DirectionExtensions.All)
                {
                    var n = monsterPos + d.ToOffset();
                    if (!grid.IsStandable(n) || n == robot.Position) continue;
                    var with = ReachableTiles(monsterPos, n);
                    if (OpenSides(monsterPos, with) == 0) continue;
                    int cut = open.Count - with.Count;
                    if (cut < bestCut) { bestCut = cut; princessPos = n; }
                }
                if (grid.InBounds(princessPos))
                {
                    grid.SetOccupied(princessPos, true);
                    questGoal = QuestGoal.Create(QuestKind.Princess, GridView.ToWorld(princessPos) + Vector3.up * GridView.SurfaceY, fx);
                }
            }
            hammerAmmo = 0;
            stompTimer = 5f;
            SpawnOrb();
            SetupArenaRing();
            if (level.cage) BipSay("mira");
            hazards.IsProtected = p => p == monsterPos || p == orbPos;
        }

        /// <summary>
        /// A new orb lands on a free tile away from the robot and the monster, so every hit needs a run. Only tiles the
        /// robot can actually walk to count, and the orb doesn't wait forever: after about <see cref="OrbStay"/> seconds (more when it lies farther) it
        /// flickers and jumps to another tile, so a block or a stomp in the way never stalls the fight.
        /// </summary>
        private void SpawnOrb()
        {
            var reach = ReachableTiles();
            var options = new List<(GridPos p, int d)>();
            foreach (var t in grid.AllPositions())
            {
                if (!grid.IsStandable(t) || t == monsterPos || t == robot.Position || hazards.IsThreatened(t) || !reach.Contains(t)) continue;
                if (InArenaZone(t)) continue;
                options.Add((t, t.Manhattan(robot.Position)));
            }
            if (options.Count == 0) { orbLeft = 1f; return; }
            // A run, but not across the whole floor: 3-8 tiles away keeps it near the follow camera on big floors.
            var near = options.FindAll(o => o.d >= 3 && o.d <= 8);
            if (near.Count == 0) { options.Sort((a, b) => b.d.CompareTo(a.d)); near = options.GetRange(0, Mathf.Max(1, options.Count / 3)); }
            var pick = near[Random.Range(0, near.Count)];
            orbPos = pick.p;
            if (orb != null) Destroy(orb.gameObject);
            orb = ThunderHammer.Create(GridView.ToWorld(orbPos) + Vector3.up * GridView.SurfaceY);
            orbLeft = OrbStay + pick.d * 0.6f;
            orbCheck = 1f;
            fx.Burst(GridView.ToWorld(orbPos) + Vector3.up * 0.6f, ThunderHammer.Electric, ThunderHammer.ElectricGlow, 16, 3f);
        }

        private void PickUpOrb()
        {
            if (orb != null) Destroy(orb.gameObject);
            orb = null;
            orbPos = new GridPos(-99, -99);
            hammerAmmo = Mathf.Min(Data.Weapons.MaxAmmo, hammerAmmo + 1);
            if (aura == null) aura = ShoulderHammer();
            fx.Burst(robot.transform.position + Vector3.up * 0.5f, ThunderHammer.Electric, ThunderHammer.ElectricGlow, 26, 4f);
            AudioManager.PlaySfx(Sfx.Shield, 0.9f, 1.4f);
            Haptics.Medium();
            FloatAt(robot.transform.position, Loc.F("float.ammo", hammerAmmo, Data.Weapons.MaxAmmo), ThunderHammer.Electric);
        }

        private void OnRobotBumped(GridPos target)
        {
            if (State == GameState.Playing && level != null && level.mission == MissionType.Thief)
            {
                if (target == thiefPos) CatchThief();
                return;
            }
            if (State != GameState.Playing || level == null || level.mission != MissionType.Monster || monster == null || target != monsterPos) return;
            TryEnterArena(); // walking into the monster starts the fight too
        }

        private void UpdateMonster(float dt)
        {
            if (monster == null || monster.Dead) return;
            if (orb != null && (!grid.IsStandable(orbPos) || hazards.IsThreatened(orbPos))) SpawnOrb();
            if (orb == null && hammerAmmo < Data.Weapons.MaxAmmo) SpawnOrb();
            if (orb != null && !previewing)
            {
                // Every second: if the way to the orb got cut off, it moves at once; otherwise it moves when its time is up.
                orbLeft -= dt;
                orbCheck -= dt;
                if (orbCheck <= 0f)
                {
                    orbCheck = 1f;
                    if (!ReachableTiles().Contains(orbPos)) orbLeft = 0f;
                }
                orb.Leaving = orbLeft < OrbWarn;
                if (orbLeft <= 0f)
                {
                    FloatAt(orb.transform.position + Vector3.up * 0.8f, Loc.T("float.orbMoved"), ThunderHammer.Electric);
                    SpawnOrb();
                }
            }
        }

        private void ClearMonster()
        {
            if (monster != null) Destroy(monster.gameObject);
            if (orb != null) Destroy(orb.gameObject);
            if (aura != null) Destroy(aura);
            monster = null;
            orb = null;
            aura = null;
            hammerAmmo = 0;
            stompWindup = -1f;
            ClearArena();
        }

        // ---------- Goal preview ----------

        // Levels with a special goal open on it: the camera looks at the princess, the monster, WARDEN or the exit
        // door for a moment, a bubble says what to do ("Defeat the monster!"), and only then does the level start.
        private bool previewing;
        private bool? pendingIntro;
        private const float PreviewSeconds = 2.3f;

        private void StartMissionPreview()
        {
            // Bonus runs start at once; a retry of the level just briefed only gets the quick look at the goal.
            briefedNow = false;
            if (!bonusRun && levelIndex != lastBriefed)
            {
                lastBriefed = levelIndex;
                briefedNow = true;
                StartCoroutine(Briefing());
                return;
            }
            StartGoalLook();
        }

        // ---------- Mission briefing ----------

        // Before a level starts, the floor blurs and a card explains the mission step by step, each step with a small 3D
        // scene: grab the Thunder Hammer, hit the monster, keep clear when it stomps, the princess is freed... The player
        // taps through at their own pace (or skips); then the camera points at the goal on the floor and play begins.
        private BriefingStage briefStage;
        private int lastBriefed = -1;
        private bool briefedNow; // the briefing told the mission, so the level banner doesn't repeat it

        private List<(BriefShot shot, string text)> BriefSteps()
        {
            var steps = new List<(BriefShot, string)>();
            if (level.mission == MissionType.Hunt)
            {
                // The core loop, told the same way every time: the crates, the crowd, then the monster.
                steps.Add((BriefShot.Block, Loc.T("brief.huntCrates")));
                steps.Add((BriefShot.HammerHit, Loc.T("brief.huntCrowd")));
                steps.Add((BriefShot.Stomp, Loc.T("brief.huntMonster")));
                return steps;
            }
            // The card opens with vanG: the threat and why it built this cell.
            if (Loc.Has(CardKey("vang"))) steps.Add((BriefShot.Warden, Loc.T("story.warden") + ": " + Loc.T(CardKey("vang"))));
            switch (level.mission)
            {
                case MissionType.Monster:
                    if (level.cage) steps.Add((BriefShot.HammerHit, Loc.T("brief.cage")));
                    steps.Add((BriefShot.Hammer, Loc.T("brief.hammer")));
                    steps.Add((BriefShot.HammerHit, Loc.F("brief.hit", Mathf.CeilToInt(objectivesTotal / (float)Weapons.HitDamage))));
                    steps.Add((BriefShot.Stomp, Loc.T("brief.stomp")));
                    if (level.guardsPrincess) steps.Add((BriefShot.Princess, Loc.T("brief.princessFreed")));
                    if (level.phased) steps.Add((BriefShot.HammerHit, Loc.T("brief.phased")));
                    break;
                case MissionType.Thief:
                    if (level.thiefRace) steps.Add((BriefShot.Thief, Loc.F("brief.thiefRace", objectivesTotal)));
                    else
                    {
                        steps.Add((BriefShot.Thief, Loc.F("brief.thief", objectivesTotal)));
                        steps.Add((BriefShot.Coins, Loc.T("brief.thiefCoins")));
                    }
                    break;
                case MissionType.Escort:
                    steps.Add((BriefShot.Escort, level.escortSeconds > 0f ? Loc.F("brief.escortTime", (int)level.escortSeconds)
                        : level.escortStops > 1 ? Loc.F("brief.escortStops", level.escortStops) : Loc.T("brief.escort")));
                    steps.Add((BriefShot.Block, Loc.T("brief.escortSafe")));
                    break;
                case MissionType.Clone:
                    steps.Add((BriefShot.Clone, Loc.T("brief.clone")));
                    if (level.cloneKnockouts > 0) steps.Add((BriefShot.Block, Loc.F("brief.cloneKo", level.cloneKnockouts)));
                    else steps.Add((BriefShot.QuestItem, Loc.F("brief.cloneCores", objectivesTotal)));
                    steps.Add((BriefShot.Block, Loc.T("brief.cloneBlock")));
                    break;
                case MissionType.Quest:
                    steps.Add((BriefShot.QuestItem, MissionText(level)));
                    steps.Add((BriefShot.QuestGoal, Loc.T("brief.questGoal") + " " + Loc.T("quest.go." + level.quest)));
                    break;
                case MissionType.Exit:
                    steps.Add((BriefShot.Key, Loc.F("brief.keys", objectivesTotal)));
                    steps.Add((BriefShot.Door, Loc.T("brief.door")));
                    break;
                case MissionType.CollectCoins:
                case MissionType.CoinRain:
                case MissionType.Treasure:
                    steps.Add((BriefShot.Coins, MissionText(level)));
                    break;
                case MissionType.Paint:
                    steps.Add((BriefShot.Paint, MissionText(level)));
                    if (level.final)
                    {
                        steps.Add((BriefShot.Paint, Loc.T("brief.final")));
                        steps.Add((BriefShot.Block, Loc.T("brief.finalBlocks")));
                    }
                    break;
                case MissionType.Boss:
                    steps.Add((BriefShot.Warden, MissionText(level)));
                    break;
                default:
                    steps.Add((BriefShot.Robot, MissionText(level)));
                    break;
            }
            if (level.marathon) steps.Add((BriefShot.Super, Loc.F("brief.marathon", (int)(level.surviveSeconds * 0.5f), (int)SuperSeconds)));
            // The first floors also remind what the danger is.
            if (levelIndex < 3) steps.Add((BriefShot.Block, Loc.T("brief.dodge")));
            // And it closes with the helper's tip: how to beat this cell.
            if (level.cardGoal && Loc.Has(CardKey("help"))) steps.Add((BriefShot.Robot, HelperName + ": " + Loc.T(CardKey("help"))));
            return steps;
        }

        /// <summary>The Loc key of one of this level's card texts ("vang", "help", "goal", "hook", "title").</summary>
        private string CardKey(string part) => "lvl." + (level != null ? level.number : 0) + "." + part;

        private string HelperName => Loc.T(level.helper == Helper.Lumi ? "story.lumi" : level.helper == Helper.Kuzgun ? "story.kuzgun" : "story.bip");

        private Color HelperColor => level.helper == Helper.Lumi ? new Color(1f, 0.8f, 0.95f) : level.helper == Helper.Kuzgun ? Palette.UiGold : new Color(0.55f, 1f, 0.6f);

        private IEnumerator Briefing()
        {
            previewing = true;
            FreezePlay();
            if (briefStage == null) briefStage = BriefingStage.Create(fx, t => robot.BuildLookalike(t));
            MonsterLook(out var kind, out var tint);
            briefStage.Setup(kind, tint, level.mission == MissionType.Clone ? QuestKind.Cores : level.quest, objectivesTotal);
            var steps = BriefSteps();
            var texts = new List<string>();
            foreach (var s in steps) texts.Add(s.text);
            cameraRig.SetMenuFocus(true);
            bool done = false;
            void OnStep(int i) => briefStage.Show(steps[i].shot);
            void OnDone() => done = true;
            ui.Briefing.StepShown += OnStep;
            ui.Briefing.Finished += OnDone;
            try
            {
                ui.Briefing.Show(texts, briefStage.Texture);
                while (!done && State == GameState.Playing) yield return null;
            }
            finally
            {
                ui.Briefing.StepShown -= OnStep;
                ui.Briefing.Finished -= OnDone;
                briefStage.SetVisible(false);
                cameraRig.SetMenuFocus(false);
            }
            if (State != GameState.Playing) yield break;
            // Then a moment on the floor itself: where the goal is.
            if (!StartGoalLook(1.6f)) EndPreview();
        }

        private void FreezePlay()
        {
            hazards.Freeze();
            powerUps.Freeze();
            floorRules.Freeze();
            enemies.Freeze();
            hunt.Freeze();
            levelEvents.Freeze();
            coins.Freeze();
        }

        private void EndPreview()
        {
            if (State == GameState.Playing)
            {
                hazards.Resume();
                powerUps.Resume();
                floorRules.Resume();
                enemies.Resume();
                hunt.Resume();
                levelEvents.Resume();
                coins.Resume();
            }
            previewing = false;
            if (pendingIntro.HasValue && State == GameState.Playing) ShowLevelIntro(pendingIntro.Value);
            pendingIntro = null;
        }

        /// <summary>The camera looks at the level's goal (monster, princess, WARDEN) with a bubble; false when there is none.</summary>
        private bool StartGoalLook(float seconds = PreviewSeconds)
        {
            Transform target = null;
            string text = null;
            switch (level.mission)
            {
                case MissionType.Monster:
                    if (monster != null) target = monster.transform;
                    text = Loc.T(level.cage ? "callout.cage" : level.guardsPrincess ? "callout.monsterPrincess" : "callout.monster");
                    break;
                case MissionType.Quest:
                    if (questGoal != null) target = questGoal.transform;
                    text = Loc.T("callout." + level.quest);
                    break;
                case MissionType.Boss:
                    if (warden != null) target = warden.transform;
                    text = Loc.T("callout.boss");
                    break;
                case MissionType.Thief:
                    if (thief != null) target = thief.transform;
                    text = Loc.T("callout.thief");
                    break;
                case MissionType.Escort:
                    if (buddy != null) target = buddy.transform;
                    text = Loc.T("callout.escort");
                    break;
                case MissionType.Clone:
                    if (clones.Count > 0) target = clones[0].view.transform;
                    text = Loc.T("callout.clone");
                    break;
            }
            if (target == null) return false;
            StartCoroutine(MissionPreview(target, text, seconds));
            return true;
        }

        private IEnumerator MissionPreview(Transform target, string text, float seconds)
        {
            previewing = true;
            FreezePlay();
            bool big = grid.Width > FollowFrom || grid.Height > FollowFrom;
            if (big) cameraRig.Follow(target, FollowWindow, FollowWindow);
            else cameraRig.Focus(target.position, seconds);
            float lift = level.mission == MissionType.Boss ? 1.2f : 2.6f;
            ui.ShowCallout(text, target.position + Vector3.up * lift, cameraRig.Cam, seconds);
            yield return new WaitForSeconds(seconds);
            if (big) cameraRig.Follow(robot.transform, FollowWindow, FollowWindow);
            EndPreview();
        }

        private void RefreshHud()
        {
            string Seconds(float s) => Mathf.Max(0f, s).ToString("0.0", CultureInfo.InvariantCulture);
            string text;
            if (roadPhase != RoadPhase.None)
            {
                text = roadPhase == RoadPhase.Walk ? Loc.T("hud.roadWalk") : Loc.F("hud.road", Mathf.RoundToInt(runner.Progress * 100f));
                ui.SetMission(text, roadPhase == RoadPhase.Walk ? 1f : runner.Progress, Earned + (roadPhase == RoadPhase.Run ? runner.Coins : 0));
                return;
            }
            switch (level.mission)
            {
                case MissionType.CollectCoins: text = Loc.F("hud.coins", coinsThisRun, level.coinTarget); break;
                case MissionType.Exit:
                    text = portal != null && portal.IsOpen
                        ? Loc.T("hud.exitOpen")
                        : Loc.F("hud.exitWait", objectivesDone, objectivesTotal);
                    break;
                case MissionType.Paint:
                    text = level.final
                        ? Loc.F("hud.final", Mathf.RoundToInt(100f * painted.Count / Mathf.Max(1, grid.FloorCount)))
                        : Loc.F("hud.paint", painted.Count, PaintGoal);
                    break;
                case MissionType.CoinRain: text = Loc.F("hud.rain", coinsThisRun, level.coinTarget, Seconds(level.surviveSeconds - elapsed)); break;
                case MissionType.Treasure: text = Loc.F("hud.treasure", coinsThisRun, Seconds(level.surviveSeconds - elapsed)); break;
                case MissionType.Tunnel: text = Loc.F("hud.tunnel", coinsThisRun, Mathf.RoundToInt(runner.Progress * 100f)); break;
                case MissionType.Boss: text = Loc.F("hud.boss", objectivesDone, objectivesTotal); break;
                case MissionType.Quest: text = questReady ? Loc.T("quest.hudGo." + level.quest) : Loc.F("quest.hud." + level.quest, objectivesDone, objectivesTotal); break;
                case MissionType.Monster: text = ArenaHud(); break;
                case MissionType.Hunt: text = HuntHud(); break;
                case MissionType.Thief:
                    text = level.thiefRace ? Loc.F("hud.thiefRace", coinsThisRun, thiefCoins, objectivesTotal) : Loc.F("hud.thief", objectivesDone, objectivesTotal);
                    break;
                case MissionType.Escort:
                    text = EscortTimed ? Loc.F("hud.escortTime", EscortStepsLeft())
                        : level.escortStops > 1 ? Loc.F("hud.escortStops", objectivesDone, objectivesTotal, EscortStepsLeft())
                        : Loc.F("hud.escort", EscortStepsLeft());
                    break;
                case MissionType.Clone: text = Loc.F(level.cloneKnockouts > 0 ? "hud.cloneKo" : "hud.clone", objectivesDone, objectivesTotal); break;
                default: text = Loc.F("hud.survive", Seconds(level.surviveSeconds - elapsed)); break;
            }
            if (level.timeLimit > 0f && level.mission != MissionType.CoinRain) text += "  ·  " + Loc.F("hud.timeLeft", Seconds(level.timeLimit - elapsed));
            ui.SetMission(text, MissionProgress(), Earned);
        }
    }
}
