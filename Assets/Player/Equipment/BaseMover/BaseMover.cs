using Player.Movement;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Taser
{
    public class BaseMover : Item
    {
        [SerializeField] float maxDistance = 5f;

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            if (CameraMovement.Instance == null) return;
            Transform aim = CameraMovement.Instance.transform;
            RequestPackRpc(aim.position, aim.forward);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPackRpc(Vector3 origin, Vector3 forward, RpcParams rpcParams = default)
        {
            if (!TryGetUsableHolderServer(rpcParams.Receive.SenderClientId, out var holder) ||
                !IsValidAim(holder, origin, forward, Physics.DefaultRaycastLayers)) return;
            if (Physics.Raycast(origin, forward, out RaycastHit hit, maxDistance) &&
                hit.collider.TryGetComponent<IMovable>(out var movable)) movable.Pack();
        }
        #endregion
    }
}
