using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The duel's fighting moves (Mortal Kombat style) on the battle screen: the power bar in three parts at the bottom
    /// left (the rival's thin one under the top bar), the combo count big in the middle, and the move buttons on the
    /// right above the heart and shield — the special (one part of the bar), the ultimate (all three) — with the moves
    /// that come and go in the middle at the bottom: the power hit after three blows in a row, the knockdown after
    /// five, the breaker when caught in the rival's combo, the finish after a knock-down. All on a canvas of its own.
    /// </summary>
    public class DuelSkillBar : MonoBehaviour
    {
        public enum Move { Special, Heavy, Upper, Ultimate, Breaker, Finish }
        public event Action<Move> Pressed;

        private RectTransform root;
        private Image[] parts, rivalParts;
        private Image[] partFill;
        private Button special, ultimate, context;
        private TextMeshProUGUI specialText, contextText, comboText;
        private CanvasGroup comboGroup, contextGroup, specialGroup, ultGroup;
        private Move contextMove;
        private float comboShow, contextPulse;
        private int shownCombo = -1;
        private float shownMeter = -1f, shownRival = -1f;

        public static DuelSkillBar Create(RectTransform hud, string specialName)
        {
            var go = new GameObject("DuelSkills", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(hud, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Canvas>(); // its own: the bar filling and the combo popping redraw only this
            go.AddComponent<GraphicRaycaster>();
            var bar = go.AddComponent<DuelSkillBar>();
            bar.root = rt;
            bar.Build(specialName);
            return bar;
        }

        private void Build(string specialName)
        {
            // The power bar: three parts, bottom left.
            var meter = UiFactory.Box("Power", root, new Vector2(0f, 0f), new Vector2(40f, 250f), new Vector2(420f, 54f));
            meter.pivot = new Vector2(0f, 0.5f);
            parts = new Image[3];
            partFill = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = UiFactory.Box("Part", meter, new Vector2(0f, 0.5f), new Vector2(i * 140f, 0f), new Vector2(132f, 46f));
                slot.pivot = new Vector2(0f, 0.5f);
                parts[i] = UiFactory.Fill(slot, new Color(0.06f, 0.06f, 0.14f, 0.85f), UiSprites.Rounded, 2f);
                parts[i].raycastTarget = false;
                var fill = UiFactory.Rect("Fill", slot, Vector2.zero, new Vector2(0f, 1f), new Vector2(4f, 4f), new Vector2(-4f, -4f));
                partFill[i] = UiFactory.Fill(fill, new Color(1f, 0.75f, 0.2f), UiSprites.Rounded, 2f);
                partFill[i].raycastTarget = false;
            }
            var label = Flat.Label(meter, Loc.T("duel.power"), new Vector2(0f, 1f), new Vector2(0f, 26f), new Vector2(420f, 40f), 24f, Palette.UiGold, TextAlignmentOptions.Left, bold: true);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            // The rival's bar: thin, under the top bar on the right.
            var rivalMeter = UiFactory.Box("RivalPower", root, new Vector2(1f, 1f), new Vector2(-40f, -540f), new Vector2(300f, 18f));
            rivalMeter.pivot = new Vector2(1f, 0.5f);
            rivalParts = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = UiFactory.Box("Part", rivalMeter, new Vector2(1f, 0.5f), new Vector2(-i * 102f, 0f), new Vector2(96f, 16f));
                slot.pivot = new Vector2(1f, 0.5f);
                rivalParts[i] = UiFactory.Fill(slot, new Color(0.3f, 0.1f, 0.1f, 0.8f), UiSprites.Rounded, 4f);
                rivalParts[i].raycastTarget = false;
            }

            special = Round("Special", new Vector2(-75f, 770f), new Color(1f, 0.55f, 0.15f), specialName, () => Pressed?.Invoke(Move.Special), out specialText);
            specialGroup = special.gameObject.AddComponent<CanvasGroup>();
            ultimate = Round("Ultimate", new Vector2(-75f, 895f), new Color(0.75f, 0.35f, 1f), Loc.T("duel.ult"), () => Pressed?.Invoke(Move.Ultimate), out _);
            ultGroup = ultimate.gameObject.AddComponent<CanvasGroup>();

            // The move that comes and goes: big, bottom middle.
            var ctx = UiFactory.Box("Context", root, new Vector2(0.5f, 0f), new Vector2(0f, 520f), new Vector2(420f, 130f));
            ctx.pivot = new Vector2(0.5f, 0.5f);
            var face = UiFactory.Chunky(ctx, UiFactory.StyleOf(new Color(1f, 0.3f, 0.25f)), 1f, 0f, UiSprites.Rounded);
            contextText = UiFactory.OutlinedText("Text", ctx, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(400f, 110f), "", 46f, new Color(0.25f, 0.02f, 0.02f));
            contextText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            context = ctx.gameObject.AddComponent<Button>();
            context.targetGraphic = face;
            context.onClick.AddListener(() => { AudioManager.PlaySfx(Sfx.Click, 1f, 1.4f); Pressed?.Invoke(contextMove); });
            ctx.gameObject.AddComponent<ButtonPress>();
            contextGroup = ctx.gameObject.AddComponent<CanvasGroup>();
            ctx.gameObject.SetActive(false);

            // The combo count, big in the middle.
            comboText = UiFactory.OutlinedText("Combo", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(900f, 140f), "", 96f, new Color(0.35f, 0.08f, 0f));
            comboText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            comboText.color = new Color(1f, 0.82f, 0.25f);
            comboGroup = comboText.gameObject.AddComponent<CanvasGroup>();
            comboGroup.alpha = 0f;
            comboGroup.blocksRaycasts = false;
        }

        private Button Round(string name, Vector2 at, Color colour, string text, Action onClick, out TextMeshProUGUI label)
        {
            var b = UiFactory.Box(name, root, new Vector2(1f, 0f), at, new Vector2(110f, 110f));
            b.pivot = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Chunky(b, UiFactory.StyleOf(colour), 1f, 0f, UiSprites.Circle);
            label = UiFactory.OutlinedText("Label", b, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(100f, 70f), text, 24f, new Color(colour.r * 0.2f, colour.g * 0.15f, colour.b * 0.2f));
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 24f;
            var button = b.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => { AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.3f); onClick(); });
            b.gameObject.AddComponent<ButtonPress>();
            return button;
        }

        /// <summary>The bars (0..3 each), redrawn only when they changed.</summary>
        public void SetMeters(float mine, float rival)
        {
            if (Mathf.Abs(mine - shownMeter) > 0.004f)
            {
                shownMeter = mine;
                for (int i = 0; i < 3; i++)
                {
                    float k = Mathf.Clamp01(mine - i);
                    var r = partFill[i].rectTransform;
                    r.anchorMax = new Vector2(k, 1f);
                    partFill[i].color = k >= 1f ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.55f, 0.15f);
                }
                bool canSpecial = mine >= 1f, canUlt = mine >= 3f;
                special.interactable = canSpecial;
                specialGroup.alpha = canSpecial ? 1f : 0.45f;
                ultimate.interactable = canUlt;
                ultGroup.alpha = canUlt ? 1f : 0.35f;
            }
            if (Mathf.Abs(rival - shownRival) > 0.004f)
            {
                shownRival = rival;
                for (int i = 0; i < 3; i++) rivalParts[i].color = rival >= i + 1 ? new Color(1f, 0.35f, 0.3f) : new Color(0.3f, 0.1f, 0.1f, 0.8f);
            }
        }

        /// <summary>The combo count pops up (2 or more blows in a row).</summary>
        public void ShowCombo(int hits)
        {
            if (hits < 2 || hits == shownCombo) return;
            shownCombo = hits;
            comboText.text = Loc.F("duel.combo", hits);
            comboShow = 1.4f;
        }

        /// <summary>A big word in the middle for a moment (ULTIMATE!, COMBO BREAKER!, FINISHED!).</summary>
        public void Shout(string text, Color colour)
        {
            comboText.text = text;
            comboText.color = colour;
            comboShow = 1.6f;
            shownCombo = -1;
        }

        /// <summary>The move that comes and goes (null: none now).</summary>
        public void SetContext(Move? move)
        {
            bool on = move.HasValue;
            if (context.gameObject.activeSelf != on) context.gameObject.SetActive(on);
            if (!on) return;
            if (contextMove == move.Value && contextText.text.Length > 0) return;
            contextMove = move.Value;
            contextText.text = Loc.T(move.Value switch { Move.Heavy => "duel.heavy", Move.Upper => "duel.upper", Move.Breaker => "duel.breaker", _ => "duel.finish" });
            ((Image)context.targetGraphic).color = move.Value switch
            {
                Move.Heavy => new Color(1f, 0.55f, 0.2f), Move.Upper => new Color(1f, 0.3f, 0.25f), Move.Breaker => new Color(0.35f, 0.75f, 1f), _ => new Color(0.95f, 0.2f, 0.35f),
            };
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (comboShow > 0f)
            {
                comboShow -= dt;
                comboGroup.alpha = Mathf.Clamp01(comboShow / 0.35f);
                float pop = 1f + Mathf.Max(0f, comboShow - 1.2f) * 1.2f;
                comboText.rectTransform.localScale = new Vector3(pop, pop, 1f);
                if (comboShow <= 0f) { shownCombo = -1; comboText.color = new Color(1f, 0.82f, 0.25f); }
            }
            if (context.gameObject.activeSelf)
            {
                contextPulse += dt * 6f;
                float s = 1f + Mathf.Sin(contextPulse) * 0.05f;
                context.transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
