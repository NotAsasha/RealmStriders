using Player;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

public class ItemReturnZone : MonoBehaviour
{
    [SerializeField] private Vector3 returnPosition;

    private void OnTriggerEnter(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null)
        {
            if (human.IsOwner && !human.IsDead())
            {
                if (GameManager.Instance != null)
                    human.ToSpawnPoint();
                else
                    human.Teleport(returnPosition);
            }
            return;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        Item item = other.GetComponentInParent<Item>();
        if (item == null || item.isCurrentlyHeld) return;

        item.ReturnToSpawnServer(returnPosition);
    }
}
