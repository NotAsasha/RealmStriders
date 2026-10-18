using Steamworks;
using UnityEngine;

namespace Steam
{
    public class SteamOfflineIndicator : MonoBehaviour
    {
        [Tooltip("Об'єкт (наприклад, текст 'Offline mode'), який активується, коли Steam НЕ запущений.")]
        [SerializeField] private GameObject offlineObject;

        [Tooltip("Опціональний об'єкт, який активується, коли Steam підключено.")]
        [SerializeField] private GameObject onlineObject;

        private void Start()
        {
            UpdateIndicatorState();
        }

        public void UpdateIndicatorState()
        {
            bool isSteamActive = SteamManager.Instance != null
                ? SteamManager.Instance.IsSteamActive
                : SteamClient.IsValid;

            GameObject targetOffline = offlineObject != null ? offlineObject : gameObject;
            targetOffline.SetActive(!isSteamActive);

            if (onlineObject != null)
            {
                onlineObject.SetActive(isSteamActive);
            }
        }
    }
}
