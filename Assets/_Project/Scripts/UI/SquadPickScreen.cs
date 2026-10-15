using System;
using SquashBot.Audio;
using SquashBot.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// Soldiers to bring onto the floor: each group's picture and name, how many are at home, how many come (0 to 5)
    /// and the stage they join foi in.
    /// </summary>
    public class SquadPickScreen : MonoBehaviour
    {
        public event Action Closed;

        private UiScreen screen;
        private RectTransform list;
        private TextMeshProUGUI total;
        private int stages = 1;
        private const float RowH = 190f;

        public bool IsOpen => screen.IsVisible;
        public void Hide() => screen.Hide(true);

        public static SquadPickScreen Create(Transform canvasRoot)
        {
            var s = new GameObject("SquadPickUi", typeof(RectTransform)).AddComponent<SquadPickScreen>();
            var rt = (RectTransform)s.transform;
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            s.screen = UiScreen.Create("SquadPick", rt, out var root);
            Flat.Background(root);
            Flat.Label(root, Loc.T("squad.title"), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(960f, 80f), 52f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hint = Flat.Label(root, Loc.T("squad.hint"), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(940f, 90f), 26f, Flat.Muted, TextAlignmentOptions.Center, wrap: true);
            hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s.list = UiFactory.Rect("List", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -260f - Army.Groups * RowH), new Vector2(0f, -260f));
            s.total = Flat.Label(root, "", new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(940f, 60f), 32f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            s.total.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var ok = Flat.Button(root, Loc.T("squad.ok"), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 110f), Flat.Blue, Flat.Text, () => { s.screen.Hide(true); s.Closed?.Invoke(); }, 40f);
            ((RectTransform)ok.transform).pivot = new Vector2(0.5f, 0.5f);
            return s;
        }

        /// <param name="stageCount">The floor's stages (one: no stage to pick).</param>
        public void Show(int stageCount)
        {
            stages = Mathf.Max(1, stageCount);
            Build();
            screen.Show();
            transform.SetAsLastSibling();
        }

        private void Build()
        {
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            for (int g = 0; g < Army.Groups; g++) Row(g);
            total.text = Loc.F("squad.total", LevelSquad.Total);
        }

        private void Row(int g)
        {
            var kind = Army.KindOf(g);
            var colour = ArmyScreen.FamilyColour(kind);
            var row = Flat.Panel(list, "Row" + g, new Vector2(0.5f, 1f), new Vector2(0f, -g * RowH), new Vector2(1000f, RowH - 16f), Flat.Card);
            row.pivot = new Vector2(0.5f, 1f);
            var tile = Flat.Panel(row, "Tile", new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(140f, 140f), new Color(colour.r, colour.g, colour.b, 0.18f), 2f);
            tile.pivot = new Vector2(0f, 0.5f);
            ChallengeScreen.UnitPicture(tile, kind, new Vector2(0f, 4f), 120f);
            var name = Flat.Label(row, Loc.T("army.kind." + kind), new Vector2(0f, 1f), new Vector2(180f, -22f), new Vector2(360f, 46f), 32f, Flat.Text, bold: true);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            int home = SoldierPool.Available(g);
            var info = Flat.Label(row, Loc.F("army.pool", home, Army.Soldiers), new Vector2(0f, 1f), new Vector2(180f, -72f), new Vector2(360f, 36f), 24f, Flat.Muted);
            info.rectTransform.pivot = new Vector2(0f, 1f);
            int n = LevelSquad.Count(g);
            // How many: − n +
            var right = new Vector2(1f, 0.5f);
            Button B(string t, Vector2 at, Vector2 size, Color face, Action a)
            {
                var b = Flat.Button(row, t, right, at, size, face, Flat.Text, () => { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f); a(); }, 32f);
                ((RectTransform)b.transform).pivot = new Vector2(1f, 0.5f);
                return b;
            }
            float y = stages > 1 ? 36f : 0f;
            B("+", new Vector2(-20f, y), new Vector2(80f, 70f), colour, () => { LevelSquad.SetCount(g, Mathf.Min(n + 1, Mathf.Min(LevelSquad.MaxPerGroup, home))); Build(); }).interactable = n < Mathf.Min(LevelSquad.MaxPerGroup, home);
            var count = Flat.Label(row, n.ToString(), right, new Vector2(-110f, y), new Vector2(80f, 70f), 40f, Flat.Text, TextAlignmentOptions.Center, bold: true);
            count.rectTransform.pivot = new Vector2(1f, 0.5f);
            B("-", new Vector2(-200f, y), new Vector2(80f, 70f), Flat.CardHi, () => { LevelSquad.SetCount(g, n - 1); Build(); }).interactable = n > 0;
            if (stages <= 1) return;
            // The stage they join foi in: ◀ Stage N ▶ (as letters: the font may not have arrows).
            int st = LevelSquad.Stage(g, stages);
            B(">", new Vector2(-20f, -46f), new Vector2(80f, 64f), Flat.CardHi, () => { LevelSquad.SetStage(g, (st + 1) % stages); Build(); });
            var stageText = Flat.Label(row, Loc.F("squad.stage", st + 1), right, new Vector2(-110f, -46f), new Vector2(170f, 64f), 26f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            stageText.rectTransform.pivot = new Vector2(1f, 0.5f);
            B("<", new Vector2(-290f, -46f), new Vector2(80f, 64f), Flat.CardHi, () => { LevelSquad.SetStage(g, (st + stages - 1) % stages); Build(); });
        }
    }
}
