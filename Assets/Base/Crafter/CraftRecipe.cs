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
                networkObject == null || networkObject.PrefabIdHash == 0)
                return false;

            prefabHash = networkObject.PrefabIdHash;
            return true;
        }

        private static string GetItemName(Item item)
        {
            if (item == null)
                return "???";

            return item.name.Replace("(Clone)", string.Empty).Trim();
        }
    }
}
