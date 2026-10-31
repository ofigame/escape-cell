using UnityEngine;
using UnityEngine.EventSystems;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// A duel button read the moment the finger lands (a strike never waits for the finger to lift): pressed this
    /// frame, and held (the shield) until the finger lifts or slides off.
    /// </summary>
    public class HoldButtonV2 : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }
        private bool pressed;

        /// <summary>Pressed since last asked (once per press).</summary>
        public bool TakePress() { bool p = pressed; pressed = false; return p; }

        public void OnPointerDown(PointerEventData e) { Held = true; pressed = true; }
        public void OnPointerUp(PointerEventData e) => Held = false;
        public void OnPointerExit(PointerEventData e) { if (!e.pointerPress) return; Held = false; }
        private void OnDisable() { Held = false; pressed = false; }
    }
}
