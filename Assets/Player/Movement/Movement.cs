using FileSystem;
using Steamworks;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
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

        public Controls _controls;
        public bool isPaused;
        public bool isInInteraction;

        private CharacterController player;
        private bool isGrounded;
        private Vector3 velocity = new();
        private float currentSpeed;
        private Transform playerTransform;
        private SettingsFile settingsFile;
        private GameFileHandler _fileHandler;

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

        private void OnJump(InputAction.CallbackContext obj)
        {
            // Handle jumping
            if (!isGrounded) return;
            velocity.y = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
        }
        private void OnPause(InputAction.CallbackContext obj)
        {
            isPaused = !isPaused;

            if (isPaused)
                SwitchToInteractionControls();
            else
            {
                if (isInInteraction) return;
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

        void FixedUpdate()
        {
            isGrounded = Physics.Raycast(playerTransform.position, Vector3.down, playerHeight * 0.5f + 0.3f, whatIsGround);
            if (isGrounded && velocity.y < 0) velocity.y = 0f;

            var MovementControls = _controls.Gameplay.Movement;

            // Calculate movement direction and speed
            Vector3 move = playerTransform.right * MovementControls.ReadValue<Vector3>().x + playerTransform.forward * MovementControls.ReadValue<Vector3>().z;
            if (move.magnitude > 1) move.Normalize();
            currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : moveSpeed;

            // Apply movement
            Vector3 finalPosition = currentSpeed * move + velocity;
            player.Move(finalPosition * Time.deltaTime);
            velocity.y += gravity * Time.deltaTime;
        }
    }
}