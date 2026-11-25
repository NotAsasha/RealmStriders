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
        public const int MaxPlayers = 4;

        private const string GameLobbyKey = "realm_striders_session";
        private const string GameLobbyValue = "1";
        private const string LobbyNameKey = "name";
        private const string LobbyFullMessage = "This lobby is full. Refresh to find another game.";
        private static readonly TimeSpan ClientConnectionTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan LobbySearchTimeout = TimeSpan.FromSeconds(22);

        public static SteamManager Instance { get; private set; }

        public Lobby? CurrentLobby { get; private set; }

        public bool IsSteamActive => SteamClient.IsValid && SteamClient.IsLoggedOn;
        public bool IsSingleplayer { get; private set; }

        public event Action<Lobby, Friend> OnMemberJoined;
        public event Action<Lobby, Friend> OnMemberLeft;
        public event Action<ulong> OnRemoteClientConnected;
        public event Action<ulong> OnRemoteClientDisconnected;

        private FacepunchTransport cachedTransport;
        private UnityTransport cachedUnityTransport;
        private readonly HashSet<ulong> approvedRemoteClients = new();
        private TaskCompletionSource<string> clientConnection;
        private bool sessionStarting;
        private int sessionAttempt;
        private int hostPlayerLimit;
        private bool clientConnectedToHost;
        private string notificationMessage;
        private string pendingMenuNotification;
        private bool notificationMenuOnly;
        private float notificationExpiresAt;

        private FacepunchTransport Transport
        {
            get
            {
                if (cachedTransport != null) return cachedTransport;

                if (NetworkManager.Singleton != null)
                {
                    cachedTransport = NetworkManager.Singleton.GetComponentInChildren<FacepunchTransport>(true);
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
                    cachedUnityTransport = NetworkManager.Singleton.GetComponentInChildren<UnityTransport>(true);
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
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            sessionAttempt++;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite -= OnLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
            UnsubscribeNetworkEvents();
            Instance = null;
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
            if (notificationMenuOnly && scene.name != "Menu") notificationMessage = null;
            if (scene.name == "Menu" && !string.IsNullOrEmpty(pendingMenuNotification))
            {
                ShowNotification(pendingMenuNotification, menuOnly: true);
                pendingMenuNotification = null;
            }
            if (scene.name == "Menu" || scene.name == "SteamBoot")
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnApplicationQuit() => Disconnect();

        private bool ConfigureTransport(NetworkManager network, bool singleplayer)
        {
            var loopback = UnityTransport;
            var steam = Transport;
            NetworkTransport selected = singleplayer ? (NetworkTransport)loopback : steam;
            if (selected == null) return false;

            if (steam != null) steam.gameObject.SetActive(!singleplayer);
            if (loopback != null) loopback.gameObject.SetActive(singleplayer);
            network.NetworkConfig.NetworkTransport = selected;
            // Both peers include this setting in NGO's connection configuration hash.
            network.NetworkConfig.ConnectionApproval = true;
            IsSingleplayer = singleplayer;
            return true;
        }

        private string GetSessionStartError(NetworkManager network)
        {
            if (network == null) return "Game services are not ready. Please try again.";
            if (sessionStarting || network.ShutdownInProgress)
                return "A session is still starting or stopping. Please try again.";
            if (network.IsListening) return "Leave your current game before starting another.";
            return null;
        }

        // A null error means both the requested lobby (if online) and the host are ready.
        public async Task<string> StartHostAsync(bool friendsOnly, bool singleplayer)
        {
            var network = NetworkManager.Singleton;
            string error = GetSessionStartError(network);
            if (error != null) return error;
            if (!singleplayer && !IsSteamActive)
                return "Steam disconnected. Reconnect to create an online lobby.";

            sessionStarting = true;
            int attempt = ++sessionAttempt;
            bool started = false;
            try
            {
                if (!singleplayer)
                {
                    const string lobbyError = "Steam could not create the lobby. Check Steam and try again.";
                    Lobby? created = await SteamMatchmaking.CreateLobbyAsync(MaxPlayers);
                    if (!created.HasValue) return lobbyError;
                    if (this == null || attempt != sessionAttempt)
                    {
                        LeaveLobby(created);
                        return "Lobby creation was canceled.";
                    }

                    Lobby lobby = created.Value;
                    CurrentLobby = lobby;
                    if (!lobby.SetJoinable(false) ||
                        !lobby.SetData(GameLobbyKey, GameLobbyValue) ||
                        !lobby.SetData(LobbyNameKey, SteamClient.Name + "'s lobby") ||
                        !(friendsOnly ? lobby.SetFriendsOnly() : lobby.SetPublic()) ||
                        !lobby.SetJoinable(true))
                        return lobbyError;
                }

                // No frame elapses between publishing the lobby and starting the host.
                started = TryStartHost(network, singleplayer);
                if (started) return null;
                return singleplayer
                    ? "Could not start solo play. Please try again."
                    : "Could not start the online host. Check Steam and try again.";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamManager] Host setup failed: {ex}");
                return singleplayer
                    ? "Could not start solo play. Please try again."
                    : "Could not start the Steam lobby. Check Steam and try again.";
            }
            finally
            {
                if (!started && attempt == sessionAttempt) AbortSessionStartup(network);
                sessionStarting = false;
            }
        }

        public void StartSingleplayer()
        {
            var network = NetworkManager.Singleton;
            if (GetSessionStartError(network) != null) return;

            try
            {
                if (TryStartHost(network, singleplayer: true)) return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamManager] Solo host setup failed: {ex}");
            }

            AbortSessionStartup(network);
        }

        private bool TryStartHost(NetworkManager network, bool singleplayer)
        {
            if (!ConfigureTransport(network, singleplayer)) return false;
            hostPlayerLimit = singleplayer ? 1 : MaxPlayers;
            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnClientConnectedCallback += OnClientConnectedCallback;
            network.OnClientDisconnectCallback += OnClientDisconnectCallback;
            return network.StartHost();
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var network = NetworkManager.Singleton;
            bool localHost = request.ClientNetworkId == NetworkManager.ServerClientId;
            if (!localHost)
            {
                if (hostPlayerLimit == 1)
                    response.Reason = "This is a solo game.";
                else if (network == null || !network.IsServer || !CurrentLobby.HasValue ||
                         (GameManager.Instance != null && (GameManager.Instance.hasStartedMission.Value ||
                                                           GameManager.Instance.IsResolvingMission)))
                    response.Reason = "This lobby is not accepting players. Refresh and try again.";
                else if (approvedRemoteClients.Count >= hostPlayerLimit - 1)
                    response.Reason = LobbyFullMessage;
                else
                    approvedRemoteClients.Add(request.ClientNetworkId);
            }

            response.Approved = string.IsNullOrEmpty(response.Reason);
            response.CreatePlayerObject = network != null && network.NetworkConfig.PlayerPrefab != null;
        }

        public async Task<string> TryConnectLobby(SteamId id)
        {
            if (!IsSteamActive) return "Connect to Steam to join a lobby.";
            var network = NetworkManager.Singleton;
            string error = GetSessionStartError(network);
            if (error != null) return error;

            sessionStarting = true;
            int attempt = ++sessionAttempt;
            bool joined = false;
            bool networkStarted = false;
            try
            {
                var lobby = new Lobby(id);
                RoomEnter entry = await lobby.Join();
                if (entry != RoomEnter.Success)
                {
                    return entry switch
                    {
                        RoomEnter.Full => LobbyFullMessage,
                        RoomEnter.DoesntExist => "This lobby has closed. Refresh to find another game.",
                        RoomEnter.NotAllowed => "This lobby is unavailable. Ask the host for a Steam invite.",
                        _ => "Steam could not join the lobby. Check Steam and try again."
                    };
                }

                if (this == null || attempt != sessionAttempt || SceneManager.GetActiveScene().name != "Menu")
                {
                    LeaveLobby(lobby);
                    return "Joining was canceled.";
                }

                CurrentLobby = lobby;
                if (lobby.GetData(GameLobbyKey) != GameLobbyValue)
                    return "This lobby was created by an incompatible build.";
                SteamId owner = lobby.Owner.Id;
                if ((ulong)owner == 0 || owner == SteamClient.SteamId)
                    return "This lobby no longer has a reachable host. Refresh and try again.";
                if (!ConfigureTransport(network, singleplayer: false))
                    return "The Steam transport is unavailable. Restart the game and try again.";

                Transport.targetSteamId = owner;
                clientConnectedToHost = false;
                clientConnection = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                network.OnClientConnectedCallback += ClientConnected;
                network.OnClientDisconnectCallback += ClientDisconnected;
                network.OnClientStopped += ClientStopped;
                networkStarted = network.StartClient();
                if (!networkStarted) return "Could not connect to the host. Please try again.";

                Task<string> connection = clientConnection.Task;
                if (await Task.WhenAny(connection, Task.Delay(ClientConnectionTimeout)) != connection)
                    return "The host did not respond. Please try again.";

                error = await connection;
                joined = error == null && attempt == sessionAttempt;
                return joined ? null : error ?? "Joining was canceled.";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamManager] Lobby join failed: {ex}");
                return "Could not join the Steam lobby. Check Steam and try again.";
            }
            finally
            {
                clientConnection = null;
                sessionStarting = false;
                if (!joined && attempt == sessionAttempt)
                {
                    if (networkStarted && SceneManager.GetActiveScene().name != "Menu")
                    {
                        ReturnToMenuWithError("Could not finish joining the host. Please try again.");
                    }
                    else
                    {
                        AbortSessionStartup(network);
                    }
                }
            }
        }

        public void Disconnect()
        {
            Debug.Log("[SteamManager] Leaving current lobby and stopping network.");
            sessionAttempt++;
            clientConnection?.TrySetResult("Joining was canceled.");
            clientConnectedToHost = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ClearLobby();
            ResetNetwork();
        }

        private void ClearLobby()
        {
            Lobby? lobby = CurrentLobby;
            CurrentLobby = null;
            LeaveLobby(lobby);
        }

        private static void LeaveLobby(Lobby? lobby)
        {
            if (!lobby.HasValue || !SteamClient.IsValid) return;

            try
            {
                lobby.Value.Leave();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamManager] Error leaving lobby: {ex.Message}");
            }
        }

        private void AbortSessionStartup(NetworkManager network)
        {
            ClearLobby();
            clientConnectedToHost = false;
            UnsubscribeNetworkEvents();
            if (network != null && network.IsListening && !network.ShutdownInProgress)
                network.Shutdown(discardMessageQueue: true);
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
            approvedRemoteClients.Clear();
            var network = NetworkManager.Singleton;
            if (network == null) return;

            if (network.ConnectionApprovalCallback == ApproveConnection)
                network.ConnectionApprovalCallback = null;
            network.OnClientConnectedCallback -= OnClientConnectedCallback;
            network.OnClientConnectedCallback -= ClientConnected;
            network.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            network.OnClientDisconnectCallback -= ClientDisconnected;
            network.OnClientStopped -= ClientStopped;
        }

        public async Task<(Lobby[] Lobbies, string Error)> RefreshLobbies()
        {
            if (!IsSteamActive) return (Array.Empty<Lobby>(), "Connect to Steam to find lobbies.");

            const string queryError = "Steam could not refresh the list. Try Refresh again.";
            try
            {
                Task<Lobby[]> request = SteamMatchmaking.LobbyList
                    .FilterDistanceWorldwide()
                    .WithKeyValue(GameLobbyKey, GameLobbyValue)
                    .WithSlotsAvailable(1)
                    .WithMaxResults(20)
                    .RequestAsync();
                if (await Task.WhenAny(request, Task.Delay(LobbySearchTimeout)) != request || !IsSteamActive)
                    return (Array.Empty<Lobby>(), queryError);

                return (await request ?? Array.Empty<Lobby>(), null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamManager] Lobby search failed: {ex}");
                return (Array.Empty<Lobby>(), queryError);
            }
        }

        public static string GetLobbyName(Lobby lobby)
        {
            string name = lobby.GetData(LobbyNameKey);
            return string.IsNullOrWhiteSpace(name) ? "Realm Striders lobby" : name;
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

        private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            if (SceneManager.GetActiveScene().name != "Menu")
            {
                ShowNotification("Return to the menu, then accept the Steam invitation again.");
                return;
            }

            string error = await TryConnectLobby(lobby.Id);
            if (error != null) ShowNotification(error, menuOnly: true);
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

        #endregion

        #region Netcode Callbacks

        private void ClientConnected(ulong clientId)
        {
            clientConnectedToHost = true;
            clientConnection?.TrySetResult(null);
        }

        private void ClientDisconnected(ulong clientId) => HandleClientDisconnect();

        private void ClientStopped(bool wasHost) => HandleClientDisconnect();

        private void HandleClientDisconnect()
        {
            if (clientConnectedToHost)
            {
                clientConnectedToHost = false;
                HandleHostLeft();
                return;
            }

            string reason = NetworkManager.Singleton?.DisconnectReason;
            clientConnection?.TrySetResult(string.IsNullOrEmpty(reason)
                ? "Could not connect to the host. Please try again."
                : reason);
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            Debug.Log($"[SteamManager] Remote client connected to host. ClientId: {clientId}");
            OnRemoteClientConnected?.Invoke(clientId);
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            approvedRemoteClients.Remove(clientId);
            Debug.Log($"[SteamManager] Remote client disconnected from host. ClientId: {clientId}");
            OnRemoteClientDisconnected?.Invoke(clientId);
        }

        #endregion

        private void HandleHostLeft()
        {
            const string message = "host left the game";
            Debug.LogWarning(message);
            ReturnToMenuWithError(message);
        }

        public void ReturnToMenuWithError(string message)
        {
            NotifyOnMenu(message);
            Disconnect();
            SceneManager.LoadScene("SteamBoot", LoadSceneMode.Single);
        }

        public void NotifyOnMenu(string message) => pendingMenuNotification = message;

        private void ShowNotification(string message, bool menuOnly = false)
        {
            notificationMessage = message;
            notificationMenuOnly = menuOnly;
            notificationExpiresAt = Time.realtimeSinceStartup + 10f;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(notificationMessage) || Time.realtimeSinceStartup > notificationExpiresAt)
            {
                return;
            }
            if (notificationMenuOnly && SceneManager.GetActiveScene().name != "Menu") return;

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
                wordWrap = true,
                fontSize = 20
            });
        }
    }
}
