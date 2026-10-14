using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// Setting up defences before a level: the fortress seen from above stage by stage (arrows to switch stage), the
    /// three defences to pick from with how many are ready, a hint line, and START. Taps on the scene go to the game
    /// (it places or takes back a defence there).
    /// </summary>
    public class DefenseEditorScreen : MonoBehaviour
    {
        public event Action<DefenseKind> KindSelected;
        public event Action<int> StageStep;
        public event Action StartPressed;
        public event Action BackPressed;
        public event Action<Vector2> Tapped;
        /// <summary>A finger dragged over the scene (pixels): the view pans.</summary>
        public event Action<Vector2> Dragged;

        private UiScreen screen;
        private TextMeshProUGUI stageText, hint;
        private Button prev, next;
        private readonly Image[] faces = new Image[3];
        private readonly TextMeshProUGUI[] counts = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[3];
        private readonly RawImage[] pictures = new RawImage[3];

        public UiScreen Screen => screen;
        public bool IsOpen => screen.IsVisible;

        public static DefenseEditorScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("DefenseEditor", canvasRoot, out var root);
            var e = root.gameObject.AddComponent<DefenseEditorScreen>();
            e.screen = screen;
            e.Build(root);
            return e;
        }

        private void Build(RectTransform root)
        {
            // The scene itself catches taps (under the panels).
            var area = UiFactory.Stretch("TapArea", root);
            UiFactory.Fill(area, new Color(0f, 0f, 0f, 0.001f));
            var tap = area.gameObject.AddComponent<TapArea>();
            tap.Tapped += p => Tapped?.Invoke(p);
            tap.Dragged += d => Dragged?.Invoke(d);

            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -250f), Vector2.zero);
            UiFactory.Fill(bar, new Color(0.06f, 0.06f, 0.14f, 0.82f));
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 20f), new Vector2(124f, 124f), () => BackPressed?.Invoke(), 64f);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(620f, 80f), Loc.T("def.title"), 52f, Palette.UiText, title: true);
            prev = UiFactory.MakeButton(bar, "<", Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(-250f, 24f), new Vector2(110f, 90f), () => StageStep?.Invoke(-1), 50f);
            next = UiFactory.MakeButton(bar, ">", Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(250f, 24f), new Vector2(110f, 90f), () => StageStep?.Invoke(1), 50f);
            stageText = UiFactory.TextBox("Stage", bar, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(260f, 80f), "", 40f, Palette.UiCyan);

            var panel = UiFactory.Rect("Panel", root, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve), new Vector2(0f, Monetization.Ads.BannerReserve + 520f));
            UiFactory.Fill(panel, new Color(0.06f, 0.06f, 0.14f, 0.85f));
            hint = UiFactory.TextBox("Hint", panel, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1000f, 90f), "", 30f, new Color(0.9f, 0.92f, 1f, 0.9f), FontStyles.Normal);
            hint.textWrappingMode = TextWrappingModes.Normal;
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 18f;
            hint.fontSizeMax = 30f;
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = UiFactory.MakeButton(panel, "", Kind.Secondary, new Vector2((i + 0.5f) / 3f, 1f), new Vector2(0f, -104f), new Vector2(320f, 214f), () => KindSelected?.Invoke((DefenseKind)k), 30f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 1f);
                faces[i] = (Image)b.targetGraphic;
                var pic = UiFactory.Box("Picture", b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(120f, 120f));
                pic.pivot = new Vector2(0.5f, 1f);
                pictures[i] = pic.gameObject.AddComponent<RawImage>();
                pictures[i].raycastTarget = false;
                names[i] = UiFactory.TextBox("Name", b.transform, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(300f, 46f), Loc.T("def." + (DefenseKind)k), 32f, Palette.UiText, title: true);
                counts[i] = UiFactory.TextBox("Count", b.transform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(300f, 40f), "", 28f, Palette.UiGold);
            }
            UiFactory.MakeButton(panel, Loc.T("def.start"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 140f), () => StartPressed?.Invoke(), 70f);
        }

        public void Show()
        {
            screen.Show();
            screen.transform.SetAsLastSibling();
        }

        public void Hide() => screen.Hide(true);

        public void SetStage(int stage, int count)
        {
            stageText.text = Loc.F("hud.stage", stage + 1, count);
            prev.gameObject.SetActive(count > 1);
            next.gameObject.SetActive(count > 1);
            stageText.gameObject.SetActive(count > 1);
        }

        public void SetHint(string text) => hint.text = text;

        /// <summary>One defence button: how many are ready (or the level it opens at), highlighted when picked.</summary>
        public void SetKind(DefenseKind k, string count, bool locked, bool selected, Texture picture = null)
        {
            int i = (int)k;
            if (picture != null) pictures[i].texture = picture;
            pictures[i].color = locked ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            counts[i].text = count;
            counts[i].color = locked ? new Color(1f, 1f, 1f, 0.5f) : Palette.UiGold;
            names[i].color = locked ? new Color(1f, 1f, 1f, 0.5f) : Palette.UiText;
            faces[i].color = selected ? UiFactory.CyanStyle.face : UiFactory.PurpleStyle.face;
        }
    }

    /// <summary>A full-screen catcher for taps and drags that reach the scene (where the finger was, how it moved).</summary>
    public class TapArea : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler
    {
        public event Action<Vector2> Tapped;
        public event Action<Vector2> Dragged;

        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) => Dragged?.Invoke(e.delta);

        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging) return;
            AudioManager.PlaySfx(Sfx.Click, 0.4f, 1.3f);
            Tapped?.Invoke(e.position);
        }
    }
}
