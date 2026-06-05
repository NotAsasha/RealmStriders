using FileSystem.Scripts;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using System.Collections;

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

        [Header("Interaction")]
        public float moveDuration = 0.5f;


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

            SettingsFile.OnSettingsChanged += UpdateSensitivity;

            SetupInputHandlers();
            UpdateCursorState();
        }

        private void UpdateSensitivity()
        {
            SettingsFile file = (SettingsFile)GameFileHandler.Instance.SearchForFileByName("Settings");
            mouseSensitivity = file.save.sensValue;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            SettingsFile.OnSettingsChanged -= UpdateSensitivity;
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

        Coroutine cameraAnimation;
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
                cameraAnimation = StartCoroutine(MoveToInteractible(interactable.GetCameraPoint()));
            }
        }

        private IEnumerator MoveToInteractible(Transform cameraPoint)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;


            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / moveDuration);

                transform.position = Vector3.Lerp(startPos, cameraPoint.position, t);
                transform.rotation = Quaternion.Slerp(startRot, cameraPoint.rotation, t);

                yield return null;
            }

            transform.position = cameraPoint.position;
            transform.rotation = cameraPoint.rotation;
        }

        public void StopInteraction()
        {
            if (interactable == null)
            {
                Debug.LogError("---Camera: Trying to stop interacting with null.");
                return;
            }

            interactable.StopInteraction();
            interactable = null;
            StopCoroutine(cameraAnimation);

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
            previousLook = Vector2.Lerp(previousLook, lookInput, 20f * Time.deltaTime);

            float mouseX = previousLook.x * mouseSensitivity;
            float mouseY = previousLook.y * mouseSensitivity;

            // Rotates the camera and a player body
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}
