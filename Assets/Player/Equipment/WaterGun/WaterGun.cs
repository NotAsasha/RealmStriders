using UnityEngine;
using Unity.Netcode;
using InventorySystem;

public class WaterGun : Item
{
    [SerializeField] LayerMask entityLayer;
    [SerializeField] float maxDistance = 10f;
    [SerializeField] private float effectTime = 2f;
    [SerializeField] ParticleSystem particle;

    override protected void ExecuteItemAction(GameObject player)
    {
        particle.Play();
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, maxDistance, entityLayer))
        {
            if (hit.transform.TryGetComponent<Entity>(out var entity))
            {
                entity.ApplyEffectServerRpc(EffectType.Water, effectTime);
                Debug.Log($"---WaterGun: Shot entity: {entity.name}");
            }
        }
        else Debug.Log($"---WaterGun: Missed :(");
    }

}
