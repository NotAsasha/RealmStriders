using FileSystem.Scripts;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Scenes.Settings
{
    public class SettingsMenu : MonoBehaviour
    {
        [FormerlySerializedAs("_sensSlider")] [SerializeField] private Slider sensSlider;

        public UnityEngine.Events.UnityEvent OnClose;

        private SettingsFile settingsFile;
        private GameFileHandler fileHandler;

        private void Start()
        {
            fileHandler = GameFileHandler.Instance;
            settingsFile = (SettingsFile)fileHandler.SearchForFileByName("Settings");

            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (settingsFile != null && sensSlider != null)
                sensSlider.value = settingsFile.save.sensValue;
        }

        public void UpdateMouseSensitivity()
        {
            settingsFile.save.sensValue = sensSlider.value;
            settingsFile.Save(false);
        }

        public void Close()
        {
            OnClose?.Invoke();
        }
    }
}
