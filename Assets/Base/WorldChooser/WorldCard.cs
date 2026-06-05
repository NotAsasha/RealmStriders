using TMPro;
using Unity.Netcode;
using UnityEngine;
using NetString = Unity.Collections.FixedString64Bytes;
namespace Base.WorldChooser
{
    public class WorldCard : MonoBehaviour
    {
        [SerializeField] TMP_Text missionNameUI;
        [SerializeField] TMP_Text enemiesCountUI;
        [SerializeField] TMP_Text averageDangerUI;

        public NetString missionName = "World1";
        public int enemiesCount = 1;
        public float averageDanger = 1;

        private void Start()
        {
            UpdateUI();
        }
        private void UpdateUI()
        {
            missionNameUI.text = missionName.Value.ToString();
            enemiesCountUI.text = "Enemy number: " + enemiesCount;
            averageDangerUI.text = "Approximate danger: " + averageDanger;
        }
        public void SetMission()
        {
            SetMissionServerRpc(missionName.Value, enemiesCount, averageDanger);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetMissionServerRpc(NetString missionName, int enemiesCount, float averageDanger)
        {
            if (GameManager.Instance.hasStartedMission.Value) return;
            Debug.Log("ChangeGlobalMissionServerRpc");

            GameManager.Instance.missionName = missionName.ToString();
            GameManager.Instance.enemiesCount = enemiesCount;
            GameManager.Instance.averageDanger = averageDanger;
        }
    }
}
