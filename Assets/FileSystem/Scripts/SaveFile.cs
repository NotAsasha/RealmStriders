using Base.BaseUpgrader;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FileSystem.Scripts
{
    [CreateAssetMenu(fileName = "SaveFile", menuName = "Not/SaveFile")]
    public class SaveFile : GameFile
    {
        private const int CurrentSaveFormatVersion = 2;

        [Header("SaveFile Info")] public SaveData save;

        protected override Action PrepareData(string inputData)
        {
            JObject json = ReadJsonObject(inputData);
            int version = ReadFormatVersion(json, CurrentSaveFormatVersion);
            HasField(json, nameof(SaveData.teamRating), JTokenType.Integer, true);
            HasField(json, nameof(SaveData.teamMoney), JTokenType.Integer, true);
            HasField(json, nameof(SaveData.baseUpgrades), JTokenType.Integer, true);
            HasField(json, nameof(SaveData.campaignCompleted), JTokenType.Boolean);
            bool hasLossRating = HasField(json, nameof(SaveData.lossRating), JTokenType.Integer, version > 0);
            bool hasLegacyLossRating = HasField(json, "looseRating", JTokenType.Integer);
            HasNumber(json, nameof(SaveData.baseChargePercent), version > 0);
            HasField(json, nameof(SaveData.objects), JTokenType.Array, true);

            foreach (JToken item in json[nameof(SaveData.objects)])
                ValidateObjectFields(item, version);

            if (HasField(json, nameof(SaveData.removedSceneFurniture), JTokenType.Array, version >= 2))
            {
                foreach (JToken item in json[nameof(SaveData.removedSceneFurniture)])
                {
                    if (item is not JObject removed)
                        throw new InvalidDataException("Expected an object for removed scene furniture.");
                    HasField(removed, nameof(SceneFurnitureEntry.prefabID), JTokenType.Integer, true);
                    HasField(removed, nameof(SceneFurnitureEntry.sceneObjectHash), JTokenType.Integer, true);
                }
            }

            SaveData loaded = json.ToObject<SaveData>(SaveSerializer);
            if (!hasLossRating && hasLegacyLossRating)
                loaded.lossRating = json["looseRating"].Value<int>();
            if (loaded.teamRating < 0 || loaded.teamMoney < 0 || loaded.baseUpgrades < 0 ||
                loaded.baseChargePercent > 100)
                throw new InvalidDataException("Campaign values are outside their supported range.");

            HashSet<uint> sceneObjects = new();
            foreach (ObjectEntry entry in loaded.objects)
            {
                if (entry.prefabID < 0 || entry.charge < 0)
                    throw new InvalidDataException("A saved item has invalid prefab or charge data.");

                if (version >= 2 && (entry.isSceneObject != (entry.sceneObjectHash != 0) ||
                                    (entry.isSceneObject && !sceneObjects.Add(entry.sceneObjectHash))))
                    throw new InvalidDataException("A saved item has an invalid or duplicate scene identity.");
            }

            foreach (SceneFurnitureEntry removed in loaded.removedSceneFurniture)
            {
                if (removed.prefabID < 0 || removed.sceneObjectHash == 0 || !sceneObjects.Add(removed.sceneObjectHash))
                    throw new InvalidDataException("A removed furniture entry has an invalid or duplicate scene identity.");
            }

            NetworkItemsHandler items = NetworkItemsHandler.Instance;
            if (items != null && items.IsServer && GameManager.Instance != null &&
                GameManager.Instance.currentSave == this &&
                !items.ValidateSavedPrefabs(loaded.objects, loaded.removedSceneFurniture))
                throw new InvalidDataException("A saved item references an invalid prefab.");

            return () => save = loaded;
        }

        private static void ValidateObjectFields(JToken item, int version)
        {
            if (item is not JObject entry)
                throw new InvalidDataException("Expected a saved item object.");
            HasField(entry, nameof(ObjectEntry.prefabID), JTokenType.Integer, true);
            HasField(entry, nameof(ObjectEntry.isSceneObject), JTokenType.Boolean, version > 0);
            HasField(entry, nameof(ObjectEntry.sceneObjectHash), JTokenType.Integer, version >= 2);
            HasField(entry, nameof(ObjectEntry.hasCharge), JTokenType.Boolean, version > 0);
            HasField(entry, nameof(ObjectEntry.charge), JTokenType.Integer, version > 0);
            HasField(entry, nameof(ObjectEntry.otherInfo), JTokenType.String);
            HasField(entry, nameof(ObjectEntry.position), JTokenType.Object, true);
            HasField(entry, nameof(ObjectEntry.rotation), JTokenType.Object, true);
            var position = (JObject)entry[nameof(ObjectEntry.position)];
            var rotation = (JObject)entry[nameof(ObjectEntry.rotation)];
            HasNumber(position, "x", true);
            HasNumber(position, "y", true);
            HasNumber(position, "z", true);
            HasNumber(rotation, "x", true);
            HasNumber(rotation, "y", true);
            HasNumber(rotation, "z", true);
            HasNumber(rotation, "w", true);

            Quaternion value = rotation.ToObject<Quaternion>(SaveSerializer);
            float magnitude = Quaternion.Dot(value, value);
            if (!float.IsFinite(magnitude) || magnitude < 0.000001f)
                throw new InvalidDataException("A saved item has an invalid rotation.");
        }

        protected override void ResetData() => save = new();
        protected override void OnSaved(Action applyData) => applyData();

        public override string GetData()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsServer || game.currentSave != this)
                return JsonUtility.ToJson(save);

            NetworkItemsHandler items = NetworkItemsHandler.Instance;
            BaseManager baseManager = BaseManager.Instance;
            if (items == null || !items.IsWorldRestored || baseManager == null)
                throw new InvalidOperationException("The host world has not finished restoring this save.");

            return JsonUtility.ToJson(new SaveData
            {
                formatVersion = CurrentSaveFormatVersion,
                teamRating = game.teamRating.Value,
                campaignCompleted = game.campaignCompleted.Value,
                lossRating = game.lossRating.Value,
                teamMoney = game.teamMoney.Value,
                objects = items.GetSaveablesInfo(),
                removedSceneFurniture = items.GetRemovedSceneFurniture(),
                baseUpgrades = baseManager.baseUpgrades.Value,
                baseChargePercent = baseManager.PowerGrid != null
                    ? baseManager.PowerGrid.CurrentChargePercent.Value
                    : 100f
            });
        }

        public bool LoadGameSave()
        {
            GameManager game = GameManager.Instance;
            BaseManager baseManager = BaseManager.Instance;
            NetworkItemsHandler items = NetworkItemsHandler.Instance;
            if (IsLoadBlocked || save == null || game == null || !game.IsServer || baseManager == null || items == null)
                return false;
            if (items.IsWorldRestored) return true;

            // When selected from the menu, prefab data could only be checked after the base loaded.
            if (!items.ValidateSavedPrefabs(save.objects, save.removedSceneFurniture))
            {
                Load();
                if (IsLoadBlocked) return false;
            }

            if (!items.LoadSaveables(save.objects,
                    save.formatVersion >= 2 ? save.removedSceneFurniture : null, save.formatVersion))
                return false;

            game.campaignCompleted.Value = save.campaignCompleted;
            game.teamRating.Value = save.teamRating;
            game.lossRating.Value = save.lossRating;
            game.teamMoney.Value = save.teamMoney;
            baseManager.baseUpgrades.Value = save.baseUpgrades;
            baseManager.PowerGrid?.RestoreChargeServer(save.baseChargePercent);
            return true;
        }

        [Serializable]
        public class SaveData
        {
            public int formatVersion;
            public int teamRating = 3;
            public bool campaignCompleted;
            public int lossRating;
            public int teamMoney = 300;

            public List<ObjectEntry> objects = new();
            public List<SceneFurnitureEntry> removedSceneFurniture = new();

            public int baseUpgrades;
            public float baseChargePercent = 100f;
        }
    }
}
