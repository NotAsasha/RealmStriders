using Unity.Netcode;
using UnityEngine;

namespace Base.BaseUpgrader
{
    public class Base : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkObject radarTerminal;
        [SerializeField] private NetworkObject radarButton;
        [SerializeField] private GameObject casinoWall;

        [Header("Camera & Layers")]
        [Tooltip("Layer for detecting enemies.")]
        [SerializeField] private LayerMask radarOnlyLayer; 
        private Camera radarCamera;

        [Header("Prices")]
        [SerializeField] private int terminalPrice = 200;
        [SerializeField] private int detectionPrice = 300;
        [SerializeField] private int beamPrice = 400;
        [SerializeField] private int casinoPrice = 300;

        public readonly NetworkVariable<bool> isTerminalBought =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> isDetectionBought =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> isBeamBought =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> isCasinoBought =
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

            var radar = radarTerminal.GetComponent<Radar.Radar>();
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
            isCasinoBought.OnValueChanged += OnCasinoBoughtChanged;

            OnTerminalBoughtChanged(false, isTerminalBought.Value);
            OnDetectionBoughtChanged(false, isDetectionBought.Value);
            OnBeamBoughtChanged(false, isBeamBought.Value);
            OnCasinoBoughtChanged(false, isCasinoBought.Value);
        }

        public override void OnNetworkDespawn()
        {
            isTerminalBought.OnValueChanged -= OnTerminalBoughtChanged;
            isDetectionBought.OnValueChanged -= OnDetectionBoughtChanged;
            isBeamBought.OnValueChanged -= OnBeamBoughtChanged;
            isCasinoBought.OnValueChanged -= OnCasinoBoughtChanged;
        }

        #endregion

        #region --Handlers--

        private void OnTerminalBoughtChanged(bool _, bool newV)
        {
            radarTerminal.gameObject.SetActive(newV);
            if (newV) radarTerminal.Spawn();
        }

        private void OnBeamBoughtChanged(bool _, bool newV)
        {
            radarButton.gameObject.SetActive(newV);
            if (newV) radarButton.Spawn();
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


        private void OnCasinoBoughtChanged(bool _, bool newV)
        {
            casinoWall.SetActive(!newV);
        }

        #endregion




        //
        // NEEDS TO BE REFACTORED, terrible code, TODO
        //

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BuyTerminalServerRpc()
        {
            if (isTerminalBought.Value) return;
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.teamMoney.Value < terminalPrice) return;

            GameManager.Instance.teamMoney.Value -= terminalPrice;
            isTerminalBought.Value = true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BuyDetectionServerRpc()
        {
            //buy the previous one
            if (!isTerminalBought.Value) return;

            if (isDetectionBought.Value) return;
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.teamMoney.Value < detectionPrice) return;

            GameManager.Instance.teamMoney.Value -= detectionPrice;
            isDetectionBought.Value = true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BuyBeamServerRpc()
        {
            //buy the previous one
            if (!isDetectionBought.Value) return;

            if (isBeamBought.Value) return;
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.teamMoney.Value < beamPrice) return;

            GameManager.Instance.teamMoney.Value -= beamPrice;
            isBeamBought.Value = true;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BuyCasinoServerRpc()
        {
            if (isCasinoBought.Value) return;
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.teamMoney.Value < casinoPrice) return;

            GameManager.Instance.teamMoney.Value -= casinoPrice;
            isCasinoBought.Value = true;
        }
    }
}
