using UnityEngine;
using TMPro;
using Unity.Netcode;

namespace Player.InventorySystem
{
    public class Dosimeter : Item
    {
        [SerializeField] TMP_Text danger;
        private GameManager gameManager = GameManager.instance;

        NetworkVariable<bool> isOn = new(false, 0, 0);

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
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