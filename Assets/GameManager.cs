using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Steam;

public class GameManager : NetworkBehaviour
{
    public NetworkVariable<int> teamRating = new(1, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> teamMoney = new(1000, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> hasStartedMission = new(false, writePerm: NetworkVariableWritePermission.Server);

    private int alivePlayers = 0;
    
    public static GameManager instance = null;

    #region Unity Lifecycle

    private void Awake()
    {
        SetupSingletone();
        SetupInputHandlers();
        DontDestroyOnLoad(gameObject);

        Application.targetFrameRate = 240;
    }

    private void OnDisable()
    {
        CleanupInputHandlers();
    }

    #endregion

    #region Initialization

    private void SetupSingletone()
    {
        if (instance != null) Destroy(instance);
        instance = this;
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

    [ServerRpc(RequireOwnership = false)]
    public void OnPlayerDeathServerRpc()
    {
        alivePlayers -= 1;
        Debug.Log($"---MissionManager: Allive players: {alivePlayers}.");
        if (alivePlayers <= 0)
        {
            Debug.Log("---MissionManager: Everyone died, stopping mission.");
           // StopMissionServerRpc();
        }
    }

    #region MissionRpc
    [ServerRpc(RequireOwnership = false)]
    public void StartMissionServerRpc()
    {
        if (hasStartedMission.Value) return;

        //Spawn Monsters

        RevivePlayers();
        alivePlayers = NetworkManager.Singleton.ConnectedClients.Count;

        //Stop Lobby Connections
        if (SteamManager.Instance.CurrentLobby != null)
            SteamManager.Instance.CurrentLobby.Value.SetJoinable(false);

        hasStartedMission.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void StopMissionServerRpc()
    {
        RevivePlayers();
        if (!hasStartedMission.Value) return;
        hasStartedMission.Value = false;

        //Calculate rating



        //Resume Lobby Connections
        if (SteamManager.Instance.CurrentLobby != null)
            SteamManager.Instance.CurrentLobby.Value.SetJoinable(true);
    }
    private void RevivePlayers()
    {
        foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
        {
            var human = player.PlayerObject.gameObject.GetComponent<Human>();
            human.isDead.Value = false;
            human.playerHealth.Value = Human.defaultHealth;
        }
    }
    #endregion


    private void StartMission()
    {
        //Open Portal
    }

    private void StopMission()
    {
        //Close portal
    }
}
