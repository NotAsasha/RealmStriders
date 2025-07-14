using Player;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class Terminal : NetworkBehaviour, IInteractable
{
    [SerializeField] private Canvas terminalCanvas;
    [SerializeField] private Vector3 CameraPositionOffset = new Vector3(-1f, 1.4f, -0.5f);
    [SerializeField] private Quaternion CameraRotation = Quaternion.Euler(20, 90, 0);
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

        terminalCanvas.worldCamera = _player.GetComponent<Camera>();
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

}
