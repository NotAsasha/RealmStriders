using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Sprinkler
{
    public sealed class Sprinkler : Item
    {
        [SerializeField, Min(0.1f)] private float effectRadius = 5f;
        [SerializeField, Min(0.5f)] private float wetDuration = 2f;
        [SerializeField] private ParticleSystem sprayParticles;
        [SerializeField] private AudioSource sprayAudio;

        private const float SprayInterval = 0.5f;
        private readonly NetworkVariable<bool> isOn = new(false);
        private float nextSprayTime;

        public bool IsOn => isOn.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (sprayAudio != null)
            {
                sprayAudio.playOnAwake = false;
                sprayAudio.loop = true;
            }
            isOn.OnValueChanged += UpdateSpray;
            UpdateSpray(false, isOn.Value);
        }

        public override void OnNetworkDespawn()
        {
            isOn.OnValueChanged -= UpdateSpray;
            UpdateSpray(false, false);
            base.OnNetworkDespawn();
        }

        private void UpdateSpray(bool previous, bool active)
        {
            if (active) sprayParticles.Play();
            else sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (sprayAudio == null) return;
            if (active) sprayAudio.Play();
            else sprayAudio.Stop();
        }

        protected override void ExecuteItemAction(GameObject player)
        {
            if (IsSpawned && IsClient) ToggleServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ToggleServerRpc(RpcParams rpc = default)
        {
            if (!TryGetUsableHolderServer(rpc.Receive.SenderClientId, out _)) return;
            isOn.Value = !isOn.Value;
            nextSprayTime = Time.time;
        }

        public override void TurnOffServer()
        {
            if (IsServer) isOn.Value = false;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !isOn.Value || Time.time < nextSprayTime) return;
            nextSprayTime = Time.time + SprayInterval;
            if (isCurrentlyHeld && !IsEquipped)
            {
                TurnOffServer();
                return;
            }

            Vector3 origin = transform.position;
            float radiusSquared = effectRadius * effectRadius;
            foreach (NetworkObject candidate in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if ((candidate.transform.position - origin).sqrMagnitude <= radiusSquared &&
                    candidate.TryGetComponent(out Entity entity) && entity.isActiveAndEnabled)
                {
                    entity.ApplyEffect(EffectType.Water, wetDuration);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, effectRadius);
        }
    }
}
