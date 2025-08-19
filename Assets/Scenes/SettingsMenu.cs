using FileSystem;
using UnityEngine;
using UnityEngine.UI;
namespace Menu
{
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] private Slider _sensSlider;

        private SettingsFile settingsFile;
        private GameFileHandler _fileHandler;

        private void Start()
        {
            _fileHandler = GameFileHandler.Instance;
            settingsFile = (SettingsFile)_fileHandler.SearchForFileByName("Settings");

            _sensSlider.value = settingsFile.save._sensValue;
        }

        public void UpdateMouseSensativity()
        {
            settingsFile.save._sensValue = _sensSlider.value;
            settingsFile.Save(false);
        }

        
    }
}
