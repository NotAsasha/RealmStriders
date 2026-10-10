using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Dosimeter
{
    public class Dosimeter : Item
    {
        [SerializeField] private TMP_Text danger;
        [SerializeField] private Transform spinObj;
        [SerializeField, Min(0f)] private float spinSpeed = 360f;
        [SerializeField, Min(0f)] private float spinAcceleration = 720f;
        [SerializeField, Min(0f)] private float spinDeceleration = 900f;

        private readonly GameManager gameManager = GameManager.Instance;
        private float currentSpinSpeed;

        NetworkVariable<bool> isOn = new(false, 0, 0);

        #region Item Specific Functionality

        protected override void ExecuteItemAction(GameObject player)
        {
            SwitchStateServerRpc();
        }

        [ServerRpc]
        private void SwitchStateServerRpc()
        {
            isOn.Value = !isOn.Value;
        }
        #endregion

        private void Update()
        {
            UpdateSpinAnimation();

            if (isOn.Value)
            {
                danger.text = gameManager.missionDuration.ToString();
            }
        }

        private void UpdateSpinAnimation()
        {
            if (spinObj == null) return;

            float targetSpeed = isOn.Value ? spinSpeed : 0f;
            float speedChange = (isOn.Value ? spinAcceleration : spinDeceleration) * Time.deltaTime;
            currentSpinSpeed = Mathf.MoveTowards(currentSpinSpeed, targetSpeed, speedChange);

            if (currentSpinSpeed > 0f)
            {
                spinObj.Rotate(Vector3.right, currentSpinSpeed * Time.deltaTime, Space.Self);
            }
        }
    }
}