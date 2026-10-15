using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.OpenWorld
{
    /// <summary>One line of a list panel: a colour swatch, a name, a line under it, and a button.</summary>
    public class OwRow
    {
        public string text, sub, button;
        public Color swatch = Color.clear;
        public bool enabled = true;
        /// <summary>A long text (a report): a tall row, the words wrapping.</summary>
        public bool tall;
        public Action onClick;
    }

    /// <summary>
    /// The open world's screen: money and barn at the top, BACK and the camera switch, the stick on the left (and a drag
    /// on the right turning the TPS camera), the big action button for what foi stands by (sow, harvest, milk, sell...)
    /// with a second one beside it, short notes, and a scrolling list panel (crops to sow, things to build, the barn,
    /// the co-op). Fits itself upright or sideways.
    /// </summary>
    public class OwUi : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        private Vector2 look;
        public event Action ActionPressed, Action2Pressed, BackPressed, CameraPressed;

        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform safe, actionRect, action2Rect, listPanel, listContent, toast, topBar;
        private TextMeshProUGUI money, barn, actionText, action2Text, toastText, listTitle, hint;
        private Button actionButton, action2Button;
        private CanvasGroup toastGroup;
        private float toastLeft;
        private bool sideways;

        public bool ListOpen => listPanel != null && listPanel.gameObject.activeSelf;

        public Vector2 TakeLook() { var l = look; look = Vector2.zero; return l; }

        public static OwUi Create()
        {
            var canvas = UiFactory.CreateCanvas("OpenWorldUi", out var scaler);
            canvas.sortingOrder = 150;
            var ui = canvas.gameObject.AddComponent<OwUi>();
            ui.canvas = canvas;
            ui.scaler = scaler;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            UiFactory.Fit(scaler);
            safe = UiFactory.Stretch("Safe", canvas.transform);
            safe.gameObject.AddComponent<SquashBot.UI.SafeArea>();

            // Drag areas under everything: the stick (left), the camera (right).
            var lookArea = UiFactory.Rect("Look", safe, new Vector2(0.45f, 0f), new Vector2(1f, 0.85f));
            lookArea.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            lookArea.gameObject.AddComponent<LookPad>().owner = this;
            var stickArea = UiFactory.Rect("Stick", safe, new Vector2(0f, 0f), new Vector2(0.45f, 0.85f));
            stickArea.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            stickArea.gameObject.AddComponent<Pad>().Build(this);

            // Top: BACK, money, barn, camera.
            topBar = UiFactory.Rect("Top", safe, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -150f), Vector2.zero);
            var back = Flat.Button(topBar, "←", new Vector2(0f, 0.5f), new Vector2(90f, -10f), new Vector2(120f, 110f), Flat.CardHi, Flat.Text, () => BackPressed?.Invoke(), 52f);
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0.5f);
            var moneyPill = Flat.Panel(topBar, "Money", new Vector2(0.5f, 0.5f), new Vector2(-170f, -10f), new Vector2(300f, 100f), new Color(0.05f, 0.08f, 0.06f, 0.8f));
            moneyPill.pivot = new Vector2(0.5f, 0.5f);
            var bill = UiFactory.Box("Bill", moneyPill, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(64f, 42f));
            bill.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(bill, new Color(0.45f, 0.8f, 0.45f), UiSprites.Rounded, 2f).raycastTarget = false;
            money = Flat.Label(moneyPill, "", new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(190f, 80f), 44f, new Color(0.7f, 1f, 0.7f), TextAlignmentOptions.Left, bold: true);
            money.rectTransform.pivot = new Vector2(0f, 0.5f);
            var barnPill = Flat.Panel(topBar, "Barn", new Vector2(0.5f, 0.5f), new Vector2(170f, -10f), new Vector2(300f, 100f), new Color(0.12f, 0.06f, 0.04f, 0.8f));
            barnPill.pivot = new Vector2(0.5f, 0.5f);
            barn = Flat.Label(barnPill, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 80f), 36f, new Color(1f, 0.82f, 0.6f), TextAlignmentOptions.Center, bold: true);
            barn.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var cam = Flat.Button(topBar, "TPS", new Vector2(1f, 0.5f), new Vector2(-100f, -10f), new Vector2(150f, 100f), Flat.CardHi, Flat.Text, () => CameraPressed?.Invoke(), 34f);
            ((RectTransform)cam.transform).pivot = new Vector2(0.5f, 0.5f);
            camLabel = cam.GetComponentInChildren<TextMeshProUGUI>();

            // The action buttons, bottom right.
            actionButton = Flat.Button(safe, "", new Vector2(1f, 0f), new Vector2(-210f, 260f), new Vector2(360f, 150f), new Color(0.3f, 0.75f, 0.4f), Color.white, () => { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f); ActionPressed?.Invoke(); }, 38f);
            actionRect = (RectTransform)actionButton.transform;
            actionRect.pivot = new Vector2(0.5f, 0.5f);
            actionText = actionButton.GetComponentInChildren<TextMeshProUGUI>();
            actionText.enableAutoSizing = true; actionText.fontSizeMin = 22f; actionText.fontSizeMax = 38f;
            action2Button = Flat.Button(safe, "", new Vector2(1f, 0f), new Vector2(-210f, 430f), new Vector2(360f, 110f), new Color(0.3f, 0.5f, 0.9f), Color.white, () => { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.1f); Action2Pressed?.Invoke(); }, 32f);
            action2Rect = (RectTransform)action2Button.transform;
            action2Rect.pivot = new Vector2(0.5f, 0.5f);
            action2Text = action2Button.GetComponentInChildren<TextMeshProUGUI>();
            action2Text.enableAutoSizing = true; action2Text.fontSizeMin = 20f; action2Text.fontSizeMax = 32f;
            // DEFENCE and ARMY (near the farm), under the top bar on the left.
            defenceButton = Flat.Button(safe, Loc.T("ow.defence"), new Vector2(0f, 1f), new Vector2(150f, -230f), new Vector2(250f, 90f), new Color(0.75f, 0.45f, 0.2f), Color.white, () => { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.1f); DefencePressed?.Invoke(); }, 28f);
            ((RectTransform)defenceButton.transform).pivot = new Vector2(0.5f, 0.5f);
            armyButton = Flat.Button(safe, Loc.T("ow.army"), new Vector2(0f, 1f), new Vector2(150f, -335f), new Vector2(250f, 90f), new Color(0.3f, 0.45f, 0.85f), Color.white, () => { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.1f); ArmyPressed?.Invoke(); }, 28f);
            ((RectTransform)armyButton.transform).pivot = new Vector2(0.5f, 0.5f);
            hint = Flat.Label(safe, "", new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(900f, 60f), 32f, Color.white, TextAlignmentOptions.Center, bold: true);
            hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            SetAction(null, false);
            SetAction2(null);

            // A short note in the middle.
            toast = Flat.Panel(safe, "Toast", new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(900f, 120f), new Color(0.04f, 0.04f, 0.1f, 0.85f));
            toast.pivot = new Vector2(0.5f, 0.5f);
            toastText = Flat.Label(toast, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 110f), 34f, Color.white, TextAlignmentOptions.Center, bold: true, wrap: true);
            toastText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.alpha = 0f;

            // The list panel (hidden).
            listPanel = UiFactory.Rect("List", safe, Vector2.zero, Vector2.one, new Vector2(50f, 140f), new Vector2(-50f, -170f));
            UiFactory.Fill(listPanel, new Color(0.06f, 0.07f, 0.12f, 0.96f), UiSprites.Rounded, 1f);
            listTitle = Flat.Label(listPanel, "", new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 80f), 46f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            listTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var close = Flat.Button(listPanel, "X", new Vector2(1f, 1f), new Vector2(-70f, -60f), new Vector2(100f, 90f), Flat.CardHi, Flat.Text, CloseList, 40f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
            var view = UiFactory.Rect("View", listPanel, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -120f));
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            listContent = UiFactory.Rect("Content", view, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            listContent.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = listContent;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            listPanel.gameObject.SetActive(false);
        }

        private TextMeshProUGUI camLabel;

        public void SetCameraLabel(bool tps) => camLabel.text = tps ? Loc.T("ow.cam.iso") : "TPS";

        public void SetMoney(long m) => money.text = m.ToString("N0");

        public void SetBarn(int stored, int size) => barn.text = Loc.F("ow.barnPill", stored, size);

        /// <summary>The big button: its words, shown or not, pressable or not (a timer reads greyed).</summary>
        public void SetAction(string text, bool on, bool enabled = true)
        {
            bool show = on && !string.IsNullOrEmpty(text) && !ListOpen;
            if (actionButton.gameObject.activeSelf != show) actionButton.gameObject.SetActive(show);
            if (!show) return;
            actionText.text = text;
            actionButton.interactable = enabled;
        }

        public void SetAction2(string text)
        {
            bool show = !string.IsNullOrEmpty(text) && !ListOpen;
            if (action2Button.gameObject.activeSelf != show) action2Button.gameObject.SetActive(show);
            if (show) action2Text.text = text;
        }

        public void SetHint(string text, bool alarm = false)
        {
            hint.text = text ?? "";
            hint.color = alarm ? new Color(1f, 0.45f, 0.4f) : Color.white;
        }

        public event Action DefencePressed, ArmyPressed;
        private Button defenceButton, armyButton;

        /// <summary>DEFENCE and ARMY show near the farm (not over a list).</summary>
        public void SetFarmButtons(bool on)
        {
            on &= !ListOpen;
            if (defenceButton.gameObject.activeSelf != on) defenceButton.gameObject.SetActive(on);
            if (armyButton.gameObject.activeSelf != on) armyButton.gameObject.SetActive(on);
        }

        public void Toast(string text)
        {
            toastText.text = text;
            toastLeft = 2.6f;
            toastGroup.alpha = 1f;
        }

        // ---------- The list panel ----------

        public void ShowList(string title, List<OwRow> rows)
        {
            listTitle.text = title;
            for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);
            float y = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                float h = r.tall ? 330f : 120f;
                var row = Flat.Panel(listContent, "Row", new Vector2(0.5f, 1f), new Vector2(0f, -y - 6f), new Vector2(0f, h - 12f), Flat.Card);
                row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.offsetMin = new Vector2(0f, row.offsetMin.y); row.offsetMax = new Vector2(0f, row.offsetMax.y);
                row.sizeDelta = new Vector2(0f, h - 12f);
                y += h;
                if (r.tall)
                {
                    // A report: the words over the row, the button under them.
                    var words = Flat.Label(row, r.text, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(760f, 200f), 32f, Flat.Text, TextAlignmentOptions.Top, bold: true, wrap: true);
                    words.rectTransform.pivot = new Vector2(0.5f, 1f);
                    words.enableAutoSizing = true; words.fontSizeMin = 20f; words.fontSizeMax = 32f;
                    var click = r.onClick;
                    var ok = Flat.Button(row, r.button ?? "OK", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(300f, 84f), new Color(0.3f, 0.75f, 0.4f), Flat.Text, () => { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f); click?.Invoke(); }, 30f);
                    ((RectTransform)ok.transform).pivot = new Vector2(0.5f, 0.5f);
                    continue;
                }
                float x = 24f;
                if (r.swatch.a > 0f)
                {
                    var sw = UiFactory.Box("Swatch", row, new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(64f, 64f));
                    sw.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(sw, r.swatch, UiSprites.Circle).raycastTarget = false;
                    x = 110f;
                }
                var name = Flat.Label(row, r.text, new Vector2(0f, 0.5f), new Vector2(x, 18f), new Vector2(560f, 50f), 34f, r.enabled ? Flat.Text : Flat.Muted, TextAlignmentOptions.Left, bold: true);
                name.rectTransform.pivot = new Vector2(0f, 0.5f);
                name.enableAutoSizing = true; name.fontSizeMin = 20f; name.fontSizeMax = 34f;
                var sub = Flat.Label(row, r.sub ?? "", new Vector2(0f, 0.5f), new Vector2(x, -24f), new Vector2(560f, 40f), 24f, Flat.Muted, TextAlignmentOptions.Left);
                sub.rectTransform.pivot = new Vector2(0f, 0.5f);
                sub.enableAutoSizing = true; sub.fontSizeMin = 16f; sub.fontSizeMax = 24f;
                if (!string.IsNullOrEmpty(r.button))
                {
                    var click = r.onClick;
                    var b = Flat.Button(row, r.button, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(230f, 84f), r.enabled ? new Color(0.3f, 0.75f, 0.4f) : Flat.CardHi, Flat.Text, () => { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f); click?.Invoke(); }, 28f);
                    ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
                    b.interactable = r.enabled;
                    var bt = b.GetComponentInChildren<TextMeshProUGUI>();
                    bt.enableAutoSizing = true; bt.fontSizeMin = 16f; bt.fontSizeMax = 28f;
                }
            }
            listContent.sizeDelta = new Vector2(0f, y + 12f);
            listContent.anchoredPosition = Vector2.zero;
            listPanel.gameObject.SetActive(true);
            listPanel.SetAsLastSibling();
            SetAction(null, false);
            SetAction2(null);
        }

        public void CloseList()
        {
            if (listPanel != null) listPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            UiFactory.Fit(scaler);
            bool now = Screen.width > Screen.height;
            if (now != sideways)
            {
                sideways = now;
                actionRect.anchoredPosition = now ? new Vector2(-230f, 200f) : new Vector2(-210f, 260f);
                action2Rect.anchoredPosition = now ? new Vector2(-230f, 360f) : new Vector2(-210f, 430f);
                topBar.offsetMin = new Vector2(0f, now ? -130f : -150f);
            }
            if (toastLeft > 0f)
            {
                toastLeft -= Time.unscaledDeltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastLeft / 0.4f);
            }
        }

        // ---------- The stick and the camera drag ----------

        private class Pad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
        {
            private OwUi owner;
            private RectTransform area, baseRect, knob;
            private Vector2 home = new Vector2(230f, 260f);
            private int pointer = int.MinValue;
            private const float Radius = 130f;

            public void Build(OwUi o)
            {
                owner = o;
                area = (RectTransform)transform;
                baseRect = UiFactory.Box("Base", area, Vector2.zero, home, new Vector2(Radius * 2.3f, Radius * 2.3f));
                baseRect.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(baseRect, new Color(1f, 1f, 1f, 0.14f), UiSprites.Circle).raycastTarget = false;
                UiFactory.Fill(UiFactory.Stretch("Rim", baseRect), new Color(1f, 1f, 1f, 0.45f), UiSprites.Ring, 0.2f).raycastTarget = false;
                knob = UiFactory.Box("Knob", baseRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Radius * 0.95f, Radius * 0.95f));
                knob.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(knob, new Color(1f, 1f, 1f, 0.75f), UiSprites.Circle).raycastTarget = false;
            }

            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

            public void OnPointerDown(PointerEventData e)
            {
                if (pointer != int.MinValue) return;
                pointer = e.pointerId;
                var local = Local(e.position);
                if ((local - baseRect.anchoredPosition).magnitude > Radius * 1.1f) baseRect.anchoredPosition = local;
                Track(local);
            }

            public void OnDrag(PointerEventData e) { if (e.pointerId == pointer) Track(Local(e.position)); }

            public void OnPointerUp(PointerEventData e)
            {
                if (e.pointerId != pointer) return;
                pointer = int.MinValue;
                owner.Move = Vector2.zero;
                baseRect.anchoredPosition = home;
                knob.anchoredPosition = Vector2.zero;
            }

            private void OnDisable() { pointer = int.MinValue; if (owner != null) owner.Move = Vector2.zero; }

            private Vector2 Local(Vector2 screen)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var p);
                return p + area.rect.size * area.pivot;
            }

            private void Track(Vector2 local)
            {
                var d = Vector2.ClampMagnitude(local - baseRect.anchoredPosition, Radius);
                knob.anchoredPosition = d;
                owner.Move = d / Radius;
            }
        }

        private class LookPad : MonoBehaviour, IDragHandler, IInitializePotentialDragHandler
        {
            public OwUi owner;
            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
            public void OnDrag(PointerEventData e) => owner.look += e.delta / Mathf.Max(1f, Screen.height);
        }
    }
}
