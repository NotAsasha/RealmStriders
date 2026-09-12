using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Base.WorldChooser
{
    public class WorldCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionNameUI;
        [SerializeField] private TMP_Text enemiesCountUI;
        [SerializeField] private TMP_Text averageDangerUI;

        private WorldChooser worldChooser;
        private FixedString64Bytes missionName;
        private int enemiesCount;
        private float averageDanger;

        public void Setup(WorldChooser parent, FixedString64Bytes targetMissionName, int targetEnemiesCount, float targetAverageDanger)
        {
            worldChooser = parent;
            missionName = targetMissionName;
            enemiesCount = targetEnemiesCount;
            averageDanger = targetAverageDanger;

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (missionNameUI != null)
                missionNameUI.text = missionName.ToString();

            if (enemiesCountUI != null)
                enemiesCountUI.text = $"Enemies: {enemiesCount}";

            if (averageDangerUI != null)
                averageDangerUI.text = $"Danger: {averageDanger:F1}";
        }

        public void SetMission()
        {
            if (worldChooser != null)
            {
                worldChooser.SetMissionServerRpc(missionName, enemiesCount, averageDanger);
            }
            else
            {
                Debug.LogError("[WorldCard] Reference to WorldChooser is null.");
            }
        }
    }
}