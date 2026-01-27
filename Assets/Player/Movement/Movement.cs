using FileSystem;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    public class Movement : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7.5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float gravity = Physics.gravity.y;
        [SerializeField] private float jumpHeight = 1f;

        [Header("Ground Check")]
        public float playerHeight = 1.75f;
        public LayerMask whatIsGround;

        public static Movement instance;
        public Camera playerCamera;

        public Controls _controls;
        public bool isPaused;
        public bool isInInteraction;

        public AudioSource audioSource;
        public AudioResource[] stepSounds;

        private CharacterController player;
        private Vector3 velocity = new();
        private float currentSpeed;
        private Transform playerTransform;
        private SettingsFile settingsFile;
        private GameFileHandler _fileHandler;
        private bool jumpRequested;
        #region Unity Lifecycle

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            SetupSingletone();

            _controls = new();
            player = GetComponent<CharacterController>();
            playerTransform = transform;
            _fileHandler = GameFileHandler.Instance;
            playerCamera = GetComponentInChildren<Camera>();

            settingsFile = (SettingsFile)_fileHandler.SearchForFileByName("Settings");
            LoadBindings();
            _controls.System.Enable();
            _controls.Gameplay.Enable();

            SetupInputHandlers();
        }
        public override void OnNetworkDespawn()
        {
            CleanupInputHandlers();
        }

        #endregion

        #region Initialization

        private void SetupSingletone()
        {
            if (instance != null) Destroy(instance);
            instance = this;
        }

        private void LoadBindings()
        {
            string rebinds = settingsFile.save.rebinds;
            if (rebinds.Length < 5) { }
            if (!string.IsNullOrEmpty(rebinds))
            {
                try { _controls.LoadBindingOverridesFromJson(rebinds); }
                catch { Debug.LogWarning("---Movement: Unable to rewrite bindings"); }
                Debug.Log("---Movement: Bindings loaded!"); 
            }
        }

        private void SetupInputHandlers()
        {
            _controls.Gameplay.Jump.performed += OnJump;
            _controls.System.Pause.performed += OnPause;
        }

        private void CleanupInputHandlers()
        {
            _controls.Gameplay.Jump.performed -= OnJump;
            _controls.System.Pause.performed -= OnPause;
        }

        #endregion

        #region Input Handling

        private void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                jumpRequested = true;
        }

        private void OnPause(InputAction.CallbackContext obj)
        {
            if (isInInteraction) return;
            isPaused = !isPaused;

            if (isPaused)
                SwitchToInteractionControls();
            else
            {
                
                SwitchToGameplayControls();
            }
        }

        #endregion

        public void SwitchToGameplayControls()
        {
            _controls.Gameplay.Enable();
            _controls.UI.Disable();
        }
        public void SwitchToInteractionControls()
        {
            _controls.Gameplay.Disable();
            _controls.Gameplay.Interact.Enable();
            _controls.UI.Enable();
        }

        Vector3 previousMove;
        void Update()
        {
            
            if (player.isGrounded && velocity.y < 0) velocity.y = 0f;

            var MovementControls = _controls.Gameplay.Movement;

            // Calculate movement direction and speed
            Vector3 move = playerTransform.right * MovementControls.ReadValue<Vector3>().x + playerTransform.forward * MovementControls.ReadValue<Vector3>().z;
            if (move.magnitude > 1) move.Normalize();

            if (jumpRequested && player.isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpRequested = false;
            }

            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            currentSpeed = isRunning ? runSpeed : moveSpeed;

            //Steps sound
            if (isSoundReady && player.isGrounded && player.velocity.magnitude > 0.3f) PlayStepsSound(isRunning ? 0.3f : 0.5f);
            

            // Apply movement
            Vector3 targetMove = move * currentSpeed;
            Vector3 smoothMove = Vector3.Lerp(previousMove, targetMove, 10f * Time.deltaTime);
            previousMove = smoothMove;

            player.Move((smoothMove + velocity) * Time.deltaTime);
            velocity.y += gravity * Time.deltaTime;
        }

        bool isSoundReady = true;
        private void PlayStepsSound(float cooldown)
        {
            isSoundReady = false;
            int soundsCount = stepSounds.Length;
            audioSource.resource = stepSounds[Random.Range(0, soundsCount)];
            audioSource.Play();
            StartCoroutine(SoundTimer(cooldown));
        }

        private IEnumerator SoundTimer(float cooldown)
        {
            yield return new WaitForSeconds(cooldown);
            isSoundReady = true;
            audioSource.Stop();
        }
    }
}