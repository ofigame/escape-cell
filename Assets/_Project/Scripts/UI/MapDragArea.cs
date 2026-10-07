using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SquashBot.UI
{
    /// <summary>
    /// A see-through area that catches drags on the 3D map (the level buttons sit inside it, so a drag that starts on
    /// one still scrolls and doesn't count as a tap).
    /// </summary>
    public class MapDragArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Vertical drag in screen pixels (up = positive).</summary>
        public event Action<float> Dragged;
        public event Action Released;

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData.delta.y);

        public void OnEndDrag(PointerEventData eventData) => Released?.Invoke();
    }
}
