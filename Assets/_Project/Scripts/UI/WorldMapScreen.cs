using System;
using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The world map, seen from above: the 25 worlds as islands strung along a winding road over a dark sea, each in
    /// its own colours once open (grey and locked before), with an animated pin standing over where the journey is
    /// ("World 3 · Level 24"). Swipe along it; tap an open world for its ten floors (replayed alone, for stars) and its
    /// battle arena, or carry on with the journey.
    /// </summary>
    public class WorldMapScreen : MonoBehaviour
    {
        public event Action Back, Continue;
        public event Action<int> LevelPicked, ArenaPicked;

        private UiScreen screen;
        private RectTransform content, pin, panel;
        private ScrollRect scroll;
        private TextMeshProUGUI pinLabel, coinsText;
        private readonly List<RectTransform> islands = new List<RectTransform>();
        private Vector2 pinBase;
        private int worldCount, levelsPerWorld;

        private const float Step = 380f;

        public bool IsOpen => screen.IsVisible;

        public static WorldMapScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("WorldMap", canvasRoot, out var root);
            var map = root.gameObject.AddComponent<WorldMapScreen>();
            map.screen = s;
            map.Build(root);
            return map;
        }

        private void Build(RectTransform root)
        {
            // The sea: deep blue fading darker to the edges, a few soft swells.
            UiFactory.Fill(UiFactory.Stretch("Sea", root), new Color(0.06f, 0.14f, 0.26f));
            var glow = UiFactory.Box("Glow", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2600f, 1400f));
            glow.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(glow, new Color(0.2f, 0.45f, 0.65f, 0.35f), UiSprites.Shadow, 0.3f).raycastTarget = false;

            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -150f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 0f), new Vector2(0f, 1f));
            content.pivot = new Vector2(0f, 0.5f);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.1f;
            scroll.scrollSensitivity = 60f;

            // Top bar: back, title, coins.
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -150f), Vector2.zero);
            UiFactory.Fill(bar, new Color(0.03f, 0.05f, 0.12f, 0.85f)).raycastTarget = false;
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(110f, 110f), () => Back?.Invoke(), 60f);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 110f), Loc.T("wmap.title"), 60f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(260f, 96f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), "0", 48f, Palette.UiGold, align: TextAlignmentOptions.Left);

            // Carry on with the journey: bottom right.
            UiFactory.MakeButton(root, Loc.T("wmap.continue"), Kind.Primary, new Vector2(1f, 0f), new Vector2(-40f, 170f), new Vector2(520f, 140f), () => Continue?.Invoke(), 52f);
        }

        /// <summary>Lays the map out for where the journey stands: worlds up to <paramref name="openWorld"/> are open.</summary>
        public void Show(int worlds, int perWorld, int openWorld, int pinWorld, string pinText, int coins)
        {
            worldCount = worlds;
            levelsPerWorld = perWorld;
            coinsText.text = coins.ToString();
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            islands.Clear();
            panel = null;
            float width = 300f + worlds * Step;
            content.sizeDelta = new Vector2(width, 0f);
            // The road first (under the islands): dots from island to island, gold where it is open.
            for (int w = 0; w + 1 < worlds; w++)
            {
                var a = IslandAt(w);
                var b = IslandAt(w + 1);
                for (int k = 1; k < 8; k++)
                {
                    var p = Vector2.Lerp(a, b, k / 8f) + new Vector2(0f, Mathf.Sin(k / 8f * Mathf.PI) * 30f);
                    var dot = UiFactory.Box("Dot", content, new Vector2(0f, 0.5f), p, new Vector2(18f, 18f));
                    dot.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(dot, w < openWorld ? new Color(1f, 0.85f, 0.35f, 0.9f) : new Color(0.6f, 0.65f, 0.75f, 0.35f), UiSprites.Circle).raycastTarget = false;
                }
            }
            for (int w = 0; w < worlds; w++) islands.Add(Island(w, w <= openWorld));
            // The pin over where the journey is.
            pin = UiFactory.Box("Pin", content, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(120f, 170f));
            pin.pivot = new Vector2(0.5f, 0f);
            var tip = UiFactory.Box("Tip", pin, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(52f, 52f));
            tip.pivot = new Vector2(0.5f, 0.5f);
            tip.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiFactory.Fill(tip, new Color(0.95f, 0.22f, 0.25f), UiSprites.Rounded, 6f).raycastTarget = false;
            var head = UiFactory.Box("Head", pin, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(104f, 104f));
            UiFactory.Fill(head, new Color(0.95f, 0.22f, 0.25f), UiSprites.Circle).raycastTarget = false;
            var hole = UiFactory.Box("Hole", head, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            hole.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(hole, Color.white, UiSprites.Circle).raycastTarget = false;
            var labelPill = UiFactory.Pill("Label", pin, new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(440f, 70f), new Color(0.05f, 0.05f, 0.12f, 0.92f));
            labelPill.pivot = new Vector2(0.5f, 0f);
            pinLabel = UiFactory.Text(labelPill, pinText, 32f, Palette.UiGold);
            pinBase = IslandAt(pinWorld) + new Vector2(0f, 95f);
            pin.anchoredPosition = pinBase;
            screen.Show();
            transform.SetAsLastSibling();
            // Open at the pin, a little left of the middle.
            Canvas.ForceUpdateCanvases();
            float view = ((RectTransform)scroll.viewport).rect.width;
            float x = IslandAt(pinWorld).x - view * 0.42f;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(x / Mathf.Max(1f, width - view));
        }

        public void Hide() => screen.Hide(true);

        private static Vector2 IslandAt(int w) => new Vector2(220f + w * Step, Mathf.Sin(w * 1.1f) * 120f - 70f);

        /// <summary>A world's island: soft shadow, a shore, a bright top in the world's colour, two hills, its number and name.</summary>
        private RectTransform Island(int w, bool open)
        {
            var theme = WorldTheme.ForWorld(w);
            var colour = open ? theme.accent : new Color(0.42f, 0.44f, 0.5f);
            var shore = open ? Color.Lerp(theme.accent, new Color(0.95f, 0.88f, 0.65f), 0.55f) : new Color(0.32f, 0.34f, 0.4f);
            var root = UiFactory.Box("World" + w, content, new Vector2(0f, 0.5f), IslandAt(w), new Vector2(280f, 220f));
            root.pivot = new Vector2(0.5f, 0.5f);
            var shadow = UiFactory.Box("Shadow", root, new Vector2(0.5f, 0.5f), new Vector2(10f, -22f), new Vector2(330f, 250f));
            shadow.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(shadow, new Color(0f, 0.02f, 0.06f, 0.55f), UiSprites.Shadow, 0.5f).raycastTarget = false;
            var sand = UiFactory.Box("Shore", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 210f));
            sand.pivot = new Vector2(0.5f, 0.5f);
            var sandImg = UiFactory.Fill(sand, shore, UiSprites.Circle);
            var top = UiFactory.Box("Land", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(230f, 165f));
            top.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(top, colour, UiSprites.CircleGradient).raycastTarget = false;
            foreach (var (pos, size) in new[] { (new Vector2(-50f, 30f), 70f), (new Vector2(45f, 40f), 90f) })
            {
                var hill = UiFactory.Box("Hill", root, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size * 0.75f));
                hill.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(hill, Color.Lerp(colour, Color.white, open ? 0.35f : 0.15f), UiSprites.CircleGradient).raycastTarget = false;
            }
            var num = UiFactory.TextBox("Number", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(200f, 100f), (w + 1).ToString(), 72f, open ? Color.white : new Color(1f, 1f, 1f, 0.5f), title: true);
            num.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            num.raycastTarget = false;
            var name = UiFactory.Pill("Name", root, new Vector2(0.5f, 0f), new Vector2(0f, -70f), new Vector2(320f, 60f), new Color(0.03f, 0.04f, 0.1f, open ? 0.85f : 0.55f));
            name.pivot = new Vector2(0.5f, 0.5f);
            var nameText = UiFactory.Text(name, LevelCatalog.WorldName(w * levelsPerWorld), 26f, open ? Palette.UiText : new Color(1f, 1f, 1f, 0.45f));
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 16f;
            nameText.fontSizeMax = 26f;
            if (!open)
            {
                // A padlock: a ring for the shackle over a rounded body.
                var lockRoot = UiFactory.Box("Lock", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(70f, 80f));
                lockRoot.pivot = new Vector2(0.5f, 0.5f);
                var shackle = UiFactory.Box("Shackle", lockRoot, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(46f, 46f));
                shackle.pivot = new Vector2(0.5f, 1f);
                UiFactory.Fill(shackle, new Color(0.85f, 0.87f, 0.92f), UiSprites.Ring, 0.3f).raycastTarget = false;
                var body = UiFactory.Box("Body", lockRoot, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(64f, 48f));
                body.pivot = new Vector2(0.5f, 0f);
                UiFactory.Fill(body, new Color(0.85f, 0.87f, 0.92f), UiSprites.Rounded, 4f).raycastTarget = false;
                return root;
            }
            var button = sand.gameObject.AddComponent<Button>();
            button.targetGraphic = sandImg;
            int world = w;
            button.onClick.AddListener(() => { Audio.AudioManager.PlaySfx(Audio.Sfx.Click, 0.8f, 1.1f); OpenWorld(world); });
            return root;
        }

        /// <summary>An open world's card: its ten floors (stars won; the ones not reached yet dim), its arena, the journey.</summary>
        private void OpenWorld(int w)
        {
            if (panel != null) Destroy(panel.gameObject);
            var root = (RectTransform)transform;
            panel = UiFactory.Rect("WorldCard", root, Vector2.zero, Vector2.one);
            UiFactory.Fill(panel, new Color(0f, 0f, 0.05f, 0.55f)).raycastTarget = true;
            var card = UiFactory.Card("Card", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1180f, 780f));
            card.pivot = new Vector2(0.5f, 0.5f);
            var theme = WorldTheme.ForWorld(w);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 90f), LevelCatalog.WorldName(w * levelsPerWorld), 52f, theme.accent, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 1f);
            UiFactory.MakeButton(card, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(96f, 96f), () => { Destroy(panel.gameObject); panel = null; }, 50f);
            int unlocked = SaveData.UnlockedLevel;
            for (int i = 0; i < levelsPerWorld; i++)
            {
                int level = w * levelsPerWorld + i;
                int col = i % 5, row = i / 5;
                var pos = new Vector2(-440f + col * 220f, -170f - row * 210f);
                bool reached = level <= unlocked;
                var b = UiFactory.MakeButton(card, (level + 1).ToString(), reached ? Kind.Secondary : Kind.Icon, new Vector2(0.5f, 1f), pos, new Vector2(190f, 150f), () =>
                {
                    if (!reached) return;
                    Destroy(panel.gameObject);
                    panel = null;
                    LevelPicked?.Invoke(level);
                }, 52f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 1f);
                if (!reached) b.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
                int stars = Progress.Stars(level);
                for (int s = 0; s < 3; s++)
                {
                    var star = UiFactory.Box("Star", b.transform, new Vector2(0.5f, 0f), new Vector2((s - 1) * 40f, 18f), new Vector2(36f, 36f));
                    star.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(star, s < stars ? Palette.UiGold : new Color(1f, 1f, 1f, 0.18f), UiSprites.Star).raycastTarget = false;
                }
            }
            // The world's arena: open once its tenth floor is won.
            bool arenaOpen = unlocked > (w + 1) * levelsPerWorld - 1;
            var arena = UiFactory.MakeButton(card, Loc.T("arena.button"), arenaOpen ? Kind.Gold : Kind.Icon, new Vector2(0.5f, 0f), new Vector2(-280f, 40f), new Vector2(460f, 130f), () =>
            {
                if (!arenaOpen) return;
                Destroy(panel.gameObject);
                panel = null;
                ArenaPicked?.Invoke(w);
            }, 40f);
            if (!arenaOpen) arena.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
            UiFactory.MakeButton(card, Loc.T("wmap.continue"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(280f, 40f), new Vector2(460f, 130f), () => Continue?.Invoke(), 40f);
        }

        private void Update()
        {
            if (pin == null || !screen.IsVisible) return;
            // The pin bobs and its head pulses gently.
            float t = Time.unscaledTime;
            pin.anchoredPosition = pinBase + new Vector2(0f, Mathf.Abs(Mathf.Sin(t * 3f)) * 26f);
            pin.localScale = Vector3.one * (1f + Mathf.Sin(t * 6f) * 0.03f);
        }
    }
}
