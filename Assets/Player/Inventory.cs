using Player.Equipment;
using Player.Movement;
using Player.UI;
using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

namespace Player
{
    public class Inventory : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("Link to inventory ui")]
        [SerializeField] public Transform userInterface;
        [Tooltip("ui of a single slot")]
        [SerializeField] private GameObject slotPrefab;
        [Tooltip("Parent bone to hold an item")]
        [SerializeField] private Transform handAnchor;
        public Transform HandAnchor => handAnchor;

        [Tooltip("Item holder managing first-person viewmodel and IK target")]
        [SerializeField] private ItemHolder itemHolder;
        public ItemHolder ItemHolder => itemHolder;

        [Header("Configuration")]
        public int capacity = 4;
        [SerializeField] private LayerMask layer;

        [Header("Current State")]
        [SerializeField] private int activeSlotIndex;

        public readonly NetworkVariable<bool> isHoldingItem = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public event Action<bool> itemChanged;

        private Item[] items;
        private UISlot[] slots;
        private Controls controls;
        private Human human;
        private PlayerMovement playerMovement;
        private PlayerHeadSync headSync;

        #region Unity Lifecycle

        private void Awake()
        {
            human = GetComponent<Human>();
            playerMovement = GetComponent<PlayerMovement>();

            if (itemHolder == null)
            {
                itemHolder = GetComponentInChildren<ItemHolder>(true);
            }

            if (headSync == null)
            {
                headSync = GetComponentInChildren<PlayerHeadSync>(true);
            }

            items = new Item[capacity];
            slots = new UISlot[capacity];
        }

        private void Start()
        {
            InitializeInventory();
            SetupInputHandlers();
            UpdateUI();
        }

        public override void OnDestroy()
        {
            CleanupInputHandlers();
        }

        #endregion

        #region Initialization

        private void InitializeInventory()
        {
            if (playerMovement != null && playerMovement.controls != null)
            {
                controls = playerMovement.controls;
            }
            else
            {
                Debug.LogError("Movement controls not found on this player object!");
            }
        }

        private void SetupInputHandlers()
        {
            if (controls == null || human == null) return;

            controls.Gameplay.MouseWheel.performed += OnMouseWheelChanged;
            controls.Gameplay.SwitchSlot.performed += OnSlotSwitched;
            controls.Gameplay.Use.performed += OnUseItem;
            controls.Gameplay.Drop.performed += OnDropItem;

            human.isDead.OnValueChanged += OnDeathStateChange;
        }

        private void CleanupInputHandlers()
        {
            if (controls == null || human == null) return;

            controls.Gameplay.MouseWheel.performed -= OnMouseWheelChanged;
            controls.Gameplay.SwitchSlot.performed -= OnSlotSwitched;
            controls.Gameplay.Use.performed -= OnUseItem;
            controls.Gameplay.Drop.performed -= OnDropItem;

            human.isDead.OnValueChanged -= OnDeathStateChange;
        }

        #endregion

        #region Public Methods

        public bool AddItem(GameObject itemToAdd, int targetSlot = -1)
        {
            if (itemToAdd == null) return false;

            Item takableComponent = itemToAdd.GetComponent<Item>();
            if (takableComponent == null) return false;

            if (targetSlot == -1)
            {
                targetSlot = FindEmptySlot();
                if (targetSlot == -1) return false;
            }

            if (!IsValidSlot(targetSlot) || items[targetSlot] != null)
                return false;

            items[targetSlot] = takableComponent;
            SetItemParentServerRpc(itemToAdd, gameObject);

            if (targetSlot != activeSlotIndex)
            {
                SetItemActiveServerRpc(itemToAdd, false);
            }
            else
            {
                if (IsOwner)
                {
                    isHoldingItem.Value = true;
                    itemHolder?.SetActiveItem(takableComponent);
                }
                itemChanged?.Invoke(true);
            }

            UpdateUI();
            return true;
        }

        public bool RemoveItem(int slot)
        {
            if (!IsValidSlot(slot) || items[slot] == null)
                return false;

            items[slot] = null;

            if (slot == activeSlotIndex)
            {
                if (IsOwner)
                {
                    isHoldingItem.Value = false;
                    itemHolder?.SetActiveItem(null);
                }
                itemChanged?.Invoke(false);
            }

            UpdateUI();
            return true;
        }

        public Item GetActiveItem()
        {
            return items != null && IsValidSlot(activeSlotIndex) ? items[activeSlotIndex] : null;
        }

        public Item GetItem(int slot)
        {
            return items != null && IsValidSlot(slot) ? items[slot] : null;
        }

        public void ToggleUI(bool isActive)
        {
            if (userInterface != null)
            {
                userInterface.gameObject.SetActive(isActive);
            }
        }

        #endregion

        #region Input Handling

        private void OnMouseWheelChanged(InputAction.CallbackContext context)
        {
            if (IsPlayerInInteraction()) return;

            float scrollValue = context.ReadValue<float>();
            if (Mathf.Approximately(scrollValue, 0f)) return;

            int newSlotIndex = CalculateNewSlotIndex(scrollValue);
            if (newSlotIndex == activeSlotIndex) return;

            ChangeActiveSlot(newSlotIndex);
        }

        private void OnSlotSwitched(InputAction.CallbackContext context)
        {
            if (IsPlayerInInteraction()) return;

            int newSlot = (int)context.ReadValue<float>() - 1;
            if (newSlot == activeSlotIndex) return;
            ChangeActiveSlot(newSlot);
        }

        private void OnUseItem(InputAction.CallbackContext context)
        {
            if (IsPlayerInInteraction()) return;

            Item activeItem = GetActiveItem();
            if (activeItem != null && activeItem.IsConsumable())
                RemoveItem(activeSlotIndex);

            activeItem?.Use(gameObject);
        }

        private void OnDropItem(InputAction.CallbackContext context)
        {
            Item itemToDrop = GetActiveItem();
            if (itemToDrop == null) return;

            items[activeSlotIndex] = null;
            if (IsOwner)
            {
                isHoldingItem.Value = false;
                itemHolder?.SetActiveItem(null);
                itemChanged?.Invoke(false);
            }

            GetDropPose(out Vector3 dropOrigin, out Vector3 dropForward);
            ReleaseItemVisual(itemToDrop.gameObject);
            DropItemServerRpc(itemToDrop.gameObject, dropOrigin, dropForward);
            UpdateUI();
        }

        private void OnDeathStateChange(bool _, bool isDead)
        {
            ToggleUI(!isDead);

            if (!isDead) return;
            DropEverything();
        }

        #endregion

        #region Private Helper Methods

        private bool IsPlayerInInteraction()
        {
            return playerMovement != null && playerMovement.isInInteraction;
        }

        private int FindEmptySlot()
        {
            if (items[activeSlotIndex] == null) return activeSlotIndex;

            for (int i = 0; i < capacity; i++)
            {
                if (items[i] == null) return i;
            }
            return -1;
        }

        private bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < capacity;
        }

        private int CalculateNewSlotIndex(float scrollValue)
        {
            int direction = scrollValue > 0 ? -1 : 1;
            int newIndex = activeSlotIndex + direction;

            if (newIndex >= capacity) newIndex = 0;
            else if (newIndex < 0) newIndex = capacity - 1;

            return newIndex;
        }

        private void ChangeActiveSlot(int newSlotIndex)
        {
            if (newSlotIndex >= capacity) throw new ArgumentOutOfRangeException();

            Item currentItem = GetActiveItem();
            if (currentItem != null)
            {
                currentItem.OnUnequip();
                SetItemActiveServerRpc(currentItem.gameObject, false);
            }

            activeSlotIndex = newSlotIndex;

            Item newItem = GetActiveItem();
            if (newItem != null)
            {
                SetItemActiveServerRpc(newItem.gameObject, true);
                if (IsOwner)
                {
                    isHoldingItem.Value = true;
                    itemHolder?.SetActiveItem(newItem);
                }
                itemChanged?.Invoke(true);
            }
            else
            {
                if (IsOwner)
                {
                    isHoldingItem.Value = false;
                    itemHolder?.SetActiveItem(null);
                }
                itemChanged?.Invoke(false);
            }
            UpdateUI();
        }

        private void DropEverything()
        {
            for (int i = 0; i < capacity; ++i)
            {
                if (items[i] == null) continue;
                GetDropPose(out Vector3 dropOrigin, out Vector3 dropForward);
                DropItemServerRpc(items[i].gameObject, dropOrigin, dropForward);
                items[i] = null;
            }
            if (IsOwner)
            {
                isHoldingItem.Value = false;
                itemHolder?.SetActiveItem(null);
                itemChanged?.Invoke(false);
            }
            UpdateUI();
        }

        #endregion

        #region UI Management

        private void UpdateUI()
        {
            if (userInterface == null || slotPrefab == null) return;

            ValidateUISetup();

            if (userInterface.childCount == 0)
            {
                CreateUISlots();
            }

            for (int i = 0; i < capacity; i++)
            {
                if (slots[i] == null) continue;
                bool hasItem = items[i] != null;
                bool isActive = i == activeSlotIndex;
                slots[i].UpdateUI(hasItem, isActive);
            }
        }

        private void ValidateUISetup()
        {
            if (userInterface.childCount > capacity)
            {
                Debug.LogError($"UI has more slots ({userInterface.childCount}) than inventory capacity ({capacity})!");
            }
        }

        private void CreateUISlots()
        {
            for (int i = 0; i < capacity; i++)
            {
                GameObject slotObject = Instantiate(slotPrefab, userInterface);
                slots[i] = slotObject.GetComponent<UISlot>();

                if (slots[i] == null)
                {
                    Debug.LogError($"Slot prefab at index {i} doesn't have UISlot component!");
                }
            }
        }

        #endregion

        #region Network RPCs and Item Parenting

        private void ToggleNetworkTransform(GameObject item, bool isEnabled)
        {
            if (item.TryGetComponent<NetworkTransform>(out var netTransform))
            {
                netTransform.enabled = isEnabled;
            }
        }

        private void ReleaseItemVisual(GameObject item)
        {
            if (item == null) return;

            if (item.TryGetComponent<NetworkObject>(out var networkObject))
            {
                networkObject.TryRemoveParent();
            }

            RemoveParentConstraint(item);
            ToggleNetworkTransform(item, true);

            if (!item.activeSelf)
            {
                item.SetActive(true);
            }
        }

        private void GetDropPose(out Vector3 origin, out Vector3 forward)
        {
            if (CameraMovement.Instance != null)
            {
                origin = CameraMovement.Instance.transform.position;
                forward = CameraMovement.Instance.transform.forward;
                return;
            }

            origin = transform.position + Vector3.up;
            forward = transform.forward;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetItemActiveServerRpc(NetworkObjectReference objRef, bool isActive)
        {
            if (objRef.TryGet(out NetworkObject networkObject))
            {
                networkObject.gameObject.SetActive(isActive);
                if (isActive && networkObject.TryGetComponent<Item>(out var item))
                {
                    item.UpdateRemoteHeldPose();
                }
                SetItemActiveClientRpc(objRef, isActive);
            }
        }

        [ClientRpc]
        public void SetItemActiveClientRpc(NetworkObjectReference objRef, bool isActive)
        {
            if (IsServer) return;
            if (objRef.TryGet(out NetworkObject networkObject))
            {
                networkObject.gameObject.SetActive(isActive);
                if (isActive && networkObject.TryGetComponent<Item>(out var item))
                {
                    item.UpdateRemoteHeldPose();
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetItemParentServerRpc(NetworkObjectReference objRef, NetworkObjectReference newParentRef)
        {
            if (!objRef.TryGet(out NetworkObject obj) || !newParentRef.TryGet(out NetworkObject newParent))
                return;

            obj.TrySetParent(newParent.transform);
            ToggleNetworkTransform(obj.gameObject, false);
            ApplyParentConstraint(obj.gameObject);

            SetItemParentClientRpc(objRef, newParentRef);
        }

        [ClientRpc]
        private void SetItemParentClientRpc(NetworkObjectReference objRef, NetworkObjectReference newParentRef)
        {
            if (IsServer) return;
            if (objRef.TryGet(out NetworkObject obj) && newParentRef.TryGet(out NetworkObject newParent))
            {
                obj.TrySetParent(newParent.transform);
                ToggleNetworkTransform(obj.gameObject, false);
                ApplyParentConstraint(obj.gameObject);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DropItemServerRpc(
            NetworkObjectReference objRef,
            Vector3 requestedOrigin,
            Vector3 requestedForward)
        {
            if (!objRef.TryGet(out NetworkObject networkObject)) return;

            Vector3 playerOrigin = transform.position + Vector3.up;
            Vector3 dropOrigin = requestedOrigin;
            if ((dropOrigin - playerOrigin).sqrMagnitude > 9f)
            {
                dropOrigin = playerOrigin;
            }

            Vector3 dropForward = requestedForward;
            if (dropForward.sqrMagnitude < 0.0001f)
            {
                dropForward = transform.forward;
            }
            else
            {
                dropForward.Normalize();
            }

            Vector3 dropPos = dropOrigin + dropForward * 0.75f;
            if (Physics.Raycast(dropOrigin, dropForward, out RaycastHit hit, 1f, layer))
            {
                dropPos = hit.point - dropForward * 0.2f;
            }

            networkObject.transform.position = dropPos;

            networkObject.TryRemoveParent();
            RemoveParentConstraint(networkObject.gameObject);
            ToggleNetworkTransform(networkObject.gameObject, true);

            if (!networkObject.gameObject.activeSelf)
            {
                networkObject.gameObject.SetActive(true);
            }

            Item takable = networkObject.GetComponent<Item>();
            takable?.Drop();
            itemChanged?.Invoke(false);
            DropItemClientRpc(objRef);
        }

        [ClientRpc]
        private void DropItemClientRpc(NetworkObjectReference objRef)
        {
            if (IsServer) return;
            itemChanged?.Invoke(false);
            if (objRef.TryGet(out NetworkObject networkObject))
            {
                if (networkObject.TryGetComponent<Item>(out var takable))
                {
                    takable.Drop();
                }
                ReleaseItemVisual(networkObject.gameObject);
            }
        }

        public Quaternion GetRemoteAimRotation()
        {
            if (headSync == null)
            {
                headSync = GetComponentInChildren<PlayerHeadSync>(true);
            }

            if (itemHolder == null)
            {
                itemHolder = GetComponentInChildren<ItemHolder>(true);
            }

            float pitch = headSync != null ? headSync.CurrentPitch : 0f;
            if (itemHolder != null && itemHolder.ClampItemPitch)
            {
                pitch = Mathf.Clamp(pitch, itemHolder.MinItemPitch, itemHolder.MaxItemPitch);
            }
            else
            {
                pitch = Mathf.Clamp(pitch, -45f, 45f);
            }

            return transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
        }

        public void ApplyParentConstraint(GameObject item, Transform customAnchor = null)
        {
            if (item == null) return;

            ParentConstraint constraint = item.GetComponent<ParentConstraint>();
            if (constraint != null)
            {
                constraint.constraintActive = false;
            }

            if (item.TryGetComponent<Item>(out var itemComp))
            {
                itemComp.BindHolderInventory(this, constraint);

                if (IsOwner && itemHolder != null)
                {
                    if (item.activeSelf)
                    {
                        itemHolder.SetActiveItem(itemComp);
                    }
                }
                else
                {
                    itemComp.UpdateRemoteHeldPose();
                }
            }
        }

        private void RemoveParentConstraint(GameObject item)
        {
            if (item != null && item.TryGetComponent<Item>(out var itemComp))
            {
                itemComp.ClearHolderInventory();
            }

            ParentConstraint constraint = item != null ? item.GetComponent<ParentConstraint>() : null;
            if (constraint == null) return;

            while (constraint.sourceCount > 0)
            {
                constraint.RemoveSource(0);
            }
            constraint.constraintActive = false;
        }

        #endregion

        #region Utilities

        public void OnDrawGizmosSelected()
        {
            if (handAnchor == null) return;
            Gizmos.color = Color.blueViolet;
            Gizmos.DrawSphere(transform.position + handAnchor.localPosition, 0.1f);
        }

        #endregion
    }
}