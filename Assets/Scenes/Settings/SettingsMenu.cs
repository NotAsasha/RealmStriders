using FileSystem.Scripts;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Scenes.Settings
{
    public class SettingsMenu : MonoBehaviour
    {
        [FormerlySerializedAs("_sensSlider")] [SerializeField] private Slider sensSlider;

        private SettingsFile settingsFile;
        private GameFileHandler fileHandler;

        private void Start()
        {
            fileHandler = GameFileHandler.Instance;
            settingsFile = (SettingsFile)fileHandler.SearchForFileByName("Settings");

            sensSlider.value = settingsFile.save.sensValue;
        }

        public void UpdateMouseSensativity()
        {
            settingsFile.save.sensValue = sensSlider.value;
            settingsFile.Save(false);
        }

        
    }
}
