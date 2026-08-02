using TMPro;
using UnityEngine;
using NetString = Unity.Collections.FixedString64Bytes;

namespace Base.WorldChooser
{
    public class WorldCard : MonoBehaviour
    {
        [SerializeField] TMP_Text missionNameUI;
        [SerializeField] TMP_Text enemiesCountUI;
        [SerializeField] TMP_Text averageDangerUI;

        private WorldChooser worldChooser;

        private NetString missionName = "World1";
        private int enemiesCount = 1;
        private float averageDanger = 1;

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

        public void Setup(WorldChooser _parent, NetString _missionName, int _enemiesCount, float _averageDanger)
        {
            worldChooser = _parent;
            missionName = _missionName;
            enemiesCount = _enemiesCount;
            averageDanger = _averageDanger;
        }

        public void SetMission()
        {
            if (worldChooser != null)
            {
                worldChooser.SetMissionServerRpc(missionName, enemiesCount, averageDanger);
            }
            else
            {
                Debug.LogError("WorldChooser.Instance не знайдено на сцені!");
            }
        }
    }
}