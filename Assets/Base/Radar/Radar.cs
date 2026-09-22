using Base.BaseUpgrader;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Base.Radar
{
    public class Radar : Terminal, IPowerConsumer
    {
        [Header("Settings")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private Vector2 border = new Vector2(5f, 5f);

        [SerializeField] private float beamLifetime = 7f;
        [SerializeField] private float shotCost = 80;

        [Header("References")]
        public Camera radarCamera;
        [SerializeField] private Transform crosshair;
        [SerializeField] private Transform screen;
        [SerializeField] private NetworkObject beamPrefab;
        [SerializeField] private float mapSizeMultiplier = 25.6f;

        public readonly NetworkVariable<Vector2> syncedCrosshairPos = new(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private InputAction control;
        private NetworkObject beam;
        private PowerGrid powerGrid;
        private bool isPowered = true;
        private float beamEndTime;

        private bool IsTerminalBought => BaseManager.Instance != null &&
            (BaseManager.Instance.baseUpgrades.Value & (int)BaseUpgrades.IsTerminalBought) != 0;

        public int PowerDemand => !IsTerminalBought ? 0 : (isPowered ? 4 : 2);
        public int Priority => 100;
        public bool IsPowered => isPowered;

        private void Awake()
        {
            if (crosshair != null)
            {
                var netTransform = crosshair.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                if (netTransform != null)
                {
                    Destroy(netTransform);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            powerGrid = BaseManager.Instance != null ? BaseManager.Instance.PowerGrid : null;
            powerGrid?.RegisterConsumer(this);

            if (radarCamera != null)
            {
                radarCamera.transform.position = new Vector3(0f, 100f, 0f);
                radarCamera.transform.eulerAngles = new Vector3(90f, 0f, 0f);
            }

            syncedCrosshairPos.OnValueChanged += OnCrosshairPosChanged;
            if (crosshair != null)
            {
                crosshair.localPosition = new Vector3(syncedCrosshairPos.Value.x, 0f, syncedCrosshairPos.Value.y);
            }
        }

        public override void OnNetworkDespawn()
        {
            syncedCrosshairPos.OnValueChanged -= OnCrosshairPosChanged;
            powerGrid?.UnregisterConsumer(this);
            base.OnNetworkDespawn();
        }

        private void OnCrosshairPosChanged(Vector2 oldPos, Vector2 newPos)
        {
            if (IsLocalPlayerControlling()) return;

            if (crosshair != null)
            {
                crosshair.localPosition = new Vector3(newPos.x, 0f, newPos.y);
            }
        }

        private bool IsLocalPlayerControlling()
        {
            return IsTaken() && NetworkManager.Singleton != null &&
                   currentInteractingClientId.Value == NetworkManager.Singleton.LocalClientId;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SpawnBeamServerRpc()
        {
            if (!isPowered || powerGrid == null || powerGrid.IsGridOverloaded.Value) return;

            powerGrid.ConsumeInstantChargeServer(shotCost);

            Vector2 targetPos = syncedCrosshairPos.Value;
            Vector3 spawnPos = new(targetPos.x * mapSizeMultiplier, 100f, targetPos.y * mapSizeMultiplier);

            beam = Instantiate(beamPrefab, spawnPos, Quaternion.identity);
            beam.Spawn();
            beamEndTime = Time.time + beamLifetime;
        }

        private void Update()
        {
            if (IsServer && beam != null && Time.time >= beamEndTime)
            {
                if (beam.IsSpawned) beam.Despawn();
                beam = null;
            }

            if (!IsLocalPlayerControlling()) return;
            if (!isPowered) return;

            if (control == null && PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
            {
                control = PlayerMovement.Instance.controls.UI.Navigate;
            }
            if (control == null) return;

            Vector2 input = control.ReadValue<Vector2>();
            if (input.sqrMagnitude > 0.001f && crosshair != null)
            {
                Vector3 move = moveSpeed * Time.deltaTime * new Vector3(input.x, 0f, input.y);
                Vector3 newLocalPos = crosshair.localPosition + move;

                newLocalPos.x = Mathf.Clamp(newLocalPos.x, -border.x, border.x);
                newLocalPos.z = Mathf.Clamp(newLocalPos.z, -border.y, border.y);
                newLocalPos.y = 0f;

                crosshair.localPosition = newLocalPos;

                UpdateCrosshairServerRpc(new Vector2(newLocalPos.x, newLocalPos.z));
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpdateCrosshairServerRpc(Vector2 pos)
        {
            syncedCrosshairPos.Value = pos;
            if (crosshair != null)
            {
                crosshair.localPosition = new Vector3(pos.x, 0f, pos.y);
            }
        }

        public void OnPowerStateChanged(bool powered)
        {
            isPowered = powered;
        }
    }
}