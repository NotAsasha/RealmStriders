using System;
using System.Collections;
using FileSystem.Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.Events;

namespace Steam
{
    public class LobbyUiManager : MonoBehaviour
    {
        [Header("UI Toggle")]
        [SerializeField] private Toggle friendsOnly;

        [Header("Menu pages")]
        [SerializeField] private GameObject mainPage;
        [SerializeField] private GameObject hostPage;
        [SerializeField] private GameObject joinPage;
        [SerializeField] private GameObject settingsPage;

        [Header("Save selection")]
        [SerializeField] private SaveSlot[] saveSlots = Array.Empty<SaveSlot>();
        [SerializeField] private Button createLobbyButton;
        [SerializeField] private TMP_Text hostStatus;
        [SerializeField] private UnityEvent onHostStarted = new();
        [SerializeField] private Color selectedSaveColor = new(0.40f, 0.30f, 0.20f);
        [SerializeField] private Color normalSaveColor = new(0.15f, 0.14f, 0.16f);
        [SerializeField] private GameObject fileManagementDialog;
        [SerializeField] private GameObject deleteConfirmationDialog;
        [SerializeField] private Selectable manageFilesButton;
        [SerializeField] private Selectable managementCancel;
        [SerializeField] private Selectable confirmationCancel;
        private int selectedSave = -1;

        [Serializable]
        private sealed class SaveSlot
        {
            public LoadSaveButton source;
            public Image background;
            public TMP_Text selectionLabel;
        }

        public void SelectSave(int index)
        {
            if (index < 0 || index >= saveSlots.Length || saveSlots[index].source.gameSave == null) return;
            selectedSave = index;
            RefreshSaveSelection();
        }

        private void RefreshSaveSelection()
        {
            bool valid = selectedSave >= 0 && selectedSave < saveSlots.Length &&
                         saveSlots[selectedSave].source.gameSave != null;
            for (int i = 0; i < saveSlots.Length; i++)
            {
                bool selected = valid && i == selectedSave;
                saveSlots[i].background.color = selected ? selectedSaveColor : normalSaveColor;
                saveSlots[i].selectionLabel.text = selected ? "SELECTED" : "SELECT";
            }
            if (createLobbyButton != null) createLobbyButton.interactable = valid;
            if (hostStatus != null) hostStatus.text = valid
                ? "Save " + (selectedSave + 1).ToString("00") + " selected."
                : "Select a save to create a lobby.";
        }

        public void OpenFileManagement()
        {
            if (transition != null || !hostPage.activeInHierarchy) return;
            SetHostInteraction(false);
            fileManagementDialog.SetActive(true);
            managementCancel.Select();
        }

        public void RequestDeleteFiles()
        {
            if (!fileManagementDialog.activeInHierarchy) return;
            fileManagementDialog.SetActive(false);
            deleteConfirmationDialog.SetActive(true);
            confirmationCancel.Select();
        }

        public void CloseFileManagement()
        {
            if (fileManagementDialog != null) fileManagementDialog.SetActive(false);
            if (deleteConfirmationDialog != null) deleteConfirmationDialog.SetActive(false);
            SetHostInteraction(true);
            if (manageFilesButton != null && hostPage.activeInHierarchy) manageFilesButton.Select();
        }

        private void SetHostInteraction(bool enabled)
        {
            foreach (PagePresentation page in pagePresentations)
                if (page.page == hostPage) page.group.interactable = enabled;
        }

        [Header("Page transitions")]
        [SerializeField] private PagePresentation[] pagePresentations = Array.Empty<PagePresentation>();
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.32f;
        [SerializeField, Min(0f)] private float titleTravel = 24f;

        [Serializable]
        private sealed class PagePresentation
        {
            public GameObject page;
            public CanvasGroup group;
            public RectTransform title;
            public Selectable firstSelection;
            [NonSerialized] public Vector2 titlePosition;
        }

        private Coroutine transition;
        private GameObject destination;
        private bool presentationInitialized;

        private void InitializePresentation()
        {
            if (presentationInitialized) return;
            foreach (PagePresentation page in pagePresentations)
                if (page.title != null) page.titlePosition = page.title.anchoredPosition;
            presentationInitialized = true;
        }

        public void ShowMainMenu() => ShowPage(mainPage);
        public void ShowHostLobby() => ShowPage(hostPage);
        public void ShowJoinLobby() => ShowPage(joinPage);
        public void ShowSettings() => ShowPage(settingsPage);

