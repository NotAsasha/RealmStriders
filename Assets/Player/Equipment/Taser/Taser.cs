using Player.Movement;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Enemy;

namespace Player.Equipment.Taser
{
    public class Taser : Item, IChargeable
    {
        [Header("Taser Settings")]
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float cooldown = 0.5f;
        [SerializeField] private float freezeTime = 2f;
        [SerializeField] private float comboWeakDuration = 6f;
        [SerializeField] private int shotCost = 20;
        [SerializeField] private int maxCharge = 100;

        [Header("References & Layers")]
        [SerializeField] private LayerMask entityLayer;
        [SerializeField] private LayerMask wallLayer = 1;
        [SerializeField] private TMP_Text danger;
        [SerializeField] private AudioSource sound;
        [SerializeField] private ParticleSystem particle;
        [SerializeField] private SoundProducer soundProducer;

        public readonly NetworkVariable<int> chargePercent = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private float nextLocalFireTime;
        private float nextServerFireTime;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            nextLocalFireTime = 0f;
            nextServerFireTime = 0f;
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

        protected override void ExecuteItemAction(GameObject player)
        {
            if (!IsClient || !IsSpawned || Time.time < nextLocalFireTime || CurrentCharge < shotCost) return;

            var camera = CameraMovement.Instance;
            if (camera == null) return;

            nextLocalFireTime = Time.time + cooldown;
            PlayShootEffects();
            FireServerRpc(camera.transform.position, camera.transform.forward);
        }

        [Rpc(SendTo.Server)]
        private void FireServerRpc(Vector3 origin, Vector3 forward, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (!TryGetUsableHolderServer(sender, out var holder) ||
                Time.time < nextServerFireTime || CurrentCharge < shotCost ||
                !IsValidAim(holder, origin, forward, wallLayer)) return;

            nextServerFireTime = Time.time + cooldown;
            ((IChargeable)this).ModifyCharge(-shotCost);
            UpdateRemoteHeldPose();
            soundProducer?.EmitSoundServer();

            if (Physics.Raycast(origin, forward, out RaycastHit hit, maxDistance, entityLayer) &&
                !hit.transform.IsChildOf(holder.transform) &&
                !Physics.Linecast(origin, hit.point, wallLayer, QueryTriggerInteraction.Ignore))
            {
                var entity = hit.collider.GetComponentInParent<Entity>();
                if (entity != null && entity.IsSpawned)
                {
                    entity.ApplyEffect(EffectType.Freeze, freezeTime);
                    if (entity.IsEffectActive(EffectType.Water))
                    {
                        entity.ApplyWeakStack(1, comboWeakDuration, slot: 3);
                    }
                }
            }

            BroadcastShootFxClientRpc(sender);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void BroadcastShootFxClientRpc(ulong senderClientId)
        {
            if (NetworkManager.LocalClientId == senderClientId) return;

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
