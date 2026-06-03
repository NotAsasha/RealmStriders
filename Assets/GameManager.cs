using Enemy;
using FileSystem.Scripts;
using Player;
using Portals;
using Steam;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public Vector3 spawnPoint = new(0f, -49f, 0f);
    public float baseRadius = 20f;
    public int defaultMissionTime = 360;
    public int maxTimeSpread = 120;

    public NetworkVariable<int> teamRating = new(3, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> lossRating = new(0, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> teamMoney = new(10000, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> hasStartedMission = new(false, writePerm: NetworkVariableWritePermission.Server);


    //public List<NetworkObject>

    private int alivePlayers;

    public float missionDuration;

    public int AlivePlayersCount { get => alivePlayers; }

    public Scene missionScene;
    public string missionName = "World1";
    public int enemiesCount = 1;
    public float averageDanger = 1;

    public List<Enemy.Enemy> activeEnemies = new();

    public SaveFile currentSave;

    public static GameManager Instance = null;

    private EnemySpawner spawner;

    #region Unity Lifecycle

    private void Awake()
    {
        SetupSingleton();
        SetupInputHandlers();
        DontDestroyOnLoad(gameObject);

        //Decrease timer every second
        InvokeRepeating(nameof(Radiation), 0f, 1f);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 144;
        spawner = GetComponent<EnemySpawner>();
        }

        private void OnDisable()
        {
        CleanupInputHandlers();
        }

        #endregion

        #region Initialization

        private void SetupSingleton()
        {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        }

        private void SetupInputHandlers()
        {
        hasStartedMission.OnValueChanged += OnMissionStatusChanged;
        }

        private void CleanupInputHandlers()
        {
        hasStartedMission.OnValueChanged -= OnMissionStatusChanged;
        }
        #endregion

        private void OnMissionStatusChanged(bool oldValue, bool newValue)
        {
        if (newValue)
        {
            Debug.Log("---MissionManager: Start Mission.");
            StartMission();
        }
        else
        {
            Debug.Log($"---MissionManager: End Mission, Rating before: {teamRating.Value}.");
            StopMission();
        }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void OnPlayerDeathServerRpc()
        {
        alivePlayers -= 1;
        Debug.Log($"---MissionManager: Allive players: {alivePlayers}.");
        if (alivePlayers <= 0)
        {
            Debug.Log("---MissionManager: Everyone died, stopping mission.");
            StopMissionServerRpc();
        }
        }

        #region MissionRpc

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void StartMissionServerRpc()
        {
        if (hasStartedMission.Value) return;
        if (missionName == "") return;

        //Spawn Monsters

        RevivePlayers();
        alivePlayers = NetworkManager.Singleton.ConnectedClients.Count;

        //Stop Lobby Connections
        if (SteamManager.Instance.CurrentLobby != null)
            SteamManager.Instance.CurrentLobby.Value.SetJoinable(false);

        LoadWorld(missionName, enemiesCount, averageDanger);
        StartTimer();

        hasStartedMission.Value = true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void StopMissionServerRpc() => StartCoroutine(StopMissionClock());

        private IEnumerator StopMissionClock()
        {
        //revive if died in lobby
        if (!hasStartedMission.Value)
        {
            yield return new WaitForSeconds(5f);
            RevivePlayers();
            yield break;
        }

        //Calculate rating
        int tempRating = CalculateRating(teamRating.Value);

        KillEnemies();


        //stop the mission (turn off the portals)
        hasStartedMission.Value = false;
        yield return new WaitForSeconds(5f);


        teamRating.Value = tempRating;
        lossRating.Value += 1;


        if (teamRating.Value <= lossRating.Value)
        {
            //Loss
            Debug.Log("---MissionManager: Game Over, you lost...");
            Cursor.lockState = CursorLockMode.None;

            //TEMP - to main menu TODO
            SteamManager.Instance.Disconnect();
            SceneManager.LoadScene("SteamBoot", LoadSceneMode.Single);
            yield break;
        }


        //kill players out of base
        if (alivePlayers > 0)
        {
            KillOutOfRangePlayers();
        }


        //unload world
        UnloadWorld();
        missionName = "";


        //revive
        RevivePlayers();


        //Resume Lobby Connections
        if (SteamManager.Instance.CurrentLobby != null)
            SteamManager.Instance.CurrentLobby.Value.SetJoinable(true);
        }

        #endregion

        private void RevivePlayers()
        {
        foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
        {
            Debug.Log($"---Mission: Reviving player: {player.ClientId}");
            var human = player.PlayerObject.gameObject.GetComponent<Human>();
            if (human.isDead.Value)
            {
                human.isDead.Value = false;
            }
            human.entityHealth.Value = human.dangerLevel;
        }
        }

        private void KillOutOfRangePlayers()
        {
        foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
        {
            var human = player.PlayerObject.gameObject.GetComponent<Human>();
            if (human.isDead.Value) continue;

            float distanceToSpawn = Vector3.Distance(human.transform.position, spawnPoint);
            if (distanceToSpawn > baseRadius) human.isDead.Value = true;
        }
        }

        private void StartTimer()
        {
        missionDuration = Random.Range(defaultMissionTime - maxTimeSpread, defaultMissionTime + maxTimeSpread);

        StartTimerClientRpc(missionDuration);
        }

        [ClientRpc]
        private void StartTimerClientRpc(float serverDuration)
        {
        missionDuration = serverDuration;
        }
        private int CalculateRating(int current)
        {
        if (alivePlayers <= 0)
        {
            current -= 1;
        }
        else
        {
            bool areAllDead = activeEnemies.All(enemy => enemy.isDead.Value);
            if (areAllDead)
            {
                current += 1;
            }
        }
        
        return current;
        }

    private void StartMission()
    {
        //Open Portal
        PortalManager.Instance.isForward = true;
        PortalManager.Instance.ChangeState(true);
    }

    private void StopMission()
    {
        //Close portal
        PortalManager.Instance.ChangeState(false);
    }

    #region World Manager

    private string currentSceneName;
    public void LoadWorld(string sceneToLoad, int monsters = 0, float avgDanger = 0)
    {
        if (currentSceneName != null)
        {
            Debug.LogError("---MissionManager: Trying to load mission without ending the previous one.");
        }

        if (!IsServer)
        {
            Debug.Log("Waiting for server to load scene...");
            return;
        }

        //Scene
        NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Additive);
        Scene sceneToUnload = SceneManager.GetSceneByName(sceneToLoad);
        currentSceneName = sceneToLoad;
        missionScene = sceneToUnload;

        //Enemies

        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnSceneLoaded;
    }

    private void OnSceneLoaded(ulong conn, string sceneName, LoadSceneMode mode)
    {
        spawner.SpawnEnemies(teamRating.Value, enemiesCount);
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded; // ������� -- ok, comment broke...
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid())
        {
            missionScene = loadedScene;

            // Робимо сцену світу Primary (Active) для цього конкретного гравця
            SceneManager.SetActiveScene(loadedScene);

            // Оновлюємо скайбокс та параметри освітлення
            DynamicGI.UpdateEnvironment();
        }
    }

    private void KillEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            enemy.isDead.Value = true;
        }
    }

    public void UnloadWorld()
    {
        if (currentSceneName == null)
        {
            Debug.LogError("---MissionManager: Trying to stop non-existing mission.");
            return;
        }

        foreach (var enemy in activeEnemies)
        {
            enemy.GetComponent<NetworkObject>().Despawn();
            Destroy(enemy.gameObject);
        }
        activeEnemies.Clear();

        Scene sceneToUnload = SceneManager.GetSceneByName(currentSceneName);
        NetworkManager.Singleton.SceneManager.UnloadScene(sceneToUnload);
        currentSceneName = null;
    }

    #endregion

    private void Radiation()
    {
        if (!hasStartedMission.Value || !IsServer) return;

        missionDuration -= 1;
        if (missionDuration <= 0)
        {
            StopMissionServerRpc();
        }
    }
}