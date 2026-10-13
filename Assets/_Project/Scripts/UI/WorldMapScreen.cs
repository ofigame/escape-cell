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
    /// The world map: the whole journey from floor 1 to floor 250 seen from straight above, drawn from the real roads and
    /// floors (JourneyMapArt) — each world a strip of its land, the roads winding from floor to floor, the tunnel and the
    /// arena at its end; worlds not reached yet are grey. An animated pin stands where the journey is. Swipe up and down;
    /// tap a floor's number to replay it alone, a world's name or its arena badge for its arena, or carry on.
    /// </summary>
    public class WorldMapScreen : MonoBehaviour
    {
        public event Action Back, Continue;
        public event Action<int> LevelPicked, ArenaPicked;

        private UiScreen screen;
        private RectTransform content, pin;
        private ScrollRect scroll;
        private TextMeshProUGUI coinsText;
        private Vector2 pinBase;
        private int openWorld;
        private readonly List<(RectTransform rect, RawImage image, JourneyMapArt.WorldPlan plan)> blocks = new List<(RectTransform, RawImage, JourneyMapArt.WorldPlan)>();

        /// <summary>Canvas units per map tile.</summary>
        private const float S = 4.5f;

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
            UiFactory.Fill(UiFactory.Stretch("Back", root), new Color(0.07f, 0.09f, 0.14f));
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -150f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 0f), new Vector2(1f, 0f));
            content.pivot = new Vector2(0.5f, 0f);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 80f;

            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -150f), Vector2.zero);
            UiFactory.Fill(bar, new Color(0.03f, 0.05f, 0.12f, 0.92f)).raycastTarget = false;
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(110f, 110f), () => Back?.Invoke(), 60f);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 110f), Loc.T("wmap.title"), 60f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(260f, 96f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), "0", 48f, Palette.UiGold, align: TextAlignmentOptions.Left);
            UiFactory.MakeButton(root, Loc.T("wmap.continue"), Kind.Primary, new Vector2(1f, 0f), new Vector2(-40f, 170f), new Vector2(520f, 140f), () => Continue?.Invoke(), 52f);
        }

        /// <summary>
        /// The map for where the journey stands: worlds up to <paramref name="open"/> open; the pin over floor
        /// <paramref name="pinLevel"/> (on its road when <paramref name="onRoad"/>, at the world's arena when <paramref name="atArena"/>).
        /// </summary>
        public void Show(Func<int, JourneyMapArt.WorldPlan> planOf, int open, int pinLevel, bool onRoad, bool atArena, string pinText, int coins)
        {
            openWorld = open;
            coinsText.text = coins.ToString();
            ReleasePictures();
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            blocks.Clear();
            float y = 40f;
            for (int w = 0; w < LevelCatalog.WorldCount; w++)
            {
                var plan = planOf(w);
                var rect = UiFactory.Box("World" + w, content, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(plan.width * S, plan.height * S));
                rect.pivot = new Vector2(0.5f, 0f);
                var img = rect.gameObject.AddComponent<RawImage>();
                img.color = new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = false;
                blocks.Add((rect, img, plan));
                Labels(rect, plan, w <= open);
                y += plan.height * S + 60f;
            }
            content.sizeDelta = new Vector2(0f, y + 200f);
            // The pin over where the journey is.
            pin = UiFactory.Box("Pin", content, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(120f, 170f));
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
            var labelPill = UiFactory.Pill("Label", pin, new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(460f, 70f), new Color(0.05f, 0.05f, 0.12f, 0.92f));
            labelPill.pivot = new Vector2(0.5f, 0f);
            UiFactory.Text(labelPill, pinText, 32f, Palette.UiGold);
            pinBase = SpotOf(LevelCatalog.WorldOf(pinLevel), pinLevel, onRoad, atArena);
            pin.anchoredPosition = pinBase;
            screen.Show();
            transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            float view = ((RectTransform)scroll.viewport).rect.height;
            scroll.verticalNormalizedPosition = Mathf.Clamp01((pinBase.y - view * 0.45f) / Mathf.Max(1f, content.sizeDelta.y - view));
            RefreshPictures();
        }

        public void Hide()
        {
            screen.Hide(true);
            ReleasePictures();
        }

        private void ReleasePictures()
        {
            foreach (var b in blocks)
                if (b.image != null && b.image.texture != null) { Destroy(b.image.texture); b.image.texture = null; }
        }

        /// <summary>Where a floor (or its road, or the world's arena) is on the map, in content units.</summary>
        private Vector2 SpotOf(int w, int level, bool onRoad, bool atArena)
        {
            if (w < 0 || w >= blocks.Count) return Vector2.zero;
            var (rect, _, plan) = blocks[w];
            foreach (var p in plan.pieces)
            {
                bool match = atArena ? p.kind == JourneyMapArt.Kind.Arena
                    : p.level == level && (onRoad ? p.kind == JourneyMapArt.Kind.Road : p.kind == JourneyMapArt.Kind.Level);
                if (!match) continue;
                var c = p.Centre;
                return new Vector2((c.x - plan.width * 0.5f) * S + 95f, rect.anchoredPosition.y + c.y * S); // beside the floor's number, not over it
            }
            return new Vector2(0f, rect.anchoredPosition.y);
        }

        /// <summary>A world's name beside its strip (tap: its arena), and a number on every floor (tap: replay it).</summary>
        private void Labels(RectTransform rect, JourneyMapArt.WorldPlan plan, bool open)
        {
            var theme = WorldTheme.ForWorld(plan.world);
            var name = UiFactory.MakeButton(rect, LevelCatalog.WorldName(plan.world * LevelCatalog.LevelsPerWorld), open ? Kind.Secondary : Kind.Icon,
                new Vector2(0f, 0f), new Vector2(-40f, 40f), new Vector2(460f, 100f), () => { }, 30f);
            ((RectTransform)name.transform).pivot = new Vector2(1f, 0f);
            name.interactable = false;
            if (!open) name.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            int unlocked = SaveData.UnlockedLevel;
            foreach (var p in plan.pieces)
            {
                if (p.kind != JourneyMapArt.Kind.Level && p.kind != JourneyMapArt.Kind.Arena) continue;
                bool arena = p.kind == JourneyMapArt.Kind.Arena;
                int level = p.level;
                bool reached = open && (arena ? unlocked > level : level <= unlocked);
                var c = p.Centre;
                var badge = UiFactory.Box(arena ? "Arena" : "Level" + level, rect, new Vector2(0f, 0f), new Vector2(c.x * S, c.y * S), arena ? new Vector2(170f, 110f) : new Vector2(110f, 110f));
                badge.pivot = new Vector2(0.5f, 0.5f);
                var face = UiFactory.Fill(badge, reached ? (arena ? Palette.UiGold : theme.accent) : new Color(0.45f, 0.47f, 0.52f, 0.85f), arena ? UiSprites.Rounded : UiSprites.Circle, arena ? 2f : 1f);
                if (!arena) UiFactory.Fill(UiFactory.Stretch("Rim", badge), new Color(1f, 1f, 1f, 0.85f), UiSprites.Ring, 0.35f).raycastTarget = false;
                var t = UiFactory.Text(badge, arena ? Loc.T("arena.button") : (level + 1).ToString(), arena ? 26f : 46f, Color.white);
                t.fontStyle = FontStyles.Bold;
                if (arena) { t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 26f; }
                if (!arena && reached)
                {
                    int stars = Progress.Stars(level);
                    for (int s = 0; s < 3; s++)
                    {
                        var star = UiFactory.Box("Star", badge, new Vector2(0.5f, 0f), new Vector2((s - 1) * 34f, -8f), new Vector2(32f, 32f));
                        star.pivot = new Vector2(0.5f, 0.5f);
                        UiFactory.Fill(star, s < stars ? Palette.UiGold : new Color(1f, 1f, 1f, 0.25f), UiSprites.Star).raycastTarget = false;
                    }
                }
                if (!reached) continue;
                var button = badge.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                int world = plan.world;
                button.onClick.AddListener(() =>
                {
                    Audio.AudioManager.PlaySfx(Audio.Sfx.Click, 0.8f, 1.1f);
                    if (arena) ArenaPicked?.Invoke(world); else LevelPicked?.Invoke(level);
                });
            }
        }

        /// <summary>Pictures only for the worlds in view (and around it); the rest are let go (memory).</summary>
        private void RefreshPictures()
        {
            if (blocks.Count == 0) return;
            float viewH = ((RectTransform)scroll.viewport).rect.height;
            float bottom = -content.anchoredPosition.y, top = bottom + viewH;
            for (int i = 0; i < blocks.Count; i++)
            {
                var (rect, image, plan) = blocks[i];
                float y0 = rect.anchoredPosition.y, y1 = y0 + rect.sizeDelta.y;
                bool near = y1 > bottom - viewH && y0 < top + viewH;
                if (near && image.texture == null)
                {
                    image.texture = JourneyMapArt.Paint(plan, i <= openWorld);
                    image.color = Color.white;
                }
                else if (!near && image.texture != null)
                {
                    Destroy(image.texture);
                    image.texture = null;
                    image.color = new Color(1f, 1f, 1f, 0f);
                }
            }
        }

        private float refreshAt;

        private void Update()
        {
            if (!screen.IsVisible) return;
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + 0.25f; RefreshPictures(); }
            if (pin == null) return;
            float t = Time.unscaledTime;
            pin.anchoredPosition = pinBase + new Vector2(0f, Mathf.Abs(Mathf.Sin(t * 3f)) * 26f);
            pin.localScale = Vector3.one * (1f + Mathf.Sin(t * 6f) * 0.03f);
        }
    }
}
