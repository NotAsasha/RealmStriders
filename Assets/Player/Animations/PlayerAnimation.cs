using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Player.Movement;

namespace Player
{
    public class PlayerAnimation : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Rig handWeight;
        [SerializeField] private Inventory inventory;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Human human;

        [Header("Settings")]
        [Tooltip("Швидкість підняття/опускання рук")]
        [SerializeField] private float handAnimationSpeed = 10f;
        [Tooltip("Множник швидкості аніматора (дільник реальної швидкості)")]
        [SerializeField] private float speedModifier = 0.25f;

        private Coroutine weightLerpCoroutine;
        private static readonly int Crouched = Animator.StringToHash("Crouched");
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int StandingSpeed = Animator.StringToHash("StandingSpeed");
        private static readonly int CrouchMoveSpeed = Animator.StringToHash("CrouchMoveSpeed");
        private float standingAnimationSpeed;

        private readonly NetworkVariable<float> netSpeed = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            if (inventory == null) return;
            inventory.isHoldingItem.OnValueChanged += OnHoldingItemNetworkChanged;
            ApplyHandWeight(inventory.isHoldingItem.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (inventory != null)
                inventory.isHoldingItem.OnValueChanged -= OnHoldingItemNetworkChanged;
            if (weightLerpCoroutine != null) StopCoroutine(weightLerpCoroutine);
            weightLerpCoroutine = null;
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                float currentVelocity = 0f;
                if (characterController != null && characterController.enabled)
                {
                    Vector3 velocity = characterController.velocity;
                    currentVelocity = new Vector2(velocity.x, velocity.z).magnitude;
                }

                netSpeed.Value = currentVelocity;
            }

            UpdateAnimatorSpeed();
        }

        private void UpdateAnimatorSpeed()
        {
            if (animator == null) return;

            bool frozen = human != null && human.IsEffectActive(EffectType.Freeze);
            animator.speed = frozen ? 0f : 1f;
            if (frozen) return;
            float targetAnimationSpeed = Mathf.Max(0f, netSpeed.Value * speedModifier);
            standingAnimationSpeed = Mathf.Lerp(standingAnimationSpeed, targetAnimationSpeed, Time.deltaTime * 15f);
            animator.SetFloat(StandingSpeed, standingAnimationSpeed);
            animator.SetFloat(CrouchMoveSpeed, Mathf.Max(0.1f, netSpeed.Value / 2.5f));
            animator.SetBool(Crouched, movement != null && movement.IsCrouching);
            animator.SetBool(Moving, netSpeed.Value > 0.1f);
        }

        #region Item Rig Weight Lerping

        private void OnItemChanged(bool isHoldingSmth)
        {
            ApplyHandWeight(isHoldingSmth);
        }

        private void OnHoldingItemNetworkChanged(bool previousValue, bool newValue)
        {
            ApplyHandWeight(newValue);
        }

        private void ApplyHandWeight(bool isHolding)
        {
            float targetRigWeight = isHolding ? 1f : 0f;

            if (weightLerpCoroutine != null)
            {
                StopCoroutine(weightLerpCoroutine);
            }
            weightLerpCoroutine = StartCoroutine(AnimateRigWeightCoroutine(targetRigWeight));
        }

        private IEnumerator AnimateRigWeightCoroutine(float targetRigWeight)
        {
            float currentRigWeight = handWeight != null ? handWeight.weight : 0f;

            while (handWeight != null && Mathf.Abs(currentRigWeight - targetRigWeight) > 0.001f)
            {
                float dt = Time.deltaTime * handAnimationSpeed;
                currentRigWeight = Mathf.Lerp(currentRigWeight, targetRigWeight, dt);
                handWeight.weight = currentRigWeight;
                yield return null;
            }

            if (handWeight != null) handWeight.weight = targetRigWeight;
            weightLerpCoroutine = null;
        }

        #endregion
    }
}
