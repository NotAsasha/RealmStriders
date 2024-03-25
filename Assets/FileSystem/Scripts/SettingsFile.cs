using System;
using UnityEngine;

namespace FileSystem
{
    [CreateAssetMenu(fileName = "SettingsFile", menuName = "Not/SettingsFile")]
    public class SettingsFile : GameFile
    {
        [Header("SettingsFile Info")]
        public SettingsSave save = new();
        public override void ProcessData(string inputData)
        {
            if (inputData == null || inputData.Length == 0) { save = null; return; }
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
        public string rebinds;
        public float _sensValue;
    }
}
