using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Steam
{
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
}
