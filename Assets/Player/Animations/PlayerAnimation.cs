using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Player
{
    public class PlayerAnimation : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Rig handWeight;
        [SerializeField] private Inventory inventory;

        [Header("Settings")]
        [Tooltip("Швидкість підняття/опускання рук")]
        [SerializeField] private float handAnimationSpeed = 10f;
        [Tooltip("Множник швидкості аніматора (дільник реальної швидкості)")]
        [SerializeField] private float speedModifier = 0.25f;

        private Coroutine weightLerpCoroutine;

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
            if (inventory != null)
            {
                if (IsOwner)
                {
                    inventory.itemChanged += OnItemChanged;
                }
                else
                {
                    inventory.isHoldingItem.OnValueChanged += OnHoldingItemNetworkChanged;

                    ApplyHandWeight(inventory.isHoldingItem.Value);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (inventory != null)
            {
                if (IsOwner)
                {
                    inventory.itemChanged -= OnItemChanged;
                }
                else
                {
                    inventory.isHoldingItem.OnValueChanged -= OnHoldingItemNetworkChanged;
                }
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                float currentVelocity = 0f;
                if (characterController != null && characterController.enabled)
                {
                    currentVelocity = characterController.velocity.magnitude;
                }

                netSpeed.Value = currentVelocity;
            }

            UpdateAnimatorSpeed();
        }

        private void UpdateAnimatorSpeed()
        {
            if (animator == null) return;

            float targetAnimationSpeed = netSpeed.Value * speedModifier;
            animator.speed = Mathf.Lerp(animator.speed, targetAnimationSpeed, Time.deltaTime * 15f);
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
            float targetWeight = isHolding ? 1f : 0f;

            if (weightLerpCoroutine != null)
            {
                StopCoroutine(weightLerpCoroutine);
            }
            weightLerpCoroutine = StartCoroutine(AnimateRigWeightCoroutine(targetWeight));
        }

        private IEnumerator AnimateRigWeightCoroutine(float targetWeight)
        {
            while (!Mathf.Approximately(handWeight.weight, targetWeight))
            {
                handWeight.weight = Mathf.Lerp(handWeight.weight, targetWeight, Time.deltaTime * handAnimationSpeed);
                yield return null;
            }

            handWeight.weight = targetWeight;
            weightLerpCoroutine = null;
        }

        #endregion
    }
}