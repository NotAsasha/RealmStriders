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



        private int playerID;
        InputAction control;
        NetworkObject beam;
        private PowerGrid powerGrid;
        private bool isPowered = true;
        private float beamEndTime;

        public int PowerDemand => isPowered ? 4 : 2;
        public int Priority => 100;
        public bool IsPowered => isPowered;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            powerGrid = BaseManager.Instance != null ? BaseManager.Instance.PowerGrid : null;
            powerGrid?.RegisterConsumer(this);
            radarCamera.transform.position = new Vector3(0f, 100f, 0f);
            radarCamera.transform.eulerAngles = new Vector3(90f, 0f, 0f);
        }

        public override void OnNetworkDespawn()
        {
            powerGrid?.UnregisterConsumer(this);
            base.OnNetworkDespawn();
        }
        public void Start()
        {
            playerID = (int)PlayerMovement.Instance.GetComponent<NetworkObject>().OwnerClientId;
            control = PlayerMovement.Instance.controls.UI.Navigate;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SpawnBeamServerRpc()
        {
            if (!isPowered || powerGrid == null || powerGrid.IsGridOverloaded.Value) return;

            powerGrid.ConsumeInstantChargeServer(shotCost);

            Vector3 spawnPos = new(crosshair.localPosition.x * mapSizeMultiplier, 100f, crosshair.localPosition.z * mapSizeMultiplier);

            beam = Instantiate(beamPrefab, spawnPos, Quaternion.identity);
            beam.Spawn();
            beamEndTime = Time.time + beamLifetime; // TOFIX
        }

        private void Update()
        {
            if (IsServer && beam != null && Time.time >= beamEndTime)
            {
                if (beam.IsSpawned) beam.Despawn();
                beam = null;
            }

            if (!IsTaken() || playerID != ownerID) return;
            if (!isPowered) return;

            Vector2 input = control.ReadValue<Vector2>();

            Vector3 move = moveSpeed * Time.deltaTime * new Vector3(input.x, 0f, input.y);

            Vector3 newLocalPos = crosshair.localPosition + move;

            newLocalPos.x = Mathf.Clamp(newLocalPos.x, -border.x, border.x);
            newLocalPos.z = Mathf.Clamp(newLocalPos.z, -border.y, border.y);
            newLocalPos.y = 0f;

            crosshair.localPosition = newLocalPos;
        }

        public void OnPowerStateChanged(bool powered)
        {
            isPowered = powered;

        }
    }
}