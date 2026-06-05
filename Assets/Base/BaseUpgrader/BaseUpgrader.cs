using Base;
using Base.BaseUpgrader;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;

public class BaseUpgrader : Terminal
{
    [Header("Prices")]
    [SerializeField] private int terminalPrice = 200;
    [SerializeField] private int detectionPrice = 300;
    [SerializeField] private int beamPrice = 400;
    [SerializeField] private int casinoPrice = 300;

    private BaseManager baseObj;

    public override void OnNetworkSpawn()
    {
        baseObj = BaseManager.Instance;
    }


    //
    // NEEDS TO BE REFACTORED, terrible code, TODO -- fixed
    //
    // EDIT: now it`s better, but can still be done via dictionary, or whatever
    // EDIT 2: now it`s now that bad..

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyTerminalServerRpc()
    {
        BuyUpgrade(BaseUpgrades.IsTerminalBought, terminalPrice);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyDetectionServerRpc()
    {
        //buy the previous one
        if ((baseObj.baseUpgrades.Value & (int)BaseUpgrades.IsTerminalBought) == 0) return;

        BuyUpgrade(BaseUpgrades.IsDetectionBought, detectionPrice);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyBeamServerRpc()
    {
        //buy the previous one
        if ((baseObj.baseUpgrades.Value & (int)BaseUpgrades.IsDetectionBought) == 0) return;

        BuyUpgrade(BaseUpgrades.IsBeamBought, beamPrice);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyCasinoServerRpc()
    {
        BuyUpgrade(BaseUpgrades.IsCasinoBought, casinoPrice);
    }

    private void BuyUpgrade(BaseUpgrades toBuy, int price)
    {
        if ((baseObj.baseUpgrades.Value & (int)toBuy) != 0) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.teamMoney.Value < price) return;

        GameManager.Instance.teamMoney.Value -= price;
        baseObj.baseUpgrades.Value |= (int)toBuy;
    }
}
