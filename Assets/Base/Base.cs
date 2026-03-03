using UnityEngine;
using Unity.Netcode;

public class Base : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkObject radarTerminal;
    [SerializeField] private NetworkObject radarButton;

    [Header("Camera & Layers")]
    [Tooltip("Layer for detecting enemies.")]
    [SerializeField] private LayerMask radarOnlyLayer; 
    private Camera radarCamera;

    [Header("Prices")]
    [SerializeField] private int terminalPrice = 200;
    [SerializeField] private int detectionPrice = 300;
    [SerializeField] private int beamPrice = 400;

    public readonly NetworkVariable<bool> isTerminalBought =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<bool> isDetectionBought =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<bool> isBeamBought =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #region Unity Lifecycle

    public override void OnNetworkSpawn()
    {
        if (!radarTerminal)
        {
            Debug.LogError("[Base] radarTerminal is not assigned.", this);
            return;
        }
        if (!radarButton)
        {
            Debug.LogWarning("[Base] radarButton is not assigned.", this);
        }

        var radar = radarTerminal.GetComponent<Radar>();
        if (!radar)
        {
            Debug.LogError("[Base] Radar component not found on radarTerminal.", radarTerminal);
            return;
        }
        radarCamera = radar.radarCamera;
        if (!radarCamera && IsClient)
        {
            Debug.LogError("[Base] radarCamera is missing on Radar.", radar);
        }

        isTerminalBought.OnValueChanged += OnTerminalBoughtChanged;
        isDetectionBought.OnValueChanged += OnDetectionBoughtChanged;
        isBeamBought.OnValueChanged += OnBeamBoughtChanged;

        OnTerminalBoughtChanged(false, isTerminalBought.Value);
        OnDetectionBoughtChanged(false, isDetectionBought.Value);
        OnBeamBoughtChanged(false, isBeamBought.Value);
    }

    public override void OnNetworkDespawn()
    {
        isTerminalBought.OnValueChanged -= OnTerminalBoughtChanged;
        isDetectionBought.OnValueChanged -= OnDetectionBoughtChanged;
        isBeamBought.OnValueChanged -= OnBeamBoughtChanged;
    }

    #endregion

    #region --Handlers--

    private void OnTerminalBoughtChanged(bool _, bool newV)
    {
        if (radarTerminal)
            radarTerminal.gameObject.SetActive(newV);
    }

    private void OnBeamBoughtChanged(bool _, bool newV)
    {
        if (radarButton)
            radarButton.gameObject.SetActive(newV);
    }

    private void OnDetectionBoughtChanged(bool _, bool newV)
    {
        if (!IsClient || !radarCamera) return;

        int mask = radarOnlyLayer.value;

        if (newV)
        {
            radarCamera.cullingMask |= mask;
        }
        else
        {
            radarCamera.cullingMask &= ~mask;
        }
    }

    #endregion

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyTerminalServerRpc()
    {
        if (isTerminalBought.Value) return;
        if (GameManager.instance == null) return;

        if (GameManager.instance.teamMoney.Value < terminalPrice) return;

        GameManager.instance.teamMoney.Value -= terminalPrice;
        isTerminalBought.Value = true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyDetectionServerRpc()
    {
        if (isDetectionBought.Value) return;
        if (GameManager.instance == null) return;

        if (GameManager.instance.teamMoney.Value < detectionPrice) return;

        GameManager.instance.teamMoney.Value -= detectionPrice;
        isDetectionBought.Value = true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyBeamServerRpc()
    {
        if (isBeamBought.Value) return;
        if (GameManager.instance == null) return;

        if (GameManager.instance.teamMoney.Value < beamPrice) return;

        GameManager.instance.teamMoney.Value -= beamPrice;
        isBeamBought.Value = true;
    }
}
