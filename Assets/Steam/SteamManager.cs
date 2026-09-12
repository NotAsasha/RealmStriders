using Netcode.Transports.Facepunch;
using Player.Network;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace Steam
{
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }

        public Lobby? CurrentLobby { get; private set; }
        public List<Lobby> Lobbies { get; private set; } = new(100);

        private FacepunchTransport cachedTransport;

        private FacepunchTransport Transport
        {
            get
            {
                if (cachedTransport != null) return cachedTransport;

                if (NetworkManager.Singleton != null)
                {
                    cachedTransport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as FacepunchTransport
                                      ?? NetworkManager.Singleton.GetComponent<FacepunchTransport>();
                }

                if (cachedTransport == null)
                {
                    cachedTransport = FindFirstObjectByType<FacepunchTransport>();
                }

                return cachedTransport;
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
            }
        }

        private void Start()
        {
#if UNITY_EDITOR
            Debug.unityLogger.logEnabled = true;
#else
            Debug.unityLogger.logEnabled = Debug.isDebugBuild;
#endif
            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        private void OnDestroy()
        {
            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite -= OnLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;

            UnsubscribeNetworkEvents();
        }

        private void OnApplicationQuit() => Disconnect();

        public async void StartHost(uint maxMembers, bool isFriendsOnly)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[SteamManager] NetworkManager.Singleton is null!");
                return;
            }

            Debug.Log("[SteamManager] Initializing host...");

            UnsubscribeNetworkEvents();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;

            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("[SteamManager] Failed to start Netcode Host!");
                return;
            }

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

        public void StartClient(SteamId hostSteamId)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[SteamManager] NetworkManager.Singleton is null!");
                return;
            }

            if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[SteamManager] Network session is already running.");
                return;
            }

            if (Transport == null)
            {
                Debug.LogError("[SteamManager] FacepunchTransport not found!");
                return;
            }

            UnsubscribeNetworkEvents();
            NetworkManager.Singleton.OnClientConnectedCallback += ClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;

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
            CurrentLobby?.Leave();
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
            CurrentLobby = await SteamMatchmaking.JoinLobbyAsync(id);
        }

        public async Task<bool> RefreshLobbies(int maxResults = 20)
        {
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
            if (!CurrentLobby.HasValue) return playerList;

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
            if (lobby.Owner.Id == SteamClient.SteamId)
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

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend) { }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend) { }

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
        }

        private void ClientDisconnected(ulong clientId)
        {
            Debug.LogWarning($"[SteamManager] Local client disconnected! ClientId: {clientId}");
            UnsubscribeNetworkEvents();
        }

        private void OnServerStarted()
        {
            Debug.Log("[SteamManager] Server started successfully.");
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            Debug.Log($"[SteamManager] Remote client connected to host. ClientId: {clientId}");
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            Debug.Log($"[SteamManager] Remote client disconnected from host. ClientId: {clientId}");
        }

        #endregion
    }
}