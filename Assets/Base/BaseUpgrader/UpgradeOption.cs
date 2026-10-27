using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Base.BaseUpgrader
{
    public sealed class UpgradeOption : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Color errorHoverColor = new(.95f, .55f, .48f);
        private Color normalColor = new(.60f, .61f, .63f);
        private bool blocked;
        private bool hovered;

        public void SetStatus(bool isBlocked, Color color)
        {
            blocked = isBlocked;
            normalColor = color;
            RefreshColor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            RefreshColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            RefreshColor();
        }

        private void OnDisable()
        {
            hovered = false;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (statusLabel != null) statusLabel.color = blocked && hovered ? errorHoverColor : normalColor;
        }
    }
}
