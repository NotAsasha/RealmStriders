using Player;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;

public class Lever : NetworkBehaviour, IInteractable
{
    [SerializeField] private Canvas leverCanvas;
    [SerializeField] private Vector3 CameraPositionOffset = new Vector3(0.25f, 0.25f, -0.5f);
    [SerializeField] private Quaternion CameraRotation = Quaternion.Euler(0, 0, 0);

    #region Interatcion
    private NetworkVariable<bool> _isTaken = new(writePerm: NetworkVariableWritePermission.Server);
    public void Interact(GameObject _player)
    {
        SetTakenServerRpc(true);

        _player.transform.position = transform.position + CameraPositionOffset;
        _player.transform.rotation = CameraRotation;

        leverCanvas.worldCamera = _player.GetComponent<Camera>();
    }

    public void StopInteraction(GameObject _player)
    {
        SetTakenServerRpc(false);

        _player.transform.localPosition = _player.GetComponent<CameraMovement>().StartPosition;
    }
    [ServerRpc(RequireOwnership = false)]
    public void SetTakenServerRpc(bool _whatToSet)
    {
        _isTaken.Value = _whatToSet;
    }

    public bool IsTaken() { return _isTaken.Value; }

    #endregion


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
