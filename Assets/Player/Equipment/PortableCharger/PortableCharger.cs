using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Player.Equipment.PortableCharger
{
    // PlayerHeadSync finishes positioning the held item in LateUpdate at order 100.
    [DefaultExecutionOrder(110)]
    public sealed class PortableCharger : Item, IChargeable
    {
        [Header("Charge Transfer")]
        [SerializeField, Min(0.1f)] private float chargeDistance = 5f;
        [SerializeField, Min(0.05f)] private float chargeInterval = 0.5f;
        [SerializeField, Min(1)] private int chargePerTick = 10;
        [SerializeField] private LayerMask itemLayer;
        [SerializeField] private LayerMask wallLayer;

        [Header("Presentation")]
        [SerializeField] private Transform beamOrigin;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private AudioSource chargeSound;
        [SerializeField] private Renderer powerIndicator;
        [SerializeField] private Material powerOnMaterial;
        [SerializeField] private Material powerOffMaterial;
        [Tooltip("Front and back images for each segment, in pairs from empty to full.")]
        [SerializeField] private Image[] chargeBars = Array.Empty<Image>();

        [SerializeField] private int Capacity = 200;
        private const ulong NoTarget = ulong.MaxValue;
        private Color EmptyColor = new Color32(28, 44, 48, 255);
        private Color NormalColor = new Color32(55, 245, 219, 255);

        // A powered bank supplies charge. Excluding it here also prevents two banks feeding each other.
        private static readonly Predicate<IChargeable> AcceptsTransfer =
            item => item is not PortableCharger bank || !bank.isOn.Value;

        private readonly NetworkVariable<bool> isOn = new(false);
        private readonly NetworkVariable<int> charge = new(
            200,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        private readonly NetworkVariable<ulong> targetId = new(NoTarget);
        private Collider[] nearbyItems = new Collider[20];
        private float nextTick;

        public int CurrentCharge => charge.Value;
        public int MaxCharge => Capacity;

        public override void Awake()
        {
            base.Awake();
            if (chargeBars.Length > 0 && chargeBars[0] != null) NormalColor = chargeBars[0].color;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            isOn.OnValueChanged += UpdatePowerIndicator;
            UpdatePowerIndicator(false, isOn.Value);
            charge.OnValueChanged += UpdateChargeBars;
            UpdateChargeBars(0, CurrentCharge);
            if (lineRenderer != null)
            {
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 2;
            }
            StopBeam();
        }

        public override void OnNetworkDespawn()
        {
            isOn.OnValueChanged -= UpdatePowerIndicator;
            UpdatePowerIndicator(isOn.Value, false);
            charge.OnValueChanged -= UpdateChargeBars;
            StopBeam();
            base.OnNetworkDespawn();
        }

        void IChargeable.ModifyCharge(int amount)
        {
            if (!IsServer) return;
            charge.Value = Mathf.Clamp(CurrentCharge + amount, 0, MaxCharge);
            if (CurrentCharge == 0) TurnOffServer();
        }

        protected override void ExecuteItemAction(GameObject player)
        {
            if (IsClient && IsSpawned) ToggleServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ToggleServerRpc(RpcParams rpc = default)
        {
            if (!TryGetUsableHolderServer(rpc.Receive.SenderClientId, out _)) return;
            if (isOn.Value)
            {
                TurnOffServer();
                return;
            }
            if (CurrentCharge == 0) return;
            isOn.Value = true;
            nextTick = Time.time;
        }

        public override void TurnOffServer()
        {
            if (!IsServer) return;
            isOn.Value = false;
            targetId.Value = NoTarget;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !isOn.Value || Time.time < nextTick) return;
            nextTick = Time.time + chargeInterval;
            if (isCurrentlyHeld && !IsEquipped)
            {
                TurnOffServer();
                return;
            }

            Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position;
            IChargeable target = Charger.FindChargeableTarget(origin, chargeDistance, itemLayer, wallLayer,
                ref nearbyItems, NetworkManager, out NetworkObject targetObject, AcceptsTransfer);
            if (target == null)
            {
                targetId.Value = NoTarget;
                return;
            }

            // Show the beam for a charge interval, then validate that the same item is still in reach.
            if (targetId.Value != targetObject.NetworkObjectId)
            {
                targetId.Value = targetObject.NetworkObjectId;
                return;
            }

            int before = target.CurrentCharge;
            int amount = Mathf.Min(Mathf.Min(chargePerTick, CurrentCharge), target.MaxCharge - before);
            target.ModifyCharge(amount);
            ((IChargeable)this).ModifyCharge(before - target.CurrentCharge);
            if (target.IsFullyCharged) targetId.Value = NoTarget;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!IsSpawned || !IsClient) return;
            if (!isOn.Value || targetId.Value == NoTarget ||
                !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId.Value, out var target))
            {
                StopBeam();
                return;
            }

            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, beamOrigin != null ? beamOrigin.position : transform.position);
                lineRenderer.SetPosition(1, target.transform.position);
                lineRenderer.enabled = true;
            }
            if (chargeSound != null && !chargeSound.isPlaying) chargeSound.Play();
        }

        private void OnDisable() => StopBeam();

        private void StopBeam()
        {
            if (lineRenderer != null && lineRenderer.enabled) lineRenderer.enabled = false;
            if (chargeSound != null && chargeSound.isPlaying) chargeSound.Stop();
        }

        private void UpdateChargeBars(int previous, int current)
        {
            int segmentCount = (chargeBars.Length + 1) / 2;
            float fraction = (float)current / MaxCharge;
            int litSegments = Mathf.CeilToInt(fraction * segmentCount);
            for (int i = 0; i < chargeBars.Length; i++)
                if (chargeBars[i] != null) chargeBars[i].color = i / 2 < litSegments ? NormalColor : EmptyColor;
        }

        private void UpdatePowerIndicator(bool previous, bool powered)
        {
            Material material = powered ? powerOnMaterial : powerOffMaterial;
            if (powerIndicator != null && material != null)
                powerIndicator.sharedMaterial = material;
        }
    }
}
