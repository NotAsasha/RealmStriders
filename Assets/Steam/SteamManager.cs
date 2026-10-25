using Netcode.Transports.Facepunch;
using Player.Network;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Steam
{
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }

        public Lobby? CurrentLobby { get; private set; }
        public List<Lobby> Lobbies { get; private set; } = new(100);

        public bool IsSteamActive => SteamClient.IsValid;
        public bool IsSingleplayer { get; private set; }

        public event Action<Lobby, Friend> OnMemberJoined;
        public event Action<Lobby, Friend> OnMemberLeft;
        public event Action<ulong> OnRemoteClientConnected;
        public event Action<ulong> OnRemoteClientDisconnected;

        private FacepunchTransport cachedTransport;
        private UnityTransport cachedUnityTransport;
        private bool clientConnectedToHost;
        private string notificationMessage;
        private float notificationExpiresAt;

        private FacepunchTransport Transport
        {
            get
            {
                if (cachedTransport != null) return cachedTransport;

                if (NetworkManager.Singleton != null)
                {
                    cachedTransport = NetworkManager.Singleton.GetComponentInChildren<FacepunchTransport>(true)
                                      ?? FindFirstObjectByType<FacepunchTransport>();
                }

                return cachedTransport;
            }
        }

        private UnityTransport UnityTransport
        {
            get
            {
                if (cachedUnityTransport != null) return cachedUnityTransport;

                if (NetworkManager.Singleton != null)
                {
                    cachedUnityTransport = NetworkManager.Singleton.GetComponentInChildren<UnityTransport>(true)
                                           ?? FindFirstObjectByType<UnityTransport>();
                }

                return cachedUnityTransport;
            }
        }

        public int CurrentLobbyMemberCount => CurrentLobby?.MemberCount ?? 0;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
#if UNITY_EDITOR
            Debug.unityLogger.logEnabled = true;
#else
            Debug.unityLogger.logEnabled = Debug.isDebugBuild;
