using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIControlSound : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        [SerializeField] private Selectable control;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (control is Button || eventData.button != PointerEventData.InputButton.Left) return;
            if (CanInteract()) UIAudio.PlayClick();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!(control is Button) && CanInteract()) UIAudio.PlayClick();
        }

        // First persistent Button callback, before navigation can disable or destroy the button.
        public void PlayClick() => UIAudio.PlayClick();

        private bool CanInteract() => control.IsActive() && control.IsInteractable();
    }
}
