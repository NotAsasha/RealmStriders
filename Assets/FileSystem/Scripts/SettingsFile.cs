using System;
using UnityEngine;

namespace FileSystem
{
    [CreateAssetMenu(fileName = "SettingsFile", menuName = "Not/SettingsFile")]
    public class SettingsFile : GameFile
    {
        [Header("SettingsFile Info")]
        public SettingsSave save = new(100);
        public override void ProcessData(string inputData)
        {
            if (string.IsNullOrWhiteSpace(inputData) || inputData.Length < 5)
            {
                save = new(100);
                Debug.LogWarning("InputData is empty, resetting to default save");
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
        public SettingsSave(int a) { _sensValue = a; }
        public string rebinds;
        public float _sensValue;
    }
}
