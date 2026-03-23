using System.Collections;
using FileSystem.Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace Player.Movement
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7.5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float gravity = Physics.gravity.y;
        [SerializeField] private float jumpHeight = 1f;

        [Header("Ground Check")]
        public float playerHeight = 1.75f;
        public LayerMask whatIsGround;

        [Header("Sounds")]
        public AudioSource audioSource;
        public AudioResource[] stepSounds;

        public Camera playerCamera;

        public Controls controls;
        public bool isPaused;
        public bool isInInteraction;


        private CharacterController player;
        private Vector3 velocity;
        private float currentSpeed;
        private Transform playerTransform;
        private SettingsFile settingsFile;
        private GameFileHandler fileHandler;
        private bool jumpRequested;

        public static PlayerMovement Instance;


        #region Unity Lifecycle


        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            SetupSingleton();

            controls = new();
            player = GetComponent<CharacterController>();
            playerTransform = transform;
            fileHandler = GameFileHandler.Instance;
            playerCamera = GetComponentInChildren<Camera>();

            settingsFile = (SettingsFile)fileHandler.SearchForFileByName("Settings");
            LoadBindings();
            controls.System.Enable();
            controls.Gameplay.Enable();

            SetupInputHandlers();
        }
        public override void OnNetworkDespawn()
        {
            CleanupInputHandlers();
        }

        #endregion


        #region Initialization


        private void SetupSingleton()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        private void LoadBindings()
        {
            string rebinds = settingsFile.save.rebinds;
            if (rebinds.Length < 5) { }
            if (!string.IsNullOrEmpty(rebinds))
            {
                try { controls.LoadBindingOverridesFromJson(rebinds); }
                catch { Debug.LogWarning("---Movement: Unable to rewrite bindings"); }
                Debug.Log("---Movement: Bindings loaded!"); 
            }
        }

        private void SetupInputHandlers()
        {
            controls.Gameplay.Jump.performed += OnJump;
            controls.System.Pause.performed += OnPause;
        }

        private void CleanupInputHandlers()
        {
            controls.Gameplay.Jump.performed -= OnJump;
            controls.System.Pause.performed -= OnPause;
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


        #region ControlsHandlers


        public void SwitchToGameplayControls()
        {
            controls.Gameplay.Enable();
            controls.UI.Disable();
        }

        public void SwitchToInteractionControls()
        {
            controls.Gameplay.Disable();
            controls.Gameplay.Interact.Enable();
            controls.UI.Enable();
        }


        #endregion


        private Vector3 previousMove;
        private void Update()
        {
            if (player.isGrounded && velocity.y < 0) velocity.y = 0f;


            InputAction movementControls = controls.Gameplay.Movement;


            //Calculate movement direction and speed
            Vector3 move = playerTransform.right * movementControls.ReadValue<Vector3>().x + playerTransform.forward * movementControls.ReadValue<Vector3>().z;
            if (move.magnitude > 1) move.Normalize();
            if (jumpRequested && player.isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpRequested = false;
            }


            //Running
            bool isRunning = controls.Gameplay.Run.IsPressed();
            currentSpeed = isRunning ? runSpeed : moveSpeed;


            //Steps sound
            Vector2 horizontalVelocity = new(player.velocity.x, player.velocity.z);
            if (isSoundReady && player.isGrounded && horizontalVelocity.magnitude > 0.3f) PlayStepsSound(isRunning ? 0.3f : 0.5f);
            

            //Apply movement
            Vector3 targetMove = move * currentSpeed;
            Vector3 smoothMove = Vector3.Lerp(previousMove, targetMove, 10f * Time.deltaTime);
            previousMove = smoothMove;


            player.Move((smoothMove + velocity) * Time.deltaTime);
            velocity.y += gravity * Time.deltaTime;
        }

        private bool isSoundReady = true;
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