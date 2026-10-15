using UnityEngine;
using UnityEngine.EventSystems;

namespace Accertone.UI
{
    /// <summary>Tracks pointer / navigation state so visuals can animate between idle, hover, selected and pressed.</summary>
    public class UIHoverState : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        public bool Hovered { get; private set; }
        public bool Pressed { get; private set; }
        public bool Selected { get; private set; }
        public bool Highlighted { get { return Hovered || Selected; } }

        public void OnPointerEnter(PointerEventData eventData) { Hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { Hovered = false; Pressed = false; }
        public void OnPointerDown(PointerEventData eventData) { Pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { Pressed = false; }
        public void OnSelect(BaseEventData eventData) { Selected = true; }
        public void OnDeselect(BaseEventData eventData) { Selected = false; }

        void OnDisable()
        {
            Hovered = Pressed = Selected = false;
        }
    }
}
