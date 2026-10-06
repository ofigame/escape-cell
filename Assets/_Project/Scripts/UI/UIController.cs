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
        Camera
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

        /// <summary>Everything the result card shows.</summary>
        public struct ResultInfo
        {
            public bool won, bonusRound, hasNext, bonusAvailable;
            public string subtitle, note;
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
        private TextMeshProUGUI menuLevel, menuWorld, menuMission, menuCoins, menuLives;

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
        private RectTransform missionPill, bonusPill;
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
        private Button resultNext, resultRetry, resultMap, resultMenu, resultBonus;
        private RectTransform resultStarRow;
        private readonly Image[] resultStars = new Image[3];
        private TextMeshProUGUI resultNote, resultMeterText;
        private Image resultMeterFill;
        private int starsEarned;
        private float starTime = 99f;
        private float meterShown, meterTarget;

        public MapScreen Map { get; private set; }

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
            BuildPause(root);
            BuildResult(root);
            BuildSettings(root);
            BuildNoLives(root);
            BuildBanner(root);
        }

        // ---------- Menu ----------

        private void BuildMenu(Transform root)
        {
            menu = UiScreen.Create("Menu", root, out var t);
            UiFactory.Dim(t, new Color(0.08f, 0.06f, 0.2f, 0.25f)).raycastTarget = false;

            // Top bar
            var coins = UiFactory.Pill("Coins", t, TopLeft, new Vector2(40f, -40f), new Vector2(300f, 100f), UiFactory.PillColor);
            CoinIcon(coins, new Vector2(56f, 0f));
            menuCoins = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(190f, 90f),
                "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            var lives = UiFactory.Pill("Lives", t, TopLeft, new Vector2(360f, -40f), new Vector2(300f, 100f), UiFactory.PillColor);
            HeartIcon(lives, new Vector2(58f, 0f), 56f);
            menuLives = UiFactory.TextBox("Value", lives, new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(190f, 90f),
                "", 44f, Palette.UiText, align: TextAlignmentOptions.Left);

            var gear = UiFactory.MakeButton(t, "", Kind.Icon, TopRight, new Vector2(-40f, -40f), new Vector2(110f, 110f), ShowSettings);
            SettingsIcon(gear.transform);

            // Logo
            var logo = UiFactory.TextBox("Logo", t, Top, new Vector2(0f, -190f), new Vector2(1000f, 420f), "ESCAPE\nCELL", 200f, Color.white, title: true);
            logo.lineSpacing = -22f;
            logo.enableVertexGradient = true;
            logo.colorGradient = new VertexGradient(Color.white, Color.white, Palette.UiCyan, Palette.UiCyan);

            var tag = UiFactory.Pill("Tag", t, Top, new Vector2(0f, -630f), new Vector2(420f, 78f), new Color(1f, 0.42f, 0.5f, 0.9f));
            UiFactory.Text(tag, Loc.T("menu.tag"), 40f, Color.white).characterSpacing = 8f;

            // Bottom card
            var card = UiFactory.Card("LevelCard", t, Bottom, new Vector2(0f, Ads.BannerReserve + 60f), new Vector2(900f, 500f));
            menuWorld = UiFactory.TextBox("World", card, Top, new Vector2(0f, -36f), new Vector2(820f, 60f), "", 34f, Palette.UiCyan);
            menuWorld.characterSpacing = 4f;
            menuLevel = UiFactory.TextBox("Level", card, Top, new Vector2(0f, -86f), new Vector2(820f, 120f), "", 88f, Palette.UiText, title: true);
            menuMission = UiFactory.TextBox("Mission", card, Top, new Vector2(0f, -206f), new Vector2(820f, 60f), "", 40f,
                new Color(0.85f, 0.86f, 1f, 0.8f), FontStyles.Normal);

            UiFactory.MakeButton(card, Loc.T("menu.play"), Kind.Primary, Bottom, new Vector2(0f, 40f), new Vector2(620f, 160f), () => PlayPressed?.Invoke(), 84f);
            UiFactory.TextBox("Hint", t, Bottom, new Vector2(0f, Ads.BannerReserve + 8f), new Vector2(1000f, 46f), Loc.T("menu.hint"), 30f,
                new Color(1f, 1f, 1f, 0.6f), FontStyles.Normal);
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
            RefreshLives();
        }

        public void ShowMap(int unlocked, int coins, int focusLevel, int animateFrom = -1)
        {
            HideAll();
            Map.Show(unlocked, coins, focusLevel, animateFrom);
            SetBanner(true);
            RefreshLives();
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
            var card = UiFactory.Card("Card", t, Middle, Vector2.zero, new Vector2(860f, 1170f));
            UiFactory.TextBox("Title", card, Top, new Vector2(0f, -50f), new Vector2(800f, 120f), Loc.T("settings.title"), 90f, Palette.UiText, title: true);

            var rows = new[]
            {
                (SettingKind.Sound, "settings.sound"),
                (SettingKind.Music, "settings.music"),
                (SettingKind.Vibration, "settings.vibration"),
                (SettingKind.Language, "settings.language"),
                (SettingKind.Camera, "settings.camera"),
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
            var card = UiFactory.Card("Card", t, Middle, Vector2.zero, new Vector2(860f, 1200f));

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

            resultNote = UiFactory.TextBox("Note", card, Top, new Vector2(0f, -565f), new Vector2(800f, 56f), "", 36f, Palette.UiGold, FontStyles.Bold);

            // Bonus meter: stars fill it, a full meter unlocks a bonus round.
            UiFactory.Bar(card, Top, new Vector2(-60f, -640f), new Vector2(560f, 30f), Palette.UiGold, out resultMeterFill);
            resultMeterText = UiFactory.TextBox("Meter", card, Top, new Vector2(335f, -628f), new Vector2(200f, 54f), "", 32f,
                new Color(1f, 1f, 1f, 0.8f), FontStyles.Bold, align: TextAlignmentOptions.Left);

            resultNext = UiFactory.MakeButton(card, Loc.T("btn.next"), Kind.Primary, Top, new Vector2(0f, -710f), new Vector2(620f, 150f), () => NextPressed?.Invoke(), 70f);
            resultRetry = UiFactory.MakeButton(card, Loc.T("btn.retry"), Kind.Primary, Top, new Vector2(0f, -710f), new Vector2(620f, 150f), () => RetryPressed?.Invoke(), 70f);
            resultBonus = UiFactory.MakeButton(card, Loc.T("btn.bonus"), Kind.Gold, Top, new Vector2(0f, -710f), new Vector2(620f, 150f), () => BonusPressed?.Invoke(), 76f);
            resultBonus.gameObject.AddComponent<Pulse>();
            resultMap = UiFactory.MakeButton(card, Loc.T("btn.map"), Kind.Secondary, Top, new Vector2(0f, -880f), new Vector2(620f, 135f), () => MapPressed?.Invoke(), 60f);
            resultMenu = UiFactory.MakeButton(card, Loc.T("btn.menu"), Kind.Secondary, Top, new Vector2(0f, -1035f), new Vector2(620f, 135f), () => MenuPressed?.Invoke(), 60f);
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

            // Win: NEXT (via the map) or BONUS when one is waiting, MAP, MENU.  Lose: RETRY, MAP, MENU.
            bool bonus = info.bonusAvailable && (info.won || info.bonusRound);
            bool next = !bonus && (info.won || info.bonusRound) && info.hasNext;
            resultBonus.gameObject.SetActive(bonus);
            resultNext.gameObject.SetActive(next);
            resultRetry.gameObject.SetActive(!bonus && !next);
        }

        /// <summary>Stars pop in one by one with a chime; the bonus meter fills up behind them.</summary>
        private void UpdateResultJuice(float dt)
        {
            if (!result.IsVisible) return;

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
