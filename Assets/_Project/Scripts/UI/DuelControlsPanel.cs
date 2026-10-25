using System;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The settings' DUEL CONTROLS card: left-handed, the buttons' size and solidity, the JUMP button, the touch
    /// sensitivity, the rival's emotes hidden. Each row's button steps through its choices and takes effect at once.
    /// </summary>
    public class DuelControlsPanel : MonoBehaviour
    {
        /// <summary>A choice changed (the duel's controls lay themselves out again).</summary>
        public static event Action Changed;
        /// <summary>PLAY THE TUTORIAL pressed (the guided first duel again).</summary>
        public static event Action TutorialRequested;

        private TextMeshProUGUI[] values;
        private UiScreen screen;

        private static readonly string[] Rows = { "dc.left", "dc.size", "dc.opacity", "dc.jump", "dc.sense", "dc.hideEmotes" };

        public static DuelControlsPanel Create(Transform root)
        {
            var s = UiScreen.Create("DuelControls", root, out var t);
            UiFactory.Dim(t, new Color(0.06f, 0.05f, 0.18f, 0.55f));
            var card = UiFactory.Card("Card", t, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 200f + Rows.Length * 130f + 340f));
            card.pivot = new Vector2(0.5f, 0.5f);
            var p = s.gameObject.AddComponent<DuelControlsPanel>();
            p.screen = s;
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(820f, 110f), Loc.T("dc.title"), 64f, Palette.UiText, title: true);
            p.values = new TextMeshProUGUI[Rows.Length];
            for (int i = 0; i < Rows.Length; i++)
            {
                int row = i;
                float y = -200f - i * 130f;
                var label = UiFactory.TextBox("Label", card, new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(470f, 100f), Loc.T(Rows[i]), 40f, Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.Left);
                label.enableAutoSizing = true; label.fontSizeMin = 20f; label.fontSizeMax = 40f;
                label.textWrappingMode = TextWrappingModes.Normal;
                var b = UiFactory.MakeButton(card, "", UiFactory.ButtonKind.Secondary, new Vector2(1f, 1f), new Vector2(-40f, y), new Vector2(300f, 100f), () => { p.Step(row); }, 40f);
                p.values[i] = b.GetComponentInChildren<TextMeshProUGUI>();
                p.values[i].enableAutoSizing = true; p.values[i].fontSizeMin = 22f; p.values[i].fontSizeMax = 40f;
            }
            var tut = UiFactory.MakeButton(card, Loc.T("dc.tutorial"), UiFactory.ButtonKind.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(560f, 100f), () => { s.Hide(true); TutorialRequested?.Invoke(); }, 40f);
            UiFactory.MakeButton(card, Loc.T("btn.close"), UiFactory.ButtonKind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(560f, 140f), () => s.Hide(), 60f);
            p.Refresh();
            return p;
        }

        public void Show()
        {
            Refresh();
            screen.Show();
            transform.SetAsLastSibling();
        }

        private void Step(int row)
        {
            switch (row)
            {
                case 0: SaveData.DuelLeftHanded = !SaveData.DuelLeftHanded; break;
                case 1: SaveData.DuelButtonSize = (SaveData.DuelButtonSize + 1) % 3; break;
                case 2: SaveData.DuelButtonOpacity = (SaveData.DuelButtonOpacity + 1) % 3; break;
                case 3: SaveData.DuelJumpButton = !SaveData.DuelJumpButton; break;
                case 4: SaveData.TouchSensitivity = (SaveData.TouchSensitivity + 1) % 3; break;
                case 5: SaveData.HideRivalEmotes = !SaveData.HideRivalEmotes; break;
            }
            Refresh();
            Changed?.Invoke();
        }

        private void Refresh()
        {
            values[0].text = Loc.T(SaveData.DuelLeftHanded ? "on" : "off");
            values[1].text = Loc.T("dc.size." + SaveData.DuelButtonSize);
            values[2].text = new[] { "70%", "85%", "100%" }[SaveData.DuelButtonOpacity];
            values[3].text = Loc.T(SaveData.DuelJumpButton ? "on" : "off");
            values[4].text = Loc.T("dc.sense." + SaveData.TouchSensitivity);
            values[5].text = Loc.T(SaveData.HideRivalEmotes ? "on" : "off");
        }
    }
}
