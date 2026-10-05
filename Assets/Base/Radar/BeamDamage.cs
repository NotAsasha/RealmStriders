using Player;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace Base.Radar
{
    public class BeamDamage : NetworkBehaviour, ICollidable
    {
        [SerializeField] private float weakDuration = 5f;

        public void OnColliderEnter(GameObject collider)
        {
            if (!IsServer) return;

            if (collider.TryGetComponent<Entity>(out var entity) && !entity.isDead.Value)
            {
                Debug.Log($"---Beam: Shot entity: {entity.name}");

                entity.ApplyEffectServerRpc(EffectType.Freeze, weakDuration);
                // Slot 1: Radar Beam owns this weakness layer.
                entity.ApplyWeakStack(1, weakDuration, slot: 1);
            }
        }
    }
}
