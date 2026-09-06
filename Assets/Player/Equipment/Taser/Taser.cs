using Player.Movement;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Taser
{
    public class Taser : Item, IChargeable
    {
        [Header("Taser Settings")]
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float cooldown = 0.5f;
        [SerializeField] private float freezeTime = 2f;
        [SerializeField] private int shotCost = 20;
        [SerializeField] private int maxCharge = 100;

        [Header("References & Layers")]
        [SerializeField] private LayerMask entityLayer;
        [SerializeField] private TMP_Text danger;
        [SerializeField] private AudioSource sound;
        [SerializeField] private ParticleSystem particle;

        public readonly NetworkVariable<int> chargePercent = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private float nextFireTime;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            chargePercent.OnValueChanged += ScreenUpdate;
            UpdateScreen(chargePercent.Value);
        }

        public override void OnNetworkDespawn()
        {
            chargePercent.OnValueChanged -= ScreenUpdate;
            base.OnNetworkDespawn();
        }

        #region UI

        private void ScreenUpdate(int oldValue, int newValue) => UpdateScreen(newValue);

        private void UpdateScreen(int value)
        {
            if (danger != null) danger.text = $"{value}%";
        }

        #endregion

        #region FX Playback

        private void PlayShootEffects()
        {
            if (particle != null)
            {
                particle.Play();
            }

            if (sound != null)
            {
                sound.Play();
            }
        }

        #endregion

        #region Item Actions

        override protected void ExecuteItemAction(GameObject player)
        {
            if (Time.time < nextFireTime || chargePercent.Value < shotCost) return;
            nextFireTime = Time.time + cooldown;

            // 1. МОМЕНТАЛЬНИЙ ВІДГУК: локальний гравець бачить і чує постріл без затримки пінгу
            PlayShootEffects();

            Transform cam = CameraMovement.Instance.transform;

            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, maxDistance, entityLayer))
            {
                if (hit.transform.TryGetComponent<NetworkObject>(out var netObj))
                {
                    HitEntityServerRpc(netObj);
                    return;
                }
            }

            MissServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void HitEntityServerRpc(NetworkObjectReference targetRef, RpcParams rpcParams = default)
        {
            if (chargePercent.Value < shotCost) return;

            if (targetRef.TryGet(out NetworkObject targetNetObj) &&
                targetNetObj.TryGetComponent<Entity>(out var entity))
            {
                float distSqr = (transform.position - entity.transform.position).sqrMagnitude;
                if (distSqr <= (maxDistance * 1.5f) * (maxDistance * 1.5f))
                {
                    ((IChargeable)this).ModifyCharge(-shotCost);
                    entity.ApplyEffectServerRpc(EffectType.Freeze, freezeTime);

                    if (entity.IsEffectActive(EffectType.Water))
                    {
                        entity.AddHealth(-0.5f);
                    }

                    // Транслюємо ефекти іншим клієнтам, виключаючи відправника
                    BroadcastShootFxClientRpc(rpcParams.Receive.SenderClientId);
                }
            }
        }

        [Rpc(SendTo.Server)]
        private void MissServerRpc(RpcParams rpcParams = default)
        {
            if (chargePercent.Value < shotCost) return;

            ((IChargeable)this).ModifyCharge(-shotCost);

            BroadcastShootFxClientRpc(rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastShootFxClientRpc(ulong senderClientId)
        {
            if (NetworkManager.Singleton.LocalClientId == senderClientId) return;

            PlayShootEffects();
        }

        #endregion

        #region IChargeable Implementation

        public int CurrentCharge => chargePercent.Value;
        public int MaxCharge => maxCharge;

        void IChargeable.ModifyCharge(int amount)
        {
            if (!IsServer) return;
            chargePercent.Value = Mathf.Clamp(chargePercent.Value + amount, 0, maxCharge);
        }

        #endregion
    }
}