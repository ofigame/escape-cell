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
        Graphics,
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
        public event Action ContinueCoinsPressed;
        public event Action DoublePressed;
        public event Action GaragePressed;
        public event Action DailyBonusPressed;
        public event Action ShopPressed;
        public event Action DailyPressed;
        /// <summary>A HUD tool button was tapped (slot 0 = left, 1 = right).</summary>
        public event Action<int> ToolPressed;
        /// <summary>PLAY on the before-level card: level, start with a shield, take an extra rescue.</summary>
        public event Action<int, List<Boost>> PrelevelPlay;
        public event Action PrelevelClosed;

        /// <summary>Everything the result card shows.</summary>
        public struct ResultInfo
        {
            public bool won, bonusRound, hasNext, bonusAvailable, canContinue, canBuyContinue, canDouble;
            public int continuePrice;
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
        private Button menuDaily, menuBonus;
        private TextMeshProUGUI menuBonusLabel;

        // Before-level card
        private UiScreen prelevel;
        private TextMeshProUGUI preWorld, preTitle, preMission;
        private readonly Image[] preStars = new Image[3];
        private readonly List<BoostView> preBoosts = new List<BoostView>();
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
        private TextMeshProUGUI preBoostsLabel;
        private Image rescueFill, hoverFace;
        private Image warnLeft, warnRight;
        private bool warningActive;

        // Intro banner
        private RectTransform intro;
        private CanvasGroup introGroup;
        private TextMeshProUGUI introTitle, introText;
        private float introTime = 99f, introHold = 1.7f;

        // Pause & result
        private UiScreen pause;
        private UiScreen result;
        private TextMeshProUGUI resultTitle, resultSub, resultReward;
        private Button resultNext, resultRetry, resultMap, resultMenu, resultBonus, resultContinue, resultContinueCoins, resultDouble;
        private TextMeshProUGUI resultContinueCoinsLabel;
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
        public GuideScreen Guide { get; private set; }
        public BriefingScreen Briefing { get; private set; }
        public LanguagePicker Languages { get; private set; }
        public SkillBadges Skills { get; private set; }
        public ObjectiveArrows Arrows { get; private set; }
        public BipTip Bip { get; private set; }
        public DailyBonusScreen DailyBonus { get; private set; }

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
            BuildCallout(root);
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
            DailyBonus = DailyBonusScreen.Create(root);
            Guide = GuideScreen.Create(root);
            Briefing = BriefingScreen.Create(root);
            Languages = LanguagePicker.Create(root);
            Guide.BackPressed += Guide.Hide;
            HelpButton(Shop.transform, "shop");
            HelpButton(Garage.transform, "garage");
            HelpButton(Map.transform, "map");
            Shop.BackPressed += () => MenuPressed?.Invoke();
            Shop.PaintPressed += () => GaragePressed?.Invoke();
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

            // Logo up top; the robot stands on its lit pedestal in the middle (the lobby stage behind the menu).
            MenuArt.Logo(t, Top, new Vector2(0f, -150f));

            // Today's chest: a glowing chest button under the coins, only while it is waiting.
            menuDaily = MenuArt.DockButton(t, Loc.T("daily.chest"), MenuArt.Icon.Chest, Palette.UiGold, TopLeft, new Vector2(110f, -230f), 120f, () => DailyPressed?.Invoke());
            ((RectTransform)menuDaily.transform).pivot = new Vector2(0.5f, 0.5f);
            var ping = UiFactory.Box("Ping", menuDaily.transform.Find("Orb"), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(44f, 44f));
            ping.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(ping, Palette.UiRed, UiSprites.Circle).raycastTarget = false;
            UiFactory.Text(ping, "!", 32f, Color.white);
            menuDaily.gameObject.AddComponent<Pulse>();

            // The level card: a frosted glass panel with the world, the level, its mission and the big PLAY button.
            var card = UiFactory.Box("LevelCard", t, Bottom, new Vector2(0f, Ads.BannerReserve + 236f), new Vector2(940f, 420f));
            UiFactory.Fill(UiFactory.Rect("Shadow", card, Vector2.zero, Vector2.one, new Vector2(-30f, -44f), new Vector2(30f, 16f)),
                new Color(0.03f, 0.02f, 0.1f, 0.55f), UiSprites.Shadow, 0.6f).raycastTarget = false;
            UiFactory.Fill(card, new Color(0.11f, 0.1f, 0.26f, 0.78f), UiSprites.Rounded, 0.8f);
            var sheen = UiFactory.Rect("Sheen", card, new Vector2(0f, 0.6f), Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, -10f));
            UiFactory.Fill(sheen, new Color(1f, 1f, 1f, 0.06f), UiSprites.Rounded, 1f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", card), new Color(0.75f, 0.85f, 1f, 0.35f), UiSprites.Ring, 0.8f).raycastTarget = false;
            menuWorld = UiFactory.TextBox("World", card, Top, new Vector2(0f, -26f), new Vector2(860f, 50f), "", 30f, Palette.UiCyan);
            menuWorld.characterSpacing = 5f;
            menuLevel = UiFactory.TextBox("Level", card, Top, new Vector2(0f, -68f), new Vector2(860f, 100f), "", 76f, Palette.UiText, title: true);
            menuMission = UiFactory.TextBox("Mission", card, Top, new Vector2(0f, -170f), new Vector2(860f, 52f), "", 34f,
                new Color(0.85f, 0.86f, 1f, 0.8f), FontStyles.Normal);

            var play = UiFactory.MakeButton(card, Loc.T("menu.play"), Kind.Primary, Bottom, new Vector2(-130f, 30f), new Vector2(580f, 150f), () => PlayPressed?.Invoke(), 84f);
            play.gameObject.AddComponent<Pulse>();
            // The daily bonus games, next to PLAY: plays left today, glowing while there are some.
            menuBonus = UiFactory.MakeButton(card, "", Kind.Gold, Bottom, new Vector2(320f, 30f), new Vector2(240f, 150f), () => DailyBonusPressed?.Invoke(), 34f);
            menuBonusLabel = menuBonus.GetComponentInChildren<TextMeshProUGUI>();
            menuBonus.gameObject.AddComponent<Pulse>();

            // The dock: garage, shop, map and the guide, as glossy orbs on a glass bar.
            var dock = UiFactory.Box("Dock", t, Bottom, new Vector2(0f, Ads.BannerReserve + 14f), new Vector2(1000f, 206f));
            UiFactory.Fill(dock, new Color(0.08f, 0.07f, 0.2f, 0.72f), UiSprites.Rounded, 0.7f);
            UiFactory.Fill(UiFactory.Stretch("Rim", dock), new Color(0.75f, 0.85f, 1f, 0.25f), UiSprites.Ring, 0.7f).raycastTarget = false;
            var items = new (string label, MenuArt.Icon icon, Color color, Action click)[]
            {
                (Loc.T("btn.garage"), MenuArt.Icon.Garage, new Color(0.12f, 0.71f, 0.64f), () => GaragePressed?.Invoke()),
                (Loc.T("btn.shop"), MenuArt.Icon.Shop, new Color(0.94f, 0.54f, 0.16f), () => ShopPressed?.Invoke()),
                (Loc.T("btn.map"), MenuArt.Icon.Map, new Color(0.42f, 0.36f, 0.88f), () => PlayPressed?.Invoke()),
                (Loc.T("btn.guide"), MenuArt.Icon.Guide, new Color(0.85f, 0.35f, 0.6f), () => Guide.Show()),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                MenuArt.DockButton(dock, it.label, it.icon, it.color, new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 240f, 0f), 120f, it.click);
            }
        }

        /// <summary>A "?" next to a screen's back button that opens the guide at that screen's topic.</summary>
        private void HelpButton(Transform screenRoot, string topic)
        {
            var bar = screenRoot.Find("TopBar");
            if (bar == null) return;
            bool map = topic == "map";
            UiFactory.MakeButton(bar, "?", Kind.Icon, map ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f), map ? new Vector2(-300f, 0f) : new Vector2(176f, 0f),
                map ? new Vector2(90f, 90f) : new Vector2(104f, 104f), () => Guide.Show(topic), 56f);
        }

        /// <summary>The menu's daily bonus button: "BONUS 2/3", dim once the day's plays are used.</summary>
        public void RefreshDailyBonus()
        {
            if (menuBonus == null) return;
            int left = Data.DailyBonus.Left;
            menuBonusLabel.text = Loc.F("daily.menu", left, Data.DailyBonus.FreePlays);
            var pulse = menuBonus.GetComponent<Pulse>();
            if (pulse != null) pulse.enabled = left > 0 || Data.DailyBonus.CanWatchAd;
            if (left == 0 && !Data.DailyBonus.CanWatchAd) menuBonus.transform.localScale = Vector3.one;
            ((Image)menuBonus.targetGraphic).color = left > 0 || Data.DailyBonus.CanWatchAd ? Palette.UiGold : new Color(0.62f, 0.62f, 1f, 0.25f);
        }

        /// <summary>True while the main menu is up (its 3D lobby is showing).</summary>
        public bool MenuVisible => menu.IsVisible;

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
            RefreshDailyBonus();
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

            preBoostsLabel = UiFactory.TextBox("Boosts", card, Top, new Vector2(0f, -420f), new Vector2(780f, 56f), Loc.T("pre.boosts"), 36f, Palette.UiText);
            foreach (var b in new[] { Boost.StartShield, Boost.StartHammer, Boost.CoinMagnet, Boost.DoubleCoins }) preBoosts.Add(BoostTile(card, new Vector2(0f, -500f), b));

            UiFactory.MakeButton(card, Loc.T("menu.play"), Kind.Primary, Bottom, new Vector2(0f, 46f), new Vector2(620f, 160f),
                () => PrelevelPlay?.Invoke(preLevel, preBoosts.FindAll(v => v.on).ConvertAll(v => v.boost)), 84f);
        }

        private BoostView BoostTile(Transform card, Vector2 position, Boost boost)
        {
            var tile = UiFactory.Box(boost.ToString(), card, Top, position, new Vector2(250f, 220f));
            tile.pivot = new Vector2(0.5f, 1f);
            var view = new BoostView { boost = boost, bg = UiFactory.Fill(tile, new Color(0.12f, 0.1f, 0.26f, 0.9f), UiSprites.Rounded, 1.2f) };
            view.ring = UiFactory.Fill(UiFactory.Stretch("Ring", tile), Palette.UiGold, UiSprites.Ring, 1.2f);
            view.ring.raycastTarget = false;
            var name = UiFactory.TextBox("Name", tile, Top, new Vector2(0f, -22f), new Vector2(230f, 110f), Loc.T("shop." + boost), 32f, Palette.UiText);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.enableAutoSizing = true;
            name.fontSizeMin = 22f;
            name.fontSizeMax = 32f;
            view.info = UiFactory.TextBox("Info", tile, Bottom, new Vector2(0f, 20f), new Vector2(230f, 60f), "", 36f, Palette.UiGold);
            // "Suggested" ribbon over the tile that fits this level best.
            var tag = UiFactory.Pill("Tag", tile, Top, new Vector2(0f, 26f), new Vector2(210f, 50f), Palette.UiGold);
            UiFactory.TextBox("Text", tag, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 46f), Loc.T("pre.suggested"), 24f, UiFactory.TextDark)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            view.tag = tag.gameObject;
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = view.bg;
            button.onClick.AddListener(() => ToggleBoost(view));
            tile.gameObject.AddComponent<ButtonPress>();
            view.root = tile.gameObject;
            return view;
        }

        private void ToggleBoost(BoostView view)
        {
            bool on = view.on;
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
            view.on = !on;
            RefreshBoosts();
        }

        private void RefreshBoosts()
        {
            foreach (var v in preBoosts)
            {
                int owned = Data.Shop.Owned(v.boost);
                v.ring.gameObject.SetActive(v.on);
                v.info.text = owned > 0 || v.on ? Loc.F("shop.owned", owned) : Data.Shop.Price(v.boost).ToString();
                v.info.color = owned > 0 || v.on ? Palette.UiCyan : Palette.UiGold;
            }
        }

        /// <summary>
        /// The card before a level: mission, best stars and boosts to take along. The hammer only shows on monster levels,
        /// and the boost that suits the level best wears a "suggested" ribbon.
        /// </summary>
        public void ShowPrelevel(int levelIndex, string world, string mission, int bestStars, bool monsterLevel, Boost suggested, bool boostsAllowed = true)
        {
            preLevel = levelIndex;
            preTitle.text = Loc.F("level", levelIndex + 1);
            preWorld.text = world;
            preMission.text = mission;
            for (int i = 0; i < 3; i++) preStars[i].color = i < bestStars ? Palette.UiGold : new Color(1f, 1f, 1f, 0.15f);
            var shown = new List<BoostView>();
            foreach (var v in preBoosts)
            {
                v.on = false;
                // Three tiles: the hammer stands in for double coins on monster levels; none at all on boss and marathon levels.
                bool show = boostsAllowed && (v.boost == Boost.StartHammer ? monsterLevel : v.boost != Boost.DoubleCoins || !monsterLevel);
                v.root.SetActive(show);
                v.tag.SetActive(v.boost == suggested);
                if (show) shown.Add(v);
            }
            preBoostsLabel.text = Loc.T(boostsAllowed ? "pre.boosts" : "pre.noBoosts");
            for (int i = 0; i < shown.Count; i++)
                ((RectTransform)shown[i].root.transform).anchoredPosition = new Vector2((i - (shown.Count - 1) * 0.5f) * 270f, -500f);
            RefreshBoosts();
            prelevel.Show();
            prelevel.transform.SetAsLastSibling();
        }

        public void HidePrelevel() => prelevel.Hide(true);

        private class BoostView
        {
            public Boost boost;
            public bool on;
            public GameObject root, tag;
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
            var card = UiFactory.Card("Card", t, Middle, Vector2.zero, new Vector2(860f, 1450f));
            UiFactory.TextBox("Title", card, Top, new Vector2(0f, -50f), new Vector2(800f, 120f), Loc.T("settings.title"), 90f, Palette.UiText, title: true);

            var rows = new[]
            {
                (SettingKind.Sound, "settings.sound"),
                (SettingKind.Music, "settings.music"),
                (SettingKind.Vibration, "settings.vibration"),
                (SettingKind.Language, "settings.language"),
                (SettingKind.Camera, "settings.camera"),
                (SettingKind.Graphics, "settings.graphics"),
                (SettingKind.TestMode, "settings.test"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var (kind, label) = rows[i];
                float y = -210f - i * 140f;
                var labelText = UiFactory.TextBox("Label", card, TopLeft, new Vector2(70f, y), new Vector2(380f, 110f), Loc.T(label), 52f,
                    Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.Left);
                // Long labels and values (the "auto" graphics tier) shrink to fit instead of running into the button.
                labelText.enableAutoSizing = true;
                labelText.fontSizeMin = 30f;
                labelText.fontSizeMax = 52f;
                var button = UiFactory.MakeButton(card, "", Kind.Secondary, TopRight, new Vector2(-60f, y), new Vector2(330f, 110f),
                    () => SettingToggled?.Invoke(kind), 46f);
                settingValues[kind] = button.GetComponentInChildren<TextMeshProUGUI>();
                settingValues[kind].enableAutoSizing = true;
                settingValues[kind].fontSizeMin = 26f;
                settingValues[kind].fontSizeMax = 46f;
                settingValues[kind].margin = new Vector4(18f, 0f, 18f, 0f);
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
            if (!Audio.Haptics.Available)
            {
                // Most tablets have no vibration motor: say so instead of a switch that seems to do nothing.
                settingValues[SettingKind.Vibration].text = Loc.T("settings.noMotor");
                settingValues[SettingKind.Vibration].color = new Color(1f, 1f, 1f, 0.5f);
            }
            settingValues[SettingKind.Language].text = Loc.T("lang.name");
            settingValues[SettingKind.Camera].text = Loc.T(SaveData.PerspectiveView ? "view.3d" : "view.iso");
            var gfx = Loc.T("gfx." + GraphicsQuality.Current);
            settingValues[SettingKind.Graphics].text = GraphicsQuality.Choice.HasValue ? gfx : Loc.F("gfx.auto", gfx);
            settingValues[SettingKind.Graphics].color = Palette.UiCyan;
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
            Skills = SkillBadges.Create(t);
            Arrows = ObjectiveArrows.Create(t);
            Bip = BipTip.Create(t);

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
            Skills.HideAll();
            Arrows.Clear();
            Bip.Hide();
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
            intro = UiFactory.Pill("Intro", root, Middle, new Vector2(0f, 430f), new Vector2(960f, 250f), new Color(0.12f, 0.11f, 0.28f, 0.94f));
            UiFactory.Fill(UiFactory.Stretch("Rim", intro), new Color(0.62f, 0.92f, 1f, 0.35f), UiSprites.Ring, 1.2f).raycastTarget = false;
            introGroup = intro.gameObject.AddComponent<CanvasGroup>();
            introGroup.blocksRaycasts = false;
            introTitle = UiFactory.TextBox("Title", intro, Top, new Vector2(0f, -22f), new Vector2(900f, 60f), "", 40f, Palette.UiCyan);
            introTitle.characterSpacing = 8f;
            introText = UiFactory.TextBox("Text", intro, Bottom, new Vector2(0f, 22f), new Vector2(910f, 150f), "", 60f, Color.white, title: true);
            // Long messages wrap onto two lines instead of shrinking to an unreadable size.
            introText.textWrappingMode = TextWrappingModes.Normal;
            introText.fontSizeMin = 34f;
            intro.gameObject.SetActive(false);
        }

        // ---------- Goal callout ----------

        // A speech bubble pinned over the level's goal (the princess, the monster, WARDEN) while the camera shows it.
        private RectTransform callout;
        private TextMeshProUGUI calloutText;
        private Vector3 calloutWorld;
        private Camera calloutCam;
        private float calloutLeft, calloutAge;

        private void BuildCallout(Transform root)
        {
            callout = UiFactory.Pill("Callout", root, Middle, Vector2.zero, new Vector2(600f, 120f), new Color(0.1f, 0.08f, 0.22f, 0.95f));
            callout.pivot = new Vector2(0.5f, 0f);
            UiFactory.Fill(UiFactory.Stretch("Rim", callout), Palette.UiGold, UiSprites.Ring, 1.2f).raycastTarget = false;
            var tail = UiFactory.Box("Tail", callout, new Vector2(0.5f, 0f), new Vector2(0f, -14f), new Vector2(34f, 34f));
            tail.pivot = new Vector2(0.5f, 0.5f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiFactory.Fill(tail, Palette.UiGold).raycastTarget = false;
            calloutText = UiFactory.Text(callout, "", 52f, Palette.UiGold, title: true);
            callout.gameObject.SetActive(false);
        }

        public void ShowCallout(string text, Vector3 world, Camera cam, float seconds)
        {
            calloutText.text = text;
            calloutWorld = world;
            calloutCam = cam;
            calloutLeft = seconds;
            calloutAge = 0f;
            callout.gameObject.SetActive(true);
            callout.SetAsLastSibling();
        }

        private void UpdateCallout(float dt)
        {
            if (!callout.gameObject.activeSelf || calloutCam == null) return;
            calloutLeft -= dt;
            calloutAge += dt;
            if (calloutLeft <= 0f) { callout.gameObject.SetActive(false); return; }
            var sp = calloutCam.WorldToScreenPoint(calloutWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)callout.parent, sp, null, out var local);
            callout.anchoredPosition = local;
            float pop = calloutAge < 0.25f ? Mathf.Lerp(0.4f, 1f, calloutAge / 0.25f) + Mathf.Sin(calloutAge / 0.25f * Mathf.PI) * 0.15f : 1f;
            float fade = Mathf.Clamp01(calloutLeft / 0.3f);
            callout.localScale = Vector3.one * pop * (0.9f + 0.1f * fade);
        }

        public void ShowIntro(string title, string text)
        {
            introTitle.text = title;
            introText.text = text;
            introTime = 0f;
            // Longer messages stay up longer.
            introHold = Mathf.Clamp(1.7f + (text?.Length ?? 0) / 30f, 1.7f, 4f);
            intro.gameObject.SetActive(true);
            // Always in front: menus built after the banner (the logo) must not cover it.
            intro.SetAsLastSibling();
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
            // Or carry on for coins, no ad needed: the coins saved up finally pay off when a run goes wrong.
            resultContinueCoins = UiFactory.MakeButton(card, "", Kind.Gold, Top, new Vector2(0f, -795f), new Vector2(300f, 150f), () => ContinueCoinsPressed?.Invoke(), 40f);
            resultContinueCoinsLabel = resultContinueCoins.GetComponentInChildren<TextMeshProUGUI>();
            foreach (var l in new[] { resultContinueCoinsLabel, resultContinue.GetComponentInChildren<TextMeshProUGUI>() })
            {
                l.enableAutoSizing = true;
                l.fontSizeMin = 28f;
                l.fontSizeMax = 50f;
                l.margin = new Vector4(16f, 0f, 16f, 0f);
            }
            resultDouble = UiFactory.MakeButton(card, Loc.T("btn.double"), Kind.Gold, Top, new Vector2(0f, -965f), new Vector2(620f, 135f), () => DoublePressed?.Invoke(), 54f);
            resultMap = UiFactory.MakeButton(card, Loc.T("btn.map"), Kind.Secondary, Top, new Vector2(-160f, -1120f), new Vector2(300f, 125f), () => MapPressed?.Invoke(), 54f);
            resultMenu = UiFactory.MakeButton(card, Loc.T("btn.menu"), Kind.Secondary, Top, new Vector2(160f, -1120f), new Vector2(300f, 125f), () => MenuPressed?.Invoke(), 54f);
        }

        public void ShowResult(ResultInfo info)
        {
            pause.Hide(true);
            intro.gameObject.SetActive(false); // the result card has its own title
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
            bool contAd = !ended && info.canContinue;
            bool contCoins = !ended && info.canBuyContinue;
            bool cont = contAd || contCoins;
            bool retryTop = !bonus && !next && !cont;
            resultBonus.gameObject.SetActive(bonus);
            resultNext.gameObject.SetActive(next);
            resultContinue.gameObject.SetActive(contAd);
            resultContinueCoins.gameObject.SetActive(contCoins);
            // Both ways to continue share the top slot side by side; one alone takes all of it.
            ((RectTransform)resultContinue.transform).sizeDelta = new Vector2(contCoins ? 300f : 620f, 150f);
            ((RectTransform)resultContinue.transform).anchoredPosition = new Vector2(contCoins ? -160f : 0f, -795f);
            ((RectTransform)resultContinueCoins.transform).sizeDelta = new Vector2(contAd ? 300f : 620f, 150f);
            ((RectTransform)resultContinueCoins.transform).anchoredPosition = new Vector2(contAd ? 160f : 0f, -795f);
            resultContinueCoinsLabel.text = Loc.F("btn.continueCoins", info.continuePrice);
            resultContinue.GetComponentInChildren<TextMeshProUGUI>().text = Loc.T(contCoins ? "btn.continueAdShort" : "btn.continue");
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
            DailyBonus.Hide();
            Guide.Hide();
            Briefing.Hide();
            prelevel.Hide(true);
            settings.Hide(true);
            noLives.Hide(true);
            hud.Hide(true);
            pause.Hide(true);
            result.Hide(true);
            intro.gameObject.SetActive(false);
            warningActive = false;
            if (callout != null) callout.gameObject.SetActive(false);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            UpdateJuice(dt);
            UpdateCallout(dt);
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
                float a = introTime < 0.25f ? introTime / 0.25f : introTime > introHold ? 1f - (introTime - introHold) / 0.3f : 1f;
                introGroup.alpha = Mathf.Clamp01(a);
                float s = introTime < 0.25f ? Mathf.Lerp(1.25f, 1f, introTime / 0.25f) : 1f;
                intro.localScale = new Vector3(s, s, 1f);
                if (introTime > introHold + 0.3f) intro.gameObject.SetActive(false);
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
