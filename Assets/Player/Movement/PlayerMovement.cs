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

        [Header("Fall Damage")]
        [SerializeField] private bool enableFallDamage = true;
        [SerializeField] private float safeFallSpeed = 12f;
        [SerializeField] private float damageMultiplier = 5f;
        [SerializeField] private float instantKillSpeed = 25f;
        [SerializeField] private float fallMultiplier = 2.5f;

        [Header("Stealth & Noise")]
        public float walkNoiseRadius = 7f;
        public float runNoiseRadius = 15f;

        [Header("Ground Check")]
        public float playerHeight = 1.75f;
        public LayerMask whatIsGround;

        [Header("Sounds")]
        public AudioSource audioSource;
        public AudioClip[] stepSounds;

        public Camera playerCamera;

        public Controls controls;
        public bool isPaused;
        public bool isInInteraction;
        public Human human;

        private CharacterController player;
        private Vector3 velocity;
        private float currentSpeed;
        private Transform playerTransform;
        private SettingsFile settingsFile;
        private GameFileHandler fileHandler;
        private bool jumpRequested;
        private float maxFallSpeedThisFlight = 0f;


        public static PlayerMovement Instance;

        public NetworkVariable<float> currentNoiseRadius = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );


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
            human = GetComponent<Human>();
            playerTransform = transform;
            fileHandler = GameFileHandler.Instance;
            playerCamera = GetComponentInChildren<Camera>();

            settingsFile = (SettingsFile)fileHandler.SearchForFileByName("Settings");
            LoadBindings();
            SettingsFile.OnSettingsChanged += LoadBindings;

            controls.System.Enable();
            controls.Gameplay.Enable();

            SetupInputHandlers();
        }
        public override void OnNetworkDespawn()
        {
            SettingsFile.OnSettingsChanged -= LoadBindings;
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
            bool wasGrounded = player.isGrounded;
            InputAction movementControls = controls.Gameplay.Movement;


            //Calculate movement direction and speed
            Vector3 move = playerTransform.right * movementControls.ReadValue<Vector3>().x + playerTransform.forward * movementControls.ReadValue<Vector3>().z;
            if (move.magnitude > 1) move.Normalize();


            //Jump
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
            float targetNoise = 0f;


            if (player.isGrounded && horizontalVelocity.magnitude > 0.3f)
            {
                PlayStepsSound(isRunning ? 0.3f : 0.5f);
                targetNoise = isRunning ? runNoiseRadius : walkNoiseRadius;
            }

            if (currentNoiseRadius.Value != targetNoise)
            {
                currentNoiseRadius.Value = targetNoise;
            }

            //Apply movement
            Vector3 targetMove = move * currentSpeed;
            previousMove = Vector3.Lerp(previousMove, targetMove, 15f * Time.deltaTime);
            player.Move((previousMove + velocity) * Time.deltaTime);

            //Fall Multiplier
            if (player.isGrounded)
            {
                // Якщо в попередньому кадрі ми падали, а тепер на землі — це момент приземлення
                if (!wasGrounded)
                {
                    if (enableFallDamage && maxFallSpeedThisFlight > safeFallSpeed)
                    {
                        ApplyFallDamage(maxFallSpeedThisFlight);
                    }
                    // Обов'язково скидаємо пікову швидкість після удару об землю
                    maxFallSpeedThisFlight = 0f;
                }

                // Обнуляємо вертикальну швидкість, щоб не накопичувалася, поки стоїмо
                if (velocity.y < 0) velocity.y = 0f;
            }
            else
            {
                // Ми в повітрі: фіксуємо максимальну швидкість падіння
                if (velocity.y < 0)
                {
                    float currentFallSpeed = Mathf.Abs(velocity.y);
                    if (currentFallSpeed > maxFallSpeedThisFlight)
                    {
                        maxFallSpeedThisFlight = currentFallSpeed;
                    }
                }

                // Застосовуємо гравітацію
                if (velocity.y < 0)
                {
                    velocity.y += gravity * fallMultiplier * Time.deltaTime; // Падаємо швидше
                }
                else
                {
                    velocity.y += gravity * Time.deltaTime; // Летимо вгору зі звичайною швидкістю
                }
            }
        }

        private float nextStepTime = 0;

        private void PlayStepsSound(float cooldown)
        {
            if (Time.time < nextStepTime) return;
            if (stepSounds.Length <= 0)
            {
                Debug.LogWarning($"---Player: {name} has no movement sounds.");
                return;
            }

            audioSource.pitch = 1 + UnityEngine.Random.Range(-0.2f, 0.2f);
            audioSource.PlayOneShot(stepSounds[UnityEngine.Random.Range(0, stepSounds.Length)]);
            nextStepTime = Time.time + cooldown;
        }

        private void ApplyFallDamage(float speedAtImpact)
        {
            if (human == null) return;

            //insta death
            if (speedAtImpact >= instantKillSpeed)
            {
                Debug.Log($"---Player: Died of fall damage (Speed: {speedAtImpact})");

                human.TakeDamageRpc(100f);
                return;
            }

            //default damage
            float excessSpeed = speedAtImpact - safeFallSpeed;

            // round to not take "15.34" damage
            float damageToTake = Mathf.Round(excessSpeed * damageMultiplier);

            if (damageToTake > 0)
            {
                Debug.Log($"---Player: Took {damageToTake} fall damage! (Speed: {speedAtImpact})");

                human.TakeDamageRpc(damageToTake);
            }
        }
    }
}