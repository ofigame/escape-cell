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
        public Button backBtn, jumpBtn, runBtn, carBtn, homeBtn, decorBtn, taxiBtn, fishBtn, sellBtn, holdBtn;
        public TextMeshProUGUI money, cooler;
        // fishing: the hold button (cast / reel), the power and tension bars
        public PointerArea hold;
        public GameObject fishPanel, powerBar;
        public RectTransform greenZone, needle;
        public Image catchFill, powerFill;
        public TextMeshProUGUI fishText;
        public RectTransform stickBase, stickKnob;
        public GameObject nearChip;
        public TextMeshProUGUI nearText, help;

        public void Build(Transform parent)
        {
            canvas = Ui.MakeCanvas(parent, 12);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            var root = Ui.SafeRoot(canvas);

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
            runBtn.gameObject.SetActive(false);              // always at running pace: no run toggle
            carBtn = RoundButton(root, "BİN", new Vector2(-95, 340), 180);
            carBtn.GetComponent<Image>().color = Ui.Yellow;
            carBtn.gameObject.SetActive(false);
            taxiBtn = RoundButton(root, "MÜŞTERİ", new Vector2(-300, 340), 180);
            taxiBtn.GetComponent<Image>().color = new Color(1f, 0.93f, 0.5f);
            taxiBtn.gameObject.SetActive(false);
            var mp = Ui.Panel(root, "Money", new Color(1f, 0.96f, 0.82f, 0.95f), false);
            mp.rectTransform.Place(new Vector2(1, 1), new Vector2(240, 80), new Vector2(-30, -30));
            var coin = Ui.Plain(mp.transform, "Coin", new Color(0.95f, 0.7f, 0.1f), false);
            coin.sprite = Icons.Circle;
            coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(46, 46), new Vector2(16, 0));
            money = Ui.Text(mp.transform, "", 34, Ui.Ink, true);
            money.rectTransform.Fill(8); money.rectTransform.offsetMin = new Vector2(70, 8);
            cooler = Ui.Text(mp.transform.parent, "", 28, Ui.Ink, true);
            var cp = Ui.Panel(root, "Cooler", new Color(0.85f, 0.95f, 1f, 0.95f), false);
            cp.rectTransform.Place(new Vector2(1, 1), new Vector2(240, 66), new Vector2(-30, -120));
            cooler.transform.SetParent(cp.transform, false);
            cooler.rectTransform.Fill(6);
            fishBtn = RoundButton(root, "OLTA", new Vector2(-330, 70), 180);
            fishBtn.GetComponent<Image>().color = new Color(0.75f, 0.9f, 1f);
            fishBtn.gameObject.SetActive(false);
            sellBtn = RoundButton(root, "SAT", new Vector2(-300, 340), 180);
            sellBtn.GetComponent<Image>().color = new Color(1f, 0.86f, 0.3f);
            sellBtn.gameObject.SetActive(false);
            holdBtn = RoundButton(root, "FIRLAT", new Vector2(-70, 80), 230);
            holdBtn.GetComponent<Image>().color = Ui.Yellow;
            hold = holdBtn.gameObject.AddComponent<PointerArea>();
            holdBtn.gameObject.SetActive(false);
            // the cast's power: a bar filling up beside the hold button
            var pb = Ui.Panel(root, "Power", new Color(0, 0, 0, 0.35f), false);
            pb.rectTransform.Place(new Vector2(1, 0), new Vector2(40, 230), new Vector2(-320, 80));
            powerBar = pb.gameObject;
            powerFill = Ui.Plain(pb.transform, "Fill", new Color(1f, 0.6f, 0.15f), false);
            powerFill.rectTransform.anchorMin = Vector2.zero; powerFill.rectTransform.anchorMax = new Vector2(1, 0); powerFill.rectTransform.pivot = new Vector2(0.5f, 0);
            powerFill.rectTransform.offsetMin = new Vector2(4, 4); powerFill.rectTransform.offsetMax = new Vector2(-4, 4);
            powerBar.SetActive(false);
            // reeling: keep the needle in the green, the catch bar fills up
            var fp = Ui.Panel(root, "Reel", new Color(1, 1, 1, 0.88f), false);
            fp.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(820, 170), new Vector2(-120, 40));
            fishPanel = fp.gameObject;
            fishText = Ui.Text(fp.transform, "", 30, Ui.Ink, true);
            fishText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(780, 48), new Vector2(0, -6));
            var track = Ui.Plain(fp.transform, "Track", new Color(0.85f, 0.35f, 0.3f), false);
            track.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(740, 40), new Vector2(0, 60));
            var gz = Ui.Plain(track.transform, "Green", new Color(0.35f, 0.8f, 0.4f), false);
            greenZone = gz.rectTransform;
            greenZone.anchorMin = new Vector2(0.25f, 0); greenZone.anchorMax = new Vector2(0.8f, 1);
            greenZone.offsetMin = greenZone.offsetMax = Vector2.zero;
            var nd = Ui.Plain(track.transform, "Needle", Ui.Ink, false);
            needle = nd.rectTransform;
            needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0.5f);
            needle.sizeDelta = new Vector2(10, 62);
            var cb = Ui.Plain(fp.transform, "Catch", new Color(0, 0, 0, 0.15f), false);
            cb.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(740, 18), new Vector2(0, 22));
            catchFill = Ui.Plain(cb.transform, "Fill", new Color(0.2f, 0.55f, 0.95f), false);
            catchFill.rectTransform.anchorMin = Vector2.zero; catchFill.rectTransform.anchorMax = new Vector2(0, 1); catchFill.rectTransform.pivot = new Vector2(0, 0.5f);
            catchFill.rectTransform.offsetMin = catchFill.rectTransform.offsetMax = Vector2.zero;
            fishPanel.SetActive(false);
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
            var t = Ui.Text(img.transform, label, 34, Ui.Ink, false, true);
            t.rectTransform.Fill(20);
            ButtonIcon.Add(img.gameObject, t, Icons.ForLabel(label), size * 0.18f);
            return b;
        }

        public void SetHomeLabel(string label) => homeBtn.SetLabel(label);

        public void SetTaxiLabel(string label) => taxiBtn.SetLabel(label);

        /// <summary>The power bar (0..1).</summary>
        public void SetPower(float p) { var r = powerFill.rectTransform; r.anchorMax = new Vector2(1, Mathf.Clamp01(p)); }

        /// <summary>The reeling bars: tension (0..1) and how far the fish is landed (0..1).</summary>
        public void SetReel(float tension, float caught)
        {
            needle.anchorMin = needle.anchorMax = new Vector2(Mathf.Clamp01(tension), 0.5f);
            catchFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(caught), 1);
        }

        public void SetCarLabel(string label) => carBtn.SetLabel(label);

        public void SetRun(bool on) => runBtn.GetComponent<Image>().color = on ? Ui.Yellow : new Color(1, 1, 1, 0.8f);
    }
}
