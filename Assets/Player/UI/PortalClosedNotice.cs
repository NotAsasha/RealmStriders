using UnityEngine;

namespace Player.UI
{
    public sealed class PortalClosedNotice : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField, Min(1f)] private float displaySeconds = 6f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.4f;
        private float elapsed;
        private bool visible;
        private Vector2 restingPosition;

        private void Awake()
        {
            restingPosition = panel.anchoredPosition;
            Hide();
        }

        public void Show()
        {
            elapsed = 0f;
            visible = true;
            group.alpha = 0f;
            enabled = true;
        }

        public void Hide()
        {
            visible = false;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            panel.anchoredPosition = restingPosition;
            enabled = false;
        }

        private void OnDisable()
        {
            if (visible) Hide();
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float fade = Mathf.Min(fadeSeconds, displaySeconds * 0.5f);
            float entrance = Mathf.Clamp01(elapsed / fade);
            group.alpha = Mathf.Min(entrance, Mathf.Clamp01((displaySeconds - elapsed) / fade));
            panel.anchoredPosition = restingPosition + Vector2.up * (12f * (1f - entrance));
            if (elapsed >= displaySeconds) Hide();
        }
    }
}
