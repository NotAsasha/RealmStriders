using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class BeamDamage : NetworkBehaviour, ICollidable
{
    [SerializeField] private float damage = 2f;
    public void OnColliderEnter(GameObject collider)
    {
        if (!IsServer) return;
        var player = collider.GetComponent<Entity>();
        if (player == null || player.isDead.Value) return;

        player.AddHealth(-damage);
    }
}
