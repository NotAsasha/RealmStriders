using FileSystem;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using InventorySystem;
using Unity.Cinemachine;

namespace Player
{
    public class CameraMovement : NetworkBehaviour
    {
        [Header("Set Up")]
        public Transform playerBody;
        public Transform ragdollHead;
        public LayerMask layerMask;
        [Header("Camera")]
        public float mouseSensitivity = 1f;
        public float hitDistance = 2.0f;
        public Vector3 StartPosition;

        private float xRotation = 0f;
        private IInteractable interactable;
        private Movement movement;
        private Human human;

        #region Unity Lifecycle

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                gameObject.SetActive(false);
                return;
            }

            StartPosition = transform.localPosition;
            movement = Movement.instance;
            human = GetComponentInParent<Human>();
            SettingsFile file = (SettingsFile)GameFileHandler.Instance.SearchForFileByName("Settings");
            mouseSensitivity = file.save._sensValue;

            SetupInputHandlers();
            UpdateCursorState();
        }
        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            ClearupInputHandlers();
        }

        #endregion

        #region Initialization

        private void SetupInputHandlers()
        {
            Movement.instance._controls.System.Pause.performed += OnPausePerformed;
            Movement.instance._controls.Gameplay.Interact.performed += OnInteract;
            human.isDead.OnValueChanged += OnDeathStateCnange;
        }
        private void ClearupInputHandlers()
        {
            Movement.instance._controls.System.Pause.performed -= OnPausePerformed;
            Movement.instance._controls.Gameplay.Interact.performed -= OnInteract;
            human.isDead.OnValueChanged -= OnDeathStateCnange;
        }

        #endregion

        #region Input Handling

        private void OnPausePerformed(InputAction.CallbackContext obj)
        {
            UpdateCursorState();
        }

        private void OnInteract(InputAction.CallbackContext obj)
        {
            if (movement.isPaused) return;
            
            if (movement.isInInteraction)
            {
                StopInteraction();
                return;
            }

            StartInteraction();
        }

        private void OnDeathStateCnange(bool old, bool _isDead)
        {
            if (human.isDead.Value)
            {
                if (Movement.instance.isInInteraction) StopInteraction();

                if (!ragdollHead) return;
                transform.parent = ragdollHead;
                transform.localPosition = Vector3.zero;
            }
            else
            {
                transform.parent = playerBody;
                transform.localPosition = StartPosition;
                transform.localEulerAngles = Vector3.zero;
            }
        }

        #endregion

        #region Private Helper Methods

        private void StartInteraction()
        {
            //Check for objects in sight
            if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, hitDistance, layerMask)) return;
            interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable.IsTaken())
            {
                interactable = null;
                return;
            }

            interactable.Interact(gameObject);

            bool isSingleUse = interactable.IsSingleUse();
            ToggleInteractionUI(!isSingleUse);

            if (isSingleUse) interactable = null;
            else
            {
                transform.parent = hit.collider.gameObject.transform;
            }
        }

        public void StopInteraction()
        {
            if (interactable == null)
            {
                Debug.LogError("---Camera: Trying to stop interacting with null.");
                return;
            }

            interactable.StopInteraction(gameObject);
            interactable = null;
            transform.parent = playerBody;
            transform.localPosition = StartPosition;

            ToggleInteractionUI(false);
        }

        private void ToggleInteractionUI(bool _isInteracting)
        {
            movement.isInInteraction = _isInteracting;
            if (_isInteracting)
                movement.SwitchToInteractionControls();
            else
                movement.SwitchToGameplayControls();


            UpdateCursorState();
            GetComponentInParent<Inventory>().ToggleUI(!_isInteracting);
        }

        private void UpdateCursorState()
        {
            Cursor.lockState = (movement.isPaused || movement.isInInteraction)
        ? CursorLockMode.None
        : CursorLockMode.Locked;
        }

        #endregion

        private Vector2 previousLook;

        void Update()
        {
            if (movement.isPaused || movement.isInInteraction || human.isDead.Value || human.IsEffectActive(EffectType.Freeze)) return;

            // Checks if there is a Player Body attached
            if (playerBody == null)
            {
                Debug.LogError("Player body not assigned to CameraMovement script!");
                return;
            }

            Vector2 lookInput = Movement.instance._controls.Gameplay.Look.ReadValue<Vector2>();
            Vector2 smoothLook = Vector2.Lerp(previousLook, lookInput, 0.5f);
            previousLook = smoothLook;

            float mouseX = lookInput.x * mouseSensitivity;
            float mouseY = lookInput.y * mouseSensitivity;

            // Rotates the camera and a player body
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}
