using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>
    /// The landscape walking screen: left thumb = floating stick, right thumb = look around, run and jump buttons,
    /// back to the city view, and the name of the model you're standing next to.
    /// </summary>
    public sealed class TpsUi
    {
        public Canvas canvas;
        public PointerArea touch;
        public Button backBtn, jumpBtn, runBtn, carBtn, homeBtn, decorBtn;
        public RectTransform stickBase, stickKnob;
        public GameObject nearChip;
        public TextMeshProUGUI nearText, help;

        public void Build(Transform parent)
        {
            canvas = Ui.MakeCanvas(parent, 12);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            var root = canvas.transform;

            var t = Ui.Plain(root, "Touch", new Color(0, 0, 0, 0));
            t.rectTransform.Fill();
            touch = t.gameObject.AddComponent<PointerArea>();

            backBtn = Ui.Button(root, "Şehre dön", Color.white, Ui.Ink, 38, () => { });
            ((RectTransform)backBtn.transform).Place(new Vector2(0, 1), new Vector2(300, 100), new Vector2(30, -30));

            var hp = Ui.Panel(root, "Help", new Color(1, 1, 1, 0.8f), false);
            hp.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(900, 80), new Vector2(0, -30));
            help = Ui.Text(hp.transform, "Sol başparmak: yürü   ·   Sağ başparmak: etrafına bak", 30, Ui.Ink, true);
            help.rectTransform.Fill(10);

            var nc = Ui.Panel(root, "Near", new Color(1f, 0.95f, 0.7f), false);
            nc.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(700, 90), new Vector2(0, -130));
            nearChip = nc.gameObject;
            nearText = Ui.Text(nc.transform, "", 36, Ui.Ink, true);
            nearText.rectTransform.Fill(12);
            nearChip.SetActive(false);

            jumpBtn = RoundButton(root, "ZIPLA", new Vector2(-70, 80), 230);
            runBtn = RoundButton(root, "KOŞ", new Vector2(-330, 70), 180);
            carBtn = RoundButton(root, "BİN", new Vector2(-95, 340), 180);
            carBtn.GetComponent<Image>().color = Ui.Yellow;
            carBtn.gameObject.SetActive(false);
            homeBtn = RoundButton(root, "GİR", new Vector2(-95, 340), 180);
            homeBtn.GetComponent<Image>().color = Ui.Yellow;
            homeBtn.gameObject.SetActive(false);
            decorBtn = Ui.Button(root, "Dekore et", Ui.Yellow, Ui.Ink, 38, () => { });
            ((RectTransform)decorBtn.transform).Place(new Vector2(0, 1), new Vector2(300, 100), new Vector2(30, -150));
            decorBtn.gameObject.SetActive(false);

            var sb = Ui.Plain(root, "StickBase", new Color(1, 1, 1, 0.25f), false);
            sb.sprite = Icons.Circle;
            stickBase = sb.rectTransform;
            stickBase.anchorMin = stickBase.anchorMax = Vector2.zero;
            stickBase.sizeDelta = new Vector2(280, 280);
            var kn = Ui.Plain(stickBase, "Knob", new Color(1, 1, 1, 0.75f), false);
            kn.sprite = Icons.Circle;
            stickKnob = kn.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(120, 120), Vector2.zero);
            stickBase.gameObject.SetActive(false);
            canvas.gameObject.SetActive(false);
        }

        private static Button RoundButton(Transform root, string label, Vector2 offset, float size)
        {
            var img = Ui.Plain(root, label, new Color(1, 1, 1, 0.8f));
            img.sprite = Icons.Circle;
            img.rectTransform.Place(new Vector2(1, 0), new Vector2(size, size), offset);
            var b = img.gameObject.AddComponent<Button>();
            var t = Ui.Text(img.transform, label, 40, Ui.Ink, false, true);
            t.rectTransform.Fill(20);
            return b;
        }

        public void SetHomeLabel(string label) => homeBtn.GetComponentInChildren<TextMeshProUGUI>().text = label;

        public void SetCarLabel(string label) => carBtn.GetComponentInChildren<TextMeshProUGUI>().text = label;

        public void SetRun(bool on) => runBtn.GetComponent<Image>().color = on ? Ui.Yellow : new Color(1, 1, 1, 0.8f);
    }
}
