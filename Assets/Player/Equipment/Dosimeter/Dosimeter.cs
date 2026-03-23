using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Dosimeter
{
    public class Dosimeter : Item
    {
        [SerializeField] private TMP_Text danger;
        private readonly GameManager gameManager = GameManager.Instance;

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
            if (!isOn.Value) return;
            danger.text = gameManager.missionDuration.ToString();
        }
    }
}