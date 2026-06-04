using FileSystem.Scripts;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Assets.Player.UI
{
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] private Slider sensSlider;
        [SerializeField] private InputActionAsset inputActions;

        public UnityEngine.Events.UnityEvent OnClose;

        private SettingsFile settingsFile;
        private bool isInitialized = false;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            settingsFile = (SettingsFile)GameFileHandler.Instance.SearchForFileByName("Settings");
            isInitialized = true;

            Refresh();
        }

        private void OnEnable()
        {
            if (!isInitialized) return;
            Refresh();
        }

        private void OnDisable()
        {
            if (!isInitialized) return;

            settingsFile.Load();
            RevertInputBindings();
        }

        private void Refresh()
        {
            if (settingsFile != null && sensSlider != null)
            {
                sensSlider.value = settingsFile.save.sensValue;
            }
        }

        private void RevertInputBindings()
        {
            if (inputActions == null) return;

            if (!string.IsNullOrEmpty(settingsFile.save.rebinds))
                inputActions.LoadBindingOverridesFromJson(settingsFile.save.rebinds);
            else
                inputActions.RemoveAllBindingOverrides();
        }

        public void UpdateMouseSensitivity()
        {
            if (settingsFile != null)
            {
                settingsFile.save.sensValue = sensSlider.value;
            }
        }

        public void ApplySettings()
        {
            settingsFile.Save();
        }

        public void Close()
        {
            OnClose?.Invoke();
        }
    }
}