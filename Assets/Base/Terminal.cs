using Player;
using Player.Equipment;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;

namespace Base
{
    public class Terminal : NetworkBehaviour, IInteractable, IMovable, INetworkSaveable
    {
        [Header("Terminal Settings")]
        public int terminalPrefabID;

        [SerializeField] protected Canvas terminalCanvas;
        [SerializeField] private Transform cameraPoint;
        [SerializeField] protected AudioSource interactSound;
        [SerializeField, Min(0.1f)] private float serverInteractionReach = 4f;

        [SerializeField, Min(-1)] private int prefabId = -1;
        public int PrefabId => prefabId;
        public virtual string GetInfo() => "";
        public virtual void ApplyInfo(string _) { }

        protected int ownerID = -1;

        #region Unity LifeCycle

        public override void OnNetworkSpawn()
        {
            if (NetworkObject == null) return;
            NetworkObject.Register();
            if (IsServer) NetworkManager.OnClientDisconnectCallback += ReleaseDepartingOperator;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= ReleaseDepartingOperator;
            ReleaseCurrentPlayer(false);
            if (NetworkObject != null) NetworkObject.UnRegister();
        }

        #endregion

        #region Interaction
        public NetworkVariable<bool> isTaken = new(writePerm: NetworkVariableWritePermission.Server);
        public NetworkVariable<ulong> currentInteractingClientId = new(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private GameObject currentPlayer;

        public void SetPrefabId(int id)
        {
            prefabId = id;
        }

        public void Interact(GameObject player)
        {
            if (!IsSpawned || !IsClient || player == null) return;

            var networkObject = player.GetComponentInParent<NetworkObject>();
            var playerMovement = player.GetComponentInParent<PlayerMovement>();
            if (networkObject == null || !networkObject.IsSpawned || playerMovement == null ||
                !playerMovement.IsOwner || playerMovement.human == null ||
                playerMovement.human.isDead.Value || isTaken.Value)
            {
                return;
            }

            SetTakenServerRpc(true);
            currentPlayer = player;

            playerMovement.human.isDead.OnValueChanged -= OnPlayerDeathStateChanged;
            playerMovement.human.isDead.OnValueChanged += OnPlayerDeathStateChanged;

            //activate UI
            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null && terminalCanvas != null)
            {
                terminalCanvas.worldCamera = camera;
            }

            ownerID = (int)networkObject.OwnerClientId;
            if (interactSound != null) interactSound.Play();
        }

        public void StopInteraction()
        {
            ReleaseCurrentPlayer();
        }

        private void OnPlayerDeathStateChanged(bool _, bool isDead)
        {
            if (isDead) ReleaseCurrentPlayer();
        }

        private void ReleaseCurrentPlayer(bool notifyServer = true)
        {
            var playerMovement = currentPlayer != null ? currentPlayer.GetComponentInParent<PlayerMovement>() : null;
            if (playerMovement != null && playerMovement.human != null)
            {
                playerMovement.human.isDead.OnValueChanged -= OnPlayerDeathStateChanged;
            }

            if (notifyServer && currentPlayer != null && IsSpawned && IsClient)
            {
                SetTakenServerRpc(false);
            }
            currentPlayer = null;
            ownerID = -1;
            if (terminalCanvas != null) terminalCanvas.worldCamera = null;
            if (interactSound != null) interactSound.Stop();
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetTakenServerRpc(bool whatToSet, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!whatToSet)
            {
                ReleaseDepartingOperator(sender);
                return;
            }
            if ((isTaken.Value && currentInteractingClientId.Value != sender) || !IsPlayerInReach(sender))
            {
                RejectInteractionRpc(RpcTarget.Single(sender, RpcTargetUse.Temp));
                return;
            }
            currentInteractingClientId.Value = sender;
            isTaken.Value = true;
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server, DeferLocal = true)]
        private void RejectInteractionRpc(RpcParams rpcParams = default)
        {
            if (currentPlayer != null) currentPlayer.GetComponent<CameraMovement>()?.StopInteraction();
        }

        private void ReleaseDepartingOperator(ulong clientId)
        {
            if (!IsServer || currentInteractingClientId.Value != clientId) return;
            currentInteractingClientId.Value = ulong.MaxValue;
            isTaken.Value = false;
        }

        public bool IsOperatedByServer(ulong clientId)
        {
            return IsServer && isTaken.Value && currentInteractingClientId.Value == clientId && IsPlayerInReach(clientId);
        }

        private bool IsPlayerInReach(ulong clientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) ||
                client.PlayerObject == null || !client.PlayerObject.TryGetComponent<Human>(out var human) ||
                human.isDead.Value || human.IsBeingCaptured) return false;
            Vector3 origin = human.transform.position + Vector3.up;
            return isActiveAndEnabled && TryGetComponent<Collider>(out var surface) && surface.enabled &&
                   (surface.bounds.ClosestPoint(origin) - origin).sqrMagnitude <= serverInteractionReach * serverInteractionReach;
        }

        public bool IsTaken() => isTaken.Value;
        public Transform GetCameraPoint() => cameraPoint;

        #endregion

        #region Move

        public void Pack()
        {
            if (!IsServer || !IsSpawned || NetworkItemsHandler.Instance == null ||
                !NetworkItemsHandler.Instance.database.TryGetTerminalPrefab(PrefabId, out _)) return;
            DeactivateServer();

            GameObject cubePrefab = NetworkItemsHandler.Instance.database.cubeItemPrefab;

            GameObject cubeInstance = Instantiate(cubePrefab, GetComponent<Collider>().bounds.center + Vector3.up * 0.5f, Quaternion.identity);
            cubeInstance.GetComponent<PackedFurnitureItem>().storedPrefabID.Value = this.PrefabId;
            cubeInstance.GetComponent<NetworkObject>().Spawn();
            NetworkItemsHandler.Instance.RecordPackedSceneFurniture(NetworkObject);


            NetworkObject.Despawn(true);
        }


        protected void DeactivateServer()
        {
            if (!IsServer) return;
            currentInteractingClientId.Value = ulong.MaxValue;
            DeactivateClientRpc();
            isTaken.Value = false;
        }

        [ClientRpc]
        public void DeactivateClientRpc()
        {
            if (currentPlayer != null) currentPlayer.GetComponent<CameraMovement>().StopInteraction();
            if (interactSound != null) interactSound.Stop();
            ownerID = -1;
        }



        #endregion
    }
}
