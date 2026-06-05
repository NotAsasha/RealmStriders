using System.Collections;
using Player.Equipment;
using Player.Movement;
using Unity.Multiplayer.PlayMode;
using Unity.Netcode;
using UnityEngine;

namespace Base
{
    // If terminal is moved and saved, it just creates a copy of it. So need to add a check whether terminal is dublicated. TODO
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

        public override void OnNetworkDespawn()
        {
            this.NetworkObject.UnRegister();
        }

        #endregion

        #region Interaction
        public NetworkVariable<bool> isTaken = new(writePerm: NetworkVariableWritePermission.Server);

        private GameObject currentPlayer;

        public void SetPrefabId(int id)
        {
            prefabId = id;
        }

        public void Interact(GameObject player)
        {
            SetTakenServerRpc(true);
            currentPlayer = player;

            //activate UI
            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
            {
                terminalCanvas.worldCamera = camera;
            }

            ownerID = (int)player.GetComponentInParent<NetworkObject>().OwnerClientId;
            if (interactSound != null) interactSound.Play();
        }

        public void StopInteraction()
        {
            SetTakenServerRpc(false);

            if (currentPlayer ==  null)
            {
                Debug.LogError("Stopping Interaction, but there is no player");
            }

            ownerID = -1;
            if (interactSound != null) interactSound.Stop();
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetTakenServerRpc(bool whatToSet) => isTaken.Value = whatToSet;

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
