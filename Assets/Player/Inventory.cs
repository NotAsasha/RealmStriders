using Player.Equipment;
using Player.Movement;
using Player.UI;
using System.Collections;
using Unity.Netcode;
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

        [Header("Auto-Hide Settings")]
        [Tooltip("Automatically hide inventory UI when not in use")]
        [SerializeField] private bool autoHideUI = true;
        [Tooltip("Seconds before the inventory UI hides after last activity")]
        [SerializeField] private float autoHideDelay = 3f;
        [Tooltip("Duration of the fade-out transition in seconds")]
        [SerializeField] private float fadeDuration = 0.3f;
        [Tooltip("CanvasGroup used for smooth fading of the inventory UI")]
        [SerializeField] private CanvasGroup uiCanvasGroup;

        [Header("Current State")]
        [SerializeField] private int activeSlotIndex;

        public readonly NetworkVariable<bool> isHoldingItem = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public event System.Action<bool> itemChanged;

        private const ulong EmptySlot = ulong.MaxValue;
        private NetworkList<ulong> slotIds;
        private readonly NetworkVariable<int> selectedSlot = new(0);
        private bool pendingResolution;
        private Item presentedItem;
        private Item[] items;
        private UISlot[] slots;
        private Controls controls;
        private Human human;
        private PlayerMovement playerMovement;
        private PlayerHeadSync headSync;
        private Coroutine hideCoroutine;

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

            if (uiCanvasGroup == null && userInterface != null)
            {
                uiCanvasGroup = userInterface.GetComponent<CanvasGroup>();
                if (uiCanvasGroup == null)
                {
                    uiCanvasGroup = userInterface.gameObject.AddComponent<CanvasGroup>();
                    uiCanvasGroup.blocksRaycasts = false;
                }
            }

            slotIds = new NetworkList<ulong>();
            items = new Item[capacity];
            slots = new UISlot[capacity];
        }

        private void Start()
        {
            if (controls == null && (!IsSpawned || IsOwner))
            {
                InitializeInventory();
                SetupInputHandlers();
            }
            UpdateUI();
            if (autoHideUI && (!IsSpawned || IsOwner))
            {
                ShowInventoryUI();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            slotIds.OnListChanged += OnSlotsChanged;
            selectedSlot.OnValueChanged += OnSelectedSlotChanged;
            if (IsServer && slotIds.Count == 0)
            {
                int initialSlot = Mathf.Clamp(activeSlotIndex, 0, capacity - 1);
                for (int i = 0; i < capacity; i++) slotIds.Add(EmptySlot);
                selectedSlot.Value = initialSlot;
            }
            RefreshReplicatedItems();

            if (human != null)
            {
                human.isDead.OnValueChanged += OnDeathStateChange;
            }

            if (IsOwner)
            {
                InitializeInventory();
                SetupInputHandlers();
                UpdateUI();
                if (autoHideUI)
                {
                    ShowInventoryUI();
                }
            }
            else
            {
                ToggleUI(false);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (human != null)
            {
                human.isDead.OnValueChanged -= OnDeathStateChange;
            }
            slotIds.OnListChanged -= OnSlotsChanged;
            selectedSlot.OnValueChanged -= OnSelectedSlotChanged;
            itemHolder?.SetActiveItem(null);
            CleanupInputHandlers();
            base.OnNetworkDespawn();
        }

        private void OnDisable()
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }
        }

        public override void OnDestroy()
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }
            CleanupInputHandlers();
            base.OnDestroy();
        }

        #endregion

        #region Initialization

        private void InitializeInventory()
        {
            if (playerMovement != null && playerMovement.controls != null)
            {
                controls = playerMovement.controls;
            }
        }

        private void SetupInputHandlers()
        {
            if (controls == null || human == null) return;

            controls.Gameplay.MouseWheel.performed -= OnMouseWheelChanged;
            controls.Gameplay.SwitchSlot.performed -= OnSlotSwitched;
            controls.Gameplay.Use.performed -= OnUseItem;
            controls.Gameplay.Drop.performed -= OnDropItem;

            controls.Gameplay.MouseWheel.performed += OnMouseWheelChanged;
            controls.Gameplay.SwitchSlot.performed += OnSlotSwitched;
            controls.Gameplay.Use.performed += OnUseItem;
            controls.Gameplay.Drop.performed += OnDropItem;
        }

        private void CleanupInputHandlers()
        {
            if (controls == null || human == null) return;

            controls.Gameplay.MouseWheel.performed -= OnMouseWheelChanged;
            controls.Gameplay.SwitchSlot.performed -= OnSlotSwitched;
            controls.Gameplay.Use.performed -= OnUseItem;
            controls.Gameplay.Drop.performed -= OnDropItem;
        }

        #endregion

        #region Public Methods

        public bool AddItem(GameObject itemToAdd, int targetSlot = -1)
        {
            if (!IsSpawned || !IsOwner || itemToAdd == null ||
                !itemToAdd.TryGetComponent(out Item item) || !item.IsSpawned) return false;
            if (targetSlot == -1) targetSlot = FindEmptySlot();
            if (!IsValidSlot(targetSlot) || items[targetSlot] != null) return false;
            RequestPickupRpc(new NetworkObjectReference(item.NetworkObject), targetSlot);
            return true;
        }

        public bool RemoveItem(int slot)
        {
            if (!IsServer || !IsValidSlot(slot) || slot >= slotIds.Count) return false;
            slotIds[slot] = EmptySlot;
            UpdateHoldingState();
            return true;
        }

        public bool IsActiveItem(Item item) => item != null && GetActiveItem() == item;

        public void ForgetItemServer(Item item)
        {
            if (!IsServer || item == null || slotIds == null) return;
            for (int i = 0; i < slotIds.Count; i++)
            {
                if (slotIds[i] == item.NetworkObjectId) slotIds[i] = EmptySlot;
            }
            UpdateHoldingState();
        }

        private void Update()
        {
            if (IsSpawned && pendingResolution) RefreshReplicatedItems();
        }

        private void OnSlotsChanged(NetworkListEvent<ulong> change) => RefreshReplicatedItems();
        private void OnSelectedSlotChanged(int previous, int current) => RefreshReplicatedItems();

        public void RefreshReplicatedItems()
        {
            if (!IsSpawned || items == null) return;
            activeSlotIndex = selectedSlot.Value;
            pendingResolution = false;
            for (int i = 0; i < items.Length; i++)
            {
                Item previous = items[i];
                Item resolved = null;
                ulong id = i < slotIds.Count ? slotIds[i] : EmptySlot;
                if (id != EmptySlot)
                {
                    if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj) &&
                        obj.TryGetComponent(out resolved))
                    {
                        // Parent messages and list snapshots may arrive in either order.
                        if (obj.transform.parent != transform) pendingResolution = true;
                    }
                    else pendingResolution = true;
                }
                items[i] = resolved;
                if (previous != null && previous != resolved) previous.SetInventoryVisible(true);
                if (resolved != null) resolved.SetInventoryVisible(i == activeSlotIndex);
            }
            Item active = GetActiveItem();
            if (presentedItem != active)
            {
                presentedItem = active;
                itemHolder?.SetActiveItem(active);
                itemChanged?.Invoke(active != null);
                if (IsOwner && autoHideUI) ShowInventoryUI();
            }
            if (IsOwner) UpdateUI();
        }

        private void UpdateHoldingState()
        {
            if (IsServer)
                isHoldingItem.Value = selectedSlot.Value >= 0 && selectedSlot.Value < slotIds.Count &&
                                      slotIds[selectedSlot.Value] != EmptySlot;
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
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }

            if (userInterface != null)
            {
                if (isActive)
                {
                    if (human != null && human.isDead.Value) return;

                    UpdateUI();
                    if (autoHideUI)
                    {
                        ShowInventoryUI();
                    }
                    else
                    {
                        userInterface.gameObject.SetActive(true);
                        if (uiCanvasGroup != null)
                        {
                            uiCanvasGroup.alpha = 1f;
                        }
                    }
                }
                else
                {
                    if (uiCanvasGroup != null)
                    {
                        uiCanvasGroup.alpha = 0f;
                    }
                    userInterface.gameObject.SetActive(false);
                }
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
            if (newSlotIndex == activeSlotIndex)
            {
                if (autoHideUI)
                {
                    ShowInventoryUI();
                }
                return;
            }

            ChangeActiveSlot(newSlotIndex);
        }

        private void OnSlotSwitched(InputAction.CallbackContext context)
        {
            if (IsPlayerInInteraction()) return;

            int newSlot = (int)context.ReadValue<float>() - 1;
            if (newSlot == activeSlotIndex)
            {
                if (autoHideUI)
                {
                    ShowInventoryUI();
                }
                return;
            }
            ChangeActiveSlot(newSlot);
        }

        private void OnUseItem(InputAction.CallbackContext context)
        {
            if (IsPlayerInInteraction()) return;

            Item activeItem = GetActiveItem();
            activeItem?.Use(gameObject);
        }

        private void OnDropItem(InputAction.CallbackContext context)
        {
            Item item = GetActiveItem();
            if (item == null || IsPlayerInInteraction()) return;
            GetDropPose(out Vector3 origin, out Vector3 forward);
            DropItemServerRpc(new NetworkObjectReference(item.NetworkObject), origin, forward);
        }

        private void OnDeathStateChange(bool oldValue, bool isDead)
        {
            if (isDead)
            {
                DropEverything();
                ToggleUI(false);
            }
            else
            {
                UpdateUI();
                ToggleUI(true);
            }
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
            if (IsOwner && IsValidSlot(newSlotIndex)) SelectSlotRpc(newSlotIndex);
        }

        private void DropEverything()
        {
            if (!IsServer) return;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                Item item = items[i];
                item.TurnOffServer();
                DropItemServer(item, transform.position + Vector3.up, transform.forward);
            }
        }

        #endregion

        #region UI Management

        public void ShowInventoryUI()
        {
            if (userInterface == null) return;
            if (human != null && human.isDead.Value) return;
            if (IsPlayerInInteraction()) return;
            if (IsSpawned && !IsOwner) return;

            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }

            UpdateUI();

            userInterface.gameObject.SetActive(true);
            if (uiCanvasGroup != null)
            {
                uiCanvasGroup.alpha = 1f;
            }

            if (autoHideUI && gameObject.activeInHierarchy)
            {
                hideCoroutine = StartCoroutine(HideAfterDelayRoutine());
            }
        }

        private IEnumerator HideAfterDelayRoutine()
        {
            yield return new WaitForSeconds(autoHideDelay);

            if (uiCanvasGroup != null && fadeDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    uiCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                    yield return null;
                }
                uiCanvasGroup.alpha = 0f;
            }

            if (userInterface != null)
            {
                userInterface.gameObject.SetActive(false);
            }

            hideCoroutine = null;
        }

        private void UpdateUI()
        {
            if (userInterface == null || slotPrefab == null) return;

            ValidateUISetup();

            if (slots == null || slots.Length != capacity)
            {
                slots = new UISlot[capacity];
            }

            if (userInterface.childCount == 0)
            {
                CreateUISlots();
            }
            else
            {
                for (int i = 0; i < capacity && i < userInterface.childCount; i++)
                {
                    if (slots[i] == null)
                    {
                        slots[i] = userInterface.GetChild(i).GetComponent<UISlot>();
                    }
                }
            }

            for (int i = 0; i < capacity; i++)
            {
                if (slots[i] == null) continue;
                bool hasItem = items != null && items[i] != null;
                bool isActive = i == activeSlotIndex;
                Sprite icon = hasItem ? items[i].Icon : null;
                slots[i].UpdateUI(hasItem, isActive, icon);
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

        private void GetDropPose(out Vector3 origin, out Vector3 forward)
        {
            if (IsOwner && CameraMovement.Instance != null)
            {
                origin = CameraMovement.Instance.transform.position;
                forward = CameraMovement.Instance.transform.forward;
                return;
            }
            origin = transform.position + Vector3.up;
            forward = transform.forward;
        }

        private bool CanRequest(ulong sender) => sender == OwnerClientId &&
            (human == null || !human.isDead.Value);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPickupRpc(NetworkObjectReference reference, int slot, RpcParams rpc = default)
        {
            if (!CanRequest(rpc.Receive.SenderClientId) || !IsValidSlot(slot) ||
                slot >= slotIds.Count || slotIds[slot] != EmptySlot ||
                !reference.TryGet(out var obj) || !obj.TryGetComponent(out Item item) ||
                item.isCurrentlyHeld ||
                (obj.transform.position - (transform.position + Vector3.up)).sqrMagnitude > 16f) return;
            if (!obj.TrySetParent(NetworkObject)) return;
            slotIds[slot] = obj.NetworkObjectId;
            UpdateHoldingState();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SelectSlotRpc(int slot, RpcParams rpc = default)
        {
            if (!CanRequest(rpc.Receive.SenderClientId) || !IsValidSlot(slot) || slot == selectedSlot.Value) return;
            GetActiveItem()?.TurnOffServer();
            selectedSlot.Value = slot;
            UpdateHoldingState();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DropItemServerRpc(NetworkObjectReference reference, Vector3 origin, Vector3 forward,
            RpcParams rpc = default)
        {
            if (!CanRequest(rpc.Receive.SenderClientId) || !reference.TryGet(out var obj) ||
                !obj.TryGetComponent(out Item item) || !IsActiveItem(item) ||
                obj.transform.parent != transform || !IsFinite(origin) || !IsFinite(forward)) return;
            DropItemServer(item, origin, forward);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void DropItemServer(Item item, Vector3 origin, Vector3 forward)
        {
            Vector3 playerOrigin = transform.position + Vector3.up;
            if ((origin - playerOrigin).sqrMagnitude > 9f) origin = playerOrigin;
            forward = forward.sqrMagnitude < 0.0001f ? transform.forward : forward.normalized;
            Vector3 position = origin + forward * 0.75f;
            if (Physics.Raycast(origin, forward, out RaycastHit hit, 1f, layer))
                position = hit.point - forward * 0.2f;
            item.transform.position = position;
            if (!item.NetworkObject.TryRemoveParent()) return;
            item.Drop();
            ForgetItemServer(item);
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
            if (item == null || !item.TryGetComponent(out Item itemComp)) return;
            item.TryGetComponent(out ParentConstraint constraint);
            if (constraint != null) constraint.constraintActive = false;
            itemComp.BindHolderInventory(this, constraint);
            RefreshReplicatedItems();
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