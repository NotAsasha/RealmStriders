using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Base.BaseUpgrader
{
    public class PowerGrid : NetworkBehaviour
    {
        [Header("Battery")]
        [SerializeField] private float chargeRatePerSecond = 10f;
        [SerializeField] private float minimumChargePercent = -500f;

        [SerializeField] private float safeChargePercent = 20f;

        private readonly List<IPowerConsumer> consumers = new();
        private readonly HashSet<IPowerConsumer> registeredConsumers = new();
        public NetworkVariable<float> CurrentChargePercent = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsGridOverloaded = new(
            false,
            NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public event Action<bool> OnOverloadStateChanged;

        public override void OnNetworkSpawn()
        {
            IsGridOverloaded.OnValueChanged += OnGridOverloadedChanged;

            if (IsServer)
            {
                UpdateOverloadState(false);
            }
        }

        public override void OnNetworkDespawn()
        {
            IsGridOverloaded.OnValueChanged -= OnGridOverloadedChanged;
            consumers.Clear();
            registeredConsumers.Clear();
        }


        private float lastUpdateTime = 0f;
        private void Update()
        {
            if (!IsServer) return;
            if (Time.time - lastUpdateTime < 1f) return; // Update every second
            lastUpdateTime = Time.time;
            
            float activeConsumersDrainRate = CalculateConsumersDrainRate();
            float netRate = chargeRatePerSecond - activeConsumersDrainRate;
            float nextCharge = CurrentChargePercent.Value + netRate;

            CurrentChargePercent.Value = Mathf.Clamp(
                Mathf.Min(100f, nextCharge),
                minimumChargePercent,
                100f);

            if (IsGridOverloaded.Value && CurrentChargePercent.Value < safeChargePercent)
            {
                return; // Prevent further overload state changes if already overloaded and charge is low
            }

            UpdateOverloadState(CurrentChargePercent.Value < 0f);
        }

        public void RegisterConsumer(IPowerConsumer consumer)
        {
            if (consumer == null || !registeredConsumers.Add(consumer)) return;

            consumers.Add(consumer);
            SetConsumerPowerState(consumer, !IsGridOverloaded.Value);
        }

        public void UnregisterConsumer(IPowerConsumer consumer)
        {
            if (consumer == null || !registeredConsumers.Remove(consumer)) return;

            consumers.Remove(consumer);
        }

        public void ConsumeInstantChargeServer(float percentAmount)
        {
            if (!IsServer) return;
            if (percentAmount <= 0f) return;

            CurrentChargePercent.Value = Mathf.Max(
                minimumChargePercent,
                CurrentChargePercent.Value - percentAmount);
            UpdateOverloadState(CurrentChargePercent.Value < 0f); // Check
        }

        public void RestoreChargeServer(float chargePercent)
        {
            if (!IsServer) return;

            CurrentChargePercent.Value = Mathf.Clamp(chargePercent, minimumChargePercent, 100f);
            UpdateOverloadState(CurrentChargePercent.Value < 0f);
        }

        private float CalculateConsumersDrainRate()
        {
            float drainRate = 0f;

            for (int i = 0; i < consumers.Count; i++)
            {
                IPowerConsumer consumer = consumers[i];
                if (consumer != null)
                {
                    drainRate += Mathf.Max(0, consumer.PowerDemand);
                }
            }

            return drainRate;
        }

        private void UpdateOverloadState(bool isOverloaded)
        {
            if (IsGridOverloaded.Value == isOverloaded) return;

            IsGridOverloaded.Value = isOverloaded;
            for (int i = 0; i < consumers.Count; i++)
            {
                IPowerConsumer consumer = consumers[i];
                if (consumer != null)
                {
                    SetConsumerPowerState(consumer, !isOverloaded);
                }
            }

        }

        private void OnGridOverloadedChanged(bool _, bool isOverloaded)
        {
            OnOverloadStateChanged?.Invoke(isOverloaded);
            for (int i = 0; i < consumers.Count; i++)
            {
                IPowerConsumer consumer = consumers[i];
                if (consumer != null)
                {
                    SetConsumerPowerState(consumer, !isOverloaded);
                }
            }
        }

        private static void SetConsumerPowerState(IPowerConsumer consumer, bool isPowered)
        {
            if (consumer.IsPowered != isPowered)
            {
                consumer.OnPowerStateChanged(isPowered);
            }
        }
    }
}