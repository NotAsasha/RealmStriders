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
                    teamRating = GameManager.Instance.teamRating.Value,
                    lossRating = GameManager.Instance.lossRating.Value,
                    teamMoney = GameManager.Instance.teamMoney.Value,
                    objects = NetworkItemsHandler.Instance.GetSaveablesInfo(),
                    baseUpgrades = BaseManager.Instance.baseUpgrades.Value
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

            NetworkItemsHandler.Instance.LoadSaveables(save.objects);
        }



        [Serializable]
        public class SaveData
        {
            public int teamRating = 3;
            public int lossRating;
            public int teamMoney = 5600;

            public List<ObjectEntry> objects = new List<ObjectEntry>();

            public int baseUpgrades;
        }
    }
}