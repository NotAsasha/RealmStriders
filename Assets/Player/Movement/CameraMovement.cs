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
    [DefaultExecutionOrder(-100)]
    public class CameraMovement : NetworkBehaviour
    {
        [Header("Essential")]
        public Transform playerBody;
        public Transform ragdollHead;
        public LayerMask layerMask;

        [Header("Spectator References")]
        [SerializeField] private SpectatorController spectatorController;

        [Header("Audio")]
        [SerializeField] private AudioListener audioListener;
        public Transform AudioListenerTransform => audioListener != null ? audioListener.transform : null;

        [Header("Settings")]
        public float mouseSensitivity = 1f;
        public float hitDistance = 2.0f;
        public Vector3 startPosition;
        [SerializeField, Min(0.1f)] private float crouchCameraSpeed = 4f;
        [SerializeField, Min(0f)] private float crouchCameraForward = 0.4f;

        [Header("First-person visibility")]
        [SerializeField] private Renderer[] firstPersonHiddenRenderers = System.Array.Empty<Renderer>();
        private UnityEngine.Rendering.ShadowCastingMode[] originalShadowModes;

        private Vector3 GameplayPosition => startPosition + (movement != null
            ? new Vector3(0f, movement.CrouchCameraOffset, movement.IsCrouching ? crouchCameraForward : 0f)
            : Vector3.zero);

        [Header("Interaction")]
        public float moveDuration = 0.5f;

        private float xRotation;
        private IInteractable interactable;
        private PlayerMovement movement;
        private Human human;
        private Coroutine spectatorTransition;
        private bool inputHandlersRegistered;

        public static CameraMovement Instance;

        /// <summary>
        /// Current camera vertical look pitch in degrees (-90 to +90). Negative is looking up, positive is looking down.
        /// </summary>
        public float CurrentPitch => xRotation;


        #region Unity Lifecycle


        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                gameObject.SetActive(false);
                return;
            }

            SetupSingleton();

#if STEAMAUDIO_ENABLED
            // The owner camera appears after scene load, when Steam Audio searches for its listener.
            if (audioListener != null && SteamAudio.SteamAudioManager.Singleton != null)
                SteamAudio.SteamAudioManager.NotifyAudioListenerChangedTo(audioListener.transform);
#endif

            startPosition = transform.localPosition;
            movement = PlayerMovement.Instance;

            //terrible architecture, but hard to remake, TODO ?
            human = PlayerMovement.Instance.human;
            CacheFirstPersonVisibility();
            SetFirstPersonVisibility(true);

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
#if STEAMAUDIO_ENABLED
            if (SteamAudio.SteamAudioManager.Singleton != null)
                SteamAudio.SteamAudioManager.NotifyAudioListenerChangedTo(null);
