using System.Collections;
using Player.Equipment;
using Player.Movement;
using Unity.Multiplayer.PlayMode;
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

        [SerializeField, HideInInspector] private int prefabId;
        public int PrefabId => prefabId;
        public virtual string GetInfo() => "";
        public virtual void ApplyInfo(string _) { }

        protected int ownerID = -1;

        #region Unity LifeCycle

        public override void OnNetworkSpawn()
        {
            if (NetworkObject == null) return;
            NetworkObject.Register();
        }

        public override void OnNetworkDespawn()
        {
            ReleaseCurrentPlayer();
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

        private void ReleaseCurrentPlayer()
        {
            var playerMovement = currentPlayer != null ? currentPlayer.GetComponentInParent<PlayerMovement>() : null;
            if (playerMovement != null && playerMovement.human != null)
            {
                playerMovement.human.isDead.OnValueChanged -= OnPlayerDeathStateChanged;
            }

            if (IsSpawned && IsServer)
            {
                isTaken.Value = false;
                currentInteractingClientId.Value = ulong.MaxValue;
            }
            else if (IsSpawned && IsClient)
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
            isTaken.Value = whatToSet;
            currentInteractingClientId.Value = whatToSet ? rpcParams.Receive.SenderClientId : ulong.MaxValue;
        }

        public bool IsTaken() => isTaken.Value;
        public Transform GetCameraPoint() => cameraPoint;

        #endregion

        #region Move

        public void Pack()
        {
            PackServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void PackServerRpc()
        {
            DeactivateRpc();

            GameObject cubePrefab = NetworkItemsHandler.Instance.database.cubeItemPrefab;

            GameObject cubeInstance = Instantiate(cubePrefab, GetComponent<Collider>().bounds.center + Vector3.up * 0.5f, Quaternion.identity);
            cubeInstance.GetComponent<PackedFurnitureItem>().storedPrefabID.Value = this.PrefabId;
            cubeInstance.GetComponent<NetworkObject>().Spawn();


            NetworkObject.Despawn(true);
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DeactivateRpc()
        {
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
