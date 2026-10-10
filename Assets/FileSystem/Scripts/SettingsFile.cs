using System;
using UnityEngine;

namespace FileSystem.Scripts
{
    [CreateAssetMenu(fileName = "SettingsFile", menuName = "Not/SettingsFile")]
    public class SettingsFile : GameFile
    {
        public static event Action OnSettingsChanged;

        [Header("SettingsFile Info")]
        public SettingsSave save;
        public override void ProcessData(string inputData)
        {
            if (string.IsNullOrWhiteSpace(inputData) || inputData.Length < 5)
            {
                Debug.LogWarning("InputData is empty, resetting to default save");
                save = new();
                ApplyRuntimeSettings();
                Save();
                return;
            }

            Debug.Log("InputData:" + inputData + GetFullPath());
            SettingsSave loadedSave = new();
            JsonUtility.FromJsonOverwrite(inputData, loadedSave);
            save = loadedSave;
            ApplyRuntimeSettings();
            OnSettingsChanged?.Invoke();
        }
        public override string GetData()
        {
            OnSettingsChanged?.Invoke();
            string jsonSave = JsonUtility.ToJson(save);
            return jsonSave;
        }

        public void ApplyRuntimeSettings()
        {
            AudioListener.volume = Mathf.Clamp01(save.masterVolume);

            if (save.qualityLevel >= 0 && save.qualityLevel < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel(save.qualityLevel, true);

            QualitySettings.shadows = (ShadowQuality)Mathf.Clamp(save.shadowQuality, 0, 2);
            QualitySettings.vSyncCount = save.vSync ? 1 : 0;
            Application.targetFrameRate = save.vSync ? -1 : save.frameRate;

            if (save.resolutionWidth <= 0 || save.resolutionHeight <= 0) return;

            FullScreenMode mode = (FullScreenMode)Mathf.Clamp(save.fullScreenMode, 0, 3);
            RefreshRate refreshRate = new()
            {
                numerator = (uint)Mathf.Max(1, save.refreshRate),
                denominator = 1
            };
            Screen.SetResolution(save.resolutionWidth, save.resolutionHeight, mode, refreshRate);
        }
    }
    [Serializable]
    public class SettingsSave
    {
        public string rebinds = "";
        public float sensValue = 0.5f;
        public float masterVolume = 1.0f;
        public int qualityLevel = -1;
        public int resolutionWidth;
        public int resolutionHeight;
        public int refreshRate;
        public int fullScreenMode = (int)FullScreenMode.FullScreenWindow;
        public bool vSync;
        public int frameRate = 144;
        public int shadowQuality = (int)ShadowQuality.All;
    }
}
