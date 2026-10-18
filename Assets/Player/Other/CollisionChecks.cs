using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace Player.Other
{
    public class CollisionChecks : NetworkBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (IsHost || IsServer)
                other.GetComponentInParent<ICollidable>()?.OnColliderEnter(gameObject);
        }
        private void OnTriggerExit(Collider other)
        {
            if (IsHost || IsServer)
                other.GetComponentInParent<ICollidable>()?.OnColliderExit(gameObject);
        }
    }
}
