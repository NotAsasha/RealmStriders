using UnityEngine;
using TMPro;
using Unity.Netcode;

namespace InventorySystem
{
    public class Dosimeter : Item
    {
        [SerializeField] TMP_Text danger;
        private GameManager gameManager = GameManager.instance;

        private bool isOn;

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            isOn = !isOn;
        }
        #endregion

        private void Update()
        {
            if (!isOn) return;
            danger.text = gameManager.missionDuration.ToString();
        }
    }
}