        private void ShowPage(GameObject page)
        {
            if (page == null || transition != null) return;
            CloseFileManagement();
            InitializePresentation();
            if (Application.isPlaying && isActiveAndEnabled && pagePresentations.Length > 0)
            {
                if (page.activeSelf) return;
                destination = page;
                transition = StartCoroutine(TransitionTo(page));
                return;
            }
            SetPageImmediately(page);
        }

        private void SetPageImmediately(GameObject page)
        {
            mainPage.SetActive(page == mainPage);
            hostPage.SetActive(page == hostPage);
            joinPage.SetActive(page == joinPage);
            settingsPage.SetActive(page == settingsPage);
            foreach (PagePresentation presentation in pagePresentations)
            {
                presentation.group.alpha = 1f;
                presentation.group.interactable = true;
                presentation.group.blocksRaycasts = true;
                if (presentation.title != null)
                    presentation.title.anchoredPosition = presentation.titlePosition;
            }
        }

        private IEnumerator TransitionTo(GameObject target)
        {
            PagePresentation incoming = null;
            foreach (PagePresentation page in pagePresentations)
            {
                page.group.interactable = false;
                page.group.blocksRaycasts = false;
                if (page.page == target)
                {
                    incoming = page;
                    page.group.alpha = 0f;
                    page.page.SetActive(true);
                }
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, transitionDuration);
            while (elapsed < duration)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float incomingProgress = Mathf.SmoothStep(0f, 1f, progress);
                float outgoingProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 1.6f));
                foreach (PagePresentation page in pagePresentations)
                {
                    bool isIncoming = page == incoming;
                    page.group.alpha = isIncoming ? incomingProgress : 1f - outgoingProgress;
                    if (page.title != null && page.page.activeSelf)
                    {
                        float offset = isIncoming ? (1f - incomingProgress) * titleTravel : -outgoingProgress * titleTravel * .5f;
                        page.title.anchoredPosition = page.titlePosition + Vector2.right * offset;
                    }
                }
                // Unscaled time keeps UI responsive if gameplay has paused the clock.
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            SetPageImmediately(target);
            transition = null;
            destination = null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target == mainPage
                    ? null : incoming?.firstSelection?.gameObject);
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private SteamManager steamManager;

        private void Awake()
        {
            UnlockCursor();
        }

        private void OnEnable()
        {
            UnlockCursor();
            InitializePresentation();
        }

        private void OnDisable()
        {
            if (transition == null) return;
            StopCoroutine(transition);
            transition = null;
            if (destination != null) SetPageImmediately(destination);
            destination = null;
        }

        public void Start()
        {
            UnlockCursor();
            steamManager = SteamManager.Instance;
            RefreshSaveSelection();
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CreateLobbyButton()
        {
            if (selectedSave < 0 || selectedSave >= saveSlots.Length ||
                saveSlots[selectedSave].source.gameSave == null)
            {
                RefreshSaveSelection();
                return;
            }
            if (GameManager.Instance == null || NetworkManager.Singleton == null)
            {
                if (hostStatus != null) hostStatus.text = "Game services are not ready.";
                return;
            }
            if (NetworkManager.Singleton.IsListening) return;
            if (steamManager == null)
            {
                steamManager = SteamManager.Instance;
            }
            if (steamManager == null)
            {
                Debug.LogError("[LobbyUiManager] SteamManager instance is not available.");
                return;
            }

            try
            {
                GameManager.Instance.currentSave = saveSlots[selectedSave].source.gameSave;
                bool isFriendsOnly = friendsOnly != null && friendsOnly.isOn;
                steamManager.StartHost(6, isFriendsOnly);
                if (NetworkManager.Singleton.IsHost) onHostStarted.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyUiManager] Error starting host: {ex}");
                throw;
            }
        }

        public void DeleteFiles()
        {
            if (deleteConfirmationDialog == null || !deleteConfirmationDialog.activeInHierarchy) return;
            if (GameFileHandler.Instance == null) return;
            GameFileHandler.Instance.DeleteAvaible();
            selectedSave = -1;
            if (GameManager.Instance != null) GameManager.Instance.currentSave = null;
            foreach (SaveSlot slot in saveSlots) slot.source.RefreshText();
            RefreshSaveSelection();
            CloseFileManagement();
        }
    }
} 
