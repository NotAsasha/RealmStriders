using Base.Shop;
using Enemy;
using FileSystem.Scripts;
using Player;
using Portals;
using Steam;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public Vector3 spawnPoint = new(0f, -48f, 0f);
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
    // Pre-rolled danger tier per enemy, set when the team selects a mission.
    public FixedList32Bytes<int> dangerLevels;

    public List<Enemy.Enemy> activeEnemies = new();

    public SaveFile currentSave;

    public static GameManager Instance = null;

    private EnemySpawner spawner;
    private Coroutine stopMissionRoutine;

    #region Unity Lifecycle

    private void Awake()
    {
        SetupSingleton();
        SetupInputHandlers();
        DontDestroyOnLoad(gameObject);

        //Decrease timer every second
        InvokeRepeating(nameof(Radiation), 0f, 1f);

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

        LoadWorld(missionName);
        StartTimer();

        hasStartedMission.Value = true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StopMissionServerRpc()
    {
        if (stopMissionRoutine != null) return;
        stopMissionRoutine = StartCoroutine(StopMissionClock());
    }

    private IEnumerator StopMissionClock()
    {
        //revive if died in lobby
        if (!hasStartedMission.Value)
        {
            yield return new WaitForSeconds(5f);
            RevivePlayers();
            stopMissionRoutine = null;
            yield break;
        }

        //Calculate rating
        int tempRating = CalculateRating(teamRating.Value);

        KillEnemies();


        //stop the mission (turn off the portals)
        missionName = ""; // ---- Mission stays the same, can be changed on world chooser; -- UPD: nah, changed
        hasStartedMission.Value = false;
        yield return new WaitForSeconds(3f);


        teamRating.Value = tempRating;

        // lossRating.Value += 1; // nah, it becomes too hard. TODO - idk, come up with smth

        // DEPRECATED
        if (teamRating.Value <= lossRating.Value)
        {
            //Loss
            Debug.Log("---MissionManager: Game Over, you lost...");
            Cursor.lockState = CursorLockMode.None;

            //TEMP - to main menu.  --- TODO
            SteamManager.Instance.Disconnect();
            SceneManager.LoadScene("SteamBoot", LoadSceneMode.Single);
            stopMissionRoutine = null;
            yield break;
        }


        //kill players out of base
        if (alivePlayers > 0)
        {
            KillOutOfRangePlayers();
        }

        DestroyOutOfRangeItems();
        PreserveItemsBroughtToBase();

        //unload world
        UnloadWorld();


        //revive
        RevivePlayers();

        Shop.Instance?.EnsureFreeLeafBlowerServer();

        currentSave.Save();

        //Resume Lobby Connections
        if (SteamManager.Instance.CurrentLobby != null)
            SteamManager.Instance.CurrentLobby.Value.SetJoinable(true);

        stopMissionRoutine = null;
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
        if (alivePlayers <= 0 && current > 0)
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
    public void LoadWorld(string sceneToLoad)
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
        spawner.SpawnEnemies(dangerLevels);
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded; // ������� -- ok, comment broke...
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid())
        { 
            missionScene = loadedScene;

            // local primary scene
            SceneManager.SetActiveScene(loadedScene);
            UpdateLocalSceneRpc();
        }
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void UpdateLocalSceneRpc() {
        DynamicGI.UpdateEnvironment();
    }

    private void KillEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            enemy.isDead.Value = true;
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
    private void DestroyOutOfRangeItems()
    {
        var toDelete = NetworkItemsHandler.Instance.activeSaveables
        .Where(item => Vector3.Distance(item.transform.position, spawnPoint) > baseRadius)
        .ToList();

        foreach (var item in toDelete)
        {
            item.Despawn(true);
        }
    }

    private void PreserveItemsBroughtToBase()
    {
        if (missionScene.IsValid() == false) return;

        Scene baseScene = SceneManager.GetSceneByName("Lobby");
        if (baseScene.IsValid() == false)
        {
            Debug.LogError("---MissionManager: Cannot preserve mission items because the base scene is not loaded.");
            return;
        }

        var missionItemsInBase = NetworkItemsHandler.Instance.activeSaveables
            .Where(item => item != null
                && item.IsSpawned
                && item.IsSceneObject == true
                && item.gameObject.scene == missionScene
                && IsWithinBaseRange(item.transform.position))
            .ToList();

        foreach (var item in missionItemsInBase)
        {
            NetworkObject parentNetworkObject = item.transform.parent?.GetComponentInParent<NetworkObject>();
            NetworkObjectReference parentReference = parentNetworkObject != null
                ? new NetworkObjectReference(parentNetworkObject)
                : default;

            item.TryRemoveParent();
            item.SetSceneObjectStatus(false);
            item.DestroyWithScene = false;
            SceneManager.MoveGameObjectToScene(item.gameObject, baseScene);

            if (parentNetworkObject != null)
            {
                item.TrySetParent(parentNetworkObject);
            }

            PreserveItemClientRpc(new NetworkObjectReference(item), parentReference);
        }
    }

    [ClientRpc]
    private void PreserveItemClientRpc(NetworkObjectReference itemReference, NetworkObjectReference parentReference)
    {
        if (IsServer || !itemReference.TryGet(out NetworkObject item)) return;

        NetworkObject parentNetworkObject = null;
        if (parentReference.TryGet(out var resolvedParent))
        {
            parentNetworkObject = resolvedParent;
        }

        item.TryRemoveParent();
        item.SetSceneObjectStatus(false);
        item.DestroyWithScene = false;

        Scene baseScene = SceneManager.GetSceneByName("Lobby");
        if (baseScene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(item.gameObject, baseScene);
        }

        if (parentNetworkObject != null)
        {
            item.TrySetParent(parentNetworkObject);
        }
    }

    private bool IsWithinBaseRange(Vector3 position)
    {
        Vector3 offset = position - spawnPoint;
        return offset.sqrMagnitude <= baseRadius * baseRadius;
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

    private void OnDrawGizmosSelected()
    {
        Color transparentColor = Color.blueViolet;
        transparentColor.a = 0.15f;
        Gizmos.color = transparentColor;
        Gizmos.DrawSphere(spawnPoint, baseRadius);

        Color wireColor = Color.blueViolet;
        wireColor.a = 0.7f;
        Gizmos.color = wireColor;
        Gizmos.DrawWireSphere(spawnPoint, baseRadius);
    }
}