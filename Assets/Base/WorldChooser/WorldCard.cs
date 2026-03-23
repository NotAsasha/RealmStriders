using TMPro;
using Unity.Netcode;
using UnityEngine;
using NetString = Unity.Collections.FixedString64Bytes;
namespace Base.WorldChooser
{
    public class WorldCard : NetworkBehaviour
    {
        [SerializeField] TMP_Text missionNameUI;
        [SerializeField] TMP_Text enemiesCountUI;
        [SerializeField] TMP_Text averageDangerUI;

        public NetworkVariable<NetString> missionName = new("World1");
        public NetworkVariable<int> enemiesCount = new(1);
        public NetworkVariable<float> averageDanger = new(1);

        public override void OnNetworkSpawn()
        {
            if (missionNameUI == null || enemiesCountUI == null || averageDangerUI == null)
            {
                Debug.LogError($"WorldCard on {gameObject.name} has nulls...");
                return;
            }

            missionName.OnValueChanged += (oldV, newV) => missionNameUI.text = newV.ToString();
            enemiesCount.OnValueChanged += (oldV, newV) => enemiesCountUI.text = "Enemy number: " + newV;
            averageDanger.OnValueChanged += (oldV, newV) => averageDangerUI.text = "Approximate danger: " + newV;

            UpdateUI();
        }

        private void UpdateUI()
        {
            missionNameUI.text = missionName.Value.ToString();
            enemiesCountUI.text = "Enemy number: " + enemiesCount.Value;
            averageDangerUI.text = "Approximate danger: " + averageDanger.Value;
        }
        public void SetMission()
        {
            SetMissionServerRpc(missionName.Value, enemiesCount.Value, averageDanger.Value);
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
