using UnityEngine;
using UnityEngine.EventSystems;

namespace Base.Interactables
{
    public class LeverHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Lever lever;
        [SerializeField] private bool cancelOnPointerExit = false;

        public void SetLever(Lever targetLever)
        {
            lever = targetLever;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (lever != null)
            {
                lever.StartHold();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (lever != null)
            {
                lever.StopHold();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (cancelOnPointerExit && lever != null)
            {
                lever.StopHold();
            }
        }

        private void OnDisable()
        {
            if (lever != null)
            {
                lever.StopHold();
            }
        }
    }
}
