using Netcode.Transports.Facepunch;
using Player.Network;
using Steamworks;
using Steamworks.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Steam
{
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; } = null;

        private FacepunchTransport transport;
        public NetworkVariable<int> playerCount = new();
        public Lobby? CurrentLobby { get; private set; } = null;

        public List<Lobby> Lobbies { get; private set; } = new List<Lobby>(capacity: 100);
        public GameObject playerPrefab;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if UNITY_EDITOR
            Debug.unityLogger.logEnabled = true;
#else
            Debug.unityLogger.logEnabled = Debug.isDebugBuild;
#endif

            transport = NetworkManager.Singleton.GetComponent<FacepunchTransport>();

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

            if (NetworkManager.Singleton == null)
                return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }

        private void OnApplicationQuit() => Disconnect();

        public async void StartHost(uint maxMembers, bool isFriendsOnly)
        {
            Debug.Log($"---CrewManager: Creating host...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;

            NetworkManager.Singleton.StartHost();

            CurrentLobby = await SteamMatchmaking.CreateLobbyAsync((int)maxMembers);
            if (CurrentLobby.HasValue)
            {
                if (!isFriendsOnly) CurrentLobby.Value.SetPublic();
                else CurrentLobby.Value.SetFriendsOnly();
                
                CurrentLobby.Value.SetJoinable(true);
            }
        }

        public void StartClient(SteamId hostSteamId)
        {
            // Захист від повторного запуску
            if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) return;

            NetworkManager.Singleton.OnClientConnectedCallback += ClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;
            
            transport.targetSteamId = hostSteamId;

            Debug.Log($"---CrewManager: Joining host with SteamID: {transport.targetSteamId}", this);
            
            if (NetworkManager.Singleton.StartClient())
                Debug.Log("---CrewManager: StartClient initiated successfully!", this);
        }

        public void Disconnect()
        {
            Debug.Log($"---CrewManager: Left team.");
            CurrentLobby?.Leave();
            CurrentLobby = null;

            if (NetworkManager.Singleton == null)
                return;

            ResetNetwork();
        }

        private void ResetNetwork()
        {
            if (NetworkManager.Singleton != null)
            {
                Debug.Log("[SteamManager] Resetting old NetworkManager");
                NetworkManager.Singleton.Shutdown();
                Destroy(NetworkManager.Singleton.gameObject);
            }

            if (GameManager.Instance != null)
            {
                Debug.Log("[SteamManager] Resetting old GameManager");
                Destroy(GameManager.Instance.gameObject);
                GameManager.Instance = null;
            }
        }

        public async void TryConnectLobby(uint id)
        {
            CurrentLobby = await SteamMatchmaking.JoinLobbyAsync(id);
        }

        public async Task<bool> RefreshLobbies(int maxResults = 20)
        {
            try
            {
                Lobbies.Clear();

                var lobbies = await SteamMatchmaking.LobbyList
                    .FilterDistanceClose()
                    .WithMaxResults(maxResults)
                    .RequestAsync();

                if (lobbies != null)
                {
                    for (int i = 0; i < lobbies.Length; i++)
                        Lobbies.Add(lobbies[i]);
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.Log("Error fetching lobbies", this);
                Debug.LogException(ex, this);
                return false;
            }
        }

        public async Task<List<SteamPlayer>> GetLobbyMembersAsync()
        {
            List<SteamPlayer> playerList = new();

            foreach (var member in CurrentLobby.Value.Members)
            {
                var imageTask = await member.GetMediumAvatarAsync();

                SteamPlayer player = new(member.Name, member.Id, imageTask, member);
                playerList.Add(player);
            }
            return playerList;
        }

        #region Steam Callbacks

        private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            Debug.Log($"---CrewManager: Join requested to lobby of {lobby.Owner.Name} ({lobby.Owner.Id})");

            CurrentLobby = await SteamMatchmaking.JoinLobbyAsync(lobby.Id);

            if (!CurrentLobby.HasValue)
            {
                Debug.LogError("---CrewManager: Failed to join Steam lobby!");
                return;
            }

            StartClient(CurrentLobby.Value.Owner.Id);
        }

        private void OnLobbyInvite(Friend friend, Lobby lobby) => Debug.Log($"You got an invite from {friend.Name}", this);

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend) { }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                playerCount.Value = NetworkManager.Singleton.ConnectedClients.Count;
            }
        }

        private void OnLobbyEntered(Lobby lobby)
        {   
            Debug.Log($"Entered Steam lobby {lobby.Id}. Am I host? {NetworkManager.Singleton.IsHost}", this);

            CurrentLobby = lobby;

            // Якщо ми не хост (підключаємося як клієнт через оверлей або списку лобі)
            if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient)
            {
                StartClient(lobby.Owner.Id);
            }
        }

        private void OnLobbyCreated(Result result, Lobby lobby)
        {
            if (result != Result.OK)
            {
                Debug.LogError($"Lobby couldn't be created!, {result}", this);
                return;
            }

            lobby.SetFriendsOnly();
            lobby.SetData("name", "Realm Striders Lobby");
            lobby.SetJoinable(true);
            Debug.Log($"Lobby created with ID: {lobby.Id}");
        }

        #endregion

        #region Network Callbacks

        private void ClientConnected(ulong clientId) => Debug.Log($"I'm connected, clientId={clientId}");

        private void ClientDisconnected(ulong clientId)
        {
            Debug.Log($"I'm disconnected, clientId={clientId}");

            NetworkManager.Singleton.OnClientDisconnectCallback -= ClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= ClientConnected;
        }

        private void OnServerStarted() { }

        private void OnClientConnectedCallback(ulong clientId) 
        {
            Debug.Log($"Client connected to host, clientId={clientId}", this);
            if (NetworkManager.Singleton.IsServer)
            {
                playerCount.Value = NetworkManager.Singleton.ConnectedClients.Count;
            }
        }

        private void OnClientDisconnectCallback(ulong clientId) 
        {
            Debug.Log($"Client disconnected from host, clientId={clientId}", this);
            if (NetworkManager.Singleton.IsServer)
            {
                playerCount.Value = NetworkManager.Singleton.ConnectedClients.Count;
            }
        }

        #endregion
    }
}