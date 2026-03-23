using Player;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace Base.Radar
{
    public class BeamDamage : NetworkBehaviour, ICollidable
    {
        [SerializeField] private float damage = 2f;
        public void OnColliderEnter(GameObject collider)
        {
            if (!IsServer) return;

            if (collider.TryGetComponent<Entity>(out var entity) && !entity.isDead.Value)
            {
                Debug.Log($"---Beam: Shot entity: {entity.name}");

                entity.ApplyEffectServerRpc(EffectType.Water, 5f);
                entity.AddHealth(-damage);
            }
        }
    }
}
