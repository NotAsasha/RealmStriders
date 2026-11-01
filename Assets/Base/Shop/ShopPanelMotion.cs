using System.Collections;
using UnityEngine;

namespace Base.Shop
{
    public sealed class ShopPanelMotion : MonoBehaviour
    {
        [SerializeField] private Transform mainPanel;
        [SerializeField] private Transform screen;
        [SerializeField] private CanvasGroup screenInput;
        [SerializeField] private Canvas shopCanvas;
        [SerializeField] private Canvas controlsCanvas;
        [SerializeField] private GameObject catalog;
        [SerializeField] private ItemCard[] cards;
        [SerializeField] private Vector3 panelTravel = new(0f, -1.3f, 0f);
        [SerializeField] private Vector3 screenTravel = new(0f, -.65f, 0f);
        [SerializeField, Min(.01f)] private float lowerDuration = .14f;
        [SerializeField, Min(.01f)] private float riseDuration = .34f;
        private Vector3 panelHome;
        private Vector3 screenHome;
        private float lowered;
        private Coroutine transition;
        private bool initialized;

        public bool IsTransitioning => transition != null;

        private void Awake()
        {
            panelHome = mainPanel.localPosition;
            screenHome = screen.localPosition;
            initialized = true;
        }

        private void LateUpdate() => controlsCanvas.worldCamera = shopCanvas.worldCamera;

        public void Show(GameObject page)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(ChangePage(page));
        }

        private IEnumerator ChangePage(GameObject page)
        {
            screenInput.interactable = false;
            screenInput.blocksRaycasts = false;
            screenInput.alpha = 0f;
            yield return MoveTo(1f, lowerDuration);
            foreach (ItemCard card in cards) card.gameObject.SetActive(false);
            catalog.SetActive(page == catalog);
            page.SetActive(true);
            screenInput.alpha = 1f;
            yield return MoveTo(0f, riseDuration);
            screenInput.interactable = true;
            screenInput.blocksRaycasts = true;
            transition = null;
        }

        private IEnumerator MoveTo(float target, float duration)
        {
            float start = lowered;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetPosition(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }
            SetPosition(target);
        }

        private void SetPosition(float amount)
        {
            lowered = amount;
            mainPanel.localPosition = panelHome + panelTravel * amount;
            screen.localPosition = screenHome + screenTravel * amount;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            if (transition != null) StopCoroutine(transition);
            transition = null;
            SetPosition(0f);
            screenInput.alpha = 1f;
            screenInput.interactable = true;
            screenInput.blocksRaycasts = true;
        }
    }
}
