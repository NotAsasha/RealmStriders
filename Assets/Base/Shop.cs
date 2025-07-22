using UnityEngine;
using Unity.Netcode;
public class Shop : NetworkBehaviour
{
    [SerializeField] Vector3 spawnPosition;
    public void BuyItem(NetworkObject _item, int _price)
    {
        if (GameManager.instance.teamMoney.Value < _price) return;

        GameManager.instance.teamMoney.Value -= _price;
        _item.InstantiateAndSpawn(NetworkManager.Singleton, 0, false, false, false, spawnPosition);
        Debug.Log($"---Shop: Item bought: {_item.name}");

    }
}
