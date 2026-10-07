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
        private int missionIndex;

        public void Setup(WorldChooser parent, int targetMissionIndex, FixedString64Bytes targetMissionName, int targetEnemiesCount, float targetAverageDanger)
        {
            worldChooser = parent;
            missionIndex = targetMissionIndex;
            missionName = targetMissionName;
            UpdateUI(targetEnemiesCount, targetAverageDanger);
        }

        private void UpdateUI(int enemiesCount, float averageDanger)
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
                // Only the mission index is sent — the server resolves dangerLevels itself.
                worldChooser.SetMissionServerRpc(missionIndex);
            }
            else
            {
                Debug.LogError("[WorldCard] Reference to WorldChooser is null.");
            }
        }
    }
}