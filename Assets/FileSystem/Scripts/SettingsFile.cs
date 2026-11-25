using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FileSystem.Scripts
{
    public enum CrouchMode
    {
        Hold = 0,
        Toggle = 1
    }

    public enum VoiceMode
    {
        ButtonHold = 0,
        VoiceActivation = 1
    }

    [CreateAssetMenu(fileName = "SettingsFile", menuName = "Not/SettingsFile")]
    public class SettingsFile : GameFile
    {
        private const int CurrentSettingsFormatVersion = 1;
        private JObject loadedSettings;

        public static event Action OnSettingsChanged;
        public static event Action OnGraphicsSettingsApplied;

        [Header("SettingsFile Info")]
        public SettingsSave save;
        protected override Action PrepareData(string inputData)
        {
            JObject json = ReadJsonObject(inputData);
            bool current = ReadFormatVersion(json, CurrentSettingsFormatVersion) > 0;
            bool hasSettings = HasNumber(json, nameof(SettingsSave.sensValue), current);
            bool legacySensitivity = HasNumber(json, "_sensValue");
            hasSettings |= legacySensitivity;
            if (json[nameof(SettingsSave.rebinds)]?.Type != JTokenType.Null || current)
                hasSettings |= HasField(json, nameof(SettingsSave.rebinds), JTokenType.String, current);
            hasSettings |= HasNumber(json, nameof(SettingsSave.masterVolume), current);
            hasSettings |= HasField(json, nameof(SettingsSave.qualityLevel), JTokenType.Integer, current);
            hasSettings |= HasField(json, nameof(SettingsSave.resolutionWidth), JTokenType.Integer, current);
            hasSettings |= HasField(json, nameof(SettingsSave.resolutionHeight), JTokenType.Integer, current);
            hasSettings |= HasField(json, nameof(SettingsSave.refreshRate), JTokenType.Integer, current);
            hasSettings |= HasField(json, nameof(SettingsSave.fullScreenMode), JTokenType.Integer, current);
            hasSettings |= HasField(json, nameof(SettingsSave.vSync), JTokenType.Boolean, current);
            hasSettings |= HasField(json, nameof(SettingsSave.frameRate), JTokenType.Integer, current);
            hasSettings |= HasField(json, nameof(SettingsSave.shadowQuality), JTokenType.Integer, current);
            hasSettings |= HasField(json, nameof(SettingsSave.voiceMode), JTokenType.Integer, current);
            hasSettings |= HasField(json, nameof(SettingsSave.crouchMode), JTokenType.Integer, current);
            if (!hasSettings) throw new InvalidDataException("The file contains no settings.");

            SettingsSave loaded = json.ToObject<SettingsSave>(SaveSerializer);
            if (legacySensitivity && json[nameof(SettingsSave.sensValue)] == null)
                loaded.sensValue = json["_sensValue"].Value<float>();

            loaded.rebinds ??= "";
            if (!string.IsNullOrWhiteSpace(loaded.rebinds))
                HasField(ReadJsonObject(loaded.rebinds), "bindings", JTokenType.Array, true);

            if (loaded.sensValue < 0 || loaded.masterVolume < 0 || loaded.masterVolume > 1 ||
                loaded.qualityLevel < -1 || loaded.resolutionWidth < 0 || loaded.resolutionHeight < 0 ||
                (loaded.resolutionWidth == 0) != (loaded.resolutionHeight == 0) || loaded.refreshRate < 0 ||
                loaded.frameRate < -1 || loaded.frameRate == 0 ||
                !Enum.IsDefined(typeof(FullScreenMode), loaded.fullScreenMode) ||
                !Enum.IsDefined(typeof(ShadowQuality), loaded.shadowQuality) ||
                !Enum.IsDefined(typeof(VoiceMode), loaded.voiceMode) ||
                !Enum.IsDefined(typeof(CrouchMode), loaded.crouchMode))
            {
                throw new InvalidDataException("A settings value is outside its supported range.");
            }

            return () => ApplyLoadedSettings(loaded, json);
        }

        protected override void ResetData() => ApplyLoadedSettings(new SettingsSave(), null);

        private void ApplyLoadedSettings(SettingsSave loaded, JObject source)
        {
            save = loaded;
            loadedSettings = source;
            ApplyRuntimeSettings();
            OnSettingsChanged?.Invoke();
        }

        public override string GetData()
        {
            JObject json = loadedSettings != null ? (JObject)loadedSettings.DeepClone() : new JObject();
            json.Merge(JObject.FromObject(save, SaveSerializer));
            json["formatVersion"] = CurrentSettingsFormatVersion;
            return json.ToString(Formatting.None);
        }

        protected override void OnSaved(Action applyData) => OnSettingsChanged?.Invoke();

        public void ApplyRuntimeSettings()
        {
            AudioListener.volume = Mathf.Clamp01(save.masterVolume);

            if (save.qualityLevel >= 0 && save.qualityLevel < QualitySettings.names.Length &&
                save.qualityLevel != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(save.qualityLevel, true);

            QualitySettings.shadows = (ShadowQuality)Mathf.Clamp(save.shadowQuality, 0, 2);
            QualitySettings.vSyncCount = save.vSync ? 1 : 0;
            Application.targetFrameRate = save.vSync ? -1 : save.frameRate;

            if (save.resolutionWidth > 0 && save.resolutionHeight > 0)
            {
                FullScreenMode mode = (FullScreenMode)Mathf.Clamp(save.fullScreenMode, 0, 3);
                RefreshRate refreshRate = new()
                {
                    numerator = (uint)Mathf.Max(1, save.refreshRate),
                    denominator = 1
                };
                Screen.SetResolution(save.resolutionWidth, save.resolutionHeight, mode, refreshRate);
            }

            OnGraphicsSettingsApplied?.Invoke();
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
        public VoiceMode voiceMode = VoiceMode.ButtonHold;
        public CrouchMode crouchMode = CrouchMode.Hold;
    }
}
