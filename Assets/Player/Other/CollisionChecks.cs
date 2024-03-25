using Unity.Netcode;
using UnityEngine;
namespace Player
{
    public class CollisionChecks : NetworkBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Plate"))
            {
                if (IsHost || IsServer) other.GetComponent<PressurePlate>().CallButtonPressServerRpc();
            }
        }
    }
}
