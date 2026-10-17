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
    /// The bag, opened mid-level (the game waits): every weapon the robot owns as a big card with its picture, name and
    /// how it hits; the one in hand is marked. Tapping a card takes that weapon and goes back to the fight.
    /// </summary>
    public class WeaponBag : MonoBehaviour
    {
        public event Action<WeaponDef> Picked;
        /// <summary>The gun in hand tapped again: put it down.</summary>
        public event Action<WeaponDef> Dropped;
        public event Action Closed;

        private UiScreen screen;
        private RectTransform grid;

        public bool IsOpen => screen.IsVisible;

        public static WeaponBag Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("WeaponBag", canvasRoot, out var root);
            var bag = root.gameObject.AddComponent<WeaponBag>();
            bag.screen = s;
            bag.Build(root);
            return bag;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.06f, 0.78f));
            UiFactory.TextBox("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(900f, 110f), Loc.T("bag.title"), 72f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.TextBox("Hint", root, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(900f, 60f), Loc.T("bag.hint"), 32f, new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // The cards scroll (many weapons, or the short landscape screen of the arenas): a masked view over the grid.
            var view = UiFactory.Rect("View", root, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 200f), new Vector2(-30f, -300f));
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // takes the drag
            grid = UiFactory.Rect("Grid", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            grid.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = grid;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            this.scroll = scroll;
            UiFactory.MakeButton(root, Loc.T("bag.back"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(560f, 130f), () =>
            {
                screen.Hide();
                Closed?.Invoke();
            }, 56f).GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        }

        /// <param name="gunsAllowed">The battle arenas: guns can be taken too (on the floors they are shown dimmed).</param>
        public void Show(bool gunsAllowed = false)
        {
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            var owned = Armory.All.FindAll(Armory.Owned);
            // The ones that can be taken here first (on the floors the guns, shown dimmed, come last).
            if (!gunsAllowed) owned.Sort((a, b) => (a.kind == WeaponKind.Gun ? 1 : 0).CompareTo(b.kind == WeaponKind.Gun ? 1 : 0));
            var current = Armory.Equipped;
            var currentGun = gunsAllowed ? Armory.EquippedGun : null;
            const float w = 320f, h = 400f, gap = 20f;
            var area = ((RectTransform)transform).rect;
            int cols = Mathf.Clamp(Mathf.FloorToInt((area.width - 60f + gap) / (w + gap)), 3, 5); // landscape: more across
            grid.sizeDelta = new Vector2(0f, Mathf.CeilToInt(owned.Count / (float)cols) * (h + gap) + 20f);
            grid.anchoredPosition = Vector2.zero;
            if (scroll != null) scroll.velocity = Vector2.zero;
            for (int i = 0; i < owned.Count; i++)
            {
                var weapon = owned[i];
                int col = i % cols, row = i / cols;
                float x = (col - (cols - 1) * 0.5f) * (w + gap);
                float y = -row * (h + gap);
                var card = UiFactory.Card(weapon.id, grid, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(w, h));
                card.pivot = new Vector2(0.5f, 1f);
                bool inHand = weapon == current || (currentGun != null && weapon == currentGun);
                bool usable = gunsAllowed || weapon.kind != WeaponKind.Gun;
                if (inHand)
                    UiFactory.Fill(UiFactory.Stretch("InHand", card), new Color(0.3f, 1f, 0.55f, 0.9f), UiSprites.Ring, 0.6f).raycastTarget = false;
                var halo = UiFactory.Box("Halo", card, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(280f, 280f));
                halo.pivot = new Vector2(0.5f, 0.5f);
                var glow = WeaponModels.Glow(weapon.tier);
                UiFactory.Fill(halo, new Color(glow.r, glow.g, glow.b, 0.35f), UiSprites.Shadow, 0.5f).raycastTarget = false;
                var pic = UiFactory.Box("Picture", card, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(240f, 240f));
                pic.pivot = new Vector2(0.5f, 0.5f);
                var raw = pic.gameObject.AddComponent<RawImage>();
                raw.texture = ItemPreview.Weapon(weapon);
                raw.raycastTarget = false;
                UiFactory.TextBox("Name", card, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(300f, 50f), Loc.T("weapon." + weapon.id), 30f, Palette.UiText, title: true)
                    .rectTransform.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.TextBox("Stats", card, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(300f, 40f),
                    Loc.F("bag.stats", Armory.Damage(weapon), weapon.reach), 24f, new Color(0.85f, 0.9f, 1f, 0.85f), FontStyles.Normal).rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (inHand)
                    UiFactory.TextBox("InHandLabel", card, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(280f, 40f), Loc.T("weapon.equipped"), 24f, new Color(0.4f, 1f, 0.6f), title: true)
                        .rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (weapon.chest)
                {
                    var badge = UiFactory.Box("ChestBadge", card, new Vector2(1f, 1f), new Vector2(-12f, -64f), new Vector2(120f, 42f)); // under the "in hand" line
                    badge.pivot = new Vector2(1f, 1f);
                    UiFactory.Fill(badge, Palette.UiGold, UiSprites.Rounded, 2f).raycastTarget = false;
                    UiFactory.Text(badge, Loc.T("chests.badge"), 22f, new Color(0.3f, 0.16f, 0.06f), title: true).raycastTarget = false;
                }
                bool dropGun = inHand && weapon.kind == WeaponKind.Gun && gunsAllowed;
                if (dropGun)
                    UiFactory.TextBox("DropLabel", card, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(300f, 34f), Loc.T("bag.dropGun"), 22f, new Color(1f, 0.75f, 0.45f), title: true)
                        .rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (!usable)
                {
                    // A gun on the floors: shown, not taken (the battle arenas only).
                    card.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
                    UiFactory.TextBox("ArenaOnly", card, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(300f, 40f), Loc.T("bag.arenaOnly"), 22f, new Color(1f, 0.8f, 0.4f), title: true)
                        .rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    continue;
                }
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card.Find("Background").GetComponent<Image>();
                button.onClick.AddListener(() =>
                {
                    Audio.AudioManager.PlaySfx(Audio.Sfx.Click, 0.8f, 1.2f);
                    screen.Hide();
                    if (dropGun) { Dropped?.Invoke(weapon); return; }
                    Picked?.Invoke(weapon);
                });
                card.gameObject.AddComponent<ButtonPress>();
            }
            screen.Show();
            transform.SetAsLastSibling();
        }

        public void Hide() => screen.Hide(true);

        private ScrollRect scroll;
    }
}
