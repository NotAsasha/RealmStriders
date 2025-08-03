using UnityEngine;
using Unity.Netcode;
using System;
using Player;
using UnityEngine.InputSystem;

namespace InventorySystem
{
    public class Inventory : NetworkBehaviour
    {
        [Header("Configuration")]
        public int capacity = 4;
        [SerializeField] private Vector3 handPosition;
        [SerializeField] public Transform userInterface;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private LayerMask layer;

        [Header("Current State")]
        [SerializeField] private int activeSlotIndex;

        // Arrays for items and UI
        private Item[] items;
        private UISlot[] slots;
        
        // Input controls
        private Controls controls;

        #region Unity Lifecycle
        
        void Start()
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
            items = new Item[capacity];
            slots = new UISlot[capacity];
            
            if (Movement.instance?._controls != null)
            {
                controls = Movement.instance._controls;
            }
            else
            {
                Debug.LogError("Movement controls not found!");
            }
        }

        private void SetupInputHandlers()
        {
            if (controls == null) return;

            controls.Gameplay.MouseWheel.performed += OnMouseWheelChanged;
            controls.Gameplay.Use.performed += OnUseItem;
            controls.Gameplay.Drop.performed += OnDropItem;
        }

        private void CleanupInputHandlers()
        {
            if (controls == null) return;

            controls.Gameplay.MouseWheel.performed -= OnMouseWheelChanged;
            controls.Gameplay.Use.performed -= OnUseItem;
            controls.Gameplay.Drop.performed -= OnDropItem;
        }

        #endregion

        #region Public Methods

        public bool AddItem(GameObject itemToAdd, int targetSlot = -1)
        {
            if (itemToAdd == null) return false;

            Item takableComponent = itemToAdd.GetComponent<Item>();
            if (takableComponent == null) return false;

            // If no slot specified, find first empty slot
            if (targetSlot == -1)
            {
                targetSlot = FindEmptySlot();
                if (targetSlot == -1) return false; // Inventory full
            }

            // Check if target slot is valid and empty
            if (!IsValidSlot(targetSlot) || items[targetSlot] != null) 
                return false;

            if (targetSlot != activeSlotIndex)
                SetItemActiveServerRpc(itemToAdd, false);

            items[targetSlot] = takableComponent;
            SetItemParentServerRpc(itemToAdd, gameObject);
            UpdateUI();
            
            return true;
        }

        public bool RemoveItem(int slot)
        {
            if (!IsValidSlot(slot) || items[slot] == null) 
                return false;

            items[slot] = null;
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

        public void ToggleUI(bool _isActive)
        {
            userInterface.gameObject.SetActive(_isActive);
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
            DropItemServerRpc(itemToDrop.gameObject);
            UpdateUI();
        }

        #endregion

        #region Private Helper Methods

        private bool IsPlayerInInteraction()
        {
            return Movement.instance != null && Movement.instance.isInInteraction;
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

            // Wrap around if out of bounds
            if (newIndex >= capacity) newIndex = 0;
            else if (newIndex < 0) newIndex = capacity - 1;

            return newIndex;
        }

        private void ChangeActiveSlot(int newSlotIndex)
        {
            // Deactivate current item
            Item currentItem = GetActiveItem();
            if (currentItem != null)
            {
                SetItemActiveServerRpc(currentItem.gameObject, false);
            }

            // Change active slot
            activeSlotIndex = newSlotIndex;

            // Activate new item
            Item newItem = GetActiveItem();
            if (newItem != null)
            {
                SetItemActiveServerRpc(newItem.gameObject, true);
            }

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

        #region Network RPCs

        [ServerRpc(RequireOwnership = false)]
        public void SetItemActiveServerRpc(NetworkObjectReference objRef, bool isActive)
        {
            if (objRef.TryGet(out NetworkObject networkObject))
            {
                networkObject.gameObject.SetActive(isActive);
                SetItemActiveClientRpc(objRef, isActive);
            }
            else
            {
                Debug.LogWarning("Failed to get network object for SetItemActive");
            }
        }
        [ClientRpc]
        public void SetItemActiveClientRpc(NetworkObjectReference objRef, bool isActive)
        {
            if (IsServer) return;
            objRef.TryGet(out NetworkObject networkObject);
            networkObject.gameObject.SetActive(isActive);
        }
       [ServerRpc(RequireOwnership = false)]
        public void SetItemParentServerRpc(NetworkObjectReference objRef, NetworkObjectReference newParentRef)
        {
            if (!objRef.TryGet(out NetworkObject obj))
            {
                Debug.LogWarning("Failed to get item object for SetItemParent");
                return;
            }

            if (!newParentRef.TryGet(out NetworkObject newParent))
            {
                Debug.LogWarning("Failed to get parent object for SetItemParent");
                return;
            }

            obj.transform.SetParent(newParent.transform);
            obj.transform.localPosition = handPosition;
            obj.transform.localRotation = Quaternion.identity;

            SetItemParentClientRpc(objRef, newParentRef);
        }

        [ClientRpc]
        private void SetItemParentClientRpc(NetworkObjectReference objRef, NetworkObjectReference newParentRef)
        {
            if (IsServer) return;
            if (objRef.TryGet(out NetworkObject obj) && newParentRef.TryGet(out NetworkObject newParent))
            {
                obj.transform.SetParent(newParent.transform);
                obj.transform.localPosition = handPosition;
                obj.transform.localRotation = Quaternion.identity;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void DropItemServerRpc(NetworkObjectReference objRef)
        {
            if (!objRef.TryGet(out NetworkObject networkObject))
            {
                Debug.LogWarning("Failed to get network object for DropItem");
                return;
            }

            if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 1f, layer))
                networkObject.transform.localPosition = handPosition + new Vector3(0, 0, 0.75f);
            networkObject.transform.parent = null;
            
            if (!networkObject.gameObject.activeSelf)
            {
                networkObject.gameObject.SetActive(true);
            }

            Item takable = networkObject.GetComponent<Item>();
            takable?.Drop(gameObject);

            DropItemClientRpc(objRef);
        }

        [ClientRpc]
        private void DropItemClientRpc(NetworkObjectReference objRef)
        {
            if (!IsServer)
            {
                if (objRef.TryGet(out NetworkObject networkObject))
                {
                    networkObject.transform.parent = null;
                    
                    if (!networkObject.gameObject.activeSelf)
                    {
                        networkObject.gameObject.SetActive(true);
                    }
                }
            }
        }

        [ServerRpc]
        public void DeparentObjectServerRpc(NetworkObjectReference objectRef)
        {
            if (objectRef.TryGet(out NetworkObject networkObject))
            {
                networkObject.transform.parent = null;
            }
            else
            {
                Debug.LogWarning("Failed to get network object for Deparent");
            }
        }

        #endregion

        #region Debug and Utilities

        public void DebugInventoryState()
        {
            Debug.Log($"Inventory State - Active Slot: {activeSlotIndex}");
            for (int i = 0; i < capacity; i++)
            {
                string itemName = items[i]?.gameObject.name ?? "Empty";
                Debug.Log($"Slot {i}: {itemName}");
            }
        }

        #endregion
    }
}