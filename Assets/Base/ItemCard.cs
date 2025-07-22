using Unity.Netcode;
using UnityEngine;

public class ItemCard : MonoBehaviour
{
    [SerializeField] string itemName;
    [SerializeField] int itemPrice;
    [SerializeField] NetworkObject itemPrefab;

    Shop shop;

    private void OnEnable()
    {
        shop = GetComponentInParent<Shop>();
    }
    public void BuyItem()
    {
        shop.BuyItem(itemPrefab, itemPrice);
    }
}
