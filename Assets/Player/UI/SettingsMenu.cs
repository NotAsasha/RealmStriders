using System;
using System.Collections.Generic;
using FileSystem.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Player.UI
{
    public class SettingsMenu : MonoBehaviour
    {
        [Header("Existing settings")]
        [SerializeField] private Slider sensSlider;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Graphics settings")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown windowModeDropdown;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private TMP_Dropdown frameRateDropdown;
        [SerializeField] private TMP_Dropdown shadowQualityDropdown;

        public UnityEngine.Events.UnityEvent OnClose;

        private SettingsFile settingsFile;
        private bool isInitialized;
        private readonly List<ResolutionOption> resolutionOptions = new();

        private static readonly int[] FrameRates = { 30, 60, 120, 144, 240, -1 };
        private static readonly string[] FrameRateLabels = { "30", "60", "120", "144", "240", "Unlimited" };
        private static readonly FullScreenMode[] FullScreenModes =
        {
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed,
            FullScreenMode.ExclusiveFullScreen
        };
        private static readonly string[] FullScreenModeLabels = { "Borderless", "Windowed", "Fullscreen" };
        private static readonly ShadowQuality[] ShadowQualities =
        {
            ShadowQuality.Disable,
            ShadowQuality.HardOnly,
            ShadowQuality.All
        };
        private static readonly string[] ShadowQualityLabels = { "Disabled", "Hard Only", "All" };

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            settingsFile = (SettingsFile)GameFileHandler.Instance.SearchForFileByName("Settings");
            if (settingsFile == null)
            {
                Debug.LogError("Settings file was not found.");
                return;
            }

            BuildResolutionOptions();
            ConfigureGraphicsDropdowns();
            isInitialized = true;
            Refresh();
        }

        private void BuildResolutionOptions()
        {
            resolutionOptions.Clear();
            foreach (Resolution resolution in Screen.resolutions)
            {
                int refreshRate = Mathf.RoundToInt((float)resolution.refreshRateRatio.value);
                int existingIndex = resolutionOptions.FindIndex(option =>
                    option.Width == resolution.width && option.Height == resolution.height);

                if (existingIndex < 0)
                {
                    resolutionOptions.Add(new ResolutionOption(resolution.width, resolution.height, refreshRate));
                }
                else if (refreshRate > resolutionOptions[existingIndex].RefreshRate)
                {
                    resolutionOptions[existingIndex] = new ResolutionOption(
                        resolution.width, resolution.height, refreshRate);
                }
            }

            if (resolutionOptions.Count == 0)
                resolutionOptions.Add(new ResolutionOption(Screen.width, Screen.height, 60));
        }

        private void ConfigureGraphicsDropdowns()
        {
            SetOptions(qualityDropdown, QualitySettings.names);
            SetOptions(resolutionDropdown, GetResolutionLabels());
            SetOptions(windowModeDropdown, FullScreenModeLabels);
            SetOptions(frameRateDropdown, FrameRateLabels);
            SetOptions(shadowQualityDropdown, ShadowQualityLabels);
        }

        private static void SetOptions(TMP_Dropdown dropdown, IReadOnlyList<string> options)
        {
            if (dropdown == null) return;

            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
        }

        private string[] GetResolutionLabels()
        {
            string[] labels = new string[resolutionOptions.Count];
            for (int i = 0; i < resolutionOptions.Count; i++)
                labels[i] = resolutionOptions[i].Width + " x " + resolutionOptions[i].Height;
            return labels;
        }

        private void OnEnable()
        {
            if (isInitialized) Refresh();
        }

        private void OnDisable()
        {
            if (!isInitialized) return;

            settingsFile.Load();
            RevertInputBindings();
        }

        private void Refresh()
        {
            sensSlider.value = settingsFile.save.sensValue;

            float masterVolume = Mathf.Clamp01(settingsFile.save.masterVolume);
            settingsFile.save.masterVolume = masterVolume;
            masterVolumeSlider.value = masterVolume;
            AudioListener.volume = masterVolume;

            SelectGraphicsValues();
        }

        private void SelectGraphicsValues()
        {
            if (qualityDropdown != null)
            {
                int quality = settingsFile.save.qualityLevel >= 0 &&
                              settingsFile.save.qualityLevel < qualityDropdown.options.Count
                    ? settingsFile.save.qualityLevel
                    : QualitySettings.GetQualityLevel();
                qualityDropdown.SetValueWithoutNotify(quality);
            }

            resolutionDropdown?.SetValueWithoutNotify(
                FindResolutionIndex(settingsFile.save.resolutionWidth, settingsFile.save.resolutionHeight));
            windowModeDropdown?.SetValueWithoutNotify(
                FindIndex(FullScreenModes, (FullScreenMode)settingsFile.save.fullScreenMode));
            vSyncToggle?.SetIsOnWithoutNotify(settingsFile.save.vSync);
            frameRateDropdown?.SetValueWithoutNotify(FindIndex(FrameRates, settingsFile.save.frameRate));
            shadowQualityDropdown?.SetValueWithoutNotify(
                FindIndex(ShadowQualities, (ShadowQuality)settingsFile.save.shadowQuality));
        }

        public void UpdateMouseSensitivity()
        {
            settingsFile.save.sensValue = sensSlider.value;
        }

        public void UpdateMasterVolume()
        {
            settingsFile.save.masterVolume = Mathf.Clamp01(masterVolumeSlider.value);
            AudioListener.volume = settingsFile.save.masterVolume;
        }

        public void UpdateQuality(int value)
        {
            if (settingsFile == null) return;
            settingsFile.save.qualityLevel = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
        }

        public void UpdateResolution(int value)
        {
            if (settingsFile == null) return;
            if (!IsValidIndex(value, resolutionOptions.Count)) return;

            ResolutionOption resolution = resolutionOptions[value];
            settingsFile.save.resolutionWidth = resolution.Width;
            settingsFile.save.resolutionHeight = resolution.Height;
            settingsFile.save.refreshRate = resolution.RefreshRate;
        }

        public void UpdateWindowMode(int value)
        {
            if (settingsFile == null) return;
            if (!IsValidIndex(value, FullScreenModes.Length)) return;

            settingsFile.save.fullScreenMode = (int)FullScreenModes[value];
        }

        public void UpdateVSync(bool value)
        {
            if (settingsFile == null) return;
            settingsFile.save.vSync = value;
        }

        public void UpdateFrameRate(int value)
        {
            if (settingsFile == null) return;
            if (!IsValidIndex(value, FrameRates.Length)) return;

            settingsFile.save.frameRate = FrameRates[value];
        }

        public void UpdateShadowQuality(int value)
        {
            if (settingsFile == null) return;
            if (!IsValidIndex(value, ShadowQualities.Length)) return;

            settingsFile.save.shadowQuality = (int)ShadowQualities[value];
        }

        public void ApplySettings()
        {
            if (settingsFile == null)
            {
                Debug.LogError("Cannot apply settings because the settings file is not initialized.");
                return;
            }

            settingsFile.save.sensValue = sensSlider != null
                ? sensSlider.value
                : settingsFile.save.sensValue;
            settingsFile.save.masterVolume = masterVolumeSlider != null
                ? Mathf.Clamp01(masterVolumeSlider.value)
                : settingsFile.save.masterVolume;

            SyncGraphicsSettingsFromControls();
            settingsFile.ApplyRuntimeSettings();

            if (!settingsFile.Save())
                Debug.LogError("Failed to save settings.");
        }

        private void SyncGraphicsSettingsFromControls()
        {
            if (qualityDropdown != null && IsValidIndex(qualityDropdown.value, qualityDropdown.options.Count))
                settingsFile.save.qualityLevel = qualityDropdown.value;

            if (resolutionDropdown != null && IsValidIndex(resolutionDropdown.value, resolutionOptions.Count))
            {
                ResolutionOption resolution = resolutionOptions[resolutionDropdown.value];
                settingsFile.save.resolutionWidth = resolution.Width;
                settingsFile.save.resolutionHeight = resolution.Height;
                settingsFile.save.refreshRate = resolution.RefreshRate;
            }

            if (windowModeDropdown != null && IsValidIndex(windowModeDropdown.value, FullScreenModes.Length))
                settingsFile.save.fullScreenMode = (int)FullScreenModes[windowModeDropdown.value];

            if (vSyncToggle != null)
                settingsFile.save.vSync = vSyncToggle.isOn;

            if (frameRateDropdown != null && IsValidIndex(frameRateDropdown.value, FrameRates.Length))
                settingsFile.save.frameRate = FrameRates[frameRateDropdown.value];

            if (shadowQualityDropdown != null && IsValidIndex(shadowQualityDropdown.value, ShadowQualities.Length))
                settingsFile.save.shadowQuality = (int)ShadowQualities[shadowQualityDropdown.value];
        }

        public void Close()
        {
            OnClose?.Invoke();
        }

        private void RevertInputBindings()
        {
            if (inputActions == null) return;

            if (!string.IsNullOrEmpty(settingsFile.save.rebinds))
                inputActions.LoadBindingOverridesFromJson(settingsFile.save.rebinds);
            else
                inputActions.RemoveAllBindingOverrides();
        }

        private int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                if (resolutionOptions[i].Width == width && resolutionOptions[i].Height == height)
                    return i;
            }

            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                if (resolutionOptions[i].Width == Screen.width && resolutionOptions[i].Height == Screen.height)
                    return i;
            }

            return 0;
        }

        private static int FindIndex<T>(IReadOnlyList<T> values, T value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(values[i], value)) return i;
            }

            return 0;
        }

        private static bool IsValidIndex(int index, int count)
        {
            return index >= 0 && index < count;
        }

        private readonly struct ResolutionOption
        {
            public ResolutionOption(int width, int height, int refreshRate)
            {
                Width = width;
                Height = height;
                RefreshRate = refreshRate;
            }

            public int Width { get; }
            public int Height { get; }
            public int RefreshRate { get; }
        }
    }
}
