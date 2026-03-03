using UnityEngine;
using Unity.Netcode;
using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
public class Shop : NetworkBehaviour
{
    [SerializeField] Vector3 spawnPosition;
    [SerializeField] TMP_Text moneyCounter;
    [SerializeField] List<ItemCard> cards;

    public override void OnNetworkSpawn()
    {
        GameManager.instance.teamMoney.OnValueChanged += UpdateUI;

        moneyCounter.text = GameManager.instance.teamMoney.Value.ToString();
    }

    private void UpdateUI(int oldV, int newV)
    {
        moneyCounter.text = newV.ToString();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
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
