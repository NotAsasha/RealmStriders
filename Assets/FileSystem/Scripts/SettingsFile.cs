using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FileSystem.Scripts
{
    [CreateAssetMenu(fileName = "SettingsFile", menuName = "Not/SettingsFile")]
    public class SettingsFile : GameFile
    {
        [Header("SettingsFile Info")]
        public SettingsSave save;
        public override void ProcessData(string inputData)
        {
            if (string.IsNullOrWhiteSpace(inputData) || inputData.Length < 5)
            {
                Debug.LogWarning("InputData is empty, resetting to default save");
                save = new();
                Save(false);
                return;
            }

            Debug.Log("InputData:" + inputData + GetFullPath());
            save = JsonUtility.FromJson<SettingsSave>(inputData);
        }
        public override string GetData()
        {
            string jsonSave = JsonUtility.ToJson(save); 
            return jsonSave;
        }
    }
    [Serializable]
    public class SettingsSave
    {
        public string rebinds = "";
        [FormerlySerializedAs("_sensValue")] public float sensValue = 0.5f;
    }
}
