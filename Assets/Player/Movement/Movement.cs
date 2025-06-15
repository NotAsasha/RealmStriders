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
        CharacterController player;
        [SerializeField] private float moveSpeed = 7.5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float gravity = Physics.gravity.y;
        [SerializeField] private float jumpHeight = 1f;

        [Header("Keybinds")]
        public KeyCode jumpKey = KeyCode.Space;

        [Header("Ground Check")]
        public float playerHeight = 1.75f;
        public LayerMask whatIsGround;

        [Header("Other Settings")]
        public bool isNetwork = true;

        public static Controls _controls;
        public static bool isPaused = false;

        private bool isGrounded;
        private Vector3 velocity = new();
        private float currentSpeed;
        private Transform playerTransform;
        private SettingsFile settingsFile;
        private GameFileHandler _fileHandler;

        private const KeyCode EscapeKey = KeyCode.Escape;
        void Start()
        {
            // Checks of you are owner of this Network Object and if Network is turned on
            if (!IsOwner && isNetwork) enabled = false;

            Application.targetFrameRate = 240;
            player = GetComponent<CharacterController>();
            playerTransform = transform;
            Debug.Log(_controls);
            _controls = new();
            _fileHandler = GameFileHandler.Instance;
            settingsFile = (SettingsFile)_fileHandler.SearchForFileByName("Settings");
            LoadBindings();
            _controls.System.Enable();
            _controls.Gameplay.Enable();
            _controls.Gameplay.Jump.performed += OnJump;
            _controls.System.Pause.performed += OnPause;
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
        private void OnJump(InputAction.CallbackContext obj)
        {
            // Handle jumping
            if (isGrounded)
            {
                velocity.y = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
            }
        }
        private void OnPause(InputAction.CallbackContext obj)
        {
            isPaused = !isPaused;
            if (isPaused)
            {
                _controls.Gameplay.Disable();
                _controls.UI.Enable();
            }
            else
            {
                _controls.Gameplay.Enable();
                _controls.UI.Disable();
            }

        }
        void FixedUpdate()
        {
            if (isPaused) return;

            isGrounded = Physics.Raycast(playerTransform.position, Vector3.down, playerHeight * 0.5f + 0.3f, whatIsGround);
            if (isGrounded && velocity.y < 0) velocity.y = 0f;

            var MovementControls = _controls.Gameplay.Movement;
            // Calculate movement direction and speed
            Vector3 move = playerTransform.right * MovementControls.ReadValue<Vector3>().x + playerTransform.forward * MovementControls.ReadValue<Vector3>().z;
            if (move.magnitude > 1) move.Normalize();

            // Set the current speed based on whether the run key is pressed
            currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : moveSpeed;

            // Calculate the final position using the movement direction and speed
            Vector3 finalPosition = currentSpeed * move + velocity;

            // Apply the final position to the character controller
            player.Move(finalPosition * Time.deltaTime);

            // Apply gravity to the velocity
            velocity.y += gravity * Time.deltaTime;
        }
    }
}