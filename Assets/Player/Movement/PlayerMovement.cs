using System;
using System.Collections;
using FileSystem.Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        [Header("Crouching")]
        [SerializeField, Min(0.1f)] private float crouchHeight = 1.6f;
        [SerializeField, Min(0f)] private float crouchCameraDrop = 0.65f;
        [SerializeField, Min(0f)] private float crouchSpeed = 2.5f;
        [SerializeField, Min(0f)] private float crouchRunSpeed = 3.25f;
        [SerializeField, Range(0f, 1f)] private float crouchNoiseMultiplier = 0.35f;
        [SerializeField] private LayerMask standingObstructions = ~0;

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
        [SerializeField] private PlayerAudio playerAudio;

        public Camera playerCamera;

        public Controls controls;
        public bool isPaused;
        public bool isInInteraction;
        public Action<bool> onInteractionStateChanged;
        public Human human;

        public Vector3 CurrentInputMove => controls.Gameplay.Movement.ReadValue<Vector3>();
        public bool IsRunning => controls != null && controls.Gameplay.Run.IsPressed();
        public bool IsCrouching => isCrouching.Value;
        public float CrouchCameraOffset => IsCrouching ? -crouchCameraDrop : 0f;
        public bool IsGrounded => player.isGrounded;

        private readonly NetworkVariable<bool> isCrouching = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly Collider[] standingHits = new Collider[32];
        private float standingHeight;
        private Vector3 standingCenter;
        private float standingStepOffset;
        private bool crouchRequested;
        private bool suppressCrouchToggle;
        private CrouchMode crouchMode;
        private float EffectiveCrouchHeight => Mathf.Clamp(crouchHeight, player.radius * 2f, standingHeight);

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

        private void Awake()
        {
            player = GetComponent<CharacterController>();
            human = GetComponent<Human>();
            playerTransform = transform;
            standingHeight = player.height;
            standingCenter = player.center;
            standingStepOffset = player.stepOffset;
        }

        public override void OnNetworkSpawn()
        {
            isCrouching.OnValueChanged += OnCrouchChanged;
            if (human != null) human.isDead.OnValueChanged += OnDeathChanged;
            ApplyCrouch(IsCrouching);
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            

            controls = new();
            fileHandler = GameFileHandler.Instance;

            settingsFile = (SettingsFile)fileHandler.SearchForFileByName("Settings");
            LoadBindings();
            SettingsFile.OnSettingsChanged += LoadBindings;

            controls.System.Enable();
            controls.Gameplay.Enable();

            SetupSingleton();
            SetupInputHandlers();
        }
        public override void OnNetworkDespawn()
        {
            isCrouching.OnValueChanged -= OnCrouchChanged;
            if (human != null) human.isDead.OnValueChanged -= OnDeathChanged;
            ApplyCrouch(false);
            crouchRequested = false;
            suppressCrouchToggle = false;
            if (settingsFile != null) SettingsFile.OnSettingsChanged -= LoadBindings;
            CleanupInputHandlers();
            if (Instance == this) Instance = null;
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
            crouchMode = settingsFile.save.crouchMode;
            crouchRequested = IsCrouching;
            string rebinds = settingsFile.save.rebinds;
            controls.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(rebinds))
            {
                try { controls.LoadBindingOverridesFromJson(rebinds); }
                catch { Debug.LogWarning("---Movement: Unable to rewrite bindings"); }
                Debug.Log("---Movement: Bindings loaded!");
            }
        }

        private void SetupInputHandlers()
        {
            if (controls == null) return;
            controls.Gameplay.Jump.performed += OnJump;
            controls.System.Pause.performed += OnPause;
        }

        private void CleanupInputHandlers()
        {
            if (controls != null)
            {
                controls.Gameplay.Jump.performed -= OnJump;
                controls.System.Pause.performed -= OnPause;
                controls.Disable();
                controls.Dispose();
            }

            controls = null;
        }

        #endregion


        #region Input Handling


        private void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed && !IsCrouching && !isPaused && !isInInteraction &&
                (human == null || !human.IsEffectActive(EffectType.Freeze)))
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
            if (controls == null) return;
            suppressCrouchToggle = false;
            foreach (InputControl control in controls.Gameplay.Crouch.controls)
            {
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.isPressed)
                {
                    suppressCrouchToggle = true;
                    break;
                }
            }
            controls.Gameplay.Enable();
            controls.UI.Disable();
        }

        public void SwitchToInteractionControls()
        {
            if (controls == null) return;
            controls.Gameplay.Disable();
            jumpRequested = false;
            suppressCrouchToggle = true;
            controls.Gameplay.Interact.Enable();
            controls.UI.Enable();
        }


        #endregion


        private Vector3 previousMove;

        public void ResetMovementState()
        {
            velocity = Vector3.zero;
            previousMove = Vector3.zero;
            jumpRequested = false;
            maxFallSpeedThisFlight = 0f;
            crouchRequested = false;
            if (IsSpawned && IsOwner && (!player.enabled || CanStand())) isCrouching.Value = false;
        }

        private void OnCrouchChanged(bool previous, bool current) => ApplyCrouch(current);

        private void ApplyCrouch(bool crouched)
        {
            float height = crouched ? EffectiveCrouchHeight : standingHeight;
            player.stepOffset = Mathf.Min(standingStepOffset, height * 0.5f);
            player.height = height;
            // Keep the feet fixed while the capsule's upper half shrinks.
            player.center = standingCenter + Vector3.up * ((height - standingHeight) * 0.5f);
        }

        private void OnDeathChanged(bool previous, bool dead)
        {
            if (!IsOwner || !dead) return;
            crouchRequested = false;
            jumpRequested = false;
            isCrouching.Value = false;
            currentNoiseRadius.Value = 0f;
        }

        private void UpdateCrouch()
        {
            if (isPaused || isInInteraction) return;
            InputAction action = controls.Gameplay.Crouch;
            if (!action.IsPressed()) suppressCrouchToggle = false;
            if (crouchMode == CrouchMode.Toggle)
            {
                if (!suppressCrouchToggle && action.WasPressedThisFrame()) crouchRequested = !crouchRequested;
            }
            else crouchRequested = action.IsPressed();

            if (crouchRequested == IsCrouching) return;
            if (!crouchRequested && !CanStand()) return;
            isCrouching.Value = crouchRequested;
            jumpRequested = false;
        }

        private bool CanStand()
        {
            Vector3 scale = playerTransform.lossyScale;
            float radius = player.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float fullHeight = Mathf.Max(standingHeight * Mathf.Abs(scale.y), radius * 2f);
            float lowHeight = Mathf.Max(EffectiveCrouchHeight * Mathf.Abs(scale.y), radius * 2f);
            Vector3 bottom = playerTransform.TransformPoint(standingCenter) - playerTransform.up * (fullHeight * 0.5f);
            Vector3 from = bottom + playerTransform.up * (lowHeight - radius);
            Vector3 to = bottom + playerTransform.up * (fullHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(from, to, Mathf.Max(0.01f, radius - 0.01f),
                standingHits, standingObstructions, QueryTriggerInteraction.Ignore);
            if (count == standingHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider hit = standingHits[i];
                if (hit.transform.IsChildOf(playerTransform) ||
                    Physics.GetIgnoreLayerCollision(gameObject.layer, hit.gameObject.layer) ||
                    Physics.GetIgnoreCollision(player, hit)) continue;
                return false;
            }
            return true;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || controls == null || !player.enabled) return;
            if (human != null && human.isDead.Value) return;

            // Не застосовувати гравітацію та рух, поки ще не завантажилась ігрова сцена (Lobby)
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == "Menu" || activeSceneName == "SteamBoot")
            {
                ResetMovementState();
                return;
            }

            if (human != null && human.IsEffectActive(EffectType.Freeze))
            {
                velocity = Vector3.zero;
                previousMove = Vector3.zero;
                jumpRequested = false;
                if (currentNoiseRadius.Value != 0f) currentNoiseRadius.Value = 0f;
                return;
            }

            // Обмежуємо крок часу, щоб під час фризів ПК CharacterController не пролітав крізь тонкі коллайдери підлоги
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            bool wasGrounded = player.isGrounded;
            UpdateCrouch();
            InputAction movementControls = controls.Gameplay.Movement;


            //Calculate movement direction and speed
            Vector3 move = playerTransform.right * movementControls.ReadValue<Vector3>().x + playerTransform.forward * movementControls.ReadValue<Vector3>().z;
            if (move.magnitude > 1) move.Normalize();


            //Jump
            if (jumpRequested && player.isGrounded && !IsCrouching)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpRequested = false;
            }


            //Running
            bool isRunning = controls.Gameplay.Run.IsPressed();
            currentSpeed = IsCrouching ? (isRunning ? crouchRunSpeed : crouchSpeed) :
                (isRunning ? runSpeed : moveSpeed);


            //Steps sound
            Vector2 horizontalVelocity = new(player.velocity.x, player.velocity.z);
            float targetNoise = 0f;


            if (player.isGrounded && horizontalVelocity.magnitude > 0.3f)
            {
                playerAudio.PlayFootstep(IsCrouching ? (isRunning ? 0.5f : 0.7f) : (isRunning ? 0.3f : 0.5f));
                targetNoise = IsCrouching ? (isRunning ? walkNoiseRadius : walkNoiseRadius * crouchNoiseMultiplier) :
                    (isRunning ? runNoiseRadius : walkNoiseRadius);
            }

            if (currentNoiseRadius.Value != targetNoise)
            {
                currentNoiseRadius.Value = targetNoise;
            }

            //Apply movement
            Vector3 targetMove = move * currentSpeed;
            previousMove = Vector3.Lerp(previousMove, targetMove, 15f * dt);
            player.Move((previousMove + velocity) * dt);

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
                    velocity.y += gravity * fallMultiplier * dt; // Падаємо швидше
                }
                else
                {
                    velocity.y += gravity * dt; // Летимо вгору зі звичайною швидкістю
                }
            }
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
