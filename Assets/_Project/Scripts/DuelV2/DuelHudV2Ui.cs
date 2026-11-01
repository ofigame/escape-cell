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
        // the old duel's round buttons (icons, the ultimate's ring, the special's wait sweep), where the player set them
        private DuelButton shield, special, ulti, context, jump, heart, power, pause;
        private bool pSpecial, pUlti, pContext, pJump, pHeart, pPower, pPause;
        private RectTransform cluster;
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
            BuildButtons(canPause);
            touch = new DuelTouchReader { Ignore = p => OverButton(p) || StrikeSide(p) };
        }

        /// <summary>
        /// The old duel's button cluster (the player's size, opacity and hand from the settings): SHIELD in the corner, the
        /// ULTIMATE over it (its three-part ring), SPECIAL beside it (its wait sweeping), the context move over the ultimate
        /// (BREAK / POWER HIT / KNOCKDOWN, only while offered), JUMP; the heart on the other side; the power when one is held.
        /// A tap anywhere else strikes.
        /// </summary>
        private void BuildButtons(bool canPause)
        {
            var s = DuelControlsSettings.Current;
            cluster = UiFactory.Stretch("Buttons", root);
            shield = DuelButton.Create(cluster, "Shield", s.Units(s.shieldDp), UiKit.Style.mine, DuelIcon.Shield);
            ulti = DuelButton.Create(cluster, "Ultimate", s.Units(s.ultiDp), UiKit.Style.warning, DuelIcon.Ulti, 3);
            special = DuelButton.Create(cluster, "Special", s.Units(s.specialDp), UiKit.Style.purple, DuelIcon.SpecialShock);
            context = DuelButton.Create(cluster, "Context", s.Units(s.contextDp), UiKit.Style.orange, DuelIcon.Heavy);
            jump = DuelButton.Create(cluster, "Jump", s.Units(s.jumpDp), UiKit.Style.success, DuelIcon.Jump);
            heart = DuelButton.Create(cluster, "Heart", s.Units(s.heartDp), UiKit.Style.danger, DuelIcon.Heart);
            power = DuelButton.Create(cluster, "Power", s.Units(s.specialDp), UiKit.Style.info, DuelIcon.SpecialSkyStrike);
            if (canPause) pause = DuelButton.Create(cluster, "Pause", s.Units(s.pauseDp), UiKit.Style.royal, DuelIcon.Pause);
            special.Down += () => pSpecial = true;
            ulti.Down += () => pUlti = true;
            context.Down += () => pContext = true;
            jump.Down += () => pJump = true;
            heart.Down += () => pHeart = true;
            power.Down += () => pPower = true;
            if (pause != null) pause.Down += () => pPause = true;
            foreach (var b in new[] { shield, ulti, special, context, jump, heart, power, pause }) if (b != null) buttonRects.Add(b.Rect);

            float size = s.sizeSteps[Mathf.Clamp(SaveData.DuelButtonSize, 0, s.sizeSteps.Length - 1)];
            float alpha = s.opacitySteps[Mathf.Clamp(SaveData.DuelButtonOpacity, 0, s.opacitySteps.Length - 1)];
            bool left = SaveData.DuelLeftHanded;
            float edge = s.Units(s.edgeDp), gap = s.Units(s.gapDp);
            float rShield = s.Units(s.shieldDp) * 0.5f, rUlti = s.Units(s.ultiDp) * 0.5f, rSpecial = s.Units(s.specialDp) * 0.5f;
            float rContext = s.Units(s.contextDp) * 0.5f, rJump = s.Units(s.jumpDp) * 0.5f, rHeart = s.Units(s.heartDp) * 0.5f, rPause = s.Units(s.pauseDp) * 0.5f;
            var shieldAt = new Vector2(edge + rShield * 1.1f, edge + rShield * 1.1f) * size;
            var ultiAt = new Vector2(shieldAt.x, shieldAt.y + (rShield + rUlti) * 1.12f * size + gap * size);
            var specialAt = new Vector2(shieldAt.x + (rShield + rSpecial) * 1.12f * size + gap * size, shieldAt.y - (rShield - rSpecial) * size);
            var contextAt = new Vector2(ultiAt.x, ultiAt.y + (rUlti + rContext) * 1.12f * size + gap * size);
            var jumpAt = new Vector2(specialAt.x + (rSpecial + rJump) * 1.12f * size + gap * size, specialAt.y);
            var powerAt = new Vector2(specialAt.x, specialAt.y + (rSpecial * 2f) * 1.12f * size + gap * size);
            Corner(shield, shieldAt, left, size, alpha);
            Corner(ulti, ultiAt, left, size, alpha);
            Corner(special, specialAt, left, size, alpha);
            Corner(context, contextAt, left, size, 1f);
            Corner(jump, jumpAt, left, size, alpha);
            Corner(power, powerAt, left, size, 1f);
            Place(heart, new Vector2(left ? 1f : 0f, 0f), new Vector2((left ? -1f : 1f) * (edge + rHeart * 1.2f + 40f) * size, 600f), size, alpha);
            if (pause != null) Place(pause, new Vector2(0f, 1f), new Vector2(edge + rPause + 20f, -(edge + rPause + 20f) - 230f), 1f, 1f);
        }

        private static void Corner(DuelButton b, Vector2 fromCorner, bool left, float size, float alpha) =>
            Place(b, new Vector2(left ? 0f : 1f, 0f), new Vector2(left ? fromCorner.x : -fromCorner.x, fromCorner.y), size, alpha);

        private static void Place(DuelButton b, Vector2 anchor, Vector2 at, float size, float alpha)
        {
            b.Rect.anchorMin = b.Rect.anchorMax = anchor;
            b.Rect.anchoredPosition = at;
            b.SetLook(alpha, size);
        }

        /// <summary>The striking side: the screen's right part (the left one walks and swipes; the left-handed: mirrored).</summary>
        private static bool StrikeSide(Vector2 screen) => SaveData.DuelLeftHanded ? screen.x < Screen.width * 0.45f : screen.x > Screen.width * 0.45f;

        /// <summary>A finger landed on the striking side this frame (not on a button): a blow at once, never waiting for it to lift.</summary>
        private bool StrikeDown()
        {
            bool hit = false;
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    if (!t.press.wasPressedThisFrame) continue;
                    var p = t.position.ReadValue();
                    if (StrikeSide(p) && !OverButton(p)) hit = true;
                }
            var m = UnityEngine.InputSystem.Mouse.current;
            if (!hit && (ts == null || !ts.press.isPressed) && m != null && m.leftButton.wasPressedThisFrame)
            {
                var p = m.position.ReadValue();
                if (StrikeSide(p) && !OverButton(p)) hit = true;
            }
            return hit;
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
            float wait = sim.Time < me.specialReadyAt ? (me.specialReadyAt - sim.Time) / Mathf.Max(0.1f, s.specialCooldown) : 0f;
            special.SetWait(wait);
            special.SetUsable(wait <= 0f && me.Free);
            ulti.SetParts(me.meter / Mathf.Max(0.01f, s.meterMax) * 3f, UiKit.Style.warning, UiKit.A(UiKit.Style.warning, 0.55f));
            ulti.SetUsable(me.meter >= s.meterMax);
            ulti.SetPulse(me.meter >= s.meterMax);
            DuelIcon? ctx = me.inChain && me.meter >= s.breakCost ? DuelIcon.Breaker : sim.Time <= me.knockdownUntil ? DuelIcon.Upper : sim.Time <= me.powerHitUntil ? DuelIcon.Heavy : (DuelIcon?)null;
            context.gameObject.SetActive(ctx.HasValue);
            if (ctx.HasValue) { context.SetIcon(ctx.Value); context.SetColour(ctx.Value == DuelIcon.Breaker ? UiKit.Style.info : UiKit.Style.orange); context.SetPulse(true); }
            heart.gameObject.SetActive(me.hearts > 0);
            bool has = me.powers.Count > 0;
            power.gameObject.SetActive(has);
            if (has)
            {
                var k = me.powers[0];
                power.SetIcon(k == ElementKind.Snow ? DuelIcon.SpecialIceWall : k == ElementKind.Fire ? DuelIcon.SpecialSkyStrike : DuelIcon.SpecialShock);
                power.SetColour(RainViewV2.ColourOf(k));
                power.SetUsable(sim.Time >= me.powerReadyAt);
                power.SetPulse(sim.Time >= me.powerReadyAt);
            }
        }

        /// <summary>This frame's wishes from the thumbs (the fight adds the keys): taps, slides and swipes, the buttons.</summary>
        public FighterInput Read(float towardOther)
        {
            var inp = FighterInput.None;
            var c = touch.Poll();
            if (c.tap.HasValue) inp.attack = true; // (a quick tap on the walking side strikes too)
            inp.attack |= StrikeDown();
            if (c.stick.HasValue) inp.walk = Mathf.Abs(c.stick.Value.x) > 0.25f ? Mathf.Sign(c.stick.Value.x) * towardOther : 0f;
            if (c.flick.HasValue && c.flick.Value.y > 0.5f) inp.jump = true;
            if (c.dodge) inp.dodge = true;
            inp.block = shield.Held;
            if (pSpecial) inp.special = true;
            if (pUlti) inp.ulti = true;
            if (pContext) inp.context = true;
            if (pJump) inp.jump = true;
            if (pHeart) inp.heart = true;
            if (pPower) inp.power = 0;
            if (pPause) Paused?.Invoke();
            pSpecial = pUlti = pContext = pJump = pHeart = pPower = pPause = false;
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
