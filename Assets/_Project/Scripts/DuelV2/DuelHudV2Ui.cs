using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The v2 duel's screen in the game (uGUI; Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 2.5, 3, 5.5): both health bars with
    /// their ghost, shield and bar parts, power slots, names and hearts; the round's dots, number and clock; the series
    /// in big numbers; the banners (ROUND 1, FIGHT!, KNOCK-OUT); each blow's word over the struck one; the screen's
    /// tints (the ultimate's dark, the perfect block's flash, a rain's sky) and the match's replay. Below, the thumbs:
    /// a tap anywhere strikes, a slide walks, a swipe up jumps and down dodges; the buttons: STRIKE, SHIELD (held),
    /// SPECIAL, ULTIMATE, the context (BREAK / POWER / KNOCKDOWN), the heart, the power slots, pause.
    /// </summary>
    public class DuelHudV2Ui : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform root;
        private readonly Image[] hpFill = new Image[2], ghostFill = new Image[2], shieldFill = new Image[2];
        private readonly Image[,] meterFill = new Image[2, 3];
        private readonly Image[,] slot = new Image[2, 3];
        private readonly TextMeshProUGUI[,] slotMark = new TextMeshProUGUI[2, 3];
        private readonly TextMeshProUGUI[] nameText = new TextMeshProUGUI[2];
        private readonly float[] ghost = { 1f, 1f }, ghostAt = { 0f, 0f };
        private TextMeshProUGUI topLine, series, banner, status;
        private Image tint;
        private RawImage replay;
        private TextMeshProUGUI replayLabel;
        private float seriesAt = -9f, bannerUntil;
        private int seriesValue;
        private readonly List<(TextMeshProUGUI text, Vector3 at, float born)> floats = new List<(TextMeshProUGUI, Vector3, float)>();
        private readonly Stack<TextMeshProUGUI> spare = new Stack<TextMeshProUGUI>();
        private HoldButtonV2 strike, shield, special, ulti, context, heart, pause;
        private readonly HoldButtonV2[] power = new HoldButtonV2[3];
        private TextMeshProUGUI specialText, ultiText, contextText, heartText;
        private readonly List<RectTransform> buttonRects = new List<RectTransform>();
        private DuelTouchReader touch;
        private string[] names = { "", "" };

        /// <summary>The pause button pressed (the game opens its pause screen).</summary>
        public System.Action Paused;

        public static DuelHudV2Ui Create(string me, string rival, bool canPause)
        {
            var hud = new GameObject("DuelV2Hud").AddComponent<DuelHudV2Ui>();
            hud.names = new[] { me, rival };
            hud.Build(canPause);
            return hud;
        }

        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }

        // ---------- building ----------

        private static readonly Color Hp = new Color(0.3f, 0.9f, 0.4f), HpLow = new Color(1f, 0.3f, 0.25f), Ghost = new Color(1f, 0.9f, 0.9f, 0.85f),
            ShieldC = new Color(0.55f, 0.75f, 1f), ShieldBroke = new Color(0.5f, 0.3f, 0.3f), MeterC = new Color(0.8f, 0.6f, 0.3f), MeterFull = new Color(1f, 0.75f, 0.2f);

        private void Build(bool canPause)
        {
            canvas = UiFactory.CreateCanvas("DuelV2HudCanvas", out _);
            canvas.sortingOrder = 30;
            root = UiFactory.Stretch("Root", canvas.transform);
            var safe = root.gameObject.AddComponent<SquashBot.UI.SafeArea>();
            // the full-screen tint (under everything) and the replay (over the world, under the words)
            tint = UiFactory.Fill(UiFactory.Stretch("Tint", root), Color.clear);
            tint.raycastTarget = false;
            var rr = UiFactory.Stretch("Replay", root);
            replay = rr.gameObject.AddComponent<RawImage>();
            replay.raycastTarget = false;
            replay.enabled = false;
            replayLabel = Flat.Label(root, Loc.T("v2.replay"), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, 90f), 64f, Color.white, TextAlignmentOptions.Center, true);
            replayLabel.enabled = false;

            for (int i = 0; i < 2; i++)
            {
                bool right = i == 1;
                var anchor = new Vector2(right ? 1f : 0f, 1f);
                float x = right ? -24f : 24f;
                nameText[i] = Flat.Label(root, names[i], anchor, new Vector2(x, -70f), new Vector2(470f, 46f), 34f, Color.white, right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, true);
                UiFactory.Bar(root, anchor, new Vector2(x, -122f), new Vector2(470f, 36f), Ghost, out ghostFill[i]);
                var hpBar = UiFactory.Box("Hp", root, anchor, new Vector2(x, -122f), new Vector2(470f, 36f));
                hpFill[i] = UiFactory.Fill(UiFactory.Stretch("Fill", hpBar), Hp, UiSprites.Rounded, 4f);
                hpFill[i].raycastTarget = false;
                UiFactory.Bar(root, anchor, new Vector2(x, -164f), new Vector2(470f, 12f), ShieldC, out shieldFill[i]);
                for (int p = 0; p < 3; p++)
                {
                    float px = right ? x - p * 160f : x + p * 160f;
                    UiFactory.Bar(root, anchor, new Vector2(px, -184f), new Vector2(150f, 14f), MeterC, out var mf);
                    meterFill[i, p] = mf;
                    var s = UiFactory.Box("Slot", root, anchor, new Vector2(right ? x - p * 58f : x + p * 58f, -210f), new Vector2(50f, 50f));
                    UiFactory.Fill(s, new Color(0f, 0f, 0f, 0.45f), UiSprites.Rounded, 3f).raycastTarget = false;
                    slot[i, p] = UiFactory.Fill(UiFactory.Stretch("Fill", s), Color.clear, UiSprites.Rounded, 3f);
                    slot[i, p].raycastTarget = false;
                    slot[i, p].rectTransform.offsetMin = new Vector2(5f, 5f); slot[i, p].rectTransform.offsetMax = new Vector2(-5f, -5f);
                    slotMark[i, p] = UiFactory.Text(s, "", 30f, Color.black);
                }
            }
            topLine = Flat.Label(root, "", new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(560f, 56f), 40f, Color.white, TextAlignmentOptions.Center, true);
            status = Flat.Label(root, "", new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(900f, 50f), 34f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Center, true);
            series = Flat.Label(root, "", new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(900f, 150f), 110f, new Color(1f, 0.8f, 0.2f), TextAlignmentOptions.Center, true);
            banner = Flat.Label(root, "", new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(1040f, 190f), 120f, Color.white, TextAlignmentOptions.Center, true);

            // the buttons: the right thumb's (strike, shield, the moves), the left's (the heart, the powers); pause at the top
            strike = Btn("Strike", Loc.T("v2.btn.strike"), new Vector2(1f, 0f), new Vector2(-40f, 60f), new Vector2(250f, 250f), new Color(0.85f, 0.35f, 0.25f), out _);
            shield = Btn("Shield", Loc.T("v2.btn.shield"), new Vector2(1f, 0f), new Vector2(-310f, 60f), new Vector2(190f, 190f), new Color(0.3f, 0.5f, 0.85f), out _);
            special = Btn("Special", Loc.T("v2.btn.special"), new Vector2(1f, 0f), new Vector2(-40f, 330f), new Vector2(170f, 130f), new Color(0.55f, 0.35f, 0.8f), out specialText);
            ulti = Btn("Ulti", Loc.T("v2.btn.ulti"), new Vector2(1f, 0f), new Vector2(-230f, 270f), new Vector2(170f, 130f), new Color(0.85f, 0.6f, 0.15f), out ultiText);
            context = Btn("Context", "", new Vector2(1f, 0f), new Vector2(-40f, 480f), new Vector2(360f, 120f), new Color(0.9f, 0.45f, 0.15f), out contextText);
            heart = Btn("Heart", "♥", new Vector2(0f, 0f), new Vector2(40f, 330f), new Vector2(130f, 110f), new Color(0.75f, 0.25f, 0.35f), out heartText);
            for (int p = 0; p < 3; p++) power[p] = Btn("Power" + p, (p + 1).ToString(), new Vector2(0f, 0f), new Vector2(40f + p * 140f, 60f), new Vector2(120f, 120f), new Color(0.25f, 0.3f, 0.4f), out _);
            if (canPause) pause = Btn("Pause", "II", new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(90f, 70f), new Color(0.2f, 0.22f, 0.3f), out _);
            touch = new DuelTouchReader { Ignore = OverButton };
        }

        private HoldButtonV2 Btn(string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, Color face, out TextMeshProUGUI label)
        {
            var r = UiFactory.Box(name, root, anchor, pos, size);
            UiFactory.Fill(r, new Color(face.r, face.g, face.b, 0.82f), UiSprites.Rounded, 2.5f);
            label = UiFactory.Text(r, text, Mathf.Min(48f, size.y * 0.32f), Color.white);
            label.raycastTarget = false;
            buttonRects.Add(r);
            return r.gameObject.AddComponent<HoldButtonV2>();
        }

        /// <summary>A finger on a button (its touch is the button's, not a strike or a walk).</summary>
        public bool OverButton(Vector2 screen)
        {
            foreach (var r in buttonRects) if (r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screen, null)) return true;
            return false;
        }

        // ---------- the fight's words ----------

        public void Banner(string text, Color c, float seconds) { banner.text = text; banner.color = c; bannerUntil = Time.unscaledTime + seconds; }
        public void Series(int n) { seriesValue = n; seriesAt = Time.unscaledTime; }
        public void Status(string text) => status.text = text ?? "";

        public void Float(string text, Color c, Vector3 at, float size)
        {
            var t = spare.Count > 0 ? spare.Pop() : Flat.Label(root, "", new Vector2(0f, 0f), Vector2.zero, new Vector2(700f, 90f), 50f, Color.white, TextAlignmentOptions.Center, true);
            t.gameObject.SetActive(true);
            t.text = text; t.color = c;
            t.fontSizeMax = UiKit.TypeSize(size * 1.9f);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            floats.Add((t, at, Time.unscaledTime));
        }

        // ---------- each frame ----------

        /// <summary>The bars, the clock and the buttons' words from the rules; the labels moved with the camera.</summary>
        public void Show(DuelSim sim, Camera cam, DuelCoreSettings s, string top, Color tintColour, Texture replayFrame)
        {
            for (int i = 0; i < 2; i++)
            {
                var f = sim.F[i];
                float h = f.Health01;
                if (h < ghost[i]) { if (Time.unscaledTime - ghostAt[i] > s.ghostDelay) ghost[i] = Mathf.MoveTowards(ghost[i], h, Time.unscaledDeltaTime * 0.8f); }
                else { ghost[i] = h; ghostAt[i] = Time.unscaledTime; }
                Set(hpFill[i], h, i == 1);
                hpFill[i].color = h > 0.3f ? Hp : HpLow;
                Set(ghostFill[i], ghost[i], i == 1);
                Set(shieldFill[i], f.shield / Mathf.Max(1f, s.shieldDurability), i == 1);
                shieldFill[i].color = sim.Time < f.shieldBrokenUntil ? ShieldBroke : ShieldC;
                for (int p = 0; p < 3; p++)
                {
                    float part = Mathf.Clamp01(f.meter - p);
                    Set(meterFill[i, p], part, i == 1);
                    meterFill[i, p].color = part >= 1f ? MeterFull : MeterC;
                    bool has = p < f.powers.Count;
                    slot[i, p].color = has ? RainViewV2.ColourOf(f.powers[p]) : Color.clear;
                    slotMark[i, p].text = ""; // (the slot's colour tells the power: the font has no snowflake or bolt)
                }
                nameText[i].text = names[i] + "  " + new string('♥', Mathf.Max(0, f.hearts));
            }
            topLine.text = top;
            float sAge = Time.unscaledTime - seriesAt;
            series.text = sAge < 1.4f && seriesValue >= 2 ? Loc.F("v2.series", seriesValue) : "";
            series.alpha = 1f - Mathf.Clamp01((sAge - 0.9f) / 0.5f);
            banner.gameObject.SetActive(Time.unscaledTime < bannerUntil);
            tint.color = tintColour;
            replay.enabled = replayLabel.enabled = replayFrame != null;
            if (replayFrame != null) replay.texture = replayFrame;
            // the labels over the fighters (rising, fading)
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var (t, at, born) = floats[i];
                float age = Time.unscaledTime - born;
                if (age > 1.1f || cam == null) { t.gameObject.SetActive(false); spare.Push(t); floats.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(at + Vector3.up * age * 0.8f);
                if (sp.z < 0f) { t.alpha = 0f; continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var local);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.anchoredPosition = local;
                t.alpha = 1f - Mathf.Clamp01((age - 0.7f) / 0.4f);
            }
        }

        /// <summary>The buttons' words for foi now (cooldowns, the bar, what the context button offers), and who shows.</summary>
        public void ShowButtons(DuelSim sim, Fighter me, DuelCoreSettings s)
        {
            specialText.text = sim.Time < me.specialReadyAt ? Mathf.CeilToInt(me.specialReadyAt - sim.Time).ToString() : Loc.T("v2.btn.special");
            ultiText.text = me.meter >= s.meterMax ? Loc.T("v2.btn.ulti") : Mathf.FloorToInt(me.meter) + "/3";
            string ctx = me.inChain && me.meter >= s.breakCost ? Loc.T("v2.btn.break") : sim.Time <= me.knockdownUntil ? Loc.T("v2.btn.knockdown") : sim.Time <= me.powerHitUntil ? Loc.T("v2.btn.power") : "";
            context.gameObject.SetActive(ctx.Length > 0);
            contextText.text = ctx;
            heart.gameObject.SetActive(me.hearts > 0);
            heartText.text = "♥" + me.hearts;
            for (int p = 0; p < 3; p++) power[p].gameObject.SetActive(p < me.powers.Count);
        }

        /// <summary>This frame's wishes from the thumbs (the fight adds the keys): taps, slides and swipes, the buttons.</summary>
        public FighterInput Read(float towardOther)
        {
            var inp = FighterInput.None;
            var c = touch.Poll();
            if (c.tap.HasValue) inp.attack = true;
            if (c.stick.HasValue) inp.walk = Mathf.Abs(c.stick.Value.x) > 0.25f ? Mathf.Sign(c.stick.Value.x) * towardOther : 0f;
            if (c.flick.HasValue && c.flick.Value.y > 0.5f) inp.jump = true;
            if (c.dodge) inp.dodge = true;
            if (strike.TakePress()) inp.attack = true;
            inp.block = shield.Held;
            shield.TakePress();
            if (special.TakePress()) inp.special = true;
            if (ulti.TakePress()) inp.ulti = true;
            if (context.TakePress()) inp.context = true;
            if (heart.TakePress()) inp.heart = true;
            for (int p = 0; p < 3; p++) if (power[p].TakePress()) inp.power = p;
            if (pause != null && pause.TakePress()) Paused?.Invoke();
            return inp;
        }

        private static void Set(Image fill, float v, bool fromRight)
        {
            var rt = fill.rectTransform;
            v = Mathf.Clamp01(v);
            if (fromRight) { rt.anchorMin = new Vector2(1f - v, 0f); rt.anchorMax = Vector2.one; }
            else { rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(v, 1f); }
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
