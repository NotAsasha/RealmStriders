using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace InventorySystem
{
    public abstract class Item : NetworkBehaviour, ITakable
    {
        [Header("Item Settings")]
        [SerializeField] protected bool isSingleUse = true;

        // Cached components
        private Collider itemCollider;
        private Rigidbody itemRigidbody;
        private NetworkTransform itemNetworkTransform;
        private NetworkRigidbody itemNetworkRigidbody;

        // State tracking
        protected bool isCurrentlyHeld = false;

        #region Unity Lifecycle

        private void Awake()
        {
            CacheComponents();
        }
        #endregion

        #region Component Management

        private void CacheComponents()
        {
            itemCollider = GetComponent<Collider>();
            itemRigidbody = GetComponent<Rigidbody>();
            itemNetworkTransform = GetComponent<NetworkTransform>();
            itemNetworkRigidbody = GetComponent<NetworkRigidbody>();
        }

        #endregion

        #region ITakable Implementation

        public bool IsSingleUse() => isSingleUse;

        public void Interact(GameObject player)
        {
            Take(player);
        }

        public void StopInteraction(GameObject player)
        {
            Drop(player);
        }

        virtual public void Take(GameObject player)
        {
            if (isCurrentlyHeld)
            {
                Debug.LogWarning("Cannot take item - item already held");
                return;
            }

            Inventory inventory = player.GetComponentInParent<Inventory>();

            bool wasAdded = inventory.AddItem(gameObject);
            if (wasAdded)
            {
                isCurrentlyHeld = true;

                SetPhysicsState(false);

                TakeServerRpc();
            }
            else
            {
                Debug.Log("Inventory is full or item couldn't be added!");
            }
        }

        virtual public void Drop(GameObject player)
        {
            if (!isCurrentlyHeld)
            {
                Debug.LogWarning("Trying to drop item that isn't held");
                return;
            }

            isCurrentlyHeld = false;

            // Відразу включаємо фізику локально
            SetPhysicsState(true);

            DropServerRpc();

        }

        virtual public void Use(GameObject player)
        {
            if (player == null)
            {
                Debug.LogWarning("Player is null in Use!");
                return;
            }

            Debug.Log($"Using {gameObject.name} by {player.name}");

            // Add your leaf blower functionality here
            ExecuteItemAction(player);
        }

        public GameObject GetGameObject() => gameObject;

        #endregion

        #region Item Specific Functionality

        abstract protected void ExecuteItemAction(GameObject player);

        virtual protected void HandleSingleUseItem(GameObject player)
        {
            Debug.Log("Single-use item consumed!");

            // Remove from inventory
            Inventory inventory = player.GetComponentInParent<Inventory>();
            if (inventory != null)
            {
                // Find this item in inventory and remove it
                for (int i = 0; i < inventory.capacity; i++)
                {
                    if (inventory.GetItem(i)?.GetGameObject() == gameObject)
                    {
                        inventory.RemoveItem(i);
                        break;
                    }
                }
            }
            DestroyItemServerRpc();
        }

        #endregion

        #region Network RPCs

        [ServerRpc(RequireOwnership = false)]
        private void TakeServerRpc()
        {
            // Синхронізуємо стан з усіма клієнтами, крім того хто взяв
            TakeClientRpc();
        }

        [ClientRpc]
        private void TakeClientRpc()
        {
            SetPhysicsState(false);
            isCurrentlyHeld = true;

        }

        [ServerRpc(RequireOwnership = false)]
        private void DropServerRpc()
        {
            DropClientRpc();
        }

        [ClientRpc]
        private void DropClientRpc()
        {
            SetPhysicsState(true);
            isCurrentlyHeld = false;
        }

        [ServerRpc(RequireOwnership = false)]
        protected void DestroyItemServerRpc()
        {
            if (NetworkObject != null)
            {
                NetworkObject.Despawn(true);
            }
        }

        #endregion

        #region Physics Management

        private void SetPhysicsState(bool isPhysicsEnabled)
        {
            itemCollider.enabled = isPhysicsEnabled;
            itemRigidbody.isKinematic = !isPhysicsEnabled;
            itemNetworkTransform.enabled = isPhysicsEnabled;
            itemNetworkRigidbody.enabled = isPhysicsEnabled;
        }

        #endregion

        #region Debug

        [ContextMenu("Debug Item State")]
        private void DebugItemState()
        {
            Debug.Log($"Item {gameObject.name} - Held: {isCurrentlyHeld}, " +
                     $"Collider Enabled: {itemCollider?.enabled}, " +
                     $"Rigidbody Kinematic: {itemRigidbody?.isKinematic}");
        }

        #endregion
    }
}
