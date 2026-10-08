using System;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.Journey
{
    /// <summary>
    /// The journey's landscape HUD (1920×1080 reference, scaled by height): the camera area under everything on the
    /// right, the movement stick on the left, the combat buttons in an arc at the bottom right (attack, jump, dash,
    /// slam, hammer throw), and the top bar (exit, health, objective, coins). The buttons sit above the camera area,
    /// so a finger landing on a button never turns the camera; each control keeps its own finger.
    /// </summary>
    public class JourneyHud
    {
        public Canvas Canvas { get; private set; }
        public TouchButton Attack, Jump, Dash, Slam, Throw;
        public TextMeshProUGUI CoinText, ObjectiveText;
        public Image HealthFill;
        public CanvasGroup EndPanel;
        private Image hurtFlash;
        private float hurt;

        public static JourneyHud Build(JourneyInput input, JourneyTuning tuning, Action exit, Action again)
        {
            var h = new JourneyHud();
            var canvas = UiFactory.CreateCanvas("JourneyHud", out var scaler);
            canvas.sortingOrder = 40;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            h.Canvas = canvas;
            var root = UiFactory.Stretch("SafeArea", canvas.transform);
            root.gameObject.AddComponent<SquashBot.UI.SafeArea>();
            var t = (Transform)root;

            // A red edge flash when hurt (never catches touches).
            var flash = UiFactory.Stretch("HurtFlash", canvas.transform);
            flash.SetAsFirstSibling();
            h.hurtFlash = UiFactory.Fill(flash, new Color(1f, 0.15f, 0.1f, 0f));
            h.hurtFlash.raycastTarget = false;

            // Order matters: later siblings are on top for touches.
            TouchLookArea.Create(t, input, tuning);
            TouchStick.Create(t, input, tuning);

            h.Attack = TouchButton.Create(t, "Attack", new Vector2(-230f, 230f), 250f, new Color(1f, 0.78f, 0.25f, 0.92f), input.PressAttack, input.ReleaseAttack);
            HammerIcon(h.Attack.Icon(0.62f), UiFactory.TextDark);
            h.Jump = TouchButton.Create(t, "Jump", new Vector2(-520f, 130f), 160f, new Color(0.35f, 0.6f, 1f, 0.85f), input.PressJump);
            Label(h.Jump.Icon(), "^", 90f);
            h.Dash = TouchButton.Create(t, "Dash", new Vector2(-500f, 360f), 150f, new Color(0.4f, 0.9f, 0.75f, 0.85f), input.PressDash);
            Label(h.Dash.Icon(), "»", 80f);
            h.Slam = TouchButton.Create(t, "Slam", new Vector2(-330f, 520f), 150f, new Color(1f, 0.5f, 0.3f, 0.85f), input.PressSkill1);
            var ring = h.Slam.Icon(0.55f);
            UiFactory.Fill(ring, Color.white, UiSprites.Ring, 0.35f).raycastTarget = false;
            h.Throw = TouchButton.Create(t, "Throw", new Vector2(-120f, 520f), 150f, new Color(0.7f, 0.55f, 1f, 0.85f), input.PressSkill2);
            HammerIcon(h.Throw.Icon(0.55f), Color.white);

            // Top bar.
            UiFactory.MakeButton(t, "X", UiFactory.ButtonKind.Icon, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(96f, 96f), () => exit?.Invoke(), 48f);
            var hp = UiFactory.Pill("Health", t, new Vector2(0f, 1f), new Vector2(146f, -40f), new Vector2(360f, 72f), UiFactory.PillColor);
            UIController.HeartIcon(hp, new Vector2(40f, 0f), 46f);
            UiFactory.Bar(hp, new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(260f, 22f), new Color(0.4f, 0.95f, 0.5f), out h.HealthFill);
            var coinPill = UiFactory.Pill("Coins", t, new Vector2(1f, 1f), new Vector2(-30f, -36f), new Vector2(230f, 84f), UiFactory.PillColor);
            UIController.CoinIcon(coinPill, new Vector2(50f, 0f));
            h.CoinText = UiFactory.TextBox("Value", coinPill, new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(124f, 76f), "0", 44f, Palette.UiGold, align: TextAlignmentOptions.Left);
            var obj = UiFactory.Pill("Objective", t, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(820f, 104f), new Color(0.06f, 0.1f, 0.08f, 0.6f));
            h.ObjectiveText = UiFactory.TextBox("Text", obj, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(790f, 96f), "", 32f, Color.white);
            h.ObjectiveText.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var end = UiFactory.Stretch("End", canvas.transform);
            UiFactory.Fill(end, new Color(0f, 0f, 0f, 0.85f));
            h.EndPanel = end.gameObject.AddComponent<CanvasGroup>();
            UiFactory.TextBox("Title", end, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1100f, 120f), Loc.T("forest.end"), 64f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(end, Loc.T("forest.again"), UiFactory.ButtonKind.Primary, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(520f, 130f), () => again?.Invoke(), 52f)
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(end, Loc.T("btn.menu"), UiFactory.ButtonKind.Secondary, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(520f, 110f), () => exit?.Invoke(), 44f)
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            h.ShowEnd(false);
            return h;
        }

        private static void Label(RectTransform icon, string text, float size)
        {
            var l = UiFactory.TextBox("Label", icon, new Vector2(0.5f, 0.5f), Vector2.zero, icon.sizeDelta, text, size, Color.white);
            l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            l.raycastTarget = false;
        }

        private static void HammerIcon(RectTransform icon, Color c)
        {
            float s = icon.sizeDelta.x / 150f;
            var handle = UiFactory.Box("Handle", icon, new Vector2(0.5f, 0.5f), new Vector2(-8f, -14f) * s, new Vector2(20f, 90f) * s);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.localRotation = Quaternion.Euler(0f, 0f, 35f);
            UiFactory.Fill(handle, c, UiSprites.Rounded, 8f).raycastTarget = false;
            var head = UiFactory.Box("Head", icon, new Vector2(0.5f, 0.5f), new Vector2(14f, 22f) * s, new Vector2(90f, 42f) * s);
            head.pivot = new Vector2(0.5f, 0.5f);
            head.localRotation = Quaternion.Euler(0f, 0f, 35f);
            UiFactory.Fill(head, c, UiSprites.Rounded, 8f).raycastTarget = false;
        }

        public void SetHealth(float v)
        {
            UiFactory.SetBar(HealthFill, v);
            HealthFill.color = v > 0.6f ? new Color(0.4f, 0.95f, 0.5f) : v > 0.3f ? new Color(1f, 0.82f, 0.3f) : new Color(1f, 0.38f, 0.38f);
        }

        public void FlashHurt() => hurt = 1f;

        public void Tick(float dt)
        {
            hurt = Mathf.MoveTowards(hurt, 0f, dt * 2.5f);
            hurtFlash.color = new Color(1f, 0.15f, 0.1f, hurt * 0.28f);
        }

        public void ShowEnd(bool on)
        {
            EndPanel.alpha = on ? 1f : 0f;
            EndPanel.blocksRaycasts = on;
            EndPanel.interactable = on;
        }

        public void SetVisible(bool on) => Canvas.gameObject.SetActive(on);

        public void Destroy()
        {
            if (Canvas != null) UnityEngine.Object.Destroy(Canvas.gameObject);
        }
    }
}
