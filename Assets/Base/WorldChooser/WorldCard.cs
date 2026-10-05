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

        public void Setup(WorldChooser parent, FixedString64Bytes targetMissionName, int targetEnemiesCount, float targetAverageDanger)
        {
            worldChooser = parent;
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
                // Only the mission name is sent — the server resolves dangerLevels itself.
                worldChooser.SetMissionServerRpc(missionName);
            }
            else
            {
                Debug.LogError("[WorldCard] Reference to WorldChooser is null.");
            }
        }
    }
}