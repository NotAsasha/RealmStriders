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
        var movement = Movement.instance;
        movement.isInInteraction = true;
        movement._controls.Gameplay.Movement.Disable();
        movement._controls.Gameplay.Jump.Disable();
        movement._controls.UI.Enable();
        Cursor.lockState = CursorLockMode.None;
        _player.transform.position = transform.position + CameraPositionOffset;
        _player.transform.rotation = CameraRotation;

        leverCanvas.worldCamera = _player.GetComponent<Camera>();
        // можливо, активуй UI терміналу
    }

    public void StopInteraction(GameObject _player)
    {
        SetTakenServerRpc(false);

        var movement = Movement.instance;
        movement.isInInteraction = false;
        Cursor.lockState = CursorLockMode.Locked;
        movement._controls.Gameplay.Movement.Enable();
        movement._controls.Gameplay.Jump.Enable();
        movement._controls.UI.Disable();
        _player.transform.localPosition = _player.GetComponent<CameraMovement>().StartPosition;
        // закрий UI
    }
    [ServerRpc(RequireOwnership = false)]
    public void SetTakenServerRpc(bool _whatToSet)
    {
        _isTaken.Value = _whatToSet;
    }

    public bool IsTaken() { return _isTaken.Value; }

    #endregion



    
    [ServerRpc(RequireOwnership = false)]
    public void SwitchMissionStateServerRpc()
    {
        GameManager.instance.hasStartedMission.Value = !GameManager.instance.hasStartedMission.Value;
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
