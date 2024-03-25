using FileSystem;
using UnityEngine;
using UnityEngine.UI;
namespace Menu
{
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] private Slider _sensSlider;

        private float _sensValue;
        private SettingsFile settingsFile;
        private GameFileHandler _fileHandler;
        private void Start()
        {
            // SteamMatchmaking.OnLobbyDataChanged += UpdateLobbyMembers;

            _fileHandler = GameFileHandler.Instance;
            settingsFile = (SettingsFile)_fileHandler.SearchForFileByName("Settings");
            _sensValue = settingsFile.save._sensValue;
            if (_sensValue == 0)
            {
                UpdateMouseSensativity();
            }
            _sensSlider.value = _sensValue;
        }

        public void UpdateMouseSensativity()
        {
            _sensValue = _sensSlider.value;
            settingsFile.save._sensValue = _sensValue;
            settingsFile.Save(false);
        }
    }
}
