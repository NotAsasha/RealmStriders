using Base.Shop;
using Enemy;
using FileSystem.Scripts;
using Game;
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
    public NetworkVariable<int> teamMoney = new(300, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> hasStartedMission = new(false, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> campaignCompleted = new(false, writePerm: NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<bool> endReturnLocked = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


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
    private readonly List<Enemy.Enemy> missionTargets = new();
    private int capturedTargetCount;
    private bool missionEnemiesReady;

    public bool IsResolvingMission => stopMissionRoutine != null;
    public EnemySpawner MissionSpawner => spawner;

    #region Unity Lifecycle

    private void Awake()
    {
        SetupSingleton();
        DontDestroyOnLoad(gameObject);

        //Decrease timer every second
        InvokeRepeating(nameof(Radiation), 0f, 1f);

        spawner = GetComponent<EnemySpawner>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SetupInputHandlers();
        // Initial synchronization via ReadField does not fire OnValueChanged in Netcode.
        // Explicitly trigger OnValueChanged on spawn so UI and listeners receive synced values.
        teamMoney.OnValueChanged?.Invoke(teamMoney.Value, teamMoney.Value);
        teamRating.OnValueChanged?.Invoke(teamRating.Value, teamRating.Value);
        campaignCompleted.OnValueChanged?.Invoke(campaignCompleted.Value, campaignCompleted.Value);
    }

    public override void OnNetworkDespawn()
    {
        CleanupInputHandlers();
        ClearMissionTargets();
        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadComplete -= OnSceneLoaded;
        base.OnNetworkDespawn();
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
        if (!IsServer || hasStartedMission.Value || IsResolvingMission) return;
        if (missionName == "") return;
        if (CampaignProgression.RequiresFinalMission(teamRating.Value, campaignCompleted.Value) &&
            missionName != CampaignProgression.FinalMissionName) return;

        ClearMissionTargets();
        endReturnLocked.Value = missionName == CampaignProgression.FinalMissionName;

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
        BeginStopMission(false);
    }

    private void BeginStopMission(bool timeExpired)
    {
        if (!IsServer) return;
        if (stopMissionRoutine != null) return;
        stopMissionRoutine = StartCoroutine(StopMissionClock(timeExpired));
    }

    private IEnumerator StopMissionClock(bool timeExpired)
    {
        //revive if died in lobby
        if (!hasStartedMission.Value)
        {
            yield return new WaitForSeconds(5f);
            RevivePlayers();
            stopMissionRoutine = null;
            yield break;
        }

        bool returnedToBase = HasLivingPlayerAtBase();
        bool missionSuccessful = missionEnemiesReady && missionTargets.Count == dangerLevels.Length &&
            CampaignProgression.IsMissionSuccessful(dangerLevels.Length, capturedTargetCount, returnedToBase);
        bool completedFinalMission = missionSuccessful && missionName == CampaignProgression.FinalMissionName;
        int tempRating = CampaignProgression.CalculateRating(teamRating.Value, missionSuccessful, !returnedToBase);

        endReturnLocked.Value = false;
        KillEnemies();


        //stop the mission (turn off the portals)
        missionName = string.Empty;
        hasStartedMission.Value = false;
        yield return new WaitForSeconds(4f);


        teamRating.Value = tempRating;

        //kill players out of base
        if (alivePlayers > 0)
        {
            KillOutOfRangePlayers(timeExpired);
        }

        DestroyOutOfRangeItems();
        PreserveItemsBroughtToBase();

        //unload world
        UnloadWorld();


        //revive
        RevivePlayers();

        Shop.Instance?.EnsureFreeLeafBlowerServer();

        if (completedFinalMission) campaignCompleted.Value = true;
        currentSave?.Save();
        ClearMissionTargets();

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
            if (player.PlayerObject == null) continue;

            Debug.Log($"---Mission: Reviving player: {player.ClientId}");
            if (player.PlayerObject.TryGetComponent<Human>(out var human))
            {
                if (human.isDead.Value)
                {
                    human.isDead.Value = false;
                    human.Teleport(spawnPoint);
                }
                human.entityHealth.Value = human.dangerLevel;
            }
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
    public void RegisterMissionEnemy(Enemy.Enemy enemy)
    {
        if (!IsServer || enemy == null || missionTargets.Contains(enemy)) return;
        activeEnemies.Add(enemy);
        missionTargets.Add(enemy);
        enemy.CapturedServer += OnMissionTargetCaptured;
        enemy.isDead.OnValueChanged += OnMissionTargetDeathChanged;
    }

    private void OnMissionTargetCaptured(Entity target, NetworkObject cage)
    {
        if (!IsServer || !hasStartedMission.Value || IsResolvingMission) return;
        target.CapturedServer -= OnMissionTargetCaptured;
        capturedTargetCount++;
    }

    private void OnMissionTargetDeathChanged(bool previous, bool current)
    {
        if (current) TryUnlockEndReturn();
    }

    private void TryUnlockEndReturn()
    {
        if (!IsServer || !endReturnLocked.Value || !hasStartedMission.Value || IsResolvingMission ||
            !missionEnemiesReady || missionTargets.Count == 0 || missionTargets.Count != dangerLevels.Length) return;

        foreach (Enemy.Enemy target in missionTargets)
        {
            if (target != null && !target.isDead.Value) return;
        }
        endReturnLocked.Value = false;
    }

    private void ClearMissionTargets()
    {
        foreach (Enemy.Enemy target in missionTargets)
        {
            if (target == null) continue;
            target.CapturedServer -= OnMissionTargetCaptured;
            target.isDead.OnValueChanged -= OnMissionTargetDeathChanged;
        }
        missionTargets.Clear();
        capturedTargetCount = 0;
        missionEnemiesReady = false;
    }

    private bool HasLivingPlayerAtBase()
    {
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<Human>(out var human) &&
                !human.isDead.Value && IsWithinBaseRange(human.transform.position)) return true;
        }
        return false;
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
        if (!IsServer || sceneName != currentSceneName || mode != LoadSceneMode.Additive) return;
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (!loadedScene.IsValid() || !loadedScene.isLoaded) return;
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded;
        if (loadedScene.IsValid())
        {
            missionScene = loadedScene;

            // Must set active scene BEFORE spawning enemies so that
            // NavMesh.SamplePosition queries the mission scene's NavMesh,
            // not the still-active Lobby scene.
            SceneManager.SetActiveScene(loadedScene);
            UpdateLocalSceneRpc(sceneName);
        }

        missionEnemiesReady = spawner.SpawnEnemies(dangerLevels, sceneName == CampaignProgression.FinalMissionName);
        if (!missionEnemiesReady)
        {
            Debug.LogError("Mission target setup failed; returning to the base without awarding progress.");
            BeginStopMission(false);
            return;
        }
        TryUnlockEndReturn();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void UpdateLocalSceneRpc(string sceneName) {
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid())
        {
            missionScene = loadedScene;
        }
        DynamicGI.UpdateEnvironment();
    }

    private void KillEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null && enemy.IsSpawned) enemy.isDead.Value = true;
        }
    }

    private void KillOutOfRangePlayers(bool timeExpired)
    {
        foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
        {
            var human = player.PlayerObject.gameObject.GetComponent<Human>();
            if (human.isDead.Value) continue;

            float distanceToSpawn = Vector3.Distance(human.transform.position, spawnPoint);
            if (distanceToSpawn > baseRadius) human.KillForMissionEnd(timeExpired);
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
            if (enemy == null) continue;
            if (enemy.IsSpawned) enemy.NetworkObject.Despawn(true);
            else Destroy(enemy.gameObject);
        }
        activeEnemies.Clear();

        Scene sceneToUnload = SceneManager.GetSceneByName(currentSceneName);
        NetworkManager.Singleton.SceneManager.UnloadScene(sceneToUnload);
        currentSceneName = null;
        missionScene = default;
    }

    #endregion

    private void Radiation()
    {
        if (!hasStartedMission.Value || !IsServer) return;

        missionDuration -= 1;
        if (missionDuration <= 0)
        {
            BeginStopMission(true);
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
