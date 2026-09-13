using Base.BaseUpgrader;
using Player;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Portals
{
    public class PortalManager : NetworkBehaviour, IPowerConsumer {

        [SerializeField] private Camera cameraA;
        [SerializeField] private Camera cameraB;

        [SerializeField] private Material cameraMatA;
        [SerializeField] private Material cameraMatB;

        public static PortalManager Instance;

        public bool isForward = true;

        public event Action<Human, bool> OnTeleport;

        [FormerlySerializedAs("PortalParent")] [SerializeField] GameObject portalParent;

        private PowerGrid powerGrid;
        private bool isPowerGridRegistered;
        private bool isPortalOpen;

        public int PowerDemand => isPortalOpen ? 5 : 0;
        public int Priority => 30;
        public bool IsPowered => isPortalOpen;

        void Start () {
            if (Instance == null) Instance = this;
            ChangeState(false);

            if (cameraA.targetTexture != null)
            {
                cameraA.targetTexture.Release();
            }
            cameraA.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
            cameraMatA.mainTexture = cameraA.targetTexture;

            if (cameraB.targetTexture != null)
            {
                cameraB.targetTexture.Release();
            }
            cameraB.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
            cameraMatB.mainTexture = cameraB.targetTexture;

            cameraA.gameObject.SetActive(true);
            cameraB.gameObject.SetActive(true);
            isForward = true;
            }

        public override void OnNetworkSpawn()
        {
            TryRegisterPowerGrid();
        }

        public override void OnNetworkDespawn()
        {
            if (isPowerGridRegistered)
            {
                powerGrid.UnregisterConsumer(this);
                isPowerGridRegistered = false;
            }
        }

        private void Update()
        {
            TryRegisterPowerGrid();
        }

        private void TryRegisterPowerGrid()
        {
            if (isPowerGridRegistered || BaseManager.Instance == null) return;

            PowerGrid basePowerGrid = BaseManager.Instance.PowerGrid;
            if (basePowerGrid == null || !basePowerGrid.IsSpawned) return;

            powerGrid = basePowerGrid;
            powerGrid.RegisterConsumer(this);
            isPowerGridRegistered = true;
        }
	
        public void ChangeState(bool isStarted)
        {
            isPortalOpen = isStarted;
            portalParent?.SetActive(isStarted);
        }

        public void SwitchCameras()
        {
            // Logic moved to PortalCamera.cs for better performance (visibility-based)
            isForward = !isForward;
        }

        public void CallOnTeleport(Human player)
        {
            if (!isPortalOpen) return;
            OnTeleport?.Invoke(player, isForward);
        }

        public void OnPowerStateChanged(bool powered)
        {
            //isPowered = powered;
            if (!powered)
            {
                //ChangeState(false);
            }
        }
    }
}
