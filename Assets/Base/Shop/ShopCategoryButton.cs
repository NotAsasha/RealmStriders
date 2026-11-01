using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Base.Shop
{
    public sealed class ShopCategoryButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Button control;
        [SerializeField] private Transform physicalButton;
        [SerializeField] private Renderer buttonRenderer;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Image statusPlate;
        [SerializeField] private int requiredRating;
        [SerializeField] private Vector3 pressOffset = new(0f, -.018f, 0f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock properties;
        private Vector3 restPosition;
        private bool pressed;
        private float depression;

        private void Awake()
        {
            if (properties != null) return;
            restPosition = physicalButton.localPosition;
            properties = new MaterialPropertyBlock();
        }

        public void Refresh(bool selected, bool available)
        {
            if (properties == null) Awake();
            Color color = !available ? new Color(.16f, .17f, .18f) :
                selected ? new Color(.2f, 1f, .3f) : new Color(.65f, .72f, .61f);
            buttonRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, color);
            properties.SetColor(EmissionColor, selected && available ? color * .65f : Color.black);
            buttonRenderer.SetPropertyBlock(properties);
            status.text = !available ? "LOCKED\nSTAR " + Mathf.CeilToInt(requiredRating / 3f) : selected ? "OPEN" : "READY";
            status.color = !available ? new Color(.95f, .95f, .95f) : new Color(.08f, .23f, .08f);
            if (statusPlate != null)
                statusPlate.color = !available ? new Color(.25f, .27f, .29f) : new Color32(192, 195, 199, 255);
            if (!available) pressed = false;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && control.IsInteractable()) pressed = true;
        }

        public void OnPointerUp(PointerEventData data) => pressed = false;
        public void OnPointerExit(PointerEventData data) => pressed = false;

        private void Update()
        {
            depression = Mathf.MoveTowards(depression, pressed ? 1f : 0f, Time.unscaledDeltaTime * 14f);
            physicalButton.localPosition = restPosition + pressOffset * depression;
        }

        private void OnDisable()
        {
            pressed = false;
            depression = 0f;
            if (properties != null) physicalButton.localPosition = restPosition;
        }
    }
}
