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
                other.GetComponent<ICollidable>()?.OnColliderEnter(gameObject);
        }
        private void OnTriggerExit(Collider other)
        {
            if (IsHost || IsServer)
                other.GetComponent<ICollidable>()?.OnColliderExit(gameObject);
        }
    }
}
