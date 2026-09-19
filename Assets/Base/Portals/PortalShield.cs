using Base.BaseUpgrader;
using Unity.Netcode;
using UnityEngine;

namespace Portals
{
    /// <summary>
    /// Server-authoritative portal defence.  Its visual state is replicated, while
    /// the actual enemy-pass decision stays on the server in <see cref="PortalManager"/>.
    /// </summary>
    public class PortalShield : NetworkBehaviour, IPowerConsumer
    {
        [Header("References")]
        [SerializeField] private GameObject shieldVisual;
        [SerializeField] private PortalManager portalManager;
        [SerializeField] private PowerGrid powerGrid;

        [Header("Power")]
        [SerializeField] private int activePowerDemand = 35;

        private readonly NetworkVariable<bool> isShieldActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool isPowered;
        private bool isRegisteredWithPowerGrid;

        public int PowerDemand => isShieldActive.Value ? activePowerDemand : 0;
        public int Priority => 35;
        public bool IsPowered => isPowered;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            isShieldActive.OnValueChanged += OnShieldStateChanged;
            ApplyVisualState(isShieldActive.Value);

            if (!IsServer)
            {
                return;
            }

            if (powerGrid == null)
            {
                Debug.LogWarning("PortalShield requires a PowerGrid reference.", this);
                SetShieldActiveServer(false);
                return;
            }

            powerGrid.RegisterConsumer(this);
            isRegisteredWithPowerGrid = true;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && isRegisteredWithPowerGrid)
            {
                // Do not leave a stale logical block if this network object is removed.
                portalManager?.SetMonsterPassBlockedServer(false);
                powerGrid.UnregisterConsumer(this);
                isRegisteredWithPowerGrid = false;
            }

            isShieldActive.OnValueChanged -= OnShieldStateChanged;
            base.OnNetworkDespawn();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestToggleShieldActiveServerRpc()
        {
            SetShieldActiveServer(!isShieldActive.Value);
        }

        public void OnPowerStateChanged(bool powered)
        {
            if (!IsServer)
            {
                return;
            }

            isPowered = powered;

            if (isShieldActive.Value)
            {
                SetShieldActiveServer(powered);
            }
        }

        private void SetShieldActiveServer(bool active)
        {
            if (!IsServer)
            {
                return;
            }

            isShieldActive.Value = active;
            portalManager?.SetMonsterPassBlockedServer(active);
        }

        private void OnShieldStateChanged(bool _, bool isActive)
        {
            ApplyVisualState(isActive);
        }

        private void ApplyVisualState(bool isActive)
        {
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(isActive);
            }
        }
    }
}
