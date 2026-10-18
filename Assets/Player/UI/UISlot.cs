using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    public class UISlot : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;

        [SerializeField] private Color defaultColor;
        [SerializeField] private Color takenColor;
        [SerializeField] private Color selectedColor;

        private void Awake()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (iconImage != null)
            {
                iconImage.preserveAspect = true;
            }
        }

        public void UpdateUI(bool isTaken, bool isSelected, Sprite icon = null)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color =
                    isSelected ? selectedColor :
                    isTaken ? takenColor : defaultColor;
            }

            if (iconImage != null)
            {
                bool showIcon = isTaken && icon != null;
                iconImage.gameObject.SetActive(showIcon);
                if (showIcon)
                {
                    iconImage.sprite = icon;
                    iconImage.color = defaultColor;
                }
                else
                {
                    iconImage.sprite = null;
                }
            }
        }

        public void UpdateUI(bool isTaken, bool isSelected)
        {
            UpdateUI(isTaken, isSelected, null);
        }
    }
}
