using UnityEngine;
using Unity.Netcode;
using NUnit.Framework;
using System.Collections.Generic;
public class Shop : NetworkBehaviour
{
    [SerializeField] Vector3 spawnPosition;
    [SerializeField] List<ItemCard> cards;

    [ServerRpc(RequireOwnership = false)]
    public void BuyItemServerRpc(string _name, int _price)
    {
        if (GameManager.instance.teamMoney.Value < _price) return;

        NetworkObject item = cards.Find(c => c.itemName == _name).itemPrefab;
        if (item == null) return;

        GameManager.instance.teamMoney.Value -= _price;
        item.InstantiateAndSpawn(NetworkManager.Singleton, 0, false, false, false, spawnPosition);
        Debug.Log($"---Shop: Item bought: {item.name}");
    }
}
