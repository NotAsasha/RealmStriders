using System.Collections;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Base.Interactables
{
    public class Button : NetworkBehaviour, IInteractable
    {
        [SerializeField] private float cooldown = 0f;
        [SerializeField] private UnityEvent onInteract;

        [SerializeField] private Transform slider;

        public NetworkVariable<bool> isReady = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private Coroutine sliderAnimationCoroutine;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            isReady.OnValueChanged += AnimateCooldown;

            // Синхронізуємо візуальний стан повзунка при підключенні
            UpdateSliderVisual(isReady.Value ? 0f : 1f);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            isReady.OnValueChanged -= AnimateCooldown;
        }

        public bool IsSingleUse() => true;

        public void Interact(GameObject player)
        {
            if (!isReady.Value) return;

            if (cooldown > 0f)
            {
                StartCooldownServerRpc();
            }

            onInteract.Invoke();
        }

        public void StopInteraction()
        {
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void StartCooldownServerRpc()
        {
            if (!isReady.Value) return;
            StartCoroutine(Cooldown());
        }

        private void AnimateCooldown(bool previousValue, bool newValue)
        {
            if (slider == null) return;

            if (sliderAnimationCoroutine != null)
            {
                StopCoroutine(sliderAnimationCoroutine);
            }

            // Коли isReady стає false — запускаємо спадання повзунка з 1 до 0
            if (!newValue)
            {
                sliderAnimationCoroutine = StartCoroutine(AnimateSliderCoroutine());
            }
            else
            {
                // Повертаємо у вихідне положення після завершення кулдауну
                UpdateSliderVisual(0f);
            }
        }

        private IEnumerator AnimateSliderCoroutine()
        {
            float elapsed = 0f;

            while (elapsed < cooldown)
            {
                elapsed += Time.deltaTime;
                // Нормалізований прогрес від 1 до 0
                float normalizedProgress = Mathf.Clamp01(1f - (elapsed / cooldown));
                UpdateSliderVisual(normalizedProgress);
                yield return null;
            }

            UpdateSliderVisual(0f);
            sliderAnimationCoroutine = null;
        }

        private void UpdateSliderVisual(float scaleZ)
        {
            if (slider == null) return;

            Vector3 currentScale = slider.localScale;
            currentScale.z = scaleZ;
            slider.localScale = currentScale;
        }

        private IEnumerator Cooldown()
        {
            isReady.Value = false;
            yield return new WaitForSeconds(cooldown);
            isReady.Value = true;
        }
    }
}