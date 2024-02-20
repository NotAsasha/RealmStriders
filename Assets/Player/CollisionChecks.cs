using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
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
