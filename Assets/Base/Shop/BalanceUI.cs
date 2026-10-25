using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Base.Shop
{
    public class BalanceUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text textOutput;

        private bool isSubscribed;
        private Coroutine syncCheckRoutine;

        private void OnEnable()
        {
            Subscribe();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
            }

            RefreshUI();

            if (syncCheckRoutine != null)
            {
                StopCoroutine(syncCheckRoutine);
            }
            syncCheckRoutine = StartCoroutine(SyncCheckRoutine());
        }

        private void Start()
        {
            RefreshUI();
        }

        private void OnDisable()
        {
            if (syncCheckRoutine != null)
            {
                StopCoroutine(syncCheckRoutine);
                syncCheckRoutine = null;
            }

            Unsubscribe();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete)
            {
                RefreshUI();
            }
        }

        private IEnumerator SyncCheckRoutine()
        {
            // Initial sync (ReadField) in Netcode does not trigger OnValueChanged.
            // Check periodically during early connection frames to guarantee true balance is shown.
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.5f);
                RefreshUI();
            }
            syncCheckRoutine = null;
        }

        public void RefreshUI()
        {
            Subscribe();
            if (GameManager.Instance != null)
            {
                UpdateUI(0, GameManager.Instance.teamMoney.Value);
            }
        }

        private void Subscribe()
        {
            if (!isSubscribed && GameManager.Instance != null)
            {
                GameManager.Instance.teamMoney.OnValueChanged += UpdateUI;
                isSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (isSubscribed && GameManager.Instance != null)
            {
                GameManager.Instance.teamMoney.OnValueChanged -= UpdateUI;
                isSubscribed = false;
            }
        }

        private void UpdateUI(int _, int money)
        {
            if (!textOutput)
            {
                Debug.LogError("---BalanceUI: textOutput not set.");
                return;
            }
            textOutput.text = money.ToString();
        }
    }
}