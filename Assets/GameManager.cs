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


    private readonly NetworkVariable<double> missionDeadline = new(0d);
    public int MissionSecondsRemaining => IsSpawned && hasStartedMission.Value
        ? (int)System.Math.Max(0d, System.Math.Ceiling(missionDeadline.Value - NetworkManager.ServerTime.Time))
        : 0;

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
    public static event System.Action FinalTargetsCaptured;
    public EnemySpawner MissionSpawner => spawner;

    #region Unity Lifecycle

    private void Awake()
    {
        SetupSingleton();
        DontDestroyOnLoad(gameObject);

        spawner = GetComponent<EnemySpawner>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SetupInputHandlers();
        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted += OnMissionTransitionCompleted;
                NetworkManager.SceneManager.OnUnloadEventCompleted += OnMissionTransitionCompleted;
            }
        }
        // Initial synchronization via ReadField does not fire OnValueChanged in Netcode.
        // Explicitly trigger OnValueChanged on spawn so UI and listeners receive synced values.
        teamMoney.OnValueChanged?.Invoke(teamMoney.Value, teamMoney.Value);
        teamRating.OnValueChanged?.Invoke(teamRating.Value, teamRating.Value);
        campaignCompleted.OnValueChanged?.Invoke(campaignCompleted.Value, campaignCompleted.Value);
    }

    public override void OnNetworkDespawn()
    {
        CleanupInputHandlers();
        if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        if (stopMissionRoutine != null) StopCoroutine(stopMissionRoutine);
        stopMissionRoutine = null;
        ClearMissionTargets();
        if (NetworkManager != null && NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadComplete -= OnSceneLoaded;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnMissionTransitionCompleted;
            NetworkManager.SceneManager.OnUnloadEventCompleted -= OnMissionTransitionCompleted;
        }
        missionSceneTransitionInProgress = false;
        currentSceneName = null;
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

    private void OnClientDisconnected(ulong clientId)
    {
        if (hasStartedMission.Value) CheckForTeamWipeServer(clientId);
    }

    public void CheckForTeamWipeServer(ulong? departingClientId = null)
    {
        if (!IsSpawned || !IsServer || NetworkManager.ShutdownInProgress || IsResolvingMission) return;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            if (client.ClientId != departingClientId && client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent<Human>(out var human) && !human.isDead.Value) return;
        }
        BeginStopMission(false, true);
    }

    #region MissionRpc

    public void StartMissionServer()
    {
        if (!IsServer || hasStartedMission.Value || IsResolvingMission) return;
        if (missionName == "") return;
        if (CampaignProgression.RequiresFinalMission(teamRating.Value, campaignCompleted.Value) &&
            missionName != CampaignProgression.FinalMissionName) return;
        if (!TryLoadWorld(missionName)) return;

        ClearMissionTargets();
        endReturnLocked.Value = missionName == CampaignProgression.FinalMissionName;

        //Spawn Monsters

        RevivePlayers();

        //Stop Lobby Connections
        if (SteamManager.Instance.CurrentLobby != null)
            SteamManager.Instance.CurrentLobby.Value.SetJoinable(false);

        missionDeadline.Value = NetworkManager.ServerTime.Time +
            Random.Range(defaultMissionTime - maxTimeSpread, defaultMissionTime + maxTimeSpread);

        hasStartedMission.Value = true;
    }

    public void StopMissionServer()
    {
        if (hasStartedMission.Value) BeginStopMission(false);
    }

    private void BeginStopMission(bool timeExpired, bool teamWiped = false)
    {
        if (!IsServer) return;
        if (stopMissionRoutine != null) return;
        stopMissionRoutine = StartCoroutine(StopMissionClock(timeExpired, teamWiped));
    }

    private IEnumerator StopMissionClock(bool timeExpired, bool teamWiped)
    {
        //revive if died in lobby
        if (!hasStartedMission.Value)
        {
            yield return new WaitForSeconds(5f);
            RevivePlayers();
            stopMissionRoutine = null;
            yield break;
        }

        bool returnedToBase = !teamWiped && HasLivingPlayerAtBase();
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
        KillOutOfRangePlayers(timeExpired);

        DestroyOutOfRangeItems();
        PreserveItemsBroughtToBase();

        //unload world
        yield return UnloadWorld();


        //revive
        RevivePlayers();

        Shop.Instance?.EnsureFreeLeafBlowerServer();

        if (completedFinalMission) campaignCompleted.Value = true;
        if (currentSave != null) BroadcastAutosaveResultRpc(currentSave.Save());
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

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastAutosaveResultRpc(bool succeeded)
    {
        SaveGameButton.NotifySaveCompleted(succeeded);
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
        if (missionName == CampaignProgression.FinalMissionName && missionEnemiesReady &&
            capturedTargetCount == dangerLevels.Length)
            NotifyFinalCaptureClientRpc();
    }

    [ClientRpc]
    private void NotifyFinalCaptureClientRpc()
    {
        if (IsClient) FinalTargetsCaptured?.Invoke();
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
    private bool missionSceneTransitionInProgress;

    private bool TryLoadWorld(string sceneToLoad)
    {
        if (currentSceneName != null || NetworkManager.SceneManager == null) return false;
        var status = NetworkManager.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Additive);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogWarning($"Could not start mission scene {sceneToLoad}: {status}.");
            return false;
        }

        currentSceneName = sceneToLoad;
        missionSceneTransitionInProgress = true;
        NetworkManager.SceneManager.OnLoadComplete += OnSceneLoaded;
        return true;
    }

    private void OnMissionTransitionCompleted(string sceneName, LoadSceneMode mode,
        List<ulong> completed, List<ulong> timedOut)
    {
        if (sceneName == currentSceneName)
            missionSceneTransitionInProgress = false;
    }

    private void OnSceneLoaded(ulong conn, string sceneName, LoadSceneMode mode)
    {
        if (!IsServer || conn != NetworkManager.ServerClientId || sceneName != currentSceneName ||
            mode != LoadSceneMode.Additive) return;
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (!loadedScene.IsValid() || !loadedScene.isLoaded) return;
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded;
        if (!hasStartedMission.Value || IsResolvingMission) return;
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
            if (player.PlayerObject == null || !player.PlayerObject.TryGetComponent<Human>(out var human) ||
                human.isDead.Value) continue;

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

    public bool IsWithinBaseRange(Vector3 position)
    {
        Vector3 offset = position - spawnPoint;
        return offset.sqrMagnitude <= baseRadius * baseRadius;
    }

    private IEnumerator UnloadWorld()
    {
        if (currentSceneName == null)
        {
            Debug.LogError("---MissionManager: Trying to stop non-existing mission.");
            yield break;
        }

        foreach (var enemy in activeEnemies)
        {
            if (enemy == null) continue;
            if (enemy.IsSpawned) enemy.NetworkObject.Despawn(true);
            else Destroy(enemy.gameObject);
        }
        activeEnemies.Clear();

        // A wipe can arrive while another peer is still synchronizing the additive load.
        while (missionSceneTransitionInProgress) yield return null;
        Scene sceneToUnload = SceneManager.GetSceneByName(currentSceneName);
        if (sceneToUnload.IsValid() && sceneToUnload.isLoaded)
        {
            var status = NetworkManager.SceneManager.UnloadScene(sceneToUnload);
            if (status == SceneEventProgressStatus.Started)
            {
                missionSceneTransitionInProgress = true;
                while (missionSceneTransitionInProgress) yield return null;
            }
            else
                Debug.LogError($"Could not unload mission scene {currentSceneName}: {status}.");
        }
        currentSceneName = null;
        missionScene = default;
    }

    #endregion

    private void Update()
    {
        if (IsServer && hasStartedMission.Value && NetworkManager.ServerTime.Time >= missionDeadline.Value)
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
