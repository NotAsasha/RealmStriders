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


        [Header("Audio Variation")]
        [Range(0f, 0.3f)] public float pitchVariation = 0.08f;


        [SerializeField] public AudioClip[] clickSounds;

        [Tooltip("Icon displayed in inventory slots when held")]
        [SerializeField] private Sprite icon;
        public Sprite Icon
        {
            get => icon;
            set => icon = value;
        }


        [Header("Grip Settings")]
        [Tooltip("Optional child transform defining where the hand holds this item")]
        [SerializeField] public Transform gripPoint;
        [Tooltip("Position offset applied to handAnchor if gripPoint is not assigned")]
        [SerializeField] public Vector3 gripPositionOffset;
        [Tooltip("Rotation offset (Euler angles) applied to handAnchor if gripPoint is not assigned")]
        [SerializeField] public Vector3 gripRotationOffset;

        

        [Header("Held Poses (relative to the player's grip pose)")]
        [SerializeField] private Vector3 viewPositionOffset;
        [SerializeField] private Vector3 viewRotationOffset;
        [SerializeField] private Vector3 remotePositionOffset;
        [SerializeField] private Vector3 remoteRotationOffset;
        [SerializeField] private Vector3 actionViewPositionOffset = new(-0.08f, 0.10f, -0.08f);
        [SerializeField] private Vector3 actionViewRotationOffset = new(-10f, -15f, 0f);
        [SerializeField] private Vector3 actionRemotePositionOffset = new(-0.08f, 0.22f, -0.12f);
        [SerializeField] private Vector3 actionRemoteRotationOffset = new(-15f, -15f, 0f);
        [SerializeField, Min(0.01f)] private float poseBlendDuration = 0.2f;
        [Tooltip("Optional additive pose animated by a dedicated Animator. Never written by ItemHolder.")]
        [SerializeField] private Transform holdAnimationOffset;

        public virtual bool IsHoldActionActive => false;
        public float PoseBlendDuration => poseBlendDuration;
        public bool IsEquipped => TryGetHolderInventory(out var inventory) && inventory.IsActiveItem(this);

        public Pose GetHeldPoseOffset(bool firstPerson, float actionWeight)
        {
            Vector3 position = firstPerson ? viewPositionOffset : remotePositionOffset;
            Vector3 rotation = firstPerson ? viewRotationOffset : remoteRotationOffset;
            position += (firstPerson ? actionViewPositionOffset : actionRemotePositionOffset) * actionWeight;
            Quaternion orientation = Quaternion.Euler(rotation) * Quaternion.Slerp(Quaternion.identity,
                Quaternion.Euler(firstPerson ? actionViewRotationOffset : actionRemoteRotationOffset), actionWeight);
            if (holdAnimationOffset != null)
            {
                position += orientation * holdAnimationOffset.localPosition;
                orientation *= holdAnimationOffset.localRotation;
            }
            return new Pose(position, orientation);
        }

        [Header("Inventory Presentation (assign in the prefab)")]
        [SerializeField] private Renderer[] inventoryRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Light[] inventoryLights = System.Array.Empty<Light>();
        [SerializeField] private Canvas[] inventoryCanvases = System.Array.Empty<Canvas>();
        [SerializeField] private AudioSource[] inventoryAudioSources = System.Array.Empty<AudioSource>();
        [SerializeField] private Collider[] inventoryColliders = System.Array.Empty<Collider>();
        private bool[] lightStates;
        private bool[] canvasStates;
        private bool[] audioMuteStates;
        private bool[] colliderStates;
        private bool inventoryVisible = true;

        public void SetInventoryVisible(bool visible)
        {
            if (inventoryVisible == visible) return;
            inventoryVisible = visible;
            for (int i = 0; i < inventoryRenderers.Length; i++)
                if (inventoryRenderers[i] != null) inventoryRenderers[i].forceRenderingOff = !visible;
            for (int i = 0; i < inventoryLights.Length; i++)
            {
                if (inventoryLights[i] == null) continue;
                if (!visible) lightStates[i] = inventoryLights[i].enabled;
                inventoryLights[i].enabled = visible && lightStates[i];
            }
            for (int i = 0; i < inventoryCanvases.Length; i++)
            {
                if (inventoryCanvases[i] == null) continue;
                if (!visible) canvasStates[i] = inventoryCanvases[i].enabled;
                inventoryCanvases[i].enabled = visible && canvasStates[i];
            }
            for (int i = 0; i < inventoryAudioSources.Length; i++)
            {
                if (inventoryAudioSources[i] == null) continue;
                if (!visible) audioMuteStates[i] = inventoryAudioSources[i].mute;
                inventoryAudioSources[i].mute = !visible || audioMuteStates[i];
            }
        }

        public (Vector3 translationOffset, Vector3 rotationOffset) GetGripOffsets()
        {
            if (gripPoint != null)
            {
                // World-scale and hierarchy-depth safe calculation from gripPoint to root item transform
                Quaternion gripToItemRot = Quaternion.Inverse(gripPoint.rotation) * transform.rotation;
                Vector3 gripToItemPos = Quaternion.Inverse(gripPoint.rotation) * (transform.position - gripPoint.position);
                return (gripToItemPos, gripToItemRot.eulerAngles);
            }
            return (gripPositionOffset, gripRotationOffset);
        }

        /// <summary>
        /// Places the item in world space so that its gripPoint (or grip offset) matches
        /// the exact target grip world position and rotation regardless of item scale.
        /// </summary>
        public void AlignToGripPose(Vector3 targetGripWorldPos, Quaternion targetGripWorldRot)
        {
            if (gripPoint != null)
            {
                Quaternion gripToItemRot = Quaternion.Inverse(gripPoint.rotation) * transform.rotation;
                Vector3 gripToItemPos = Quaternion.Inverse(gripPoint.rotation) * (transform.position - gripPoint.position);

                Quaternion itemWorldRot = targetGripWorldRot * gripToItemRot;
                Vector3 itemWorldPos = targetGripWorldPos + targetGripWorldRot * gripToItemPos;
                transform.SetPositionAndRotation(itemWorldPos, itemWorldRot);
            }
            else
            {
                Quaternion itemWorldRot = targetGripWorldRot * Quaternion.Euler(gripRotationOffset);
                Vector3 itemWorldPos = targetGripWorldPos + targetGripWorldRot * gripPositionOffset;
                transform.SetPositionAndRotation(itemWorldPos, itemWorldRot);
            }
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
        private ParentConstraint itemParentConstraint;
        private Inventory holderInventory;
        protected AudioSource audioSource;
        private DroppedItemHighlight droppedItemHighlight;

        public bool isCurrentlyHeld = false;

        public void BindHolderInventory(Inventory inventory, ParentConstraint constraint)
        {
            holderInventory = inventory;
            itemParentConstraint = constraint;
        }

        public void ClearHolderInventory()
        {
            holderInventory = null;
        }

        protected bool TryGetHolderInventory(out Inventory inventory)
        {
            if (transform.parent == null)
            {
                holderInventory = null;
                inventory = null;
                return false;
            }

            if (holderInventory == null || holderInventory.transform != transform.parent)
            {
                transform.parent.TryGetComponent(out holderInventory);
            }

            inventory = holderInventory;
            return inventory != null;
        }

        public bool IsHeldByClient(ulong clientId)
        {
            return TryGetHolderInventory(out var inventory) && inventory.OwnerClientId == clientId && inventory.IsActiveItem(this);
        }

        public virtual Quaternion GetRemoteGripRotation(Inventory inventory)
        {
            return inventory != null ? inventory.GetRemoteAimRotation() : transform.rotation;
        }

        public void UpdateRemoteHeldPose()
        {
            if (!TryGetHolderInventory(out var inventory))
            {
                return;
            }

            if (!inventory.IsActiveItem(this))
            {
                // Stored items still follow their holder for mission cleanup and saving.
                transform.SetPositionAndRotation(inventory.transform.position, inventory.transform.rotation);
                return;
            }
            inventory.ItemHolder?.AlignItemToHand(this);
        }

        #region Unity Lifecycle

        private void Awake()
        {
            CacheComponents();
        }

        protected virtual void LateUpdate()
        {
            UpdateRemoteHeldPose();
        }

        public override void OnNetworkSpawn()
        {
            this.NetworkObject.Register();
            CheckLateJoinParent();
        }

        public override void OnNetworkDespawn()
        {
            if (holderInventory != null && holderInventory.IsSpawned)
                holderInventory.ForgetItemServer(this);
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
            holderInventory = inventory;
            isCurrentlyHeld = true;
            SetPhysicsState(false);
            droppedItemHighlight?.Stop();

            if (itemNetworkTransform != null)
            {
                itemNetworkTransform.enabled = false;
            }

            if (inventory != null)
            {
                inventory.ApplyParentConstraint(gameObject);
            }
        }

        private void DetachFromPlayer()
        {
            holderInventory = null;
            isCurrentlyHeld = false;
            SetInventoryVisible(true);
            SetPhysicsState(true);
            droppedItemHighlight?.Play();

            if (itemNetworkTransform != null)
            {
                itemNetworkTransform.enabled = true;
            }

            if (itemParentConstraint == null)
            {
                TryGetComponent(out itemParentConstraint);
            }

            if (itemParentConstraint != null)
            {
                while (itemParentConstraint.sourceCount > 0) itemParentConstraint.RemoveSource(0);
                itemParentConstraint.constraintActive = false;
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
            itemParentConstraint = GetComponent<ParentConstraint>();
            audioSource = GetComponent<AudioSource>();
            TryGetComponent(out droppedItemHighlight);
            lightStates = new bool[inventoryLights.Length];
            canvasStates = new bool[inventoryCanvases.Length];
            audioMuteStates = new bool[inventoryAudioSources.Length];
            colliderStates = new bool[inventoryColliders.Length];
            for (int i = 0; i < inventoryColliders.Length; i++)
                colliderStates[i] = inventoryColliders[i] != null && inventoryColliders[i].enabled;
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
            if (inventory == null) return;

            inventory.AddItem(gameObject);
        }

        virtual public void Drop()
        {
            if (!isCurrentlyHeld)
            {
                return;
            }

            isCurrentlyHeld = false;
            SetPhysicsState(true);
        }

        virtual public void Use(GameObject player)
        {
            if (!IsEquipped) return;
            if (player == null)
            {
                Debug.LogWarning("Player is null in Use!");
                return;
            }

            if (clickSounds != null && clickSounds.Length > 0 && audioSource != null)
            {
                float pitchOffset = Random.Range(-pitchVariation, pitchVariation);
                audioSource.pitch = Mathf.Clamp(1.0f + pitchOffset, 0.5f, 2.0f);
                audioSource.PlayOneShot(clickSounds[UnityEngine.Random.Range(0, clickSounds.Length)]);
            }
            ExecuteItemAction(player);
        }

        public virtual void OnUnequip()
        {
            RequestTurnOffServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestTurnOffServerRpc(RpcParams rpc = default)
        {
            if (!IsHeldByClient(rpc.Receive.SenderClientId)) return;
            TurnOffServer();
        }

        public virtual void TurnOffServer()
        {
        }

        #endregion

        #region Item Specific Functionality

        virtual protected void ExecuteItemAction(GameObject player) { return; }

        virtual protected void HandleSingleUseItem(GameObject player)
        {
            DestroyItemServerRpc();
        }

        #endregion

        #region Network RPCs

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DestroyItemServerRpc()
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        public void ReturnToSpawnServer(Vector3 position)
        {
            if (!IsServer || isCurrentlyHeld || itemRigidbody == null) return;

            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, Quaternion.identity);
        }

        #endregion

        #region Physics Management

        private void SetPhysicsState(bool isPhysicsEnabled)
        {
            for (int i = 0; i < inventoryColliders.Length; i++)
                if (inventoryColliders[i] != null)
                    inventoryColliders[i].enabled = isPhysicsEnabled && colliderStates[i];
            if (itemRigidbody != null)
            {
                itemRigidbody.isKinematic = !isPhysicsEnabled || (IsSpawned && !IsServer);
            }
            if (itemNetworkRigidbody != null)
            {
                itemNetworkRigidbody.enabled = isPhysicsEnabled;
            }
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