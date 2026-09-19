using Base;
using Base.BaseUpgrader;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
namespace FileSystem.Scripts
{
    [CreateAssetMenu(fileName = "SaveFile", menuName = "Not/SaveFile")]
    public class SaveFile : GameFile
    {
        private const int CurrentSaveFormatVersion = 1;

        [Header("SaveFile Info")] public SaveData save;

        public override void ProcessData(string inputData)
        {
            Debug.Log("---SaveFile: ProcessData");

            if (string.IsNullOrWhiteSpace(inputData) || inputData.Length < 5)
            {
                Debug.LogWarning("---SaveFile: InputData is empty, resetting to default save");
                save = new();
                Save();
                return;
            }

            save = JsonUtility.FromJson<SaveData>(inputData);
        }


        public override string GetData()
        {
            Debug.Log("---SaveFile: GetData");

            if (NetworkItemsHandler.Instance != null && GameManager.Instance != null)
            {
                save = new SaveData
                {
                    formatVersion = CurrentSaveFormatVersion,
                    teamRating = GameManager.Instance.teamRating.Value,
                    lossRating = GameManager.Instance.lossRating.Value,
                    teamMoney = GameManager.Instance.teamMoney.Value,
                    objects = NetworkItemsHandler.Instance.GetSaveablesInfo(),
                    baseUpgrades = BaseManager.Instance.baseUpgrades.Value,
                    baseChargePercent = BaseManager.Instance.PowerGrid != null
                        ? BaseManager.Instance.PowerGrid.CurrentChargePercent.Value
                        : 100f
                };
            }
            return JsonUtility.ToJson(save);
        }


        public void LoadGameSave()
        {
            Debug.Log("SaveFile: ---LoadGameSave");
            GameManager.Instance.teamRating.Value = save.teamRating;
            GameManager.Instance.lossRating.Value = save.lossRating;
            GameManager.Instance.teamMoney.Value = save.teamMoney;
            BaseManager.Instance.baseUpgrades.Value = save.baseUpgrades;
            BaseManager.Instance.PowerGrid?.RestoreChargeServer(save.baseChargePercent);

            NetworkItemsHandler.Instance.LoadSaveables(save.objects, save.formatVersion < CurrentSaveFormatVersion);
        }



        [Serializable]
        public class SaveData
        {
            public int formatVersion;
            public int teamRating = 3;
            public int lossRating;
            public int teamMoney = 5600;

            public List<ObjectEntry> objects = new List<ObjectEntry>();

            public int baseUpgrades;
            public float baseChargePercent = 100f;
        }
    }
}