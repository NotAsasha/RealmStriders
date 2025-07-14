using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Steam;

public class GameManager : NetworkBehaviour
{
    public NetworkVariable<int> teamRating = new(1, writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> hasStartedMission = new(false, writePerm: NetworkVariableWritePermission.Server);


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
            Debug.Log("---GameManager: Start Mission");
            StartMission();
        }
        else
        {
            Debug.Log($"---GameManager: End Mission, Rating before: {teamRating.Value}");
            StopMission();
        }
    }

#region Rpc

    [ServerRpc(RequireOwnership = false)]
    public void StartMissionServerRpc()
    {
        if (hasStartedMission.Value) return;
        hasStartedMission.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void StopMissionServerRpc()
    {
        if (!hasStartedMission.Value) return;
        hasStartedMission.Value = false;
    }
#endregion

    private void StartMission()
    {
        //Stop Lobby Connections
        SteamManager.Instance.CurrentLobby.Value.SetJoinable(false);

        //Open Portal
        //Spawn Monsters
    }
    private void StopMission()
    {
        //Resume Lobby Connections
        SteamManager.Instance.CurrentLobby.Value.SetJoinable(true);

        //Close portal
        //Calculate rating


    }

}
