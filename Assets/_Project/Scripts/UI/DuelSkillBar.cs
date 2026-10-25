using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The duel's controls on the battle screen, gathered at the bottom right where the thumb rests (the left with the
    /// left-handed setting): the SHIELD in the corner (hold it to block; a blue ring shows its durability), the
    /// ULTIMATE right above it (a ring of the power bar's three parts, a slow gold pulse when ready), the SPECIAL to its
    /// left (the set's move, a ring of one part) and over that the move that comes and goes — POWER HIT, KNOCKDOWN and
    /// BREAKER always at the same spot, its ring running down with its time. The HEART on the other side (three dots for
    /// what is left), a small pause at the top left and the emotes at the top right. Both power bars sit under their
    /// health bars. Icons instead of words; the combo count big in the middle. All on a canvas of its own.
    /// </summary>
    public class DuelSkillBar : MonoBehaviour
    {
        public enum Move { Special, Heavy, Upper, Ultimate, Breaker, Finish, Jump }
        public event Action<Move> Pressed;
        /// <summary>An emote picked (a written one 0..13, a face taunt from FaceTaunts.First).</summary>
        public event Action<int> Emoted;
        /// <summary>The shield pressed and let go (held: blocking).</summary>
        public event Action ShieldDown, ShieldUp;
        public event Action HeartPressed, PausePressed;
        /// <summary>The knock-down's TAUNT pressed (offered a moment after the player knocked the rival down).</summary>
        public event Action TauntPressed;
        /// <summary>The live match's leave asked and confirmed.</summary>
        public event Action LeaveConfirmed;

        private EmoteWheel wheel;
        private RectTransform root, cluster;
        private DuelButton shield, ultimate, special, context, jump, heart, pause, talk;
        private Image[] heartDots;
        private Image[] myParts, rivalParts;
        private TextMeshProUGUI comboText;
        private CanvasGroup comboGroup;
        private Move contextMove;
        private float comboShow;
        private int shownCombo = -1;
        private float shownMeter = -1f, shownRival = -1f;
        private RectTransform leaveAsk;
        private bool live;

        public static DuelSkillBar Create(RectTransform hud, string specialName) => Create(hud, specialName, MoveSet.Special);

        public static DuelSkillBar Create(RectTransform hud, string specialName, SpecialKind specialMove)
        {
            var go = new GameObject("DuelSkills", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(hud, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Canvas>(); // its own: the rings filling and the combo popping redraw only this
            go.AddComponent<GraphicRaycaster>();
            var bar = go.AddComponent<DuelSkillBar>();
            bar.root = rt;
            bar.Build(specialMove);
            return bar;
        }

        // ---------- Colours (the effects' language: attack orange-red, defence blue, healing green-pink, ultimate gold, special purple) ----------

        private static readonly Color ShieldBlue = new Color(0.25f, 0.55f, 1f), UltiGold = new Color(1f, 0.72f, 0.18f), SpecialPurple = new Color(0.62f, 0.35f, 0.95f);
        private static readonly Color AttackOrange = new Color(1f, 0.5f, 0.15f), BreakerBlue = new Color(0.35f, 0.75f, 1f), HeartPink = new Color(1f, 0.45f, 0.62f);
        private static readonly Color JumpGreen = new Color(0.3f, 0.8f, 0.5f), Calm = new Color(0.22f, 0.24f, 0.36f);

        private void Build(SpecialKind specialMove)
        {
            var s = DuelControlsSettings.Current;
            cluster = UiFactory.Stretch("Cluster", root);
            shield = DuelButton.Create(cluster, "Shield", s.Units(s.shieldDp), ShieldBlue, DuelIcon.Shield);
            shield.Down += () => ShieldDown?.Invoke();
            shield.Up += () => ShieldUp?.Invoke();
            ultimate = DuelButton.Create(cluster, "Ultimate", s.Units(s.ultiDp), UltiGold, DuelIcon.Ulti, 3);
            ultimate.Down += () => Pressed?.Invoke(Move.Ultimate);
            special = DuelButton.Create(cluster, "Special", s.Units(s.specialDp), SpecialPurple, DuelIcons.ForSpecial(specialMove));
            special.Down += () => Pressed?.Invoke(Move.Special);
            context = DuelButton.Create(cluster, "Context", s.Units(s.contextDp), AttackOrange, DuelIcon.Heavy);
            context.Down += () => Pressed?.Invoke(contextMove);
            context.gameObject.SetActive(false);
            jump = DuelButton.Create(cluster, "Jump", s.Units(s.jumpDp), JumpGreen, DuelIcon.Jump);
            jump.Down += () => Pressed?.Invoke(Move.Jump);
            heart = DuelButton.Create(cluster, "Heart", s.Units(s.heartDp), HeartPink, DuelIcon.Heart);
            heart.Down += () => HeartPressed?.Invoke();
            heartDots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var d = UiFactory.Box("Dot" + i, heart.Rect, new Vector2(0.5f, 0f), new Vector2((i - 1) * 30f, -22f), new Vector2(22f, 22f));
                d.pivot = new Vector2(0.5f, 0.5f);
                heartDots[i] = UiFactory.Fill(d, Color.white, UiSprites.Circle);
                heartDots[i].raycastTarget = false;
            }
            pause = DuelButton.Create(cluster, "Pause", s.Units(s.pauseDp), new Color(0.2f, 0.45f, 0.9f), DuelIcon.Pause);
            pause.Down += () => { if (live) AskLeave(); else PausePressed?.Invoke(); };
            talk = DuelButton.Create(cluster, "Talk", s.Units(s.emoteDp), new Color(0.3f, 0.75f, 0.55f), DuelIcon.Emote);
            talk.Down += () => wheel.Toggle();

            // The emote wheel: six slots round a circle under the emote button (face taunts and written emotes).
            wheel = EmoteWheel.Create(root, new Vector2(1f, 1f), new Vector2(-320f, -700f));
            wheel.Picked += e => Emoted?.Invoke(e);

            // Both power bars, three parts each, under their health bars (the player's top left, the rival's top right).
            myParts = Meter("MyPower", new Vector2(0f, 1f), new Vector2(36f, -282f - 70f - s.meterGap), false);
            rivalParts = Meter("RivalPower", new Vector2(1f, 1f), new Vector2(-36f, -282f - 70f - s.meterGap), true);

            // The combo count, big in the middle.
            comboText = UiFactory.OutlinedText("Combo", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(900f, 140f), "", 96f, new Color(0.35f, 0.08f, 0f));
            comboText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            comboText.color = new Color(1f, 0.82f, 0.25f);
            comboGroup = comboText.gameObject.AddComponent<CanvasGroup>();
            comboGroup.alpha = 0f;
            comboGroup.blocksRaycasts = false;
            ApplySettings();
            SetShield(1f, false, 0f);
            SetHearts(3, false);
        }

        private Image[] Meter(string name, Vector2 anchor, Vector2 at, bool mirrored)
        {
            var s = DuelControlsSettings.Current;
            var bar = UiFactory.Box(name, root, anchor, at, new Vector2(340f, s.meterHeight));
            var parts = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = (mirrored ? -1f : 1f) * i * 115f;
                var slot = UiFactory.Box("Part" + i, bar, new Vector2(mirrored ? 1f : 0f, 0.5f), new Vector2(x, 0f), new Vector2(108f, s.meterHeight));
                slot.pivot = new Vector2(mirrored ? 1f : 0f, 0.5f);
                UiFactory.Fill(slot, new Color(0.06f, 0.06f, 0.14f, 0.8f), UiSprites.Rounded, 4f).raycastTarget = false;
                var fill = UiFactory.Rect("Fill", slot, Vector2.zero, new Vector2(0f, 1f), new Vector2(3f, 3f), new Vector2(-3f, -3f));
                parts[i] = UiFactory.Fill(fill, UltiGold, UiSprites.Rounded, 4f);
                parts[i].raycastTarget = false;
            }
            return parts;
        }

        /// <summary>
        /// The settings applied at once: the cluster's side (left-handed: mirrored, the heart to the right), the buttons'
        /// size and how solid they stand, whether JUMP shows.
        /// </summary>
        public void ApplySettings()
        {
            var s = DuelControlsSettings.Current;
            float size = s.sizeSteps[Mathf.Clamp(SaveData.DuelButtonSize, 0, s.sizeSteps.Length - 1)];
            float alpha = s.opacitySteps[Mathf.Clamp(SaveData.DuelButtonOpacity, 0, s.opacitySteps.Length - 1)];
            bool left = SaveData.DuelLeftHanded;
            float edge = s.Units(s.edgeDp), gap = s.Units(s.gapDp);
            float rShield = s.Units(s.shieldDp) * 0.5f, rUlti = s.Units(s.ultiDp) * 0.5f, rSpecial = s.Units(s.specialDp) * 0.5f;
            float rContext = s.Units(s.contextDp) * 0.5f, rJump = s.Units(s.jumpDp) * 0.5f, rHeart = s.Units(s.heartDp) * 0.5f;
            // the main cluster from the corner (x leftward, y upward), scaled with the buttons so the gaps keep
            var shieldAt = new Vector2(edge + rShield * 1.1f, edge + rShield * 1.1f) * size;
            var ultiAt = new Vector2(shieldAt.x, shieldAt.y + (rShield + rUlti) * 1.12f * size + gap * size);
            var specialAt = new Vector2(shieldAt.x + (rShield + rSpecial) * 1.12f * size + gap * size, shieldAt.y - (rShield - rSpecial) * size);
            var contextAt = new Vector2(specialAt.x, specialAt.y + rSpecial * 2.24f * size + gap * size);
            var jumpAt = new Vector2(specialAt.x + (rSpecial + rJump) * 1.12f * size + gap * size, specialAt.y);
            Corner(shield, shieldAt, left, size, alpha);
            Corner(ultimate, ultiAt, left, size, alpha);
            Corner(special, specialAt, left, size, alpha);
            Corner(context, contextAt, left, size, 1f);
            Corner(jump, jumpAt, left, size, alpha);
            jump.gameObject.SetActive(SaveData.DuelJumpButton);
            // the heart on the other side, above the controls' band; pause and emotes at the top corners
            Place(heart, new Vector2(left ? 1f : 0f, 0f), new Vector2((left ? -1f : 1f) * (edge + rHeart * 1.2f + 40f) * size, 600f), size, alpha);
            float rPause = s.Units(s.pauseDp) * 0.5f, rTalk = s.Units(s.emoteDp) * 0.5f;
            Place(pause, new Vector2(0f, 1f), new Vector2(edge + rPause + 20f, -(edge + rPause + 20f)), 1f, 1f);
            Place(talk, new Vector2(1f, 1f), new Vector2(-(edge + rTalk + 20f), -(edge + rTalk + 20f)), 1f, 1f);
        }

        private void Corner(DuelButton b, Vector2 fromCorner, bool left, float size, float alpha) =>
            Place(b, new Vector2(left ? 0f : 1f, 0f), new Vector2(left ? fromCorner.x : -fromCorner.x, fromCorner.y), size, alpha);

        private static void Place(DuelButton b, Vector2 anchor, Vector2 at, float size, float alpha)
        {
            b.Rect.anchorMin = b.Rect.anchorMax = anchor;
            b.Rect.anchoredPosition = at;
            b.SetLook(alpha, size);
        }

        // ---------- What the game tells it ----------

        /// <summary>A live match: the pause only asks whether to leave (a live match never stops).</summary>
        public void SetLive(bool on) => live = on;

        /// <summary>The bars (0..3 each), redrawn only when they changed: both power bars, the ultimate's and special's rings.</summary>
        public void SetMeters(float mine, float rival) => SetMeters(mine, rival, 3f);

        public void SetMeters(float mine, float rival, float ultiCost)
        {
            if (Mathf.Abs(mine - shownMeter) > 0.004f)
            {
                shownMeter = mine;
                Fill(myParts, mine, UltiGold, new Color(0.85f, 0.55f, 0.15f));
                ultimate.SetParts(mine * 3f / Mathf.Max(1f, ultiCost), UltiGold, new Color(0.85f, 0.55f, 0.15f));
                special.SetRing(Mathf.Clamp01(mine), mine >= 1f ? SpecialPurple * 1.3f : new Color(0.55f, 0.4f, 0.8f));
                bool canSpecial = mine >= 1f, canUlt = mine >= ultiCost;
                special.SetUsable(canSpecial);
                ultimate.SetUsable(canUlt);
                ultimate.SetPulse(canUlt);
            }
            if (Mathf.Abs(rival - shownRival) > 0.004f)
            {
                shownRival = rival;
                Fill(rivalParts, rival, new Color(1f, 0.38f, 0.32f), new Color(0.7f, 0.25f, 0.2f));
            }
        }

        private static void Fill(Image[] parts, float value, Color full, Color filling)
        {
            for (int i = 0; i < 3; i++)
            {
                float k = Mathf.Clamp01(value - i);
                parts[i].rectTransform.anchorMax = new Vector2(k, 1f);
                parts[i].color = k >= 1f ? full : filling;
            }
        }

        /// <summary>
        /// The shield's state: its durability (0..1; under a quarter the ring turns red), broken (grey, the sweep showing
        /// its return), <paramref name="wait"/> (0..1) the sweep of a short lock after a press let go too soon.
        /// </summary>
        public void SetShield(float durability, bool broken, float wait)
        {
            var c = broken ? new Color(0.5f, 0.5f, 0.55f) : durability < 0.25f ? new Color(1f, 0.3f, 0.25f) : new Color(0.45f, 0.8f, 1f);
            shield.SetRing(broken ? 0f : durability, c);
            shield.SetColour(broken ? new Color(0.3f, 0.32f, 0.4f) : ShieldBlue);
            shield.SetWait(wait);
        }

        /// <summary>The hearts left (three dots under it); <paramref name="usable"/> false: soft (health full or none left).</summary>
        public void SetHearts(int left, bool usable)
        {
            for (int i = 0; i < heartDots.Length; i++) heartDots[i].color = i < left ? Color.white : new Color(1f, 1f, 1f, 0.2f);
            heart.SetUsable(usable && left > 0);
        }

        /// <summary>The special's icon (the set's move).</summary>
        public void SetSpecial(SpecialKind k) => special.SetIcon(DuelIcons.ForSpecial(k));

        /// <summary>The move that comes and goes: its time left (1 .. 0) on its ring.</summary>
        public void SetContextTime(float k) => context.SetRing(k, contextMove == Move.Breaker ? new Color(0.7f, 0.9f, 1f) : new Color(1f, 0.9f, 0.5f));

        private RectTransform guardBar;
        private Image guardFill;

        /// <summary>The thin durability bar over foi's head while it blocks (<paramref name="screen"/> null: hidden).</summary>
        public void SetGuardBar(Vector2? screen, float share, bool low)
        {
            if (screen == null)
            {
                if (guardBar != null && guardBar.gameObject.activeSelf) guardBar.gameObject.SetActive(false);
                return;
            }
            if (guardBar == null)
            {
                guardBar = UiFactory.Box("GuardBar", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 16f));
                guardBar.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(guardBar, new Color(0.04f, 0.05f, 0.12f, 0.8f), UiSprites.Rounded, 6f).raycastTarget = false;
                var f = UiFactory.Rect("Fill", guardBar, Vector2.zero, new Vector2(1f, 1f), new Vector2(3f, 3f), new Vector2(-3f, -3f));
                guardFill = UiFactory.Fill(f, ShieldBlue, UiSprites.Rounded, 6f);
                guardFill.raycastTarget = false;
            }
            if (!guardBar.gameObject.activeSelf) guardBar.gameObject.SetActive(true);
            var parent = (RectTransform)guardBar.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen.Value, null, out var local))
                guardBar.anchoredPosition = local + parent.rect.size * (parent.pivot - new Vector2(0.5f, 0.5f)); // (from the pivot to the middle anchor)
            guardFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
            guardFill.color = low ? new Color(1f, 0.3f, 0.25f) : new Color(0.45f, 0.8f, 1f);
        }

        private Image cue;

        /// <summary>
        /// The training's perfect guard cue: a ring closing in on the SHIELD as the rival's blow comes (<paramref name="left"/>
        /// seconds to its landing; negative: none), green while a press now would be a perfect guard.
        /// </summary>
        public void SetGuardCue(float left, float window)
        {
            if (cue == null)
            {
                var r = UiFactory.Box("Cue", shield.Rect, new Vector2(0.5f, 0.5f), Vector2.zero, shield.Rect.sizeDelta);
                r.pivot = new Vector2(0.5f, 0.5f);
                cue = r.gameObject.AddComponent<Image>();
                cue.sprite = DuelIcons.Ring;
                cue.raycastTarget = false;
            }
            bool on = left >= 0f && left <= 0.7f;
            if (cue.gameObject.activeSelf != on) cue.gameObject.SetActive(on);
            if (!on) return;
            cue.rectTransform.localScale = Vector3.one * (1.1f + Mathf.Clamp01(left / 0.7f) * 1.4f);
            cue.color = left <= window ? new Color(0.4f, 1f, 0.45f, 1f) : new Color(1f, 1f, 1f, 0.75f);
        }

        private Button tauntButton;
        private float tauntUntil;

        /// <summary>The small TAUNT button for <paramref name="seconds"/> (after the player knocked the rival down).</summary>
        public void ShowTaunt(float seconds)
        {
            if (tauntButton == null)
            {
                tauntButton = Flat.Button(root, Loc.T("taunt.button"), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(300f, 110f), new Color(0.95f, 0.55f, 0.3f), Flat.Text, () => { tauntButton.gameObject.SetActive(false); TauntPressed?.Invoke(); }, 36f);
                ((RectTransform)tauntButton.transform).pivot = new Vector2(0.5f, 0.5f);
            }
            tauntButton.gameObject.SetActive(true);
            tauntUntil = Time.unscaledTime + seconds;
        }

        private RectTransform tipPanel;
        private TextMeshProUGUI tipText;

        /// <summary>A line of guidance over the fight (the tutorial's helper talking; null: none).</summary>
        public void SetTip(string who, string text)
        {
            if (string.IsNullOrEmpty(text)) { if (tipPanel != null) tipPanel.gameObject.SetActive(false); return; }
            if (tipPanel == null)
            {
                tipPanel = Flat.Panel(root, "Tip", new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(940f, 150f), new Color(0.05f, 0.12f, 0.3f, 0.92f), 1.4f);
                tipPanel.pivot = new Vector2(0.5f, 1f);
                tipText = Flat.Label(tipPanel, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 130f), 40f, Flat.Text, TextAlignmentOptions.Center, bold: true, wrap: true);
                tipText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tipText.enableAutoSizing = true; tipText.fontSizeMin = 24f; tipText.fontSizeMax = 40f;
            }
            tipPanel.gameObject.SetActive(true);
            tipText.text = (string.IsNullOrEmpty(who) ? "" : "<color=#7FD8FF>" + who + ":</color> ") + text;
            tipPanel.localScale = Vector3.one * 1.08f;
            tipPop = 0f;
        }

        private float tipPop = 1f;

        /// <summary>The bar's own space (the tutorial's hand and words sit in it).</summary>
        public RectTransform Root => root;

        /// <summary>Where a button stands in <see cref="Root"/>'s space: 0 shield, 1 ultimate, 2 special, 3 the move that comes and goes, 4 heart.</summary>
        public Vector2 PointOf(int which)
        {
            var b = which switch { 0 => shield, 1 => ultimate, 2 => special, 3 => context, _ => heart };
            return root.InverseTransformPoint(b.Rect.position);
        }

        /// <summary>The shield is held (true) or not, as the game sees it (a key, or the button let go from outside).</summary>
        public bool ShieldHeld => shield.Held;

        /// <summary>Lets go of every held button (a round over, a big moment).</summary>
        public void ReleaseAll() => shield.Release();

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
            if (contextMove == move.Value && context.isActiveAndEnabled && contextSet) return;
            contextSet = true;
            contextMove = move.Value;
            context.SetIcon(move.Value switch { Move.Heavy => DuelIcon.Heavy, Move.Upper => DuelIcon.Upper, Move.Breaker => DuelIcon.Breaker, _ => DuelIcon.Finish });
            context.SetColour(move.Value switch { Move.Heavy => AttackOrange, Move.Upper => new Color(1f, 0.3f, 0.25f), Move.Breaker => BreakerBlue, _ => new Color(0.95f, 0.2f, 0.35f) });
            context.SetPulse(true);
            context.SetRing(1f, Color.white);
        }

        private bool contextSet;

        /// <summary>
        /// A finger on a button or in the dead ring round it (screen pixels): no blow, walk or swipe from it. The open
        /// emote row and the leave question count too.
        /// </summary>
        public bool IsOver(Vector2 screen)
        {
            var s = DuelControlsSettings.Current;
            foreach (var b in new[] { shield, ultimate, special, context, jump, heart, pause, talk })
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                var r = b.Rect;
                Vector2 c = RectTransformUtility.WorldToScreenPoint(null, r.position);
                float radius = (r.rect.width * 0.5f * Mathf.Abs(r.localScale.x) + s.Units(s.deadZoneDp)) * root.lossyScale.x;
                if ((screen - c).sqrMagnitude <= radius * radius) return true;
            }
            if (wheel.IsOver(screen)) return true;
            if (tauntButton != null && tauntButton.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)tauntButton.transform, screen, null)) return true;
            return leaveAsk != null && leaveAsk.gameObject.activeSelf;
        }

        // ---------- Leaving a live match ----------

        private void AskLeave()
        {
            if (leaveAsk == null)
            {
                leaveAsk = UiFactory.Stretch("LeaveAsk", root);
                var dim = UiFactory.Fill(leaveAsk, new Color(0.02f, 0.02f, 0.08f, 0.6f));
                dim.gameObject.AddComponent<IgnoreSafeArea>();
                var card = Flat.Panel(leaveAsk, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 420f), Flat.Card, 1.4f);
                card.pivot = new Vector2(0.5f, 0.5f);
                var q = Flat.Label(card, Loc.T("duel.leaveAsk"), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(740f, 150f), 46f, Flat.Text, TextAlignmentOptions.Center, bold: true, wrap: true);
                q.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var yes = Flat.Button(card, Loc.T("duel.leaveYes"), new Vector2(0.5f, 0f), new Vector2(-190f, 90f), new Vector2(340f, 110f), new Color(0.85f, 0.25f, 0.3f), Flat.Text, () => { leaveAsk.gameObject.SetActive(false); LeaveConfirmed?.Invoke(); }, 36f);
                ((RectTransform)yes.transform).pivot = new Vector2(0.5f, 0.5f);
                var no = Flat.Button(card, Loc.T("duel.leaveNo"), new Vector2(0.5f, 0f), new Vector2(190f, 90f), new Vector2(340f, 110f), Flat.CardHi, Flat.Text, () => leaveAsk.gameObject.SetActive(false), 36f);
                ((RectTransform)no.transform).pivot = new Vector2(0.5f, 0.5f);
            }
            leaveAsk.gameObject.SetActive(true);
            leaveAsk.SetAsLastSibling();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (tauntButton != null && tauntButton.gameObject.activeSelf && Time.unscaledTime > tauntUntil) tauntButton.gameObject.SetActive(false);
            if (tipPanel != null && tipPop < 1f) { tipPop = Mathf.Min(1f, tipPop + dt / 0.2f); tipPanel.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, tipPop); }
            if (comboShow > 0f)
            {
                comboShow -= dt;
                comboGroup.alpha = Mathf.Clamp01(comboShow / 0.35f);
                float pop = 1f + Mathf.Max(0f, comboShow - 1.2f) * 1.2f;
                comboText.rectTransform.localScale = new Vector3(pop, pop, 1f);
                if (comboShow <= 0f) { shownCombo = -1; comboText.color = new Color(1f, 0.82f, 0.25f); }
            }
        }
    }
}
