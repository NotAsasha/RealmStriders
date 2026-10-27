using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Base.Shop
{
    public class ItemCard : MonoBehaviour
    {
        public string itemName;
        [SerializeField] int itemPrice;
        public NetworkObject itemPrefab;
        [SerializeField] private int requiredRating;
        [SerializeField] private Shop shop;
        [SerializeField] private ShopItemPreview preview;
        [SerializeField] private GameObject previewModel;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private Button buyButton;
        [SerializeField] private GameObject catalog;

        public int Price => itemPrice;
        public int RequiredRating => requiredRating;

        private void OnEnable()
        {
            if (catalog != null) catalog.SetActive(false);
            if (preview != null) preview.Show(previewModel, previewImage);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.teamMoney.OnValueChanged += RefreshPurchaseState;
                GameManager.Instance.teamRating.OnValueChanged += RefreshPurchaseState;
            }
            RefreshPurchaseState(0, 0);
        }

        private void OnDisable()
        {
            if (catalog != null) catalog.SetActive(true);
            if (preview != null) preview.Hide(previewImage);
            if (GameManager.Instance == null) return;
            GameManager.Instance.teamMoney.OnValueChanged -= RefreshPurchaseState;
            GameManager.Instance.teamRating.OnValueChanged -= RefreshPurchaseState;
        }

        private void RefreshPurchaseState(int previous, int current)
        {
            if (buyButton != null) buyButton.interactable = GameManager.Instance != null &&
                GameManager.Instance.teamMoney.Value >= itemPrice &&
                GameManager.Instance.teamRating.Value >= requiredRating;
        }

        public void BuyItem()
        {
            if (shop == null || !shop.IsSpawned || !shop.IsClient) return;
            shop.BuyItemServerRpc(itemName, itemPrice);
        }
    }
}
