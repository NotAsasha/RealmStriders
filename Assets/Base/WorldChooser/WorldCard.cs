using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Base.WorldChooser
{
    public class WorldCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionNameUI;
        [SerializeField] private TMP_Text enemiesCountUI;
        [SerializeField] private TMP_Text averageDangerUI;
        [SerializeField] private Image background;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private TMP_Text selectionLabel;
        [SerializeField] private Color normalColor = new(0.10f, 0.10f, 0.12f, 1f);
        [SerializeField] private Color selectedColor = new(0.23f, 0.24f, 0.27f, 1f);

        private WorldChooser worldChooser;
        private FixedString64Bytes missionName;
        private int missionIndex;

        public void RefreshSelection(int selectedIndex)
        {
            bool selected = missionIndex == selectedIndex;
            background.color = selected ? selectedColor : normalColor;
            selectedIndicator.SetActive(selected);
            selectionLabel.text = selected ? "SELECTED" : "CHOOSE";
        }

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
