using FileSystem.Scripts;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Player.Movement
{
    public class CameraMovement : NetworkBehaviour
    {
        [Header("Essential")]
        public Transform playerBody;
        public Transform ragdollHead;
        public LayerMask layerMask;

        [Header("Settings")]
        public float mouseSensitivity = 1f;
        public float hitDistance = 2.0f;
        public Vector3 startPosition;

        private float xRotation;
        private IInteractable interactable;
        private PlayerMovement movement;
        private Human human;

        public static CameraMovement Instance;


        #region Unity Lifecycle


        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                gameObject.SetActive(false);
                return;
            }

            SetupSingleton();

            startPosition = transform.localPosition;
            movement = PlayerMovement.Instance;

            //terrible architecture, but hard to remake, TODO ?
            human = PlayerMovement.Instance.human;

            SettingsFile file = (SettingsFile)GameFileHandler.Instance.SearchForFileByName("Settings");
            mouseSensitivity = file.save.sensValue;

            SetupInputHandlers();
            UpdateCursorState();

            if (!playerBody)
            {
                Debug.LogError("Player body not assigned to CameraMovement script!");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            CleanupInputHandlers();
        }


        #endregion

        #region Initialization

        private void SetupSingleton()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        private void SetupInputHandlers()
        {
            PlayerMovement.Instance.controls.System.Pause.performed += OnPausePerformed;
            PlayerMovement.Instance.controls.Gameplay.Interact.performed += OnInteract;
            human.isDead.OnValueChanged += OnDeathStateChange;
        }
        private void CleanupInputHandlers()
        {
            PlayerMovement.Instance.controls.System.Pause.performed -= OnPausePerformed;
            PlayerMovement.Instance.controls.Gameplay.Interact.performed -= OnInteract;
            human.isDead.OnValueChanged -= OnDeathStateChange;
        }

        #endregion

        #region Input Handling

        private void OnPausePerformed(InputAction.CallbackContext obj)
        {
            if (movement.isInInteraction) return;
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

        private void OnDeathStateChange(bool old, bool isDead)
        {
            if (human.isDead.Value)
            {
                if (PlayerMovement.Instance.isInInteraction) StopInteraction();

                if (!ragdollHead) return;
                transform.parent = ragdollHead;
                transform.localPosition = Vector3.zero;
            }
            else
            {
                transform.parent = playerBody;
                transform.localPosition = startPosition;
                transform.localEulerAngles = Vector3.zero;
            }
            transform.localScale = new Vector3(1f, 1f, 1f);
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
                transform.localScale = new Vector3(1f, 1f, 1f);
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
            transform.localPosition = startPosition;
            transform.localScale = new Vector3(1f, 1f, 1f);
            ToggleInteractionUI(false);
        }

        private void ToggleInteractionUI(bool isInteracting)
        {
            movement.isInInteraction = isInteracting;
            if (isInteracting)
                movement.SwitchToInteractionControls();
            else
                movement.SwitchToGameplayControls();


            UpdateCursorState();
            GetComponentInParent<Inventory>().ToggleUI(!isInteracting);
        }

        private void UpdateCursorState()
        {
            UnityEngine.Cursor.lockState = (movement.isPaused || movement.isInInteraction)
        ? CursorLockMode.None
        : CursorLockMode.Locked;
        }

        #endregion

        private Vector2 previousLook;

        void Update()
        {
            if (movement.isPaused || movement.isInInteraction || human.isDead.Value || human.IsEffectActive(EffectType.Freeze)) return;

            // Checks if there is a Player Body attached
            if (!playerBody)
            {
                Debug.LogError("Player body not assigned to CameraMovement script!");
                return;
            }

            Vector2 lookInput = PlayerMovement.Instance.controls.Gameplay.Look.ReadValue<Vector2>();
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