#endif
            if (IsSteamActive)
            {
                SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
                SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
                SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
                SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
                SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
                SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
            }
            else
            {
                Debug.Log("[SteamManager] Steam is not running. Singleplayer/offline mode enabled.");
                ConfigureTransport(forceSingleplayer: true);
            }
        }

        private void OnDestroy()
        {
            if (IsSteamActive)
            {
                SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
                SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
                SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
                SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
                SteamMatchmaking.OnLobbyInvite -= OnLobbyInvite;
                SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
            }

            UnsubscribeNetworkEvents();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Menu" || scene.name == "SteamBoot")
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnApplicationQuit() => Disconnect();

        public void ConfigureTransport(bool forceSingleplayer = false)
        {
            if (NetworkManager.Singleton == null) return;

            bool useSingleplayer = forceSingleplayer || !IsSteamActive;
            IsSingleplayer = useSingleplayer;

            var utp = UnityTransport;
            var fp = Transport;

            if (useSingleplayer)
            {
                if (fp != null)
                {
                    fp.gameObject.SetActive(false);
                }

                if (utp != null)
                {
                    utp.gameObject.SetActive(true);
                    NetworkManager.Singleton.NetworkConfig.NetworkTransport = utp;
                    Debug.Log("[SteamManager] Configured UnityTransport (singleplayer/offline).");
                }
                else
                {
                    Debug.LogWarning("[SteamManager] UnityTransport component not found! Adding component to NetworkManager.");
                    utp = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
                    cachedUnityTransport = utp;
                    NetworkManager.Singleton.NetworkConfig.NetworkTransport = utp;
                }
            }
            else
            {
                if (utp != null)
                {
                    utp.gameObject.SetActive(false);
                }

                if (fp != null)
                {
                    fp.gameObject.SetActive(true);
                    NetworkManager.Singleton.NetworkConfig.NetworkTransport = fp;
                    Debug.Log("[SteamManager] Configured FacepunchTransport (Steam online).");
                }
            }
        }

        public async void StartHost(uint maxMembers, bool isFriendsOnly, bool singleplayer = false)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[SteamManager] NetworkManager.Singleton is null!");
                return;
            }

            ConfigureTransport(forceSingleplayer: singleplayer || !IsSteamActive);

            Debug.Log($"[SteamManager] Initializing host... (Singleplayer: {IsSingleplayer}, Steam: {IsSteamActive}, Transport: {NetworkManager.Singleton.NetworkConfig.NetworkTransport?.GetType().Name})");

            UnsubscribeNetworkEvents();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;

            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("[SteamManager] Failed to start Netcode Host!");
                return;
            }

            if (IsSteamActive && !IsSingleplayer)
            {
                try
                {
                    // Створення лобі в Steam. Після успіху Steam сам викличе OnLobbyEntered
                    CurrentLobby = await SteamMatchmaking.CreateLobbyAsync((int)maxMembers);
                    if (CurrentLobby.HasValue)
                    {
                        if (!isFriendsOnly) CurrentLobby.Value.SetPublic();
                        else CurrentLobby.Value.SetFriendsOnly();

                        CurrentLobby.Value.SetJoinable(true);
                        CurrentLobby.Value.SetData("name", "Realm Striders Session");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SteamManager] Failed to create Steam lobby: {ex.Message}. Session will continue locally.");
                }
            }
            else
            {
                Debug.Log("[SteamManager] Host started in singleplayer/offline mode.");
            }
        }

        public void StartSingleplayer()
        {
            StartHost(1, true, singleplayer: true);
        }

        public void StartClient(SteamId hostSteamId)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[SteamManager] NetworkManager.Singleton is null!");
                return;
            }

            if (!IsSteamActive)
            {
                Debug.LogError("[SteamManager] Cannot connect via Steam: Steam is not running!");
                return;
            }

            if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[SteamManager] Network session is already running.");
                return;
            }

            ConfigureTransport(forceSingleplayer: false);

            if (Transport == null)
            {
                Debug.LogError("[SteamManager] FacepunchTransport not found!");
                return;
            }

            UnsubscribeNetworkEvents();
            NetworkManager.Singleton.OnClientConnectedCallback += ClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;
            clientConnectedToHost = false;

            // Приведення SteamId до ulong для сумісності з FacepunchTransport
            Transport.targetSteamId = (ulong)hostSteamId;

            Debug.Log($"[SteamManager] Attempting StartClient connecting to SteamID: {hostSteamId}");

            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError("[SteamManager] NetworkManager.StartClient() returned false!");
            }
        }

        public void Disconnect()
        {
            Debug.Log("[SteamManager] Leaving current lobby and stopping network.");
            clientConnectedToHost = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (IsSteamActive)
            {
                try
                {
                    CurrentLobby?.Leave();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SteamManager] Error leaving lobby: {ex.Message}");
                }
            }
            CurrentLobby = null;

            ResetNetwork();
        }

        private void ResetNetwork()
        {
            if (NetworkManager.Singleton != null)
            {
                UnsubscribeNetworkEvents();

                if (NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }

            cachedTransport = null;
            cachedUnityTransport = null;

            if (GameManager.Instance != null)
            {
                Destroy(GameManager.Instance.gameObject);
                GameManager.Instance = null;
            }
        }

        private void UnsubscribeNetworkEvents()
        {
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientConnectedCallback -= ClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= ClientDisconnected;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }

        public async void TryConnectLobby(SteamId id)
        {
            if (!IsSteamActive) return;
            CurrentLobby = await SteamMatchmaking.JoinLobbyAsync(id);
        }

        public async Task<bool> RefreshLobbies(int maxResults = 20)
        {
            if (!IsSteamActive)
            {
                Lobbies.Clear();
                return false;
            }

            try
            {
                Lobbies.Clear();

                var foundLobbies = await SteamMatchmaking.LobbyList
                    .FilterDistanceClose()
                    .WithMaxResults(maxResults)
                    .RequestAsync();

                if (foundLobbies != null)
                {
                    Lobbies.AddRange(foundLobbies);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                return false;
            }
        }

        public async Task<List<SteamPlayer>> GetLobbyMembersAsync()
        {
            List<SteamPlayer> playerList = new();
            if (!IsSteamActive || !CurrentLobby.HasValue) return playerList;

            foreach (var member in CurrentLobby.Value.Members)
            {
                var imageTask = await member.GetMediumAvatarAsync();
                playerList.Add(new SteamPlayer(member.Name, member.Id, imageTask, member));
            }

            return playerList;
        }

        #region Steam Callbacks

        // Запит на підключення через інвайт/оверлей Steam
        private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            if (!IsSteamActive) return;
            Debug.Log($"[SteamManager] Lobby join requested for ID: {lobby.Id}");

            // Надійний спосіб Facepunch: приєднуємося через статичний метод Matchmaking
            CurrentLobby = await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
            if (!CurrentLobby.HasValue)
            {
                Debug.LogError("[SteamManager] Failed to join requested Steam lobby!");
            }
        }

        // Автоматично викликається Steam після успішного входу в лобі
        private void OnLobbyEntered(Lobby lobby)
        {
            CurrentLobby = lobby;
            Debug.Log($"[SteamManager] Entered lobby {lobby.Id}. Owner: {lobby.Owner.Id}");

            // Безпечна перевірка: чи ми є творцем лобі в Steam
            if (IsSteamActive && lobby.Owner.Id == SteamClient.SteamId)
            {
                Debug.Log("[SteamManager] We are the lobby owner. Skipping client start.");
                return;
            }

            StartClient(lobby.Owner.Id);
        }

        private void OnLobbyInvite(Friend friend, Lobby lobby)
        {
            Debug.Log($"[SteamManager] Invite received from {friend.Name}");
        }

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            Debug.Log($"[SteamManager] Lobby member left: {friend.Name} ({friend.Id})");
            OnMemberLeft?.Invoke(lobby, friend);
        }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            Debug.Log($"[SteamManager] Lobby member joined: {friend.Name} ({friend.Id})");
            OnMemberJoined?.Invoke(lobby, friend);
        }

        private void OnLobbyCreated(Result result, Lobby lobby)
        {
            if (result != Result.OK)
            {
                Debug.LogError($"[SteamManager] Lobby creation failed: {result}");
            }
        }

        #endregion

        #region Netcode Callbacks

        private void ClientConnected(ulong clientId)
        {
            Debug.Log($"[SteamManager] Local client connected to server! ClientId: {clientId}");
            clientConnectedToHost = true;
        }

        private void ClientDisconnected(ulong clientId)
        {
            Debug.LogWarning($"[SteamManager] Local client disconnected! ClientId: {clientId}");
            bool lostHost = clientConnectedToHost;
            clientConnectedToHost = false;
            UnsubscribeNetworkEvents();

            if (lostHost)
            {
                HandleHostLeft();
            }
        }

        private void OnServerStarted()
        {
            Debug.Log("[SteamManager] Server started successfully.");
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            Debug.Log($"[SteamManager] Remote client connected to host. ClientId: {clientId}");
            OnRemoteClientConnected?.Invoke(clientId);
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            Debug.Log($"[SteamManager] Remote client disconnected from host. ClientId: {clientId}");
            OnRemoteClientDisconnected?.Invoke(clientId);
        }

        #endregion

        private void HandleHostLeft()
        {
            const string message = "host left the game";
            Debug.LogWarning(message);
            ShowNotification(message);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Disconnect();
            SceneManager.LoadScene("SteamBoot", LoadSceneMode.Single);
        }

        private void ShowNotification(string message)
        {
            notificationMessage = message;
            notificationExpiresAt = Time.unscaledTime + 5f;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(notificationMessage) || Time.unscaledTime > notificationExpiresAt)
            {
                return;
            }

            const float width = 420f;
            const float height = 60f;
            Rect notificationRect = new(
                (Screen.width - width) / 2f,
                Screen.height * 0.15f,
                width,
                height);

            GUI.Box(notificationRect, GUIContent.none);
            GUI.Label(notificationRect, notificationMessage, new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            });
        }
    }
}