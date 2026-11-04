using System;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace Base.Crafting
{
    [Serializable]
    public sealed class CraftRecipe
    {
        [SerializeField] private Item firstInput;
        [SerializeField] private Item secondInput;
        [SerializeField] private Item output;
        [SerializeField, TextArea(2, 4)] private string craftText;

        public Item FirstInput => firstInput;
        public Item SecondInput => secondInput;
        public Item Output => output;

        public string GetPreviewText()
        {
            if (!string.IsNullOrWhiteSpace(craftText))
                return craftText;

            return $"{GetItemName(firstInput)} + {GetItemName(secondInput)} = {GetItemName(output)}";
        }

        public bool Matches(Item first, Item second)
        {
            if (output == null ||
                !TryGetPrefabHash(output, out _) ||
                !TryGetPrefabHash(firstInput, out uint requiredFirstHash) ||
                !TryGetPrefabHash(secondInput, out uint requiredSecondHash) ||
                !TryGetPrefabHash(first, out uint firstHash) ||
                !TryGetPrefabHash(second, out uint secondHash))
                return false;

            return (firstHash == requiredFirstHash && secondHash == requiredSecondHash) ||
                   (firstHash == requiredSecondHash && secondHash == requiredFirstHash);
        }

        public bool Accepts(Item item)
        {
            if (!TryGetPrefabHash(item, out uint itemHash) ||
                !TryGetPrefabHash(firstInput, out uint firstHash) ||
                !TryGetPrefabHash(secondInput, out uint secondHash))
                return false;

            return itemHash == firstHash || itemHash == secondHash;
        }

        private static bool TryGetPrefabHash(Item item, out uint prefabHash)
        {
            prefabHash = 0;
            if (item == null || !item.TryGetComponent(out NetworkObject networkObject) ||
                networkObject == null)
                return false;

            // Scene objects have instance hashes; the save registry identifies their prefab.
            if (networkObject.IsSceneObject == true)
            {
                NetworkSaveables database = NetworkItemsHandler.Instance != null
                    ? NetworkItemsHandler.Instance.database
                    : null;
                if (database == null || database.prefabs == null ||
                    item.PrefabId < 0 || item.PrefabId >= database.prefabs.Count)
                    return false;

                networkObject = database.prefabs[item.PrefabId];
                if (networkObject == null)
                    return false;
            }

            prefabHash = networkObject.PrefabIdHash;
            return prefabHash != 0;
        }

        private static string GetItemName(Item item)
        {
            if (item == null)
                return "???";

            return item.name.Replace("(Clone)", string.Empty).Trim();
        }
    }
}
