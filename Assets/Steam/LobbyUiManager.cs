using System;
using System.Collections;
using FileSystem.Scripts;
using System.Collections.Generic;
using Steamworks;
using Lobby = Steamworks.Data.Lobby;
using UnityEngine;
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
        [SerializeField] private TMP_Text createLobbyLabel;
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
        private int pendingDelete = -1;
        [SerializeField] private TMP_Text deleteConfirmationTitle;

        [Header("Lobby browser")]
        [SerializeField] private Button refreshLobbiesButton;
        [SerializeField] private ScrollRect lobbyList;
        [SerializeField] private TMP_Text lobbyListTitle;
        [SerializeField] private TMP_Text joinStatus;

        private const float LobbyRowHeight = 60f;
        private const float LobbyRowPitch = 68f;
        private readonly List<Button> lobbyButtons = new();
        private bool hostStarting;
        private bool hostSingleplayer;
        private bool lobbiesRefreshing;
        private bool lobbyConnecting;

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

        private SaveFile SelectedSave => selectedSave >= 0 && selectedSave < saveSlots.Length
            ? saveSlots[selectedSave].source.gameSave
            : null;

        private void RefreshSaveSelection()
        {
            SaveFile save = SelectedSave;
            for (int i = 0; i < saveSlots.Length; i++)
            {
                bool selected = save != null && i == selectedSave;
                saveSlots[i].background.color = selected ? selectedSaveColor : normalSaveColor;
                saveSlots[i].selectionLabel.text = selected ? "SELECTED" : "SELECT";
            }

            hostSingleplayer = steamManager != null && !steamManager.IsSteamActive;
            friendsOnly.gameObject.SetActive(!hostSingleplayer);
            createLobbyLabel.text = hostSingleplayer ? "PLAY SOLO" : "CREATE LOBBY";
            createLobbyButton.interactable = save != null && !save.IsLoadBlocked && !hostStarting;
            if (save == null)
                hostStatus.text = hostSingleplayer ? "Steam is offline. Select a save to play solo." : "Select a save to create a lobby.";
            else if (save.IsLoadBlocked)
                hostStatus.text = "This save is unavailable. Choose another slot or delete this save.";
            else
                hostStatus.text = hostSingleplayer ? "Steam is offline. This session will be solo." : "";
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
            RequestDeleteSave(selectedSave);
        }

        public void RequestDeleteSave(int index)
        {
            if (transition != null || !hostPage.activeInHierarchy ||
                deleteConfirmationDialog.activeSelf || index < 0 || index >= saveSlots.Length ||
                saveSlots[index].source.gameSave == null) return;
            pendingDelete = index;
            SetHostInteraction(false);
            fileManagementDialog.SetActive(false);
            deleteConfirmationTitle.text = "DELETE SAVE " + (index + 1).ToString("00") + "?";
            deleteConfirmationDialog.SetActive(true);
            confirmationCancel.Select();
        }

        public void CloseFileManagement()
        {
            pendingDelete = -1;
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
        public void ShowHostLobby()
        {
            RefreshSaveSelection();
            ShowPage(hostPage);
        }

        public void ShowJoinLobby()
        {
            ShowPage(joinPage);
            if (joinPage.activeInHierarchy) RefreshJoinLobbies();
        }

        public void ShowSettings() => ShowPage(settingsPage);
        public void StartTutorial() => Game.Tutorial.TutorialSession.Open();

        private void ShowPage(GameObject page)
        {
            if (page == null || transition != null || hostStarting || lobbyConnecting) return;
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
            refreshLobbiesButton.onClick.AddListener(RefreshJoinLobbies);
            RefreshSaveSelection();
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public async void CreateLobbyButton()
        {
            if (hostStarting) return;
            SaveFile save = SelectedSave;
            if (save == null || save.IsLoadBlocked)
            {
                RefreshSaveSelection();
                return;
            }

            var game = GameManager.Instance;
            if (game == null || steamManager == null)
            {
                hostStatus.text = "Game services are not ready. Please try again.";
                return;
            }

            hostStarting = true;
            SetHostInteraction(false);
            createLobbyButton.interactable = false;
            hostStatus.text = hostSingleplayer ? "Starting solo play..." : "Creating Steam lobby...";
            SaveFile previousSave = game.currentSave;
            game.currentSave = save;
            string error = await steamManager.StartHostAsync(friendsOnly.isOn, hostSingleplayer);
            if (error != null && game != null && game.currentSave == save)
                game.currentSave = previousSave;
            if (this == null) return;

            hostStarting = false;
            SetHostInteraction(true);
            if (error == null)
            {
                onHostStarted.Invoke();
                return;
            }

            RefreshSaveSelection();
            hostStatus.text = error;
            createLobbyButton.Select();
        }

        public async void RefreshJoinLobbies()
        {
            if (lobbiesRefreshing || lobbyConnecting) return;
            ClearLobbyButtons();
            if (steamManager == null)
            {
                SetJoinFeedback("Lobby browser unavailable.", "Game services are not ready. Try Refresh again.");
                return;
            }
            if (!steamManager.IsSteamActive)
            {
                SetJoinFeedback("Steam is offline.", "Connect to Steam to refresh, or return to Host to play solo.");
                return;
            }

            lobbiesRefreshing = true;
            refreshLobbiesButton.interactable = false;
            SetJoinFeedback("Searching for lobbies...", "");
            var (lobbies, error) = await steamManager.RefreshLobbies();
            if (this == null) return;

            lobbiesRefreshing = false;
            refreshLobbiesButton.interactable = true;
            if (error != null)
            {
                SetJoinFeedback("Refresh failed.", error);
                return;
            }
            if (lobbies.Length == 0)
            {
                SetJoinFeedback("No public lobbies found.",
                    "Refresh to check again. Accept a Steam invite to join a friends-only lobby.");
                return;
            }

            foreach (Lobby lobby in lobbies) AddLobbyButton(lobby);
            lobbyList.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lobbies.Length * LobbyRowPitch);
            Canvas.ForceUpdateCanvases();
            lobbyList.verticalNormalizedPosition = 1f;
            SetJoinFeedback(null, lobbies.Length > 5 ? "Scroll to see more public lobbies." : "");
        }

        private void ClearLobbyButtons()
        {
            foreach (Button button in lobbyButtons)
            {
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
            lobbyButtons.Clear();
            lobbyList.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
        }

        private void AddLobbyButton(Lobby lobby)
        {
            var row = new GameObject("Lobby Result", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)row.transform;
            rect.SetParent(lobbyList.content, worldPositionStays: false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -lobbyButtons.Count * LobbyRowPitch);
            rect.sizeDelta = new Vector2(0f, LobbyRowHeight);

            var background = row.GetComponent<Image>();
            background.color = new Color(0.30f, 0.31f, 0.34f, 0.74f);
            var button = row.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.77f, 0.77f, 0.77f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            button.colors = colors;
            button.onClick.AddListener(() => JoinLobby(lobby.Id, button));
            lobbyButtons.Add(button);

            AddLobbyColumn(rect, SteamManager.GetLobbyName(lobby), 24f, 1150f);
            AddLobbyColumn(rect, $"{lobby.MemberCount}/{lobby.MaxMembers}", 1194f, 180f);
            AddLobbyColumn(rect, "JOIN", 1414f, 180f);
        }

        private void AddLobbyColumn(RectTransform row, string value, float offset, float width)
        {
            var column = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)column.transform;
            rect.SetParent(row, worldPositionStays: false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.up;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(offset, 0f);
            rect.sizeDelta = new Vector2(width, 0f);

            var label = column.GetComponent<TextMeshProUGUI>();
            label.font = joinStatus.font;
            label.fontSize = joinStatus.fontSize;
            label.color = lobbyListTitle.color;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = false;
            label.raycastTarget = false;
            label.text = value;
        }

        private void SetJoinFeedback(string title, string detail)
        {
            lobbyListTitle.gameObject.SetActive(title != null);
            lobbyListTitle.text = title ?? "";
            joinStatus.text = detail;
        }

        private async void JoinLobby(SteamId id, Button source)
        {
            if (lobbyConnecting || lobbiesRefreshing) return;
            SetJoinInteraction(false);
            SetJoinFeedback(null, "Joining Steam lobby...");
            string error = await steamManager.TryConnectLobby(id);
            if (this == null) return;

            if (error == null)
            {
                SetJoinFeedback(null, "Connected. Waiting for the host...");
                return;
            }

            SetJoinFeedback(null, error);
            SetJoinInteraction(true);
            source.Select();
        }

        private void SetJoinInteraction(bool enabled)
        {
            lobbyConnecting = !enabled;
            foreach (PagePresentation page in pagePresentations)
            {
                if (page.page == joinPage) page.group.interactable = enabled;
            }
        }

        public void DeleteFiles()
        {
            if (deleteConfirmationDialog == null || !deleteConfirmationDialog.activeInHierarchy) return;
            if (pendingDelete < 0 || pendingDelete >= saveSlots.Length) return;
            SaveFile save = saveSlots[pendingDelete].source.gameSave;
            if (save == null) return;
            try
            {
                save.Delete();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not delete save {pendingDelete + 1}: {ex}");
                CloseFileManagement();
                if (hostStatus != null) hostStatus.text = "Could not delete the save. Please try again.";
                return;
            }
            if (selectedSave == pendingDelete) selectedSave = -1;
            if (GameManager.Instance != null && GameManager.Instance.currentSave == save)
                GameManager.Instance.currentSave = null;
            saveSlots[pendingDelete].source.RefreshText();
            RefreshSaveSelection();
            CloseFileManagement();
        }
    }
}
