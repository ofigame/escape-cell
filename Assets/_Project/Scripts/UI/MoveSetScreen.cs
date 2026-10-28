using System;
using System.Collections.Generic;
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
    /// The move set: the three slots (special, ultimate, passive) and every move as a tile — the chosen one lit gold, the
    /// ones not won yet dim with the world whose monster gives them; a tap chooses (or tells what it does).
    /// </summary>
    public class MoveSetScreen : MonoBehaviour
    {
        private UiScreen screen;
        private RectTransform list;
        private TextMeshProUGUI about;
        private float y;

        public bool IsOpen => screen.IsVisible;
        public void Hide() => screen.Hide(true);

        public static MoveSetScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("MoveSet", canvasRoot, out var root);
            s.BackAction = () => s.Hide(); // (the Android back key: the same on every screen)
            var m = root.gameObject.AddComponent<MoveSetScreen>();
            m.screen = s;
            m.Build(root);
            return m;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.06f, 0.75f)).gameObject.AddComponent<IgnoreSafeArea>();
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 1660f));
            card.pivot = new Vector2(0.5f, 0.5f);
            screen.SetPopTarget(card);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(860f, 96f), Loc.T("moves.title"), 60f, Palette.UiGold, title: true);
            list = UiFactory.Box("List", card, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(900f, 1180f));
            list.pivot = new Vector2(0.5f, 1f);
            about = UiFactory.TextBox("About", card, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(880f, 130f), "", 30f, Palette.UiText, FontStyles.Normal);
            about.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            about.textWrappingMode = TextWrappingModes.Normal;
            about.enableAutoSizing = true; about.fontSizeMin = 18f; about.fontSizeMax = 30f;
            var close = UiFactory.MakeButton(card, Loc.T("btn.close"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(480f, 110f), () => screen.Hide(), 44f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        public void Show()
        {
            Refresh();
            about.text = Hint;
            screen.Show();
            transform.SetAsLastSibling();
        }

        /// <summary>How moves are won: two from the start, the worlds' monsters, the duel level's gifts, the passive slot.</summary>
        private static string Hint => Loc.F("moves.how", Loc.T("move.s.Fly"), Loc.T("move.u.Cinematic"), MoveSet.PassiveLevel);

        /// <summary>How a locked move is won: its world's monster (that world's last level), or a duel level's gift.</summary>
        private static string HowToWin(int world) => world <= 0 ? "" : Loc.F("moves.howOne", world, world * LevelCatalog.LevelsPerWorld);

        private void Refresh()
        {
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            y = 0f;
            Section(Loc.T("moves.special"));
            var specials = new List<(string key, bool owned, bool chosen, int world, Action pick)>();
            foreach (SpecialKind m in Enum.GetValues(typeof(SpecialKind)))
            {
                var mm = m;
                specials.Add(("move.s." + m, MoveSet.Owned(m), MoveSet.Special == m, MoveSet.WorldOf(m), () => MoveSet.Special = mm));
            }
            Tiles(specials);
            Section(Loc.T("moves.ulti"));
            var ultis = new List<(string, bool, bool, int, Action)>();
            foreach (UltiKind m in Enum.GetValues(typeof(UltiKind)))
            {
                var mm = m;
                ultis.Add(("move.u." + m, MoveSet.Owned(m), MoveSet.Ulti == m, MoveSet.WorldOf(m), () => MoveSet.Ulti = mm));
            }
            Tiles(ultis);
            Section(MoveSet.PassiveOpen ? Loc.T("moves.passive") : Loc.F("moves.passiveLocked", MoveSet.PassiveLevel));
            var passives = new List<(string, bool, bool, int, Action)>();
            foreach (PassiveKind m in Enum.GetValues(typeof(PassiveKind)))
            {
                var mm = m;
                passives.Add(("move.p." + m, MoveSet.PassiveOpen && MoveSet.Owned(m), MoveSet.Passive == m, MoveSet.WorldOf(m), () => MoveSet.Passive = mm));
            }
            Tiles(passives);
        }

        private void Section(string text)
        {
            var t = UiFactory.TextBox("Head", list, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(900f, 52f), text, 32f, Palette.UiGold, title: true, align: TextAlignmentOptions.Left);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            y -= 58f;
        }

        private static PassiveKind PassiveFrom(string key) => Enum.TryParse(key.Substring("move.p.".Length), out PassiveKind p) ? p : PassiveKind.None;

        /// <summary>A section's tiles, four to a row.</summary>
        private void Tiles(List<(string key, bool owned, bool chosen, int world, Action pick)> items)
        {
            const float W = 214f, H = 96f, Gap = 10f;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int col = i % 4, row = i / 4;
                float x = -1.5f * (W + Gap) + col * (W + Gap);
                var tile = UiFactory.Box("Tile", list, new Vector2(0.5f, 1f), new Vector2(x, y - row * (H + Gap)), new Vector2(W, H));
                tile.pivot = new Vector2(0.5f, 1f);
                var face = UiFactory.Fill(tile, it.chosen ? new Color(0.95f, 0.66f, 0.15f) : it.owned ? Flat.Card : new Color(0.08f, 0.09f, 0.16f, 0.9f), UiSprites.Rounded, 1.4f);
                var label = UiFactory.TextBox("Name", tile, new Vector2(0.5f, 0.5f), new Vector2(0f, it.owned ? 0f : 12f), new Vector2(W - 14f, 56f), Loc.T(it.key), 26f, it.owned ? Color.white : new Color(1f, 1f, 1f, 0.4f), title: true);
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.enableAutoSizing = true; label.fontSizeMin = 16f; label.fontSizeMax = 26f;
                if (!it.owned && it.world > 0)
                {
                    var lockText = UiFactory.TextBox("Lock", tile, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(W - 10f, 34f), Loc.F("moves.lockAt", it.world, it.world * LevelCatalog.LevelsPerWorld), 20f, new Color(1f, 0.75f, 0.4f, 0.8f), FontStyles.Normal);
                    lockText.rectTransform.pivot = new Vector2(0.5f, 0f);
                    lockText.enableAutoSizing = true; lockText.fontSizeMin = 13f; lockText.fontSizeMax = 20f;
                }
                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.onClick.AddListener(() =>
                {
                    about.text = Loc.T(it.key) + ": " + MoveTexts.Describe(it.key); // (its numbers: the duel table's)
                    if (!it.owned)
                    {
                        // a locked move: how it is won (and the passive slot's level, when that is shut)
                        bool slotShut = it.key.StartsWith("move.p.") && !MoveSet.PassiveOpen;
                        about.text += "\n<color=#FFC066>" + (slotShut && MoveSet.Owned(PassiveFrom(it.key)) ? Loc.F("moves.passiveLocked", MoveSet.PassiveLevel) : HowToWin(it.world) + (slotShut ? " · " + Loc.F("moves.passiveLocked", MoveSet.PassiveLevel) : "")) + "</color>";
                        AudioManager.PlaySfx(Sfx.Bump, 0.5f);
                        return;
                    }
                    AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f);
                    it.pick();
                    string keep = about.text;
                    Refresh();
                    about.text = keep;
                });
            }
            int rows = (items.Count + 3) / 4;
            y -= rows * (H + Gap) + 12f;
        }
    }
}
