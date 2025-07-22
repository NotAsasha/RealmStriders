using System.Collections;
using System.Collections.Generic;
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
    private void Awake()
    {
        if (instance != null) Destroy(instance);
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        hasStartedMission.OnValueChanged += OnMissionStatusChanged;
    }

    private void OnDisable()
    {
        hasStartedMission.OnValueChanged -= OnMissionStatusChanged;
    }

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
    [ServerRpc]
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
    [ServerRpc(RequireOwnership = false)]
    public void StartMissionServerRpc()
    {
        if (hasStartedMission.Value) return;

        //Spawn Monsters

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
