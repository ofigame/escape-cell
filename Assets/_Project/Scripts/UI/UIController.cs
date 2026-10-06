using System;
using System.Collections.Generic;
using System.Globalization;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Monetization;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    public enum SettingKind
    {
        Sound,
        Music,
        Vibration,
        Language,
        Camera,
        TestMode
    }

    /// <summary>
    /// All screens. Menus sit on glass cards above a dimmed (and, via the camera, blurred) scene;
    /// the in-game HUD uses small dark pills so it stays readable over the bright platform.
    /// Texts come from <see cref="Loc"/>; the whole UI is rebuilt when the language changes.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        public event Action PlayPressed;
        public event Action NextPressed;
        public event Action RetryPressed;
        public event Action MenuPressed;
        public event Action MapPressed;
        public event Action PausePressed;
        public event Action ResumePressed;
        public event Action<SettingKind> SettingToggled;
        public event Action<int> LevelChosen;
        public event Action WatchAdPressed;
        public event Action BonusPressed;
        public event Action<int> StoryPressed;
        public event Action ContinuePressed;
        public event Action DoublePressed;
        public event Action GaragePressed;
        public event Action CampPressed;
        public event Action ShopPressed;
        public event Action DailyPressed;
        /// <summary>A HUD tool button was tapped (slot 0 = left, 1 = right).</summary>
        public event Action<int> ToolPressed;
        /// <summary>PLAY on the before-level card: level, start with a shield, take an extra rescue.</summary>
        public event Action<int, bool, bool> PrelevelPlay;
        public event Action PrelevelClosed;

        /// <summary>Everything the result card shows.</summary>
        public struct ResultInfo
        {
            public bool won, bonusRound, hasNext, bonusAvailable, canContinue, canDouble;
            public string subtitle, note, goalText;
            public float goalProgress;
            public int coins, stars;
            public float meter;
            public string meterText;
        }

        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);

        private Canvas canvas;
        private CanvasScaler scaler;

        // Menu
        private UiScreen menu;
        private TextMeshProUGUI menuLevel, menuWorld, menuMission, menuCoins, menuLives, menuStars;
        private Button menuDaily;

        // Before-level card
        private UiScreen prelevel;
        private TextMeshProUGUI preWorld, preTitle, preMission;
        private readonly Image[] preStars = new Image[3];
        private BoostView preShield, preRescue;
        private bool preShieldOn, preRescueOn;
        private int preLevel;
        private int levelCount;

        // Out of lives
        private UiScreen noLives;
        private TextMeshProUGUI noLivesTimer;
        private float livesRefresh;

        // Settings
        private UiScreen settings;
        private readonly Dictionary<SettingKind, TextMeshProUGUI> settingValues = new Dictionary<SettingKind, TextMeshProUGUI>();
        private readonly Dictionary<SettingKind, Image> settingFaces = new Dictionary<SettingKind, Image>();

        // HUD
        private UiScreen hud;
        private TextMeshProUGUI hudLevel, hudMission, hudBonus, shieldText;
        private Image hudFill, shieldFill;
        private RectTransform shieldPill;
        private RectTransform rescuePill, hoverPill;
        private RectTransform missionPill, bonusPill, comboPill;
        private readonly ToolButton[] toolButtons = new ToolButton[2];
        private RectTransform alarmPill;
        private TextMeshProUGUI comboText;
        private Image comboFill;
        private int comboShown = 1;
        private float missionPunch = 1f, bonusPunch = 1f;
        private float lastProgress;

        private class FlyingCoin
        {
            public RectTransform rect;
            public Vector3 start;
            public float t;
        }
        private readonly List<FlyingCoin> flyingCoins = new List<FlyingCoin>();
        private TextMeshProUGUI rescueText, hoverText;
        private Image rescueFill, hoverFace;
        private Image warnLeft, warnRight;
        private bool warningActive;

        // Intro banner
        private RectTransform intro;
        private CanvasGroup introGroup;
        private TextMeshProUGUI introTitle, introText;
        private float introTime = 99f;

        // Pause & result
        private UiScreen pause;
        private UiScreen result;
        private TextMeshProUGUI resultTitle, resultSub, resultReward;
        private Button resultNext, resultRetry, resultMap, resultMenu, resultBonus, resultContinue, resultDouble;
        private RectTransform resultStarRow;
        private readonly Image[] resultStars = new Image[3];
        private TextMeshProUGUI resultNote, resultMeterText, resultGoal;
        private GameObject resultGoalBar;
        private Image resultGoalFill;
        private Image resultMeterFill;
        private int starsEarned;
        private float starTime = 99f;
        private float meterShown, meterTarget;

        public MapScreen Map { get; private set; }
        public StoryScreen Story { get; private set; }
        public ShopScreen Shop { get; private set; }
        public GarageScreen Garage { get; private set; }
        public CampScreen Camp { get; private set; }

        private GameObject bannerPlaceholder;

        private class Floater
        {
            public TextMeshProUGUI text;
            public float age;
        }
        private readonly List<Floater> floaters = new List<Floater>();

        public static UIController Create(int levelCount)
        {
            var ui = new GameObject("UI").AddComponent<UIController>();
            ui.Build(levelCount);
            return ui;
        }

        private void Build(int levelCount)
        {
            this.levelCount = levelCount;
            canvas = UiFactory.CreateCanvas("Canvas", out scaler);
            canvas.transform.SetParent(transform, false);

            // Everything sits inside the device safe area (clear of notches and the home indicator).
            var root = UiFactory.Stretch("SafeArea", canvas.transform);
            root.gameObject.AddComponent<SafeArea>();

            BuildHud(root);
            BuildIntro(root);
            BuildMenu(root);
            Map = MapScreen.Create(root, levelCount);
            Map.LevelChosen += level => LevelChosen?.Invoke(level);
            Map.BackPressed += () => MenuPressed?.Invoke();
            Map.BonusPressed += () => BonusPressed?.Invoke();
            Map.StoryPressed += world => StoryPressed?.Invoke(world);
            BuildPause(root);
            BuildResult(root);
            BuildSettings(root);
            BuildNoLives(root);
            Story = StoryScreen.Create(root);
            Shop = ShopScreen.Create(root);
            Garage = GarageScreen.Create(root);
            Camp = CampScreen.Create(root);
            Camp.BackPressed += () => MenuPressed?.Invoke();
            Shop.BackPressed += () => MenuPressed?.Invoke();
            Garage.BackPressed += () => MenuPressed?.Invoke();
            BuildPrelevel(root);
            BuildBanner(root);
        }

        // ---------- Menu ----------

        private void BuildMenu(Transform root)
        {
            menu = UiScreen.Create("Menu", root, out var t);

            // Top bar: coins, lives, stars, settings.
            var coins = UiFactory.Pill("Coins", t, TopLeft, new Vector2(40f, -40f), new Vector2(260f, 100f), UiFactory.PillColor);
            CoinIcon(coins, new Vector2(56f, 0f));
            menuCoins = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f),
                "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            var lives = UiFactory.Pill("Lives", t, TopLeft, new Vector2(320f, -40f), new Vector2(280f, 100f), UiFactory.PillColor);
            HeartIcon(lives, new Vector2(58f, 0f), 56f);
            menuLives = UiFactory.TextBox("Value", lives, new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(170f, 90f),
                "", 44f, Palette.UiText, align: TextAlignmentOptions.Left);

            var stars = UiFactory.Pill("Stars", t, TopLeft, new Vector2(620f, -40f), new Vector2(220f, 100f), UiFactory.PillColor);
            var starIcon = UiFactory.Box("Star", stars, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(60f, 60f));
            starIcon.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(starIcon, Palette.UiGold, UiSprites.Star).raycastTarget = false;
            menuStars = UiFactory.TextBox("Value", stars, new Vector2(0f, 0.5f), new Vector2(96f, 0f), new Vector2(120f, 90f),
                "0", 44f, Palette.UiGold, align: TextAlignmentOptions.Left);

            var gear = UiFactory.MakeButton(t, "", Kind.Icon, TopRight, new Vector2(-40f, -40f), new Vector2(110f, 110f), ShowSettings);
            SettingsIcon(gear.transform);

            // Logo up top; the robot itself stands in the middle of the screen, on its platform.
            var logo = UiFactory.TextBox("Logo", t, Top, new Vector2(0f, -170f), new Vector2(1000f, 300f), "ESCAPE\nCELL", 150f, Color.white, title: true);
            logo.lineSpacing = -22f;
            logo.enableVertexGradient = true;
            logo.colorGradient = new VertexGradient(Color.white, Color.white, Palette.UiCyan, Palette.UiCyan);
            logo.raycastTarget = false;

            // Daily chest banner (only while unclaimed today).
            menuDaily = UiFactory.MakeButton(t, Loc.T("daily.ready"), Kind.Gold, Bottom, new Vector2(0f, Ads.BannerReserve + 700f), new Vector2(820f, 100f),
                () => DailyPressed?.Invoke(), 36f);
            menuDaily.gameObject.AddComponent<Pulse>();

            // Shortcuts.
            var row = UiFactory.Box("Shortcuts", t, Bottom, new Vector2(0f, Ads.BannerReserve + 540f), new Vector2(900f, 130f));
            // Four shortcuts: garage, shop, camp, map.
            var shortcuts = new (string label, Action press)[]
            {
                (Loc.T("btn.garage"), () => GaragePressed?.Invoke()), (Loc.T("btn.shop"), () => ShopPressed?.Invoke()),
                (Loc.T("btn.camp"), () => CampPressed?.Invoke()), (Loc.T("btn.map"), () => PlayPressed?.Invoke()),
            };
            for (int i = 0; i < shortcuts.Length; i++)
            {
                var (label, press) = shortcuts[i];
                UiFactory.MakeButton(row, label, Kind.Secondary, new Vector2(0f, 0.5f), new Vector2(i * 228f, 0f), new Vector2(216f, 130f), () => press(), 38f);
            }

            // Level card with the big PLAY button.
            var card = UiFactory.Card("LevelCard", t, Bottom, new Vector2(0f, Ads.BannerReserve + 50f), new Vector2(900f, 460f));
            menuWorld = UiFactory.TextBox("World", card, Top, new Vector2(0f, -30f), new Vector2(820f, 56f), "", 32f, Palette.UiCyan);
            menuWorld.characterSpacing = 4f;
            menuLevel = UiFactory.TextBox("Level", card, Top, new Vector2(0f, -78f), new Vector2(820f, 110f), "", 80f, Palette.UiText, title: true);
            menuMission = UiFactory.TextBox("Mission", card, Top, new Vector2(0f, -190f), new Vector2(820f, 56f), "", 38f,
                new Color(0.85f, 0.86f, 1f, 0.8f), FontStyles.Normal);

            UiFactory.MakeButton(card, Loc.T("menu.play"), Kind.Primary, Bottom, new Vector2(0f, 36f), new Vector2(620f, 160f), () => PlayPressed?.Invoke(), 84f);
        }

        public void ShowMenu(int levelIndex, int coins, string world, string mission)
        {
            HideAll();
            menu.Show();
            SetBanner(true);
            menuLevel.text = Loc.F("level", levelIndex + 1);
            menuWorld.text = world;
            menuMission.text = mission;
            menuCoins.text = coins.ToString();
            menuStars.text = Progress.TotalStars(levelCount).ToString();
            menuDaily.gameObject.SetActive(DailyChest.Ready);
            RefreshLives();
        }

        /// <summary>Coins changed while the menu is up (daily chest, shop).</summary>
        public void RefreshMenuCoins()
        {
            menuCoins.text = SaveData.Coins.ToString();
            menuDaily.gameObject.SetActive(DailyChest.Ready);
        }

        public void ShowShop()
        {
            HideAll();
            SetBanner(true);
            Shop.Show();
        }

        public void ShowCamp()
        {
            HideAll();
            SetBanner(true);
            Camp.Show();
        }

        public void ShowGarage(int worldReached)
        {
            HideAll();
            SetBanner(true);
            Garage.Show(worldReached);
        }

        // ---------- Before a level ----------

        private void BuildPrelevel(Transform root)
        {
            prelevel = UiScreen.Create("Prelevel", root, out var t);
            UiFactory.Dim(t, new Color(0.06f, 0.05f, 0.18f, 0.55f));
            var card = UiFactory.Card("Card", t, Middle, Vector2.zero, new Vector2(860f, 1000f));
            prelevel.SetPopTarget(card);

            UiFactory.MakeButton(card, "X", Kind.Icon, TopRight, new Vector2(-24f, -24f), new Vector2(100f, 100f), () =>
            {
                prelevel.Hide();
                PrelevelClosed?.Invoke();
            }, 48f);
            preWorld = UiFactory.TextBox("World", card, Top, new Vector2(0f, -40f), new Vector2(640f, 56f), "", 32f, Palette.UiCyan);
            preWorld.characterSpacing = 4f;
            preTitle = UiFactory.TextBox("Title", card, Top, new Vector2(0f, -90f), new Vector2(760f, 120f), "", 90f, Palette.UiText, title: true);
            preMission = UiFactory.TextBox("Mission", card, Top, new Vector2(0f, -215f), new Vector2(780f, 70f), "", 38f, new Color(0.85f, 0.86f, 1f, 0.85f), FontStyles.Normal);

            for (int i = 0; i < 3; i++)
            {
                var star = UiFactory.Box("Star" + i, card, Top, new Vector2((i - 1) * 110f, -300f), new Vector2(90f, 90f));
                preStars[i] = UiFactory.Fill(star, Color.white, UiSprites.Star);
                preStars[i].raycastTarget = false;
            }

            UiFactory.TextBox("Boosts", card, Top, new Vector2(0f, -420f), new Vector2(780f, 56f), Loc.T("pre.boosts"), 36f, Palette.UiText);
            preShield = BoostTile(card, new Vector2(-195f, -490f), Boost.StartShield);
            preRescue = BoostTile(card, new Vector2(195f, -490f), Boost.ExtraRescue);

            UiFactory.MakeButton(card, Loc.T("menu.play"), Kind.Primary, Bottom, new Vector2(0f, 46f), new Vector2(620f, 160f),
                () => PrelevelPlay?.Invoke(preLevel, preShieldOn, preRescueOn), 84f);
        }

        private BoostView BoostTile(Transform card, Vector2 position, Boost boost)
        {
            var tile = UiFactory.Box(boost.ToString(), card, Top, position, new Vector2(360f, 200f));
            var view = new BoostView { boost = boost, bg = UiFactory.Fill(tile, new Color(0.12f, 0.1f, 0.26f, 0.9f), UiSprites.Rounded, 1.2f) };
            view.ring = UiFactory.Fill(UiFactory.Stretch("Ring", tile), Palette.UiGold, UiSprites.Ring, 1.2f);
            view.ring.raycastTarget = false;
            UiFactory.TextBox("Name", tile, Top, new Vector2(0f, -24f), new Vector2(330f, 60f), Loc.T("shop." + boost), 34f, Palette.UiText);
            view.info = UiFactory.TextBox("Info", tile, Bottom, new Vector2(0f, 24f), new Vector2(330f, 70f), "", 38f, Palette.UiGold);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = view.bg;
            button.onClick.AddListener(() => ToggleBoost(view));
            tile.gameObject.AddComponent<ButtonPress>();
            view.root = tile.gameObject;
            return view;
        }

        private void ToggleBoost(BoostView view)
        {
            bool on = view.boost == Boost.StartShield ? preShieldOn : preRescueOn;
            if (!on && Data.Shop.Owned(view.boost) <= 0)
            {
                if (!Data.Shop.TryBuy(view.boost))
                {
                    AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                    return;
                }
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
            }
            else
            {
                AudioManager.PlaySfx(Sfx.Click, 0.7f, on ? 0.9f : 1.2f);
            }
            if (view.boost == Boost.StartShield) preShieldOn = !on;
            else preRescueOn = !on;
            RefreshBoosts();
        }

        private void RefreshBoosts()
        {
            foreach (var v in new[] { preShield, preRescue })
            {
                bool on = v.boost == Boost.StartShield ? preShieldOn : preRescueOn;
                int owned = Data.Shop.Owned(v.boost);
                v.ring.gameObject.SetActive(on);
                v.info.text = owned > 0 || on ? Loc.F("shop.owned", owned) : Data.Shop.Price(v.boost).ToString();
                v.info.color = owned > 0 || on ? Palette.UiCyan : Palette.UiGold;
            }
        }

        /// <summary>The card before a level: mission, best stars and boosts to take along.</summary>
        public void ShowPrelevel(int levelIndex, string world, string mission, int bestStars, bool rescueAllowed)
        {
            preLevel = levelIndex;
            preShieldOn = preRescueOn = false;
            preTitle.text = Loc.F("level", levelIndex + 1);
            preWorld.text = world;
            preMission.text = mission;
            for (int i = 0; i < 3; i++) preStars[i].color = i < bestStars ? Palette.UiGold : new Color(1f, 1f, 1f, 0.15f);
            preRescue.root.SetActive(rescueAllowed);
            ((RectTransform)preShield.root.transform).anchoredPosition = new Vector2(rescueAllowed ? -195f : 0f, -490f);
            RefreshBoosts();
            prelevel.Show();
            prelevel.transform.SetAsLastSibling();
        }

        public void HidePrelevel() => prelevel.Hide(true);

        private class BoostView
        {
            public Boost boost;
            public GameObject root;
            public Image bg, ring;
            public TextMeshProUGUI info;
        }

        public void ShowMap(int unlocked, int coins, int focusLevel, int animateFrom = -1)
        {
            HideAll();
            Map.Show(unlocked, coins, focusLevel, animateFrom);
            SetBanner(true);
            RefreshLives();
        }

        /// <summary>A story scene over the blurred scene; <paramref name="onDone"/> runs when it ends.</summary>
        public void ShowStory(int scene, int world, Action onDone)
        {
            HideAll();
            SetBanner(false);
            Story.Play(scene, world, onDone);
        }

        /// <summary>Three bars: a simple, universally understood "settings" glyph.</summary>
        private static void SettingsIcon(Transform button)
        {
            var face = button.Find("Face");
            for (int i = -1; i <= 1; i++)
            {
                var bar = UiFactory.Box("Bar", face, Middle, new Vector2(0f, i * 18f), new Vector2(52f, 9f));
                UiFactory.Fill(bar, Palette.UiText, UiSprites.Rounded, 12f).raycastTarget = false;
            }
        }

        // ---------- Lives ----------

        /// <summary>"5" when full, otherwise "3  07:12" (count and time to the next life).</summary>
        public static string LivesText()
        {
            int count = Lives.Count;
            return count >= Lives.Max ? count.ToString() : $"{count}  <size=70%>{Lives.UntilNextText}</size>";
        }

        /// <summary>A heart drawn from two circles and a rotated square.</summary>
        public static void HeartIcon(Transform parent, Vector2 position, float size)
        {
            var root = UiFactory.Box("Heart", parent, new Vector2(0f, 0.5f), position, new Vector2(size, size));
            root.pivot = new Vector2(0.5f, 0.5f);
            var color = new Color(1f, 0.38f, 0.48f);
            float r = size * 0.56f;
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                var lobe = UiFactory.Box("Lobe", root, new Vector2(0.5f, 0.5f), new Vector2(x * size, size * 0.14f), new Vector2(r, r));
                UiFactory.Fill(lobe, color, UiSprites.Circle).raycastTarget = false;
            }
            var tip = UiFactory.Box("Tip", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -size * 0.08f), new Vector2(r * 1.02f, r * 1.02f));
            tip.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiFactory.Fill(tip, color).raycastTarget = false;
        }

        private void BuildNoLives(Transform root)
        {
            noLives = UiScreen.Create("NoLives", root, out var t);
            UiFactory.Dim(t, new Color(0.06f, 0.05f, 0.18f, 0.55f));
            var card = UiFactory.Card("Card", t, Middle, Vector2.zero, new Vector2(860f, 900f));
            UiFactory.TextBox("Title", card, Top, new Vector2(0f, -60f), new Vector2(800f, 120f), Loc.T("lives.title"), 80f, Palette.UiRed, title: true);

            var heart = UiFactory.Box("HeartHolder", card, Top, new Vector2(0f, -200f), new Vector2(160f, 160f));
            HeartIcon(heart, new Vector2(80f, 0f), 150f);
            UiFactory.Text(heart, "0", 64f, Color.white);

            noLivesTimer = UiFactory.TextBox("Timer", card, Top, new Vector2(0f, -390f), new Vector2(800f, 70f), "", 46f, Palette.UiText, FontStyles.Normal);
            UiFactory.MakeButton(card, Loc.T("lives.watchAd"), Kind.Primary, Top, new Vector2(0f, -500f), new Vector2(640f, 160f), () => WatchAdPressed?.Invoke(), 64f);
            UiFactory.MakeButton(card, Loc.T("btn.close"), Kind.Secondary, Top, new Vector2(0f, -690f), new Vector2(640f, 150f), () => noLives.Hide(), 60f);
        }

        public void ShowNoLives()
        {
            RefreshLives();
            noLives.Show();
        }

        public void HideNoLives() => noLives.Hide();

        private void RefreshLives()
        {
            string text = LivesText();
            menuLives.text = text;
            Map.SetLives(text);
            noLivesTimer.text = Lives.Count > 0 ? Loc.F("lives.left", Lives.Count) : Loc.F("lives.next", Lives.UntilNextText);
        }

        // ---------- Settings ----------

        private void BuildSettings(Transform root)
        {
            settings = UiScreen.Create("Settings", root, out var t);
            UiFactory.Dim(t, new Color(0.06f, 0.05f, 0.18f, 0.5f));
            var card = UiFactory.Card("Card", t, Middle, Vector2.zero, new Vector2(860f, 1310f));
            UiFactory.TextBox("Title", card, Top, new Vector2(0f, -50f), new Vector2(800f, 120f), Loc.T("settings.title"), 90f, Palette.UiText, title: true);

            var rows = new[]
            {
                (SettingKind.Sound, "settings.sound"),
                (SettingKind.Music, "settings.music"),
                (SettingKind.Vibration, "settings.vibration"),
                (SettingKind.Language, "settings.language"),
                (SettingKind.Camera, "settings.camera"),
                (SettingKind.TestMode, "settings.test"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var (kind, label) = rows[i];
                float y = -210f - i * 140f;
                UiFactory.TextBox("Label", card, TopLeft, new Vector2(70f, y), new Vector2(400f, 110f), Loc.T(label), 52f,
                    Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.Left);
                var button = UiFactory.MakeButton(card, "", Kind.Secondary, TopRight, new Vector2(-60f, y), new Vector2(330f, 110f),
                    () => SettingToggled?.Invoke(kind), 46f);
                settingValues[kind] = button.GetComponentInChildren<TextMeshProUGUI>();
                settingFaces[kind] = button.transform.Find("Face").GetComponent<Image>();
            }

            UiFactory.MakeButton(card, Loc.T("btn.close"), Kind.Primary, Bottom, new Vector2(0f, 50f), new Vector2(560f, 150f), HideSettings, 64f);
            RefreshSettings();
        }

        public void RefreshSettings()
        {
            SetSetting(SettingKind.Sound, SaveData.Sound);
            SetSetting(SettingKind.Music, SaveData.Music);
            SetSetting(SettingKind.Vibration, SaveData.Vibration);
            settingValues[SettingKind.Language].text = Loc.T("lang.name");
            settingValues[SettingKind.Camera].text = Loc.T(SaveData.PerspectiveView ? "view.3d" : "view.iso");
            SetSetting(SettingKind.TestMode, SaveData.TestMode);
        }

        private void SetSetting(SettingKind kind, bool on)
        {
            settingValues[kind].text = Loc.T(on ? "on" : "off");
            settingValues[kind].color = on ? Palette.UiCyan : new Color(1f, 1f, 1f, 0.5f);
            settingFaces[kind].color = on ? new Color(0.62f, 0.62f, 1f, 0.28f) : new Color(0f, 0f, 0f, 0.25f);
        }

        public void ShowSettings()
        {
            RefreshSettings();
            settings.Show();
        }

        private void HideSettings() => settings.Hide();

        // ---------- HUD ----------

        private void BuildHud(Transform root)
        {
            hud = UiScreen.Create("HUD", root, out var t);

            UiFactory.MakeButton(t, "II", Kind.Icon, TopLeft, new Vector2(36f, -36f), new Vector2(124f, 124f), () => PausePressed?.Invoke(), 52f);

            var mission = missionPill = UiFactory.Pill("Mission", t, Top, new Vector2(0f, -36f), new Vector2(500f, 124f), UiFactory.PillColor);
            hudLevel = UiFactory.TextBox("Level", mission, Top, new Vector2(0f, -10f), new Vector2(480f, 40f), "", 30f, Palette.UiCyan);
            hudLevel.characterSpacing = 6f;
            hudMission = UiFactory.TextBox("Text", mission, Bottom, new Vector2(0f, 12f), new Vector2(480f, 64f), "", 44f, Palette.UiText);
            UiFactory.Bar(t, Top, new Vector2(0f, -176f), new Vector2(460f, 16f), Palette.UiCyan, out hudFill);

            var bonus = bonusPill = UiFactory.Pill("Bonus", t, TopRight, new Vector2(-36f, -36f), new Vector2(220f, 124f), UiFactory.PillColor);
            CoinIcon(bonus, new Vector2(56f, 0f));
            hudBonus = UiFactory.TextBox("Value", bonus, new Vector2(0f, 0.5f), new Vector2(98f, 0f), new Vector2(110f, 80f),
                "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            // Combo (under the coin counter): streak multiplier with a draining timer.
            comboPill = UiFactory.Pill("Combo", t, TopRight, new Vector2(-36f, -176f), new Vector2(220f, 86f), new Color(0.55f, 0.32f, 0.05f, 0.85f));
            comboText = UiFactory.TextBox("Text", comboPill, Top, new Vector2(0f, -6f), new Vector2(200f, 50f), "", 38f, Palette.UiGold);
            UiFactory.Bar(comboPill, Bottom, new Vector2(0f, 10f), new Vector2(170f, 10f), Palette.UiGold, out comboFill);
            comboPill.gameObject.SetActive(false);

            shieldPill = UiFactory.Pill("Shield", t, Top, new Vector2(0f, -212f), new Vector2(420f, 86f), new Color(0.2f, 0.55f, 0.75f, 0.75f));
            shieldText = UiFactory.TextBox("Text", shieldPill, Top, new Vector2(0f, -8f), new Vector2(400f, 48f), "", 36f, Color.white);
            UiFactory.Bar(shieldPill, Bottom, new Vector2(0f, 12f), new Vector2(360f, 12f), Palette.UiCyan, out shieldFill);
            shieldPill.gameObject.SetActive(false);

            // Rescue charges (under the pause button): a life-ring icon, the count, and progress to the next one.
            rescuePill = UiFactory.Pill("Rescue", t, TopLeft, new Vector2(36f, -176f), new Vector2(200f, 92f), UiFactory.PillColor);
            var ring = UiFactory.Box("Ring", rescuePill, new Vector2(0f, 0.5f), new Vector2(18f, 6f), new Vector2(54f, 54f));
            UiFactory.Fill(ring, Palette.UiCyan, UiSprites.Ring, 0.25f).raycastTarget = false;
            var core = UiFactory.Box("Core", ring, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
            UiFactory.Fill(core, Palette.UiCyan, UiSprites.Circle).raycastTarget = false;
            rescueText = UiFactory.TextBox("Count", rescuePill, new Vector2(0f, 0.5f), new Vector2(84f, 6f), new Vector2(100f, 60f), "", 44f, Palette.UiText, align: TextAlignmentOptions.Left);
            UiFactory.Bar(rescuePill, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(160f, 8f), Palette.UiGold, out rescueFill);
            rescuePill.gameObject.SetActive(false);

            // Hover escape status (bottom centre).
            hoverPill = UiFactory.Pill("Hover", t, Bottom, new Vector2(0f, 60f), new Vector2(480f, 96f), UiFactory.PillColor);
            hoverFace = hoverPill.GetComponent<Image>();
            hoverText = UiFactory.Text(hoverPill, "", 42f, Palette.UiText);
            hoverPill.gameObject.SetActive(false);

            alarmPill = UiFactory.Pill("Alarm", t, Top, new Vector2(0f, -310f), new Vector2(560f, 80f), new Color(0.75f, 0.12f, 0.2f, 0.9f));
            UiFactory.Text(alarmPill, Loc.T("hud.alarm"), 38f, Color.white);
            alarmPill.gameObject.SetActive(false);

            // Tool buttons in the bottom corners.
            toolButtons[0] = ToolButton.Create(t, new Vector2(0f, 0f), new Vector2(40f, 170f));
            toolButtons[1] = ToolButton.Create(t, new Vector2(1f, 0f), new Vector2(-40f, 170f));
            toolButtons[0].Pressed += () => ToolPressed?.Invoke(0);
            toolButtons[1].Pressed += () => ToolPressed?.Invoke(1);

            warnLeft = WarningBar(t, new Vector2(0f, 0.5f), new Vector2(12f, 0f));
            warnRight = WarningBar(t, new Vector2(1f, 0.5f), new Vector2(-12f, 0f));
        }

        private static Image WarningBar(Transform root, Vector2 anchor, Vector2 position)
        {
            var rt = UiFactory.Box("Warning", root, anchor, position, new Vector2(56f, 640f));
            var img = UiFactory.Fill(rt, Palette.UiRed, UiSprites.Rounded, 2f);
            img.raycastTarget = false;
            UiFactory.Text(rt, "!", 60f, Color.white);
            return img;
        }

        public static void CoinIcon(Transform parent, Vector2 position)
        {
            var icon = UiFactory.Box("Coin", parent, new Vector2(0f, 0.5f), position - new Vector2(28f, 0f), new Vector2(56f, 56f));
            UiFactory.Fill(icon, Palette.UiGold, UiSprites.Circle).raycastTarget = false;
            var inner = UiFactory.Box("Inner", icon, Middle, Vector2.zero, new Vector2(36f, 36f));
            UiFactory.Fill(inner, new Color(1f, 0.9f, 0.55f), UiSprites.Circle).raycastTarget = false;
        }

        public void ShowHud(int levelIndex)
        {
            lastProgress = 0f;
            HideAll();
            hud.Show();
            SetBanner(false);
            hudLevel.text = levelIndex < 0 ? Loc.T("level.bonus") : Loc.F("level", levelIndex + 1);
        }

        public void SetMission(string text, float progress, int coinsThisRun)
        {
            // A step of progress (not just the clock ticking) makes the mission pill jump.
            if (progress > lastProgress + 0.001f && hud.IsVisible && Mathf.Abs(progress - lastProgress) > 0.02f) missionPunch = 0f;
            lastProgress = progress;
            hudMission.text = text;
            UiFactory.SetBar(hudFill, progress);
            hudBonus.text = coinsThisRun.ToString();
        }

        /// <summary>WARDEN alarm: a pulsing red banner while waves come faster and coins count double.</summary>
        public void SetAlarm(bool on)
        {
            if (alarmPill != null) alarmPill.gameObject.SetActive(on);
        }

        public void SetTool(int slot, Tool? tool, int charges, bool trial, float active) => toolButtons[slot].Set(tool, charges, trial, active);

        public void PunchTool(int slot) => toolButtons[slot].Punch();

        /// <summary>True when a screen point is on a visible tool button (input leaves those presses to the UI).</summary>
        public bool IsOverTool(Vector2 screen)
        {
            foreach (var b in toolButtons)
                if (b != null && b.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(b.Rect, screen, null)) return true;
            return false;
        }

        /// <summary>Combo multiplier (hidden at x1) and how much of its time window is left.</summary>
        public void SetCombo(int multiplier, float timeLeft)
        {
            bool on = multiplier > 1;
            if (comboPill.gameObject.activeSelf != on) comboPill.gameObject.SetActive(on);
            if (!on) { comboShown = 1; return; }
            if (multiplier != comboShown) comboPill.localScale = Vector3.one * 1.35f;
            comboShown = multiplier;
            comboText.text = Loc.F("hud.combo", multiplier);
            UiFactory.SetBar(comboFill, timeLeft);
        }

        public void SetShield(float left, float max)
        {
            bool on = left > 0f;
            if (shieldPill.gameObject.activeSelf != on) shieldPill.gameObject.SetActive(on);
            if (!on) return;
            shieldText.text = Loc.F("hud.shield", left.ToString("0.0", CultureInfo.InvariantCulture));
            UiFactory.SetBar(shieldFill, left / max);
        }

        public void SetWarning(bool active) => warningActive = active;

        /// <summary>Rescue charges; hidden in worlds where they are not unlocked yet.</summary>
        public void SetRescues(bool enabled, int count, float progress)
        {
            if (rescuePill.gameObject.activeSelf != enabled) rescuePill.gameObject.SetActive(enabled);
            if (!enabled) return;
            rescueText.text = "x" + count;
            UiFactory.SetBar(rescueFill, progress);
        }

        /// <summary>Hover escape status: ready (bright) or cooling down (dim with a countdown).</summary>
        public void SetHover(bool enabled, float cooldown)
        {
            if (hoverPill.gameObject.activeSelf != enabled) hoverPill.gameObject.SetActive(enabled);
            if (!enabled) return;
            bool ready = cooldown <= 0f;
            hoverText.text = ready ? Loc.T("hud.hoverReady") : Loc.F("hud.hoverCooldown", Mathf.CeilToInt(cooldown));
            hoverText.color = ready ? Palette.UiCyan : new Color(1f, 1f, 1f, 0.45f);
            hoverFace.color = ready ? new Color(0.14f, 0.3f, 0.45f, 0.85f) : UiFactory.PillColor;
        }

        // ---------- Intro banner ----------

        private void BuildIntro(Transform root)
        {
            intro = UiFactory.Pill("Intro", root, Middle, new Vector2(0f, 430f), new Vector2(940f, 230f), UiFactory.PillColor);
            introGroup = intro.gameObject.AddComponent<CanvasGroup>();
            introGroup.blocksRaycasts = false;
            introTitle = UiFactory.TextBox("Title", intro, Top, new Vector2(0f, -22f), new Vector2(900f, 60f), "", 40f, Palette.UiCyan);
            introTitle.characterSpacing = 8f;
            introText = UiFactory.TextBox("Text", intro, Bottom, new Vector2(0f, 30f), new Vector2(900f, 110f), "", 64f, Color.white, title: true);
            intro.gameObject.SetActive(false);
        }

        public void ShowIntro(string title, string text)
        {
            introTitle.text = title;
            introText.text = text;
            introTime = 0f;
            intro.gameObject.SetActive(true);
        }

        // ---------- Pause ----------

        private void BuildPause(Transform root)
        {
            pause = UiScreen.Create("Pause", root, out var t);
            UiFactory.Dim(t, new Color(0.06f, 0.05f, 0.18f, 0.45f));
            var card = UiFactory.Card("Card", t, Middle, Vector2.zero, new Vector2(820f, 830f));
            UiFactory.TextBox("Title", card, Top, new Vector2(0f, -60f), new Vector2(760f, 130f), Loc.T("pause.title"), 90f, Palette.UiText, title: true);
            UiFactory.MakeButton(card, Loc.T("btn.resume"), Kind.Primary, Top, new Vector2(0f, -240f), new Vector2(600f, 160f), () => ResumePressed?.Invoke(), 70f);
            UiFactory.MakeButton(card, Loc.T("btn.restart"), Kind.Secondary, Top, new Vector2(0f, -430f), new Vector2(600f, 150f), () => RetryPressed?.Invoke(), 60f);
            UiFactory.MakeButton(card, Loc.T("btn.map"), Kind.Secondary, Top, new Vector2(0f, -610f), new Vector2(600f, 150f), () => MapPressed?.Invoke(), 60f);
        }

        public void ShowPause() => pause.Show();
        public void HidePause() => pause.Hide();

        // ---------- Result ----------

        private void BuildResult(Transform root)
        {
            result = UiScreen.Create("Result", root, out var t);
            UiFactory.Dim(t, new Color(0.06f, 0.05f, 0.18f, 0.45f));
            var card = UiFactory.Card("Card", t, Middle, Vector2.zero, new Vector2(860f, 1290f));

            resultTitle = UiFactory.TextBox("Title", card, Top, new Vector2(0f, -50f), new Vector2(820f, 130f), "", 96f, Palette.UiCyan, title: true);
            resultSub = UiFactory.TextBox("Subtitle", card, Top, new Vector2(0f, -175f), new Vector2(800f, 100f), "", 38f,
                new Color(0.88f, 0.89f, 1f, 0.85f), FontStyles.Normal);

            // Three stars that pop in one after another (the middle one sits a little higher and bigger).
            resultStarRow = UiFactory.Box("Stars", card, Top, new Vector2(0f, -285f), new Vector2(560f, 150f));
            for (int i = 0; i < 3; i++)
            {
                float size = i == 1 ? 150f : 120f;
                var star = UiFactory.Box("Star" + i, resultStarRow, Middle, new Vector2((i - 1) * 170f, i == 1 ? 14f : -6f), new Vector2(size, size));
                star.pivot = new Vector2(0.5f, 0.5f);
                resultStars[i] = UiFactory.Fill(star, Color.white, UiSprites.Star);
                resultStars[i].raycastTarget = false;
            }

            var reward = UiFactory.Pill("Reward", card, Top, new Vector2(0f, -455f), new Vector2(360f, 100f), new Color(0f, 0f, 0f, 0.22f));
            CoinIcon(reward, new Vector2(70f, 0f));
            resultReward = UiFactory.TextBox("Value", reward, new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(220f, 90f),
                "", 54f, Palette.UiGold, align: TextAlignmentOptions.Left);

            resultNote = UiFactory.TextBox("Note", card, Top, new Vector2(0f, -545f), new Vector2(780f, 84f), "", 34f, Palette.UiGold, FontStyles.Bold);
            resultNote.textWrappingMode = TextWrappingModes.Normal;
            resultNote.fontSizeMin = 26f;

            // Bonus meter: stars fill it, a full meter unlocks a bonus round.
            UiFactory.Bar(card, Top, new Vector2(-60f, -640f), new Vector2(560f, 30f), Palette.UiGold, out resultMeterFill);
            resultMeterText = UiFactory.TextBox("Meter", card, Top, new Vector2(335f, -628f), new Vector2(200f, 54f), "", 32f,
                new Color(1f, 1f, 1f, 0.8f), FontStyles.Bold, align: TextAlignmentOptions.Left);

            // The coin goal picked in the shop or garage: how far the coins still have to go.
            resultGoal = UiFactory.TextBox("Goal", card, Top, new Vector2(0f, -682f), new Vector2(760f, 48f), "", 32f, Palette.UiText, FontStyles.Bold);
            resultGoalBar = UiFactory.Bar(card, Top, new Vector2(0f, -738f), new Vector2(560f, 22f), new Color(0.36f, 0.85f, 0.6f), out resultGoalFill).gameObject;

            resultNext = UiFactory.MakeButton(card, Loc.T("btn.next"), Kind.Primary, Top, new Vector2(0f, -795f), new Vector2(620f, 150f), () => NextPressed?.Invoke(), 70f);
            resultRetry = UiFactory.MakeButton(card, Loc.T("btn.retry"), Kind.Primary, Top, new Vector2(0f, -795f), new Vector2(620f, 150f), () => RetryPressed?.Invoke(), 70f);
            resultBonus = UiFactory.MakeButton(card, Loc.T("btn.bonus"), Kind.Gold, Top, new Vector2(0f, -795f), new Vector2(620f, 150f), () => BonusPressed?.Invoke(), 76f);
            resultBonus.gameObject.AddComponent<Pulse>();
            resultContinue = UiFactory.MakeButton(card, Loc.T("btn.continue"), Kind.Gold, Top, new Vector2(0f, -795f), new Vector2(620f, 150f), () => ContinuePressed?.Invoke(), 50f);
            resultContinue.gameObject.AddComponent<Pulse>();
            resultDouble = UiFactory.MakeButton(card, Loc.T("btn.double"), Kind.Gold, Top, new Vector2(0f, -965f), new Vector2(620f, 135f), () => DoublePressed?.Invoke(), 54f);
            resultMap = UiFactory.MakeButton(card, Loc.T("btn.map"), Kind.Secondary, Top, new Vector2(-160f, -1120f), new Vector2(300f, 125f), () => MapPressed?.Invoke(), 54f);
            resultMenu = UiFactory.MakeButton(card, Loc.T("btn.menu"), Kind.Secondary, Top, new Vector2(160f, -1120f), new Vector2(300f, 125f), () => MenuPressed?.Invoke(), 54f);
        }

        public void ShowResult(ResultInfo info)
        {
            pause.Hide(true);
            result.Show();
            SetBanner(true);
            resultTitle.text = Loc.T(info.bonusRound ? "result.bonusDone" : info.won ? "result.win" : "result.lose");
            resultTitle.color = info.bonusRound ? Palette.UiGold : info.won ? Palette.UiCyan : Palette.UiRed;
            resultSub.text = info.subtitle;
            resultReward.text = $"+{info.coins}";
            resultNote.text = info.note ?? "";
            bool goal = !string.IsNullOrEmpty(info.goalText);
            resultGoal.gameObject.SetActive(goal);
            resultGoalBar.SetActive(goal);
            if (goal)
            {
                resultGoal.text = info.goalText;
                resultGoal.color = info.goalProgress >= 1f ? Palette.UiGold : Palette.UiText;
                UiFactory.SetBar(resultGoalFill, info.goalProgress);
            }
            resultNote.color = info.won ? Palette.UiGold : new Color(1f, 0.6f, 0.65f);

            bool showStars = info.won && !info.bonusRound;
            resultStarRow.gameObject.SetActive(showStars);
            starsEarned = showStars ? info.stars : 0;
            starTime = showStars ? 0f : 99f;
            for (int i = 0; i < 3; i++)
            {
                resultStars[i].color = new Color(1f, 1f, 1f, 0.14f);
                resultStars[i].rectTransform.localScale = Vector3.one;
            }

            meterTarget = info.meter;
            meterShown = Mathf.Min(meterShown, meterTarget);
            UiFactory.SetBar(resultMeterFill, meterShown);
            resultMeterText.text = info.meterText;

            // Top slot: BONUS, NEXT, CONTINUE (ad) or RETRY. Second slot: 2x COINS (ad) after a win, RETRY under CONTINUE.
            // Bottom row: MAP and MENU.
            bool ended = info.won || info.bonusRound;
            bool bonus = info.bonusAvailable && ended;
            bool next = !bonus && ended && info.hasNext;
            bool cont = !ended && info.canContinue;
            bool retryTop = !bonus && !next && !cont;
            resultBonus.gameObject.SetActive(bonus);
            resultNext.gameObject.SetActive(next);
            resultContinue.gameObject.SetActive(cont);
            resultRetry.gameObject.SetActive(retryTop || cont);
            ((RectTransform)resultRetry.transform).anchoredPosition = new Vector2(0f, retryTop ? -795f : -965f);
            resultDouble.gameObject.SetActive(ended && info.canDouble && info.coins > 0);
            resultReward.transform.parent.localScale = Vector3.one;
        }

        /// <summary>The 2x coins ad paid out: show the doubled reward with a pop and retire the button.</summary>
        public void ShowDoubled(int total)
        {
            resultReward.text = $"+{total}";
            resultDouble.gameObject.SetActive(false);
            resultReward.transform.parent.localScale = Vector3.one * 1.25f;
            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.3f);
        }

        /// <summary>Stars pop in one by one with a chime; the bonus meter fills up behind them.</summary>
        private void UpdateResultJuice(float dt)
        {
            if (!result.IsVisible) return;
            var rewardPill = resultReward.transform.parent;
            rewardPill.localScale = Vector3.Lerp(rewardPill.localScale, Vector3.one, dt * 8f);

            if (starTime < 3f)
            {
                float before = starTime;
                starTime += dt;
                for (int i = 0; i < starsEarned; i++)
                {
                    float at = 0.35f + i * 0.32f;
                    if (before < at && starTime >= at)
                    {
                        AudioManager.PlaySfx(Sfx.Coin, 0.9f, 1f + i * 0.18f);
                        Haptics.Light();
                    }
                    float t = Mathf.Clamp01((starTime - at) / 0.3f);
                    if (starTime < at) continue;
                    resultStars[i].color = Color.Lerp(Color.white, Palette.UiGold, t);
                    float pop = t < 1f ? 1f + Mathf.Sin(t * Mathf.PI) * 0.45f : 1f;
                    resultStars[i].rectTransform.localScale = Vector3.one * pop;
                }
            }

            if (meterShown < meterTarget && starTime > 0.35f + starsEarned * 0.32f)
            {
                meterShown = Mathf.MoveTowards(meterShown, meterTarget, dt * 0.8f);
                UiFactory.SetBar(resultMeterFill, meterShown);
            }
        }

        // ---------- Banner ----------

        /// <summary>Bottom strip where the banner ad lives; while no real network is connected, a labelled placeholder.</summary>
        private void BuildBanner(Transform root)
        {
            var strip = UiFactory.Rect("Banner", root, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, Ads.BannerReserve));
            UiFactory.Fill(strip, new Color(0.05f, 0.04f, 0.12f, 0.92f)).raycastTarget = false;
            UiFactory.Fill(UiFactory.Rect("Line", strip, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -3f), Vector2.zero), new Color(1f, 1f, 1f, 0.12f)).raycastTarget = false;
            UiFactory.Text(strip, Loc.T("ad.banner"), 34f, new Color(1f, 1f, 1f, 0.35f));
            bannerPlaceholder = strip.gameObject;
            bannerPlaceholder.SetActive(false);
        }

        /// <summary>Banners appear only on menu-type screens, never over gameplay.</summary>
        private void SetBanner(bool on)
        {
            if (on) Ads.Banner.Show();
            else Ads.Banner.Hide();
            bannerPlaceholder.SetActive(on && Ads.Banner.IsPlaceholder);
            bannerPlaceholder.transform.SetAsLastSibling();
        }

        /// <summary>A collected coin flies from the playfield into the coin counter, which bumps when it lands.</summary>
        public void FlyCoin(Vector3 screenPos)
        {
            var rect = UiFactory.Box("FlyingCoin", canvas.transform, Middle, Vector2.zero, new Vector2(64f, 64f));
            rect.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(rect, Palette.UiGold, UiSprites.Circle).raycastTarget = false;
            var inner = UiFactory.Box("Inner", rect, Middle, Vector2.zero, new Vector2(40f, 40f));
            UiFactory.Fill(inner, new Color(1f, 0.92f, 0.6f), UiSprites.Circle).raycastTarget = false;
            rect.position = screenPos;
            flyingCoins.Add(new FlyingCoin { rect = rect, start = screenPos });
        }

        private void UpdateJuice(float dt)
        {
            for (int i = flyingCoins.Count - 1; i >= 0; i--)
            {
                var c = flyingCoins[i];
                c.t += dt / 0.5f;
                var target = bonusPill.position;
                float t = Mathf.Clamp01(c.t);
                float ease = t * t * (3f - 2f * t);
                // Arc upward on the way to the counter.
                var p = Vector3.Lerp(c.start, target, ease) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 120f * canvas.scaleFactor);
                c.rect.position = p;
                float s = Mathf.Lerp(1.3f, 0.6f, t);
                c.rect.localScale = new Vector3(s, s, 1f);
                if (c.t < 1f) continue;
                Destroy(c.rect.gameObject);
                flyingCoins.RemoveAt(i);
                bonusPunch = 0f;
            }

            Punch(bonusPill, ref bonusPunch, dt, 0.25f);
            if (comboPill.localScale.x > 1f) comboPill.localScale = Vector3.Lerp(comboPill.localScale, Vector3.one, dt * 8f);
            if (alarmPill != null && alarmPill.gameObject.activeSelf)
            {
                float a = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.05f;
                alarmPill.localScale = new Vector3(a, a, 1f);
            }
            Punch(missionPill, ref missionPunch, dt, 0.18f);
        }

        private static void Punch(RectTransform rect, ref float t, float dt, float amount)
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + dt / 0.3f);
            float s = 1f + Mathf.Sin(t * Mathf.PI) * amount * (1f - t * 0.5f);
            rect.localScale = new Vector3(s, s, 1f);
        }

        // ---------- Floating text ----------

        public void Float(Vector3 screenPos, string text, Color color, float size = 64f)
        {
            var label = UiFactory.Text(canvas.transform, text, size, color, title: true);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = Middle;
            rt.sizeDelta = new Vector2(700f, 140f);
            rt.position = screenPos;
            floaters.Add(new Floater { text = label });
        }

        // ---------- Common ----------

        private void HideAll()
        {
            menu.Hide();
            Map.Hide();
            Story.Hide();
            Shop.Hide();
            Garage.Hide();
            Camp.Hide();
            prelevel.Hide(true);
            settings.Hide(true);
            noLives.Hide(true);
            hud.Hide(true);
            pause.Hide(true);
            result.Hide(true);
            intro.gameObject.SetActive(false);
            warningActive = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            UpdateJuice(dt);
            UpdateResultJuice(dt);

            livesRefresh -= dt;
            if (livesRefresh <= 0f)
            {
                livesRefresh = 0.5f;
                RefreshLives();
            }

            // Portrait: fit the width; landscape: fit the height. Fixed-size cards then work in both.
            scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0f : 1f;

            float flash = warningActive ? 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 16f) : 0f;
            var c = Palette.UiRed;
            c.a = Mathf.MoveTowards(warnLeft.color.a, flash, dt * 6f);
            warnLeft.color = warnRight.color = c;
            bool showWarn = c.a > 0.01f;
            if (warnLeft.gameObject.activeSelf != showWarn)
            {
                warnLeft.gameObject.SetActive(showWarn);
                warnRight.gameObject.SetActive(showWarn);
            }

            if (intro.gameObject.activeSelf)
            {
                introTime += dt;
                float a = introTime < 0.25f ? introTime / 0.25f : introTime > 1.7f ? 1f - (introTime - 1.7f) / 0.3f : 1f;
                introGroup.alpha = Mathf.Clamp01(a);
                float s = introTime < 0.25f ? Mathf.Lerp(1.25f, 1f, introTime / 0.25f) : 1f;
                intro.localScale = new Vector3(s, s, 1f);
                if (introTime > 2f) intro.gameObject.SetActive(false);
            }

            float scale = canvas.scaleFactor;
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.age += dt;
                f.text.rectTransform.position += Vector3.up * (170f * scale * dt);
                float pop = f.age < 0.12f ? Mathf.Lerp(0.5f, 1.15f, f.age / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((f.age - 0.12f) / 0.15f));
                f.text.rectTransform.localScale = new Vector3(pop, pop, 1f);
                f.text.alpha = Mathf.Clamp01(1.3f - f.age * 1.5f);
                if (f.age > 0.9f)
                {
                    Destroy(f.text.gameObject);
                    floaters.RemoveAt(i);
                }
            }
        }
    }
}
