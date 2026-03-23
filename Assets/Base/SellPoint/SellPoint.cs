using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace Base.SellPoint
{
    public class SellPoint : NetworkBehaviour
    {
        [SerializeField] private Vector3 boxSize = new Vector3(0.5f, 0.5f, 2);
        [SerializeField] private LayerMask itemLayer;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void OnSellServerRpc()
        {
            Debug.Log($"---SellPoint: Started selling!");
            Collider[] colliders = Physics.OverlapBox(transform.position,
                boxSize, Quaternion.identity, itemLayer, QueryTriggerInteraction.Ignore);

            int sum = 0;
            foreach (var collider in colliders)
            {
                var item = collider.GetComponent<Item>();
                if (item == null || item.isCurrentlyHeld) continue;
                sum += item.sellPrice;
                Debug.Log($"---SellPoint: Sold {name}");
                item.DestroyItemServerRpc();
            }
            Debug.Log($"---SellPoint: Sold items for {sum}");
            GameManager.Instance.teamMoney.Value += sum;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, boxSize);
        }
    }
}
