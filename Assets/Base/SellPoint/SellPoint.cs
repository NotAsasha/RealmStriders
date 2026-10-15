using System.Collections;
using System.Collections.Generic;
using Player;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace Base.SellPoint
{
    public class SellPoint : NetworkBehaviour
    {
        [Header("Item Detection Settings")]
        [Tooltip("Full size of the volume for detecting sellable items.")]
        [SerializeField] private Vector3 boxSize = new Vector3(4f, 3f, 2.63f);
        [Tooltip("Local offset of the item detection box relative to SellPoint.")]
        [SerializeField] private Vector3 boxOffset = Vector3.zero;
        [SerializeField] private LayerMask itemLayer;

        [Header("Hazard / Kill Zone Settings")]
        [Tooltip("Enable eliminating players who are inside the booth during sell.")]
        [SerializeField] private bool enableKillZone = true;
        [Tooltip("If true, uses dedicated size and offset for the kill zone instead of item boxSize.")]
        [SerializeField] private bool useSeparateKillZone = true;
        [Tooltip("Full size of the hazard/kill box (what you see in the red Gizmo).")]
        [SerializeField] private Vector3 killBoxSize = new Vector3(3.5f, 2.5f, 2.0f);
        [Tooltip("Local offset of the hazard/kill box relative to SellPoint (negative Z moves it deeper into the booth, away from window).")]
        [SerializeField] private Vector3 killBoxOffset = new Vector3(0f, 0f, -0.3f);
        [SerializeField] private LayerMask playerLayer = ~0;

        [Header("CRT Visuals")]
        [SerializeField] private CRTSellScreen sellScreen;

        private bool isSelling;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (sellScreen == null)
            {
                sellScreen = GetComponentInChildren<CRTSellScreen>(true);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void OnSellServerRpc()
        {
            if (!IsServer || isSelling) return;

            Debug.Log("---SellPoint: Started selling sequence!");

            // 1. Collect all sellable items in the sell box.
            // NOTE: Physics.OverlapBox takes halfExtents, so we must multiply full size by 0.5f.
            Vector3 itemCenter = transform.position + transform.rotation * boxOffset;
            Vector3 itemHalfExtents = boxSize * 0.5f;

            Collider[] itemColliders = Physics.OverlapBox(
                itemCenter,
                itemHalfExtents,
                transform.rotation,
                itemLayer,
                QueryTriggerInteraction.Ignore
            );

            List<Item> itemsToSell = new List<Item>();
            List<ulong> itemNetworkIds = new List<ulong>();
            int sum = 0;

            foreach (var col in itemColliders)
            {
                var item = col.GetComponent<Item>();
                if (item == null || item.isCurrentlyHeld || item.sellPrice <= 0) continue;
                if (itemsToSell.Contains(item)) continue;

                itemsToSell.Add(item);
                sum += item.sellPrice;

                if (item.TryGetComponent<NetworkObject>(out var netObj))
                {
                    itemNetworkIds.Add(netObj.NetworkObjectId);
                }
            }

            // 2. Collect any players inside the kill zone (hazard check)
            List<Human> humansToKill = new List<Human>();
            if (enableKillZone)
            {
                Vector3 currentKillOffset = useSeparateKillZone ? killBoxOffset : boxOffset;
                Vector3 currentKillSize = useSeparateKillZone ? killBoxSize : boxSize;
                Vector3 killCenter = transform.position + transform.rotation * currentKillOffset;
                Vector3 killHalfExtents = currentKillSize * 0.5f;

                Collider[] playerColliders = Physics.OverlapBox(
                    killCenter,
                    killHalfExtents,
                    transform.rotation,
                    playerLayer,
                    QueryTriggerInteraction.Ignore
                );

                foreach (var col in playerColliders)
                {
                    var human = col.GetComponentInParent<Human>();
                    if (human == null || human.isDead.Value) continue;
                    if (!humansToKill.Contains(human))
                    {
                        humansToKill.Add(human);
                    }
                }
            }

            isSelling = true;

            // Broadcast CRT shutdown effect to all clients
            PlaySellSequenceClientRpc(itemNetworkIds.ToArray());

            // Server-side timing: collapse happens at 0.6 of sequence duration
            float duration = sellScreen != null ? sellScreen.SequenceDuration : 0.4f;
            float collapseDelay = duration * 0.6f;
            StartCoroutine(ServerFinalizeSellRoutine(itemsToSell, humansToKill, sum, collapseDelay, duration));
        }

        [Rpc(SendTo.Everyone)]
        private void PlaySellSequenceClientRpc(ulong[] itemNetIds)
        {
            List<GameObject> soldItemObjects = new List<GameObject>();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null && itemNetIds != null)
            {
                foreach (ulong id in itemNetIds)
                {
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var netObj))
                    {
                        if (netObj != null)
                        {
                            soldItemObjects.Add(netObj.gameObject);
                        }
                    }
                }
            }

            if (sellScreen != null)
            {
                sellScreen.PlaySellSequence(soldItemObjects);
            }
        }

        private IEnumerator ServerFinalizeSellRoutine(
            List<Item> items,
            List<Human> humans,
            int sum,
            float collapseDelay,
            float totalDuration)
        {
            yield return new WaitForSeconds(collapseDelay);

            // Eliminate players caught inside the sell booth at collapse moment
            foreach (var human in humans)
            {
                if (human != null && !human.isDead.Value)
                {
                    Debug.Log($"---SellPoint: Player {human.name} caught in sell zone during shutdown! Eliminating.");
                    human.AddHealth(-9999f);
                }
            }

            // Despawn sold items and reward team money
            foreach (var item in items)
            {
                if (item != null && item.NetworkObject != null && item.NetworkObject.IsSpawned)
                {
                    Debug.Log($"---SellPoint: Sold {item.name} for {item.sellPrice}");
                    item.DestroyItemServerRpc();
                }
            }

            if (sum > 0)
            {
                Debug.Log($"---SellPoint: Total sold items for {sum}");
                GameManager.Instance.teamMoney.Value += sum;
            }

            // Wait until full CRT cycle completes before allowing next sell action
            float remainingTime = Mathf.Max(0f, totalDuration - collapseDelay);
            if (remainingTime > 0f)
            {
                yield return new WaitForSeconds(remainingTime);
            }

            isSelling = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            // Green: Item Sell Zone
            Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
            Gizmos.DrawWireCube(boxOffset, boxSize);

            // Red: Player Kill Zone
            if (enableKillZone)
            {
                Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.9f);
                Vector3 currentKillOffset = useSeparateKillZone ? killBoxOffset : boxOffset;
                Vector3 currentKillSize = useSeparateKillZone ? killBoxSize : boxSize;
                Gizmos.DrawWireCube(currentKillOffset, currentKillSize);
            }
        }
    }
}
