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
    /// The close look: a shop item full screen, turned round by the finger (it keeps turning a little after a flick).
    /// Its model and picture exist only while it is open (<see cref="ItemPreview.CloseView"/>).
    /// </summary>
    public class ItemViewer : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private float spin;
        private int look;
        private bool held;

        /// <summary>Tapping <paramref name="picture"/> opens the close look over <paramref name="screen"/>.</summary>
        public static void OnTap(RawImage picture, Transform screen, string title, Func<Texture> open)
        {
            picture.raycastTarget = true;
            var b = picture.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Show(screen, title, open));
        }

        public static void Show(Transform screen, string title, Func<Texture> open)
        {
            AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.1f);
            var root = UiFactory.Stretch("Close look", screen);
            // its own layer over everything the screen has (the shop's cards included)
            var layer = root.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = 500;
            root.gameObject.AddComponent<GraphicRaycaster>();
            UiFactory.Fill(UiFactory.Stretch("Backdrop", root), UiKit.Style.backDeep).gameObject.AddComponent<IgnoreSafeArea>(); // (takes the finger: drags reach the viewer)
            var view = root.gameObject.AddComponent<ItemViewer>();

            var box = UiFactory.Box("Picture", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1040f, 1040f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var raw = box.gameObject.AddComponent<RawImage>();
            raw.texture = open();
            view.look = ItemPreview.ViewNumber;
            raw.raycastTarget = false; // (the whole screen takes the finger)

            UiFactory.TextBox("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(900f, 110f), title, 60f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.TextBox("Hint", root, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(900f, 70f), Loc.T("viewer.hint"), 36f, UiKit.A(UiKit.Style.white, 0.6f))
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(root, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-150f, -200f), new Vector2(124f, 124f), () =>
            {
                AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.1f);
                Destroy(view.gameObject);
            }, 60f);
        }

        public void OnBeginDrag(PointerEventData e) { spin = 0f; held = true; }
        public void OnEndDrag(PointerEventData e) => held = false;

        public void OnDrag(PointerEventData e)
        {
            // (in canvas-sized steps: the turn follows the finger the same on every screen)
            float k = 1920f / Mathf.Max(1f, Screen.height);
            float turn = -e.delta.x * k * 0.35f;
            ItemPreview.ViewYaw += turn;
            ItemPreview.ViewPitch = Mathf.Clamp(ItemPreview.ViewPitch + e.delta.y * k * 0.25f, -60f, 60f);
            spin = turn / Mathf.Max(0.005f, Time.unscaledDeltaTime); // (degrees a second, for the coast after letting go)
        }

        private void Update()
        {
            if (held) return;
            // Let go: it coasts to a stop.
            ItemPreview.ViewYaw += spin * Time.unscaledDeltaTime;
            spin = Mathf.MoveTowards(spin, 0f, Mathf.Max(60f, Mathf.Abs(spin) * 3f) * Time.unscaledDeltaTime);
        }

        // Its screen went: it goes too (the model and picture with it).
        private void OnDisable() => Destroy(gameObject);
        private void OnDestroy() => ItemPreview.CloseView(look); // (only its own: a new one may already be open)
    }
}
