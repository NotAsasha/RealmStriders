using Player.Equipment;
using UnityEngine;

namespace Base.Crafting
{
    [RequireComponent(typeof(Collider))]
    public sealed class CrafterInputSocket : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private int socketIndex;
        [SerializeField] private Crafter crafter;

        private void Awake()
        {
            if (crafter == null)
                crafter = GetComponentInParent<Crafter>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetItem(other, out Item item))
                return;

            crafter?.RegisterItem(socketIndex, item);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetItem(other, out Item item))
                return;

            crafter?.UnregisterItem(socketIndex, item);
        }

        private static bool TryGetItem(Collider collider, out Item item)
        {
            item = collider.GetComponentInParent<Item>();
            return item != null;
        }
    }
}