#endif
            SetFirstPersonVisibility(false);
            CleanupInputHandlers();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }


        #endregion

        #region Initialization

        private void CacheFirstPersonVisibility()
        {
            originalShadowModes = new UnityEngine.Rendering.ShadowCastingMode[firstPersonHiddenRenderers.Length];
            for (int i = 0; i < firstPersonHiddenRenderers.Length; i++)
            {
                if (firstPersonHiddenRenderers[i] != null)
                    originalShadowModes[i] = firstPersonHiddenRenderers[i].shadowCastingMode;
            }
        }

        private void SetFirstPersonVisibility(bool firstPerson)
        {
            if (originalShadowModes == null) return;
            for (int i = 0; i < firstPersonHiddenRenderers.Length; i++)
            {
                if (firstPersonHiddenRenderers[i] == null) continue;
                // Keep the complete shadow and the rig used by the hands and held equipment.
                firstPersonHiddenRenderers[i].shadowCastingMode = firstPerson
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    : originalShadowModes[i];
            }
        }

        private void SetupSingleton()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        private void SetupInputHandlers()
        {
            if (PlayerMovement.Instance == null || PlayerMovement.Instance.controls == null || human == null) return;
            PlayerMovement.Instance.controls.System.Pause.performed += OnPausePerformed;
            PlayerMovement.Instance.controls.Gameplay.Interact.performed += OnInteract;
            human.isDead.OnValueChanged += OnDeathStateChange;
            inputHandlersRegistered = true;
        }
        private void CleanupInputHandlers()
        {
            if (!inputHandlersRegistered) return;

            if (movement != null && movement.controls != null)
            {
                movement.controls.System.Pause.performed -= OnPausePerformed;
                movement.controls.Gameplay.Interact.performed -= OnInteract;
            }

            if (human != null) human.isDead.OnValueChanged -= OnDeathStateChange;
            inputHandlersRegistered = false;
        }

        #endregion

        #region Input Handling

        private void OnPausePerformed(InputAction.CallbackContext obj)
        {
            if (movement == null || !movement.IsSpawned || movement.isInInteraction) return;
            UpdateCursorState();
        }

        private void OnInteract(InputAction.CallbackContext obj)
        {
            if (movement == null || !movement.IsSpawned || human == null || human.isDead.Value) return;
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
                if (movement != null && movement.isInInteraction) StopInteraction();
                SetFirstPersonVisibility(true);
                SetSpectatorCursor(true);

                if (!ragdollHead) return;
                transform.parent = ragdollHead;
                transform.localPosition = Vector3.zero;

                // НОВЕ: Запускаємо перехід у режим глядача через 3 секунди після падіння
                if (spectatorTransition != null) StopCoroutine(spectatorTransition);
                spectatorTransition = StartCoroutine(TransitionToSpectator(3.0f));
            }
            else
            {
                // Якщо гравця воскресили (наприклад, дефібрилятором)
                if (spectatorTransition != null)
                {
                    StopCoroutine(spectatorTransition);
                    spectatorTransition = null;
                }
                if (spectatorController) spectatorController.StopSpectating();
                SetFirstPersonVisibility(true);
                this.enabled = true; // Вмикаємо назад FPS камеру
                UpdateCursorState();

                transform.parent = playerBody;
                transform.localPosition = GameplayPosition;
                transform.localEulerAngles = Vector3.zero;
            }
            transform.localScale = new Vector3(1f, 1f, 1f);
        }

        #endregion

        #region Private Helper Methods

        Coroutine cameraAnimation;
        private void StartInteraction()
        {
            if (movement == null || human == null || human.isDead.Value) return;

            //Check for objects in sight
            if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, hitDistance, layerMask)) return;
            interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

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
                Transform cameraPoint = interactable.GetCameraPoint();
                if (cameraPoint == null)
                {
                    StopInteraction();
                    return;
                }

                cameraAnimation = StartCoroutine(MoveToInteractible(cameraPoint));
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
            SetFirstPersonVisibility(false);
        }

        public void StopInteraction()
        {
            if (interactable == null)
            {
                ToggleInteractionUI(false);
                return;
            }

            interactable.StopInteraction();
            interactable = null;
            if (cameraAnimation != null) StopCoroutine(cameraAnimation);
            cameraAnimation = null;

            transform.localPosition = GameplayPosition;
            transform.localScale = new Vector3(1f, 1f, 1f);
            ToggleInteractionUI(false);
        }

        private void ToggleInteractionUI(bool isInteracting)
        {
            if (movement == null) return;
            movement.isInInteraction = isInteracting;
            if (!isInteracting) SetFirstPersonVisibility(true);
            movement.onInteractionStateChanged?.Invoke(isInteracting);
            if (isInteracting)
                movement.SwitchToInteractionControls();
            else
                movement.SwitchToGameplayControls();


            UpdateCursorState();
            if (human == null || !human.isDead.Value)
            {
                GetComponentInParent<Inventory>()?.ToggleUI(!isInteracting);
            }
        }

        private void UpdateCursorState()
        {
            if (movement == null) return;
            UnityEngine.Cursor.lockState = (movement.isPaused || movement.isInInteraction)
        ? CursorLockMode.None
        : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = movement.isPaused || movement.isInInteraction;
        }

        public void SetSpectatorCursor(bool isTerminalTarget)
        {
            if (!IsOwner) return;

            UnityEngine.Cursor.lockState = isTerminalTarget
                ? CursorLockMode.None
                : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = isTerminalTarget;
        }

        private IEnumerator TransitionToSpectator(float delay)
        {
            yield return new WaitForSeconds(delay);
            spectatorTransition = null;

            if (!human.isDead.Value) yield break;

            // Вимикаємо керування головою від першої особи
            this.enabled = false;

            // Вмикаємо менеджер спостереження
            if (spectatorController != null)
            {
                SetFirstPersonVisibility(false);
                spectatorController.StartSpectating(this.transform);
            }
        }

        #endregion

        private Vector2 previousLook;

        void Update()
        {
            if (movement == null || human == null || movement.controls == null) return;
            if (movement.isPaused || movement.isInInteraction || human.isDead.Value || human.IsEffectActive(EffectType.Freeze)) return;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, GameplayPosition,
                crouchCameraSpeed * Time.deltaTime);

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
