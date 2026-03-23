using Unity.Netcode;
using UnityEngine;

namespace Base.Shop
{
    public class ItemCard : MonoBehaviour
    {
        public string itemName;
        [SerializeField] int itemPrice;
        public NetworkObject itemPrefab;

        Shop shop;

        private void OnEnable()
        {
            shop = GetComponentInParent<Shop>();
        }
        public void BuyItem()
        {
            shop.BuyItemServerRpc(itemName, itemPrice);
        }
    }
}
