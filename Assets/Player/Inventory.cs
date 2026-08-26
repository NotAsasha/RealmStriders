using Player.Equipment;
using Player.Movement;
using Player.UI;
using System;
using Unity.Netcode;
using Unity.Netcode.Components; // Додано для доступу до NetworkTransform
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

        #region Unity Lifecycle

        void Start()
        {
            human = GetComponent<Human>();
            playerMovement = GetComponent<PlayerMovement>();

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
            items = new Item[capacity];
            slots = new UISlot[capacity];

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

            if (targetSlot != activeSlotIndex)
            {
                SetItemActiveServerRpc(itemToAdd, false);
            }
            else
            {
                if (IsOwner) isHoldingItem.Value = true;
            }

            items[targetSlot] = takableComponent;
            SetItemParentServerRpc(itemToAdd, gameObject);

            itemChanged?.Invoke(true);
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
                if (IsOwner) isHoldingItem.Value = false;
                itemChanged?.Invoke(false);
            }

            UpdateUI();
            return true;
        }

        public Item GetActiveItem()
        {
            return IsValidSlot(activeSlotIndex) ? items[activeSlotIndex] : null;
        }

        public Item GetItem(int slot)
        {
            return IsValidSlot(slot) ? items[slot] : null;
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
            if (IsOwner) isHoldingItem.Value = false;

            DropItemServerRpc(itemToDrop.gameObject);
            UpdateUI();
        }

        private void OnDeathStateChange(bool _, bool isDead)
        {
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
                SetItemActiveServerRpc(currentItem.gameObject, false);
            }

            activeSlotIndex = newSlotIndex;

            Item newItem = GetActiveItem();
            if (newItem != null)
            {
                SetItemActiveServerRpc(newItem.gameObject, true);
                if (IsOwner) isHoldingItem.Value = true;
                itemChanged?.Invoke(true);
            }
            else
            {
                if (IsOwner) isHoldingItem.Value = false;
                itemChanged?.Invoke(false);
            }
            UpdateUI();
        }

        private void DropEverything()
        {
            for (int i = 0; i < capacity; ++i)
            {
                if (items[i] == null) continue;
                DropItemServerRpc(items[i].gameObject);
                items[i] = null;
            }
            if (IsOwner) isHoldingItem.Value = false;
            UpdateUI();
        }

        #endregion

        #region UI Management

        private void UpdateUI()
        {
            ValidateUISetup();

            if (userInterface.childCount == 0)
            {
                CreateUISlots();
            }

            for (int i = 0; i < capacity; i++)
            {
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
            // Вимикаємо або вмикаємо NetworkTransform, щоб він не конфліктував із локальним рендер-аттачем
            if (item.TryGetComponent<NetworkTransform>(out var netTransform))
            {
                netTransform.enabled = isEnabled;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetItemActiveServerRpc(NetworkObjectReference objRef, bool isActive)
        {
            if (objRef.TryGet(out NetworkObject networkObject))
            {
                networkObject.gameObject.SetActive(isActive);
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
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetItemParentServerRpc(NetworkObjectReference objRef, NetworkObjectReference newParentRef)
        {
            if (!objRef.TryGet(out NetworkObject obj) || !newParentRef.TryGet(out NetworkObject newParent))
                return;

            obj.TrySetParent(newParent.transform);

            // Вимикаємо синхронізацію координат у мережі, поки предмет у руках
            ToggleNetworkTransform(obj.gameObject, false);
            ApplyParentConstraint(obj.gameObject, handAnchor);

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
                ApplyParentConstraint(obj.gameObject, handAnchor);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DropItemServerRpc(NetworkObjectReference objRef)
        {
            if (!objRef.TryGet(out NetworkObject networkObject)) return;

            if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 1f, layer))
                networkObject.transform.localPosition = handAnchor.localPosition + new Vector3(0, 0, 0.75f);

            networkObject.TryRemoveParent();
            RemoveParentConstraint(networkObject.gameObject);

            // Повертаємо мережеву синхронізацію предмету, коли він лежить на землі
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
                networkObject.TryRemoveParent();
                RemoveParentConstraint(networkObject.gameObject);

                ToggleNetworkTransform(networkObject.gameObject, true);

                if (!networkObject.gameObject.activeSelf)
                {
                    networkObject.gameObject.SetActive(true);
                }
            }
        }

        private void ApplyParentConstraint(GameObject item, Transform anchor)
        {
            ParentConstraint constraint = item.GetComponent<ParentConstraint>();
            if (constraint == null)
            {
                constraint = item.AddComponent<ParentConstraint>();
            }

            while (constraint.sourceCount > 0)
            {
                constraint.RemoveSource(0);
            }

            ConstraintSource source = new ConstraintSource { sourceTransform = anchor, weight = 1f };
            constraint.AddSource(source);

            constraint.SetTranslationOffset(0, Vector3.zero);
            constraint.SetRotationOffset(0, Vector3.zero);

            constraint.constraintActive = true;
        }

        private void RemoveParentConstraint(GameObject item)
        {
            ParentConstraint constraint = item.GetComponent<ParentConstraint>();
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