using System;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

namespace Base.BaseUpgrader
{
    [Flags]
    public enum BaseUpgrades : int
    {
        None = 0,         // 0000
        IsTerminalBought = 1 << 0,    // 0001 (1)
        IsDetectionBought = 1 << 1,    // 0010 (2)
        IsBeamBought = 1 << 2,    // 0100 (4)
        IsCasinoBought = 1 << 3,// 1000 (8)
    }
    public class BaseManager : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkObject radarTerminal;
        [SerializeField] private NetworkObject radarButton;
        [SerializeField] private GameObject casinoWall;

        [Header("Camera & Layers")]
        [Tooltip("Layer for detecting enemies.")]
        [SerializeField] private LayerMask radarOnlyLayer; 
        private Camera radarCamera;

        public NetworkVariable<int> baseUpgrades = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public static BaseManager Instance;
        #region Unity Lifecycle

        private void Awake()
        {
            Instance = this;
            baseUpgrades = new();
        }

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

            baseUpgrades.OnValueChanged += OnTerminalBoughtChanged;
            baseUpgrades.OnValueChanged += OnDetectionBoughtChanged;
            baseUpgrades.OnValueChanged += OnBeamBoughtChanged;
            baseUpgrades.OnValueChanged += OnCasinoBoughtChanged;

            OnTerminalBoughtChanged(0, baseUpgrades.Value);
            OnDetectionBoughtChanged(0, baseUpgrades.Value);
            OnBeamBoughtChanged(0, baseUpgrades.Value);
            OnCasinoBoughtChanged(0, baseUpgrades.Value);
        }

        public override void OnNetworkDespawn()
        {
            baseUpgrades.OnValueChanged -= OnTerminalBoughtChanged;
            baseUpgrades.OnValueChanged -= OnDetectionBoughtChanged;
            baseUpgrades.OnValueChanged -= OnBeamBoughtChanged;
            baseUpgrades.OnValueChanged -= OnCasinoBoughtChanged;
        }

        #endregion

        #region --Handlers--

        private void OnTerminalBoughtChanged(int _, int current)
        {
            bool isBought = (current & (int)BaseUpgrades.IsTerminalBought) != 0;

            radarTerminal.gameObject.SetActive(isBought);

            if (IsServer && isBought && !radarTerminal.IsSpawned)
            {
                radarTerminal.Spawn();
            }
        }

        private void OnBeamBoughtChanged(int _, int current)
        {
            bool isBought = (current & (int)BaseUpgrades.IsBeamBought) != 0;

            radarButton.gameObject.SetActive(isBought);

            if (IsServer && isBought && !radarButton.IsSpawned)
            {
                radarButton.Spawn();
            }
        }

        private void OnDetectionBoughtChanged(int _, int current)
        {
            if (!IsClient || !radarCamera) return;
            int mask = radarOnlyLayer.value;

            bool isBought = (current & (int)BaseUpgrades.IsDetectionBought) != 0;

            if (isBought)
                radarCamera.cullingMask |= mask;
            else
                radarCamera.cullingMask &= ~mask;
        }


        private void OnCasinoBoughtChanged(int _, int current)
        {
            bool isBought = (current & (int)BaseUpgrades.IsCasinoBought) != 0;

            casinoWall.SetActive(!isBought);
        }

        #endregion
    }
}
