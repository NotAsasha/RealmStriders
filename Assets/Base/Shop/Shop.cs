using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Base.Shop
{
    public class Shop : NetworkBehaviour
    {
        [SerializeField] Vector3 spawnPosition;
        [SerializeField] AudioSource TubeAudio;
        [SerializeField] ParticleSystem TubeParticle;
        
        [SerializeField] List<ItemCard> cards;

        public static Shop Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                StartCoroutine(DelayedInitialCheck());
            }
        }

        private System.Collections.IEnumerator DelayedInitialCheck()
        {
            // Allow save restoration in NetworkItemsHandler to complete before checking existing items
            yield return new WaitForSeconds(1.0f);
            EnsureFreeLeafBlowerServer();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BuyItemServerRpc(string name, int price)
        {
            if (GameManager.Instance.teamMoney.Value < price) return;

            NetworkObject item = cards.Find(c => c != null && c.itemName == name)?.itemPrefab;
            if (item == null) return;

            GameManager.Instance.teamMoney.Value -= price;
            PlaySpawnSoundRpc();
            item.InstantiateAndSpawn(NetworkManager.Singleton, 0, false, false, false, spawnPosition);
            Debug.Log($"---Shop: Item bought: {item.name}");
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void PlaySpawnSoundRpc()
        {
            if (TubeAudio != null) TubeAudio.Play();
            if (TubeParticle != null) TubeParticle.Play();
        }

        /// <summary>
        /// Guarantees the team always has at least one standard-issue capture tool (LeafBlower).
        /// If none exist in the base or player inventories, one is spawned at the shop chute for free.
        /// </summary>
        public void EnsureFreeLeafBlowerServer()
        {
            if (!IsServer) return;

            if (HasAnyLeafBlower())
            {
                Debug.Log("---Shop: Team already has a LeafBlower. No emergency spawn needed.");
                return;
            }

            SpawnFreeItem("LeafBlower");
        }

        public bool HasAnyLeafBlower()
        {
            if (NetworkItemsHandler.Instance != null && NetworkItemsHandler.Instance.activeSaveables != null)
            {
                foreach (var netObj in NetworkItemsHandler.Instance.activeSaveables)
                {
                    if (netObj != null && netObj.IsSpawned && netObj.GetComponent<Player.Equipment.LeafBlower.LeafBlower>() != null)
                    {
                        return true;
                    }
                }
            }

            var blowers = FindObjectsByType<Player.Equipment.LeafBlower.LeafBlower>(FindObjectsSortMode.None);
            foreach (var blower in blowers)
            {
                if (blower != null && blower.NetworkObject != null && blower.NetworkObject.IsSpawned)
                {
                    return true;
                }
            }

            return false;
        }

        public void SpawnFreeItem(string name)
        {
            if (!IsServer) return;

            var card = cards.Find(c => c != null && (c.itemName == name || (c.itemPrefab != null && c.itemPrefab.name.Contains(name))));
            if (card == null || card.itemPrefab == null)
            {
                Debug.LogWarning($"---Shop: Cannot find item prefab for {name} to spawn free item.");
                return;
            }

            PlaySpawnSoundRpc();
            card.itemPrefab.InstantiateAndSpawn(NetworkManager.Singleton, 0, false, false, false, spawnPosition);
            Debug.Log($"---Shop: Emergency free item spawned: {name}");
        }

    }
}
