using Player;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace Game.Casino
{
    public sealed class CasinoCagePedestal : NetworkBehaviour, IInteractable
    {
        [SerializeField] private NetworkObject acceptedCagePrefab;
        [SerializeField] private Transform placementPoint;
        [SerializeField] private GameObject installedCage;
        [SerializeField] private CasinoDoorPresentation presentation;
        [SerializeField, Min(0.1f)] private float interactionDistance = 4f;

        private readonly NetworkVariable<double> activationTime = new(-1d);
        public bool IsActivated => activationTime.Value >= 0d;

        public bool IsSingleUse() => true;
        public bool IsTaken() => IsActivated;
        public void StopInteraction() { }

        public void Interact(GameObject interactor)
        {
            if (!IsSpawned || IsActivated || interactor == null) return;
            var inventory = interactor.GetComponentInParent<Inventory>();
            if (inventory != null && inventory.IsOwner)
                PlaceCageRpc(new NetworkObjectReference(inventory.NetworkObject));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PlaceCageRpc(NetworkObjectReference playerReference, RpcParams rpc = default)
        {
            if (!IsServer || IsActivated || acceptedCagePrefab == null ||
                !playerReference.TryGet(out var player) || player.OwnerClientId != rpc.Receive.SenderClientId ||
                !player.TryGetComponent(out Inventory inventory) ||
                (player.TryGetComponent(out Human human) && human.isDead.Value) ||
                (player.transform.position + Vector3.up - placementPoint.position).sqrMagnitude >
                interactionDistance * interactionDistance) return;

            Item cage = inventory.GetActiveItem();
            if (cage == null || !cage.IsSpawned || cage.transform.parent != player.transform ||
                cage.NetworkObject.PrefabIdHash != acceptedCagePrefab.PrefabIdHash) return;

            // Commit once on the server. The mounted mesh has no item/network behaviour,
            // so pickup, sale, drops and late joins cannot return the spent key.
            inventory.ForgetItemServer(cage);
            cage.NetworkObject.Despawn(true);
            activationTime.Value = NetworkManager.ServerTime.Time;
        }

        public override void OnNetworkSpawn()
        {
            activationTime.OnValueChanged += OnActivationChanged;
            ApplyState(false);
        }

        public override void OnNetworkDespawn()
        {
            activationTime.OnValueChanged -= OnActivationChanged;
            presentation.ResetPresentation();
            installedCage.SetActive(false);
        }

        private void OnActivationChanged(double previous, double current) => ApplyState(true);

        private void ApplyState(bool playSound)
        {
            installedCage.SetActive(IsActivated);
            if (!IsActivated) presentation.ResetPresentation();
            else presentation.Sample((float)(NetworkManager.ServerTime.Time - activationTime.Value), playSound);
        }

        private void Update()
        {
            if (!IsSpawned || !IsActivated || presentation.IsComplete) return;
            presentation.Sample((float)(NetworkManager.ServerTime.Time - activationTime.Value), false);
        }
    }
}
