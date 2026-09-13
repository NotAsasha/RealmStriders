using UnityEngine;

namespace Base.BaseUpgrader
{
    public class PowerChargeIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PowerGrid powerGrid;
        [SerializeField] private Transform arrow;

        [SerializeField] private AudioSource alarm;

        [Header("Rotation")]
        [SerializeField] private float minimumChargeRotationX;
        [SerializeField] private float maximumChargeRotationX;
        [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.2f;



        private bool isSubscribed;
        private float initialRotationY;
        private float initialRotationZ;
        private float targetRotationX;
        private float currentRotationX;
        private float rotationVelocity;

        private void Awake()
        {
            if (arrow == null) arrow = transform;

            Vector3 initialRotation = arrow.localEulerAngles;
            initialRotationY = initialRotation.y;
            initialRotationZ = initialRotation.z;
            currentRotationX = initialRotation.x;
            targetRotationX = currentRotationX;
        }

        private void OnEnable()
        {
            TryBindPowerGrid();
        }

        private void Update()
        {
            if (!isSubscribed)
            {
                TryBindPowerGrid();
            }

            if (arrow == null) return;

            currentRotationX = Mathf.SmoothDamp(
                currentRotationX,
                targetRotationX,
                ref rotationVelocity,
                rotationSmoothTime);

            arrow.localRotation = Quaternion.Euler(
                currentRotationX,
                initialRotationY,
                initialRotationZ);
        }

        private void OnDisable()
        {
            UnbindPowerGrid();
        }

        private void TryBindPowerGrid()
        {
            if (isSubscribed) return;

            if (powerGrid == null && BaseManager.Instance != null)
            {
                powerGrid = BaseManager.Instance.PowerGrid;
            }

            if (powerGrid == null) return;

            powerGrid.CurrentChargePercent.OnValueChanged += OnChargeChanged;
            isSubscribed = true;
            SetTargetRotation(powerGrid.CurrentChargePercent.Value);
        }

        private void UnbindPowerGrid()
        {
            if (!isSubscribed || powerGrid == null) return;

            powerGrid.CurrentChargePercent.OnValueChanged -= OnChargeChanged;
            isSubscribed = false;
        }

        private void OnChargeChanged(float _, float currentChargePercent)
        {
            SetTargetRotation(currentChargePercent);
            SetSound(currentChargePercent);
        }

        private void SetTargetRotation(float chargePercent)
        {
            if (arrow == null) return;

            float normalizedCharge = Mathf.InverseLerp(0f, 100f, chargePercent);
            targetRotationX = Mathf.Lerp(
                minimumChargeRotationX,
                maximumChargeRotationX,
                normalizedCharge);
        }

        private void SetSound(float chargePercent)
        {
            if (chargePercent < 0f)
            {
                alarm.Play();
            }
            else
            {
                alarm.Stop();
            }
        }
    }
}
