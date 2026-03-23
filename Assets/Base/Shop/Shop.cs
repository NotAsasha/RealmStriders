using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Base.Shop
{
    public class Shop : NetworkBehaviour
    {
        [SerializeField] Vector3 spawnPosition;
        
        [SerializeField] List<ItemCard> cards;


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BuyItemServerRpc(string name, int price)
        {
            if (GameManager.Instance.teamMoney.Value < price) return;

            NetworkObject item = cards.Find(c => c.itemName == name).itemPrefab;
            if (item == null) return;

            GameManager.Instance.teamMoney.Value -= price;
            item.InstantiateAndSpawn(NetworkManager.Singleton, 0, false, false, false, spawnPosition);
            Debug.Log($"---Shop: Item bought: {item.name}");
        }
    }
}
