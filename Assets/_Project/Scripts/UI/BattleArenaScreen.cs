using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The battle arenas, listed like the workshop's cards: a picture of each arena, its name, how hard it is (stars),
    /// the best streak and wins so far, and FIGHT — or the level it opens at.
    /// </summary>
    public class BattleArenaScreen : MonoBehaviour
    {
        public event Action BackPressed;
        public event Action<int> ArenaChosen;

        private UiScreen screen;
        private RectTransform content;
        private const float CellW = 470f, CellH = 640f, Gap = 30f;

        public UiScreen Screen => screen;
        public bool IsOpen => screen.IsVisible;

        public static BattleArenaScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("BattleArenas", canvasRoot, out var root);
            var s = root.gameObject.AddComponent<BattleArenaScreen>();
            s.screen = screen;
            s.Build(root);
            return s;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Fill(UiFactory.Stretch("Backdrop", root), new Color(0.03f, 0.03f, 0.07f, 0.95f)).gameObject.AddComponent<IgnoreSafeArea>();
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -190f), Vector2.zero);
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(124f, 124f), () => BackPressed?.Invoke(), 64f);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 110f), Loc.T("arena.title"), 54f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(0f, Monetization.Ads.BannerReserve), new Vector2(0f, -200f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;
        }

        public void Show()
        {
            Populate();
            screen.Show();
            screen.transform.SetAsLastSibling();
        }

        public void Hide() => screen.Hide(true);

        private void Populate()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            for (int i = 0; i < BattleArenas.Count; i++) Card(i);
            content.sizeDelta = new Vector2(0f, Mathf.CeilToInt(BattleArenas.Count / 2f) * (CellH + Gap) + 60f);
            content.anchoredPosition = Vector2.zero;
        }

        private void Card(int i)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * (CellW + Gap) * 0.5f;
            float y = -30f - (i / 2) * (CellH + Gap);
            var card = UiFactory.Card("Arena " + i, content, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(CellW, CellH));
            card.pivot = new Vector2(0.5f, 1f);
            bool open = BattleArenas.Unlocked(i);
            // The arena's picture.
            var pic = UiFactory.Box("Picture", card, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(CellW - 44f, CellW - 44f));
            pic.pivot = new Vector2(0.5f, 1f);
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>("Arenas/arena_" + i);
            raw.color = open ? Color.white : new Color(0.35f, 0.35f, 0.4f);
            raw.raycastTarget = false;
            UiFactory.TextBox("Name", card, new Vector2(0.5f, 1f), new Vector2(0f, -(CellW - 6f)), new Vector2(CellW - 30f, 56f), Loc.T(BattleArenas.NameKey(i)), 36f, Palette.UiText, title: true);
            // Difficulty stars.
            for (int s = 0; s < 5; s++)
            {
                var star = UiFactory.Box("Star", card, new Vector2(0.5f, 1f), new Vector2((s - 2) * 46f, -(CellW + 50f)), new Vector2(40f, 40f));
                star.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(star, s < BattleArenas.Stars(i) ? new Color(1f, 0.45f, 0.3f) : new Color(1f, 1f, 1f, 0.15f), UiSprites.Star).raycastTarget = false;
            }
            string info = open ? Loc.F("arena.best", BattleArenas.BestStreak(i), BattleArenas.Wins(i)) : Loc.F("arena.locked", BattleArenas.UnlockAt(i) + 1);
            UiFactory.TextBox("Info", card, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(CellW - 30f, 40f), info, 24f, new Color(0.85f, 0.88f, 1f, 0.85f), FontStyles.Normal);
            var play = UiFactory.MakeButton(card, open ? Loc.T("arena.play") : Loc.F("arena.locked", BattleArenas.UnlockAt(i) + 1), open ? Kind.Gold : Kind.Secondary,
                new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(CellW - 60f, 76f), () =>
                {
                    if (!open) { AudioManager.PlaySfx(Sfx.Bump, 0.6f); return; }
                    ArenaChosen?.Invoke(i);
                }, 34f);
        }
    }
}
