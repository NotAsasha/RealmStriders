using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

public class ItemReturnZone : MonoBehaviour
{
    [SerializeField] private Vector3 returnPosition;

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        Item item = other.GetComponentInParent<Item>();
        if (item == null || item.isCurrentlyHeld) return;

        item.ReturnToSpawnServer(returnPosition);
    }
}
