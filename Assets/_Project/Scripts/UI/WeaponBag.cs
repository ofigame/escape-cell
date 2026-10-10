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
            grid = UiFactory.Rect("Grid", root, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 300f), new Vector2(-30f, -320f));
            UiFactory.MakeButton(root, Loc.T("bag.back"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(560f, 130f), () =>
            {
                screen.Hide();
                Closed?.Invoke();
            }, 56f).GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        }

        public void Show()
        {
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            var owned = Armory.All.FindAll(Armory.Owned);
            var current = Armory.Equipped;
            const float w = 320f, h = 400f, gap = 20f;
            int cols = 3;
            for (int i = 0; i < owned.Count; i++)
            {
                var weapon = owned[i];
                int col = i % cols, row = i / cols;
                float x = (col - (cols - 1) * 0.5f) * (w + gap);
                float y = -row * (h + gap);
                var card = UiFactory.Card(weapon.id, grid, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(w, h));
                card.pivot = new Vector2(0.5f, 1f);
                bool inHand = weapon == current;
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
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card.Find("Background").GetComponent<Image>();
                button.onClick.AddListener(() =>
                {
                    Audio.AudioManager.PlaySfx(Audio.Sfx.Click, 0.8f, 1.2f);
                    screen.Hide();
                    Picked?.Invoke(weapon);
                });
                card.gameObject.AddComponent<ButtonPress>();
            }
            screen.Show();
            transform.SetAsLastSibling();
        }

        public void Hide() => screen.Hide(true);
    }
}
