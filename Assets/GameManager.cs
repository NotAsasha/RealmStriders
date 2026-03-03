using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Steam;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Player;

public class GameManager : NetworkBehaviour
{
    public Vector3 spawnPoint = new(0f, -49f, 0f);
    public float baseRadius = 20f;
    public int defaultMissionTime = 360;
    public int maxTimeSpread = 120;

    public NetworkVariable<int> teamRating = new(3, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> looseRating = new(0, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> teamMoney = new(1000, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> hasStartedMission = new(false, writePerm: NetworkVariableWritePermission.Server);

    private int alivePlayers;

    public float missionDuration;

    public int AlivePlayersCount { get => alivePlayers; }

    public Scene missionScene;
    public string missionName = "World1";
    public int enemiesCount = 1;
    public float avarageDanger = 1;

    public List<Enemy> activeEnemies = new();

    public static GameManager instance = null;

    private EnemySpawner spawner;

    #region Unity Lifecycle

    private void Awake()
    {
        SetupSingletone();
        SetupInputHandlers();
        DontDestroyOnLoad(gameObject);

        //Decrease timer every second
        InvokeRepeating(nameof(Radiation), 0f, 1f);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 1000;
        spawner = GetComponent<EnemySpawner>();
    }

    private void OnDisable()
    {
        CleanupInputHandlers();
    }

    #endregion

    #region Initialization

    private void SetupSingletone()
    {
        if (instance == null)
            instance = this;
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

        LoadWorld(missionName, enemiesCount, avarageDanger);
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


        //stop the mission
        hasStartedMission.Value = false;
        yield return new WaitForSeconds(5f);


        //Calculate rating
        teamRating.Value = CalculateRating(teamRating.Value);
        looseRating.Value += 1;
        if (teamRating.Value <= looseRating.Value)
        {
            //Loose
            Debug.Log("---MissionManager: Game Over, you lost...");
            Cursor.lockState = CursorLockMode.None;

            //TEMP - to main menu
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

            float distancetoSpawn = Vector3.Distance(human.transform.position, spawnPoint);
            if (distancetoSpawn > baseRadius) human.isDead.Value = true;
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
    private int CalculateRating(int _current)
    {
        if (alivePlayers <= 0)
        {
            _current -= 1;
        }
        else
        {
            bool areAllDead = activeEnemies.Any(enemy => enemy.isDead.Value);
            if (areAllDead)
            {
                _current += 1;
            }
        }
        
        return _current;
    }

    private void StartMission()
    {
        //Open Portal
        PortalManager.instance.ChangeState(true);
    }

    private void StopMission()
    {
        //Close portal
        PortalManager.instance.ChangeState(false);
    }

    #region World Manager

    private string currentSceneName;
    public void LoadWorld(string sceneToLoad, int _monsters = 0, float avgDanger = 0)
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
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded; // відписка
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
        if (!hasStartedMission.Value) return;

        if ((missionDuration -= 1) <= 0 && IsServer)
        {
            StopMissionServerRpc();
        }
    }
}