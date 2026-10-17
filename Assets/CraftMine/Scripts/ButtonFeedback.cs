using UnityEngine;
using UnityEngine.EventSystems;

namespace CraftMine
{
    // Brief interaction feedback only: nothing keeps moving while the player plans a route.
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private UnityEngine.UI.Button button;
        private bool hovered, pressed, selected;

        private void Awake() { button = GetComponent<UnityEngine.UI.Button>(); }
        private void Update()
        {
            bool active = button.IsInteractable();
            float target = !active ? 1 : pressed ? .975f : hovered || selected ? 1.012f : 1;
            transform.localScale = Vector3.Lerp(transform.localScale,Vector3.one*target,
                1-Mathf.Exp(-24*Time.unscaledDeltaTime));
        }
        public void OnPointerEnter(PointerEventData data) { hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = pressed = false; }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && button.IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData data) { pressed = false; }
        public void OnSelect(BaseEventData data) { selected = true; }
        public void OnDeselect(BaseEventData data) { selected = pressed = false; }
        private void OnDisable() { hovered = pressed = selected = false; transform.localScale = Vector3.one; }
    }
}
