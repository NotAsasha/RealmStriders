using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

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
