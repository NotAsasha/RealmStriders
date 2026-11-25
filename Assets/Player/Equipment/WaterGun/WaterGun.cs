using Enemy;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.WaterGun
{
    [RequireComponent(typeof(EntityDetector))]
    public class WaterGun : Item
    {
        [SerializeField] private float effectTime = 2f;
        [SerializeField] private ParticleSystem particle;

        private const float ShotInterval = 0.1f;
        private EntityDetector detector;
        private float nextShotTimeLocal;
        private float nextShotTimeServer;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            detector = GetComponent<EntityDetector>();
            nextShotTimeLocal = 0f;
            nextShotTimeServer = 0f;
        }

        protected override void ExecuteItemAction(GameObject player)
        {
            if (!IsClient || !IsSpawned || Time.time < nextShotTimeLocal) return;

            nextShotTimeLocal = Time.time + ShotInterval;
            PlayShot();
            FireServerRpc(detector.EyePosition, transform.forward);
        }

        [Rpc(SendTo.Server)]
        private void FireServerRpc(Vector3 origin, Vector3 forward, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (!TryGetUsableHolderServer(sender, out var holder) ||
                Time.time < nextShotTimeServer || !IsValidAim(holder, origin, forward, detector.WallLayer)) return;

            nextShotTimeServer = Time.time + ShotInterval;
            GameObject target = detector.EntityInSight(origin, forward, true, holder.transform);
            if (target != null && target.TryGetComponent<Entity>(out var entity) && entity.IsSpawned)
            {
                entity.ApplyEffect(EffectType.Water, effectTime);
            }

            PlayShotRpc(sender);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void PlayShotRpc(ulong sender)
        {
            if (NetworkManager.LocalClientId != sender) PlayShot();
        }

        private void PlayShot()
        {
            if (particle != null) particle.Play();
        }
    }
}
