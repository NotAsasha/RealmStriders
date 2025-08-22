using Player;
using UnityEngine;
using Unity.Netcode;
public class Terminal : NetworkBehaviour, IInteractable
{
    [SerializeField] protected Canvas terminalCanvas;
    [SerializeField] private Vector3 CameraPositionOffset = new Vector3(-1f, 1.4f, -0.5f);
    [SerializeField] private Quaternion CameraRotation = Quaternion.Euler(20, 90, 0);

    [SerializeField] AudioSource interactSound;

    protected int ownerID = -1;

    #region Interatcion
    private NetworkVariable<bool> _isTaken = new(writePerm: NetworkVariableWritePermission.Server);
    public void Interact(GameObject _player)
    {
        SetTakenServerRpc(true);

        _player.transform.position = transform.position + CameraPositionOffset;
        _player.transform.eulerAngles = CameraRotation.eulerAngles + transform.eulerAngles;
        terminalCanvas.worldCamera = _player.GetComponent<Camera>();

        ownerID = (int)_player.GetComponentInParent<NetworkObject>().OwnerClientId;

        if (interactSound != null) interactSound.Play();
    }

    public void StopInteraction(GameObject _player)
    {
        SetTakenServerRpc(false);

        _player.transform.localPosition = _player.GetComponent<CameraMovement>().StartPosition;
        ownerID = -1;
        if (interactSound != null) interactSound.Stop();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetTakenServerRpc(bool _whatToSet)
    {
        _isTaken.Value = _whatToSet;
    }

    public bool IsTaken() => _isTaken.Value;
    #endregion
}
