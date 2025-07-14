using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class OnlyOnHost : NetworkBehaviour
{
    public List<Behaviour> componentsToCheck;


    public override void OnNetworkSpawn()
    {
        Debug.Log($"IsServer={IsServer}, IsHost={IsHost}, IsOwner={IsOwner}, IsSpawned={NetworkObject.IsSpawned}");

        foreach (Behaviour component in componentsToCheck)
        {
            component.enabled = IsServer;
        }
    }
}
