using Player;
using Unity.Netcode;
using UnityEngine;
public class Lever : NetworkBehaviour
{
    public void SwitchMissionState()
    {
        if (!GameManager.instance.hasStartedMission.Value)
            GameManager.instance.StartMissionServerRpc();
        else
            GameManager.instance.StopMissionServerRpc();
    }

    // Tut bug, new players will not have the color updated on join, NetworkVariables synchronize after the OnNetworkSpawn call
    protected override void OnNetworkPostSpawn()
    {
        GameManager.instance.hasStartedMission.OnValueChanged += OnMissionStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        GameManager.instance.hasStartedMission.OnValueChanged -= OnMissionStateChanged;
    }

    private void OnMissionStateChanged(bool oldValue, bool newValue)
    {
        //Debug.LogError($"OnMissionStateChanged, newValue = {newValue}");
        GetComponent<Renderer>().material.color = newValue ? Color.gray : Color.white; 
    }
}
