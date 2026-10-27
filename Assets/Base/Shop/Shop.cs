using System;
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

        [Serializable]
        private sealed class QuickBuy
        {
            public ItemCard item;
            public UnityEngine.UI.Button button;
        }

        [SerializeField] private QuickBuy[] quickBuyButtons = Array.Empty<QuickBuy>();
        private GameManager gameManager;

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
            gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.teamMoney.OnValueChanged += RefreshQuickBuyButtons;
                gameManager.teamRating.OnValueChanged += RefreshQuickBuyButtons;
            }
            RefreshQuickBuyButtons(0, 0);
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

        public override void OnNetworkDespawn()
        {
            if (gameManager != null)
            {
                gameManager.teamMoney.OnValueChanged -= RefreshQuickBuyButtons;
                gameManager.teamRating.OnValueChanged -= RefreshQuickBuyButtons;
            }
            gameManager = null;
            RefreshQuickBuyButtons(0, 0);
            base.OnNetworkDespawn();
        }

        private void RefreshQuickBuyButtons(int previous, int current)
        {
            foreach (QuickBuy entry in quickBuyButtons)
                entry.button.interactable = IsSpawned && IsClient && gameManager != null &&
                    gameManager.teamMoney.Value >= entry.item.Price &&
                    gameManager.teamRating.Value >= entry.item.RequiredRating;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BuyItemServerRpc(string name, int price)
        {
            if (!IsServer || GameManager.Instance == null) return;
            var card = cards.Find(c => c != null && c.itemName == name);
            if (card == null || card.itemPrefab == null || card.Price < 0 ||
                GameManager.Instance.teamRating.Value < card.RequiredRating ||
                GameManager.Instance.teamMoney.Value < card.Price) return;
            NetworkObject item = card.itemPrefab;

            GameManager.Instance.teamMoney.Value -= card.Price;
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
