using FileSystem;
using Unity.Netcode;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.InputSystem;
using InventorySystem;
using UnityEngine.Rendering.Universal;
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
        public float mouseSensitivity = 250f;
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
                ToggleInteractionUI(false);
                return;
            }

            StartInteraction();
        }

        private void OnDeathStateCnange(bool old, bool _isDead)
        {
            if (human.isDead.Value)
            {
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
        }

        private void StopInteraction()
        {
            if (interactable == null)
            {
                Debug.LogError("---Camera: Trying to stop interacting with null.");
                return;
            }

            interactable.StopInteraction(gameObject);
            interactable = null;
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

        void Update()
        {
            if (movement.isPaused || movement.isInInteraction || human.isDead.Value) return;

            // Checks if there is a Player Body attached
            if (playerBody == null)
            {
                Debug.LogError("Player body not assigned to CameraMovement script!");
                return;
            }

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            // Rotates the camera and a player body
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}
