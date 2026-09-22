using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Animations;

namespace Player.Equipment
{
    public class Item : NetworkBehaviour, IInteractable, INetworkSaveable
    {
        [Header("Item Settings")]
        [SerializeField] protected bool isConsumable = false;
        [SerializeField] public int sellPrice = 10;
        [SerializeField] public AudioClip[] clickSounds;

        [Header("Grip Settings")]
        [Tooltip("Optional child transform defining where the hand holds this item")]
        [SerializeField] public Transform gripPoint;
        [Tooltip("Position offset applied to handAnchor if gripPoint is not assigned")]
        [SerializeField] public Vector3 gripPositionOffset;
        [Tooltip("Rotation offset (Euler angles) applied to handAnchor if gripPoint is not assigned")]
        [SerializeField] public Vector3 gripRotationOffset;

        public (Vector3 translationOffset, Vector3 rotationOffset) GetGripOffsets()
        {
            if (gripPoint != null)
            {
                Quaternion invRot = Quaternion.Inverse(gripPoint.localRotation);
                Vector3 rot = invRot.eulerAngles;
                Vector3 trans = -(invRot * gripPoint.localPosition);
                return (trans, rot);
            }
            return (gripPositionOffset, gripRotationOffset);
        }

        [Header("Save System")]
        [SerializeField, HideInInspector] private int prefabId;
        public int PrefabId => prefabId;
        public void SetPrefabId(int id) => prefabId = id;

        public virtual string GetInfo() => "";
        public virtual void ApplyInfo(string _) {}

        private Collider itemCollider;
        private Rigidbody itemRigidbody;
        private NetworkTransform itemNetworkTransform;
        private NetworkRigidbody itemNetworkRigidbody;
        protected AudioSource audioSource;

        public bool isCurrentlyHeld = false;

        #region Unity Lifecycle

        private void Awake()
        {
            CacheComponents();
        }

        public override void OnNetworkSpawn()
        {
            this.NetworkObject.Register();
            CheckLateJoinParent();
        }

        public override void OnNetworkDespawn()
        {
            this.NetworkObject.UnRegister();
        }

        public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            base.OnNetworkObjectParentChanged(parentNetworkObject);

            if (parentNetworkObject != null && parentNetworkObject.TryGetComponent<Inventory>(out var inventory))
            {
                AttachToPlayer(inventory);
            }
            else if (parentNetworkObject == null)
            {
                DetachFromPlayer();
            }
        }

        private void CheckLateJoinParent()
        {
            if (transform.parent != null && transform.parent.TryGetComponent<Inventory>(out var inventory))
            {
                AttachToPlayer(inventory);
            }
        }

        private void AttachToPlayer(Inventory inventory)
        {
            isCurrentlyHeld = true;
            SetPhysicsState(false);

            if (itemNetworkTransform != null)
            {
                itemNetworkTransform.enabled = false;
            }

            if (inventory.HandAnchor != null)
            {
                inventory.ApplyParentConstraint(gameObject, inventory.HandAnchor);
            }
        }

        private void DetachFromPlayer()
        {
            isCurrentlyHeld = false;
            SetPhysicsState(true);

            if (itemNetworkTransform != null)
            {
                itemNetworkTransform.enabled = true;
            }

            ParentConstraint constraint = GetComponent<ParentConstraint>();
            if (constraint != null)
            {
                while (constraint.sourceCount > 0) constraint.RemoveSource(0);
                constraint.constraintActive = false;
            }
        }

        #endregion

        #region Component Management

        private void CacheComponents()
        {
            itemCollider = GetComponent<Collider>();
            itemRigidbody = GetComponent<Rigidbody>();
            itemNetworkTransform = GetComponent<NetworkTransform>();
            itemNetworkRigidbody = GetComponent<NetworkRigidbody>();
            audioSource = GetComponent<AudioSource>();
        }

        #endregion

        #region Item Implementation

        public bool IsConsumable() => isConsumable;
        public bool IsSingleUse() => true;


        public void Interact(GameObject player)
        {
            Take(player);
        }

        public void StopInteraction()
        {
            Drop();
        }

        virtual public void Take(GameObject player)
        {
            if (isCurrentlyHeld)
            {
                Debug.LogWarning("---Item: Cannot take - already held");
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
                Debug.Log("---Item: Inventory is full or idk what happened");
            }
        }

        virtual public void Drop()
        {
            if (!isCurrentlyHeld)
            {
                Debug.LogWarning("---Item: Trying to drop item that isn't held");
                return;
            }

            isCurrentlyHeld = false;

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

            //Do things
            if (clickSounds.Length > 0 && audioSource != null)
            {
                audioSource.PlayOneShot(clickSounds[UnityEngine.Random.Range(0, clickSounds.Length)]);
            }
            ExecuteItemAction(player);
        }

        #endregion

        #region Item Specific Functionality

        virtual protected void ExecuteItemAction(GameObject player) { return; }

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
                    if (inventory.GetItem(i)?.gameObject == gameObject)
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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void TakeServerRpc()
        {
            TakeClientRpc();
        }

        [ClientRpc]
        private void TakeClientRpc()
        {
            SetPhysicsState(false);
            isCurrentlyHeld = true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DestroyItemServerRpc()
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
            //itemCollider.enabled = isPhysicsEnabled;
            itemRigidbody.isKinematic = !isPhysicsEnabled;
            //itemNetworkTransform.enabled = isPhysicsEnabled;
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
