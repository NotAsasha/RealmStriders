using Player.Movement;
using Player.Network;
using Steam;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Player.UI
{
    public class PauseUIManager : MonoBehaviour
    {
        public GameObject pauseMenu;
        public GameObject settingsMenu;
        public Transform cardsParent;
        public GameObject playerCard;
        [SerializeField] private GameObject pauseContent;
        [SerializeField] private CanvasGroup pauseCanvasGroup;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.24f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.16f;

        private Coroutine pauseTransition;
        private bool isMenuOpen;

        private void Awake()
        {
            ResetPresentation();
        }

        public void OpenSettings()
        {
            pauseContent.SetActive(false);
            settingsMenu.SetActive(true);
        }

        public void CloseSettings()
        {
            settingsMenu.SetActive(false);
            pauseContent.SetActive(true);
        }

        public void Resume()
        {
            SetMenuOpen(false);
            if (PlayerMovement.Instance != null)
            {
                PlayerMovement.Instance.isPaused = false;
                PlayerMovement.Instance.SwitchToGameplayControls();
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private readonly List<PlayerCard> playerCards = new();
        private readonly HashSet<SteamId> removedMemberSteamIds = new();
        private bool isSubscribed;
        private bool isInputSubscribed;
        private bool isUpdating;
        private bool refreshRequested;

        private void Start()
        {
            SubscribeEvents();
            RefreshPauseUI();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            if (pauseTransition != null)
            {
                StopCoroutine(pauseTransition);
                pauseTransition = null;
            }
            ResetPresentation();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (!isSubscribed)
            {
                isSubscribed = true;

                if (SteamClient.IsValid)
                {
                    SteamMatchmaking.OnLobbyMemberLeave += HandleLobbyMemberLeave;
                    SteamMatchmaking.OnLobbyMemberDisconnected += HandleLobbyMemberDisconnected;
                    SteamMatchmaking.OnLobbyMemberKicked += HandleLobbyMemberKicked;
                    SteamMatchmaking.OnLobbyMemberBanned += HandleLobbyMemberBanned;
                    SteamMatchmaking.OnLobbyMemberJoined += HandleLobbyMemberJoined;
                    SteamMatchmaking.OnLobbyDataChanged += HandleLobbyDataChanged;
                }

                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback += HandleNetworkClientDisconnected;
                    NetworkManager.Singleton.OnClientConnectedCallback += HandleNetworkClientConnected;
                }

                VoiceVolumeManager.OnPlayerUnregistered += HandleVoicePlayerUnregistered;
            }

            SubscribeInput();
        }

        private void UnsubscribeEvents()
        {
            if (isSubscribed)
            {
                isSubscribed = false;

                if (SteamClient.IsValid)
                {
                    SteamMatchmaking.OnLobbyMemberLeave -= HandleLobbyMemberLeave;
                    SteamMatchmaking.OnLobbyMemberDisconnected -= HandleLobbyMemberDisconnected;
                    SteamMatchmaking.OnLobbyMemberKicked -= HandleLobbyMemberKicked;
                    SteamMatchmaking.OnLobbyMemberBanned -= HandleLobbyMemberBanned;
                    SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
                    SteamMatchmaking.OnLobbyDataChanged -= HandleLobbyDataChanged;
                }

                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleNetworkClientDisconnected;
                    NetworkManager.Singleton.OnClientConnectedCallback -= HandleNetworkClientConnected;
                }

                VoiceVolumeManager.OnPlayerUnregistered -= HandleVoicePlayerUnregistered;
            }

            UnsubscribeInput();
        }

        private void SubscribeInput()
        {
            if (isInputSubscribed) return;
            if (PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
            {
                PlayerMovement.Instance.controls.System.Pause.performed += UpdateMenuState;
                isInputSubscribed = true;
            }
        }

        private void UnsubscribeInput()
        {
            if (!isInputSubscribed) return;
            if (PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
            {
                PlayerMovement.Instance.controls.System.Pause.performed -= UpdateMenuState;
            }
            isInputSubscribed = false;
        }

        private void HandleLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            if (SteamClient.IsValid && friend.Id == SteamClient.SteamId) return;
            OnMemberRemoved(friend.Id);
        }

        private void HandleLobbyMemberDisconnected(Lobby lobby, Friend friend)
        {
            if (SteamClient.IsValid && friend.Id == SteamClient.SteamId) return;
            OnMemberRemoved(friend.Id);
        }

        private void HandleLobbyMemberKicked(Lobby lobby, Friend target, Friend kicker)
        {
            if (SteamClient.IsValid && target.Id == SteamClient.SteamId) return;
            OnMemberRemoved(target.Id);
        }

        private void HandleLobbyMemberBanned(Lobby lobby, Friend target, Friend banner)
        {
            if (SteamClient.IsValid && target.Id == SteamClient.SteamId) return;
            OnMemberRemoved(target.Id);
        }

        private void HandleVoicePlayerUnregistered(ulong clientId, SteamId steamId)
        {
            if (SteamClient.IsValid && steamId == SteamClient.SteamId) return;
            OnMemberRemoved(steamId);
        }

        private void HandleNetworkClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId) return;

            if (VoiceVolumeManager.TryGetSteamId(clientId, out SteamId steamId))
            {
                OnMemberRemoved(steamId);
            }
            else
            {
                RefreshPauseUI();
            }
        }

        private void HandleLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            removedMemberSteamIds.Remove(friend.Id);
            RefreshPauseUI();
        }

        private void HandleLobbyDataChanged(Lobby lobby)
        {
            RefreshPauseUI();
        }

        private void HandleNetworkClientConnected(ulong clientId)
        {
            RefreshPauseUI();
        }

        private void OnMemberRemoved(SteamId steamId)
        {
            removedMemberSteamIds.Add(steamId);
            RemoveCardBySteamId(steamId);
            RefreshPauseUI();
        }

        private void RemoveCardBySteamId(SteamId steamId)
        {
            for (int i = playerCards.Count - 1; i >= 0; i--)
            {
                var card = playerCards[i];
                if (card == null)
                {
                    playerCards.RemoveAt(i);
                    continue;
                }

                if (card.linkedPlayer != null && card.linkedPlayer.playerSteamId == steamId)
                {
                    playerCards.RemoveAt(i);
                    if (card.gameObject != null)
                    {
                        Destroy(card.gameObject);
                    }
                }
            }

            if (cardsParent != null)
            {
                var remainingChildCards = cardsParent.GetComponentsInChildren<PlayerCard>(true);
                foreach (var c in remainingChildCards)
                {
                    if (c != null && c.linkedPlayer != null && c.linkedPlayer.playerSteamId == steamId)
                    {
                        playerCards.Remove(c);
                        Destroy(c.gameObject);
                    }
                }
            }
        }

        private void UpdateMenuState(InputAction.CallbackContext obj)
        {
            if (PlayerMovement.Instance != null && PlayerMovement.Instance.isInInteraction)
            {
                if (CameraMovement.Instance != null)
                {
                    CameraMovement.Instance.StopInteraction();
                }
                return;
            }

            if (pauseMenu == null) return;

            SetMenuOpen(!isMenuOpen);
        }

        private void SetMenuOpen(bool opening)
        {
            if (isMenuOpen == opening) return;
            isMenuOpen = opening;

            if (pauseTransition != null)
            {
                StopCoroutine(pauseTransition);
                pauseTransition = null;
            }

            if (opening)
            {
                pauseContent.SetActive(true);
                settingsMenu.SetActive(false);
                pauseMenu.SetActive(true);
                RefreshPauseUI();
            }

            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = opening;
            pauseTransition = StartCoroutine(FadePauseMenu(opening));
        }

        private IEnumerator FadePauseMenu(bool opening)
        {
            float startAlpha = pauseCanvasGroup.alpha;
            float targetAlpha = opening ? 1f : 0f;
            // Reverse from the visible opacity without restarting a full-length transition.
            float duration = (opening ? fadeInDuration : fadeOutDuration) * Mathf.Abs(targetAlpha - startAlpha);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                pauseCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            pauseCanvasGroup.alpha = targetAlpha;
            pauseCanvasGroup.interactable = opening;
            if (!opening) ResetPresentation();
            pauseTransition = null;
        }

        private void ResetPresentation()
        {
            isMenuOpen = false;
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;
            pauseMenu.SetActive(false);
            settingsMenu.SetActive(false);
            pauseContent.SetActive(true);
        }

        public void UpdatePauseUI(Lobby lobby) => RefreshPauseUI();

        public async void RefreshPauseUI()
        {
            if (isUpdating)
            {
                refreshRequested = true;
                return;
            }

            isUpdating = true;
            try
            {
                do
                {
                    refreshRequested = false;
                    await UpdatePauseUIInternal();
                } while (refreshRequested);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PauseUIManager] Error updating pause UI: {ex.Message}");
            }
            finally
            {
                isUpdating = false;
            }
        }

        private async Task UpdatePauseUIInternal()
        {
            if (SteamManager.Instance == null) return;

            List<SteamPlayer> playerList = await SteamManager.Instance.GetLobbyMembersAsync();

            if (this == null || cardsParent == null) return;

            // Remove destroyed cards from list
            playerCards.RemoveAll(c => c == null || c.gameObject == null);

            // Set each card as inactive initially
            foreach (var card in playerCards)
            {
                card.isFoundInLobby = false;
            }

            var currentLobby = SteamManager.Instance.CurrentLobby;

            foreach (SteamPlayer player in playerList)
            {
                // If member was explicitly marked removed, don't re-add
                if (removedMemberSteamIds.Contains(player.playerSteamId))
                {
                    continue;
                }

                PlayerCard existingCard = playerCards.Find(c => c != null && c.linkedPlayer != null && c.linkedPlayer.playerSteamId == player.playerSteamId);

                if (existingCard != null)
                {
                    existingCard.isFoundInLobby = true;
                    existingCard.Setup(player, currentLobby);
                }
                else
                {
                    if (playerCard == null) continue;

                    var cardObj = Instantiate(playerCard, cardsParent);
                    if (cardObj.TryGetComponent<PlayerCard>(out var newCard))
                    {
                        newCard.isFoundInLobby = true;
                        newCard.Setup(player, currentLobby);
                        playerCards.Add(newCard);
                    }
                }
            }

            // Delete all cards that are no longer in the lobby
            for (int i = playerCards.Count - 1; i >= 0; i--)
            {
                if (!playerCards[i].isFoundInLobby)
                {
                    PlayerCard cardToDestroy = playerCards[i];
                    playerCards.RemoveAt(i);
                    if (cardToDestroy != null && cardToDestroy.gameObject != null)
                    {
                        Destroy(cardToDestroy.gameObject);
                    }
                }
            }
        }

        public void LeaveLobby()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (SteamManager.Instance != null)
            {
                SteamManager.Instance.Disconnect();
            }
            SceneManager.LoadScene("SteamBoot", LoadSceneMode.Single);
        }
    }
}
