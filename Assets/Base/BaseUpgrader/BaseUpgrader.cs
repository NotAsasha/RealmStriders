using Base;
using Base.BaseUpgrader;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class BaseUpgrader : NetworkBehaviour
{
    [Header("Prices")]
    [SerializeField] private int terminalPrice = 200;
    [SerializeField] private int detectionPrice = 300;
    [SerializeField] private int beamPrice = 400;
    [SerializeField] private int casinoPrice = 300;
    [SerializeField] private int shieldPrice = 750;
    [SerializeField] private int chargerPrice = 250;

    private BaseManager baseObj;
    private GameManager gameManager;

    [Serializable]
    private sealed class UpgradeCard
    {
        public BaseUpgrades upgrade;
        public Image background;
        public Image purchasedIndicator;
        public Button buyButton;
        public TMP_Text buttonLabel;
        public TMP_Text statusLabel;
        public TMP_Text priceLabel;
        public UpgradeOption hoverFeedback;
    }

    [Header("Menu presentation")]
    [SerializeField] private UpgradeCard[] upgradeCards = Array.Empty<UpgradeCard>();
    [SerializeField] private Color availableColor = new(.12f, .12f, .14f);
    [SerializeField] private Color purchasedColor = new(.24f, .25f, .28f);

    public override void OnNetworkSpawn()
    {
        baseObj = BaseManager.Instance;
        gameManager = GameManager.Instance;
        if (baseObj != null) baseObj.baseUpgrades.OnValueChanged += OnStateChanged;
        if (gameManager != null) gameManager.teamMoney.OnValueChanged += OnStateChanged;
        RefreshMenu();
    }

    public override void OnNetworkDespawn()
    {
        if (baseObj != null) baseObj.baseUpgrades.OnValueChanged -= OnStateChanged;
        if (gameManager != null) gameManager.teamMoney.OnValueChanged -= OnStateChanged;
        baseObj = null;
        gameManager = null;
        RefreshMenu();
    }

    private void OnStateChanged(int previous, int current) => RefreshMenu();

    private void RefreshMenu()
    {
        int owned = baseObj != null ? baseObj.baseUpgrades.Value : 0;
        int money = gameManager != null ? gameManager.teamMoney.Value : 0;
        foreach (UpgradeCard card in upgradeCards)
        {
            bool purchased = (owned & (int)card.upgrade) != 0;
            BaseUpgrades prerequisite = GetPrerequisite(card.upgrade);
            bool unlocked = prerequisite == BaseUpgrades.None || (owned & (int)prerequisite) != 0;
            int price = GetPrice(card.upgrade);
            card.background.color = purchased ? purchasedColor : availableColor;
            card.purchasedIndicator.enabled = purchased;
            card.buyButton.interactable = IsSpawned && baseObj != null && gameManager != null &&
                !purchased && unlocked && money >= price;
            var colors = card.buyButton.colors;
            colors.disabledColor = purchased ? purchasedColor : new Color(.075f, .075f, .09f);
            card.buyButton.colors = colors;
            card.buttonLabel.text = purchased ? "OWNED" : "BUY";
            card.priceLabel.text = price + " CR";
            card.hoverFeedback.SetStatus(!purchased && (!unlocked || money < price),
                purchased ? new Color(.922f, .914f, .878f) : new Color(.60f, .61f, .63f));
            card.statusLabel.text = purchased ? "PURCHASED" : !unlocked
                ? prerequisite == BaseUpgrades.IsTerminalBought ? "REQUIRES DRONE" : "REQUIRES RADAR"
                : money < price ? "NOT ENOUGH MONEY" : "AVAILABLE";
        }
    }

    private static BaseUpgrades GetPrerequisite(BaseUpgrades upgrade) => upgrade switch
    {
        BaseUpgrades.IsDetectionBought => BaseUpgrades.IsTerminalBought,
        BaseUpgrades.IsBeamBought => BaseUpgrades.IsDetectionBought,
        _ => BaseUpgrades.None
    };

    private int GetPrice(BaseUpgrades upgrade) => upgrade switch
    {
        BaseUpgrades.IsTerminalBought => terminalPrice,
        BaseUpgrades.IsDetectionBought => detectionPrice,
        BaseUpgrades.IsBeamBought => beamPrice,
        BaseUpgrades.IsCasinoBought => casinoPrice,
        BaseUpgrades.IsShieldBought => shieldPrice,
        BaseUpgrades.IsChargerBought => chargerPrice,
        _ => int.MaxValue
    };

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyTerminalServerRpc()
    {
        BuyUpgrade(BaseUpgrades.IsTerminalBought, terminalPrice);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyDetectionServerRpc()
    {
        BuyUpgrade(BaseUpgrades.IsDetectionBought, detectionPrice);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyBeamServerRpc()
    {
        BuyUpgrade(BaseUpgrades.IsBeamBought, beamPrice);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyCasinoServerRpc()
    {
        BuyUpgrade(BaseUpgrades.IsCasinoBought, casinoPrice);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyShieldServerRpc()
    {
        BuyUpgrade(BaseUpgrades.IsShieldBought, shieldPrice);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyChargerServerRpc()
    {
        BuyUpgrade(BaseUpgrades.IsChargerBought, chargerPrice);
    }

    private void BuyUpgrade(BaseUpgrades toBuy, int price)
    {
        if (!IsServer || baseObj == null || price < 0) return;
        BaseUpgrades prerequisite = GetPrerequisite(toBuy);
        if (prerequisite != BaseUpgrades.None && (baseObj.baseUpgrades.Value & (int)prerequisite) == 0) return;
        if ((baseObj.baseUpgrades.Value & (int)toBuy) != 0) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.teamMoney.Value < price) return;

        GameManager.Instance.teamMoney.Value -= price;
        baseObj.baseUpgrades.Value |= (int)toBuy;
    }
}